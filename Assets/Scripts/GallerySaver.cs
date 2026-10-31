using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Saves a JPEG into the phone's Gallery, in Pictures/&lt;album&gt;.
///
/// Uses Android's MediaStore directly (no plugin). The project's minimum
/// Android version is 10 (API 29), where MediaStore needs no storage
/// permission for the app's own images: the file is inserted as "pending",
/// written, then published so the Gallery shows it straight away.
///
/// In the Editor (no Gallery) the file is written to
/// Application.persistentDataPath so the capture can still be checked.
/// </summary>
public static class GallerySaver
{
    /// <returns>True on success. On failure, <paramref name="error"/> says why in plain words.</returns>
    public static bool SaveJpeg(byte[] jpeg, string fileName, string album, out string error)
    {
        error = null;
        if (jpeg == null || jpeg.Length == 0)
        {
            error = "The image was empty.";
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (AndroidJavaClass media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            using (AndroidJavaObject collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (AndroidJavaObject values = new AndroidJavaObject("android.content.ContentValues"))
            {
                // Column names are MediaStore.MediaColumns constants.
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "image/jpeg");
                values.Call("put", "relative_path", "Pictures/" + album);
                using (AndroidJavaObject pending = new AndroidJavaObject("java.lang.Integer", 1))
                    values.Call("put", "is_pending", pending);

                using (AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (uri == null)
                    {
                        error = "The Gallery refused the new image.";
                        return false;
                    }

                    using (AndroidJavaObject stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                    {
                        // JNI maps sbyte[] to Java byte[]; the CLR allows this reinterpreting cast.
                        stream.Call("write", (sbyte[])(Array)jpeg);
                        stream.Call("flush");
                        stream.Call("close");
                    }

                    // Publish it. An empty selection string means "no WHERE clause";
                    // passing nulls would break JNI's method lookup.
                    values.Call("clear");
                    using (AndroidJavaObject done = new AndroidJavaObject("java.lang.Integer", 0))
                        values.Call("put", "is_pending", done);
                    resolver.Call<int>("update", uri, values, "", new string[0]);
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GallerySaver] " + e);
            error = "The phone did not allow saving to the Gallery.";
            return false;
        }
#else
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, album);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, jpeg);
            Debug.Log("[GallerySaver] Editor: saved to " + path);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
#endif
    }
}
