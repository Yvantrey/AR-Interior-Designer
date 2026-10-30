using UnityEngine;

/// <summary>
/// Formatting and parsing helpers for furniture colours, shared by the colour
/// picker and (Stage B) the spec-sheet screenshot.
/// </summary>
public static class ColorFormat
{
    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>"#8E623D" (allocates; use for one-off text such as the spec sheet).</summary>
    public static string ToHex(Color32 c)
    {
        return $"#{c.r:X2}{c.g:X2}{c.b:X2}";
    }

    /// <summary>"RGB(142, 98, 61)" (allocates; use for one-off text).</summary>
    public static string ToRgb(Color32 c)
    {
        return $"RGB({c.r}, {c.g}, {c.b})";
    }

    /// <summary>
    /// Writes "RGB(142, 98, 61)  /  #8E623D" into <paramref name="buffer"/> without
    /// allocating, so the label can be refreshed on every slider tick. Returns
    /// the number of characters written. The buffer must hold at least 32 chars.
    /// </summary>
    public static int WriteRgbAndHex(char[] buffer, Color32 c)
    {
        int i = 0;
        i = Append(buffer, i, "RGB(");
        i = AppendByte(buffer, i, c.r);
        i = Append(buffer, i, ", ");
        i = AppendByte(buffer, i, c.g);
        i = Append(buffer, i, ", ");
        i = AppendByte(buffer, i, c.b);
        i = Append(buffer, i, ")  /  #");
        i = AppendHexByte(buffer, i, c.r);
        i = AppendHexByte(buffer, i, c.g);
        i = AppendHexByte(buffer, i, c.b);
        return i;
    }

    /// <summary>
    /// Parses "8E623D" or "#8E623D" (case-insensitive, surrounding spaces ignored).
    /// Returns false for anything that is not exactly 6 hex digits.
    /// </summary>
    public static bool TryParseHex(string text, out Color32 color)
    {
        color = new Color32(255, 255, 255, 255);
        if (string.IsNullOrEmpty(text))
            return false;

        int start = 0;
        int end = text.Length;
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        if (start < end && text[start] == '#') start++;
        if (end - start != 6)
            return false;

        int value = 0;
        for (int i = start; i < end; i++)
        {
            int digit = HexValue(text[i]);
            if (digit < 0)
                return false;
            value = (value << 4) | digit;
        }

        color = new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
        return true;
    }

    /// <summary>True for 0-9, A-F and a-f.</summary>
    public static bool IsHexChar(char ch) => HexValue(ch) >= 0;

    private static int HexValue(char ch)
    {
        if (ch >= '0' && ch <= '9') return ch - '0';
        if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
        if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
        return -1;
    }

    private static int Append(char[] buffer, int index, string text)
    {
        for (int i = 0; i < text.Length; i++)
            buffer[index++] = text[i];
        return index;
    }

    private static int AppendByte(char[] buffer, int index, byte value)
    {
        if (value >= 100) buffer[index++] = (char)('0' + value / 100);
        if (value >= 10) buffer[index++] = (char)('0' + value / 10 % 10);
        buffer[index++] = (char)('0' + value % 10);
        return index;
    }

    private static int AppendHexByte(char[] buffer, int index, byte value)
    {
        buffer[index++] = HexDigits[value >> 4];
        buffer[index++] = HexDigits[value & 0xF];
        return index;
    }
}
