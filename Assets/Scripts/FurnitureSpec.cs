using UnityEngine;

/// <summary>
/// Product details for one catalog item or room set, shown on the captured
/// spec sheet. Added to every generated prefab by the catalog builder (edit
/// the values in FurnitureCatalogBuilder's spec table, not here).
///
/// Sizes are not stored: they are measured live from the placed piece, so
/// they always match its current scale.
/// </summary>
public class FurnitureSpec : MonoBehaviour
{
    public string displayName;

    [Tooltip("What it is, e.g. \"Sofa\", \"Dining chair\", \"Living room set\".")]
    public string type;

    [Tooltip("Materials and finish, e.g. \"Tufted burgundy leather, rolled arms\".")]
    public string material;

    [Tooltip("Estimated weight at 100 % size, in kilograms.")]
    [Min(0f)] public float weightKg;

    public Sprite thumbnail;

    [Tooltip("True for a room set (several pieces placed as one).")]
    public bool isRoomSet;
    public int pieceCount = 1;

    /// <summary>
    /// Estimated weight at a given size. Weight grows with volume, so a piece
    /// scaled to 110 % weighs about 1.1³ ≈ 1.33 times as much.
    /// </summary>
    public float WeightAtScale(float scaleFactor)
    {
        return weightKg * scaleFactor * scaleFactor * scaleFactor;
    }
}
