using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The list of everything the user can place, grouped into tabs (Living Room,
/// Bedroom, Dining, Office, Café, Room Sets). Built and refreshed by
/// Tools > AR Furniture Designer > Build Furniture Catalog, which also renders
/// each entry's thumbnail; you can still edit names or order in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "FurnitureCatalog", menuName = "AR Furniture Designer/Furniture Catalog")]
public class FurnitureCatalog : ScriptableObject
{
    public List<CatalogCategory> categories = new List<CatalogCategory>();
}

[Serializable]
public class CatalogCategory
{
    [Tooltip("Tab label, e.g. \"Living Room\".")]
    public string displayName;

    public List<CatalogEntry> entries = new List<CatalogEntry>();
}

[Serializable]
public class CatalogEntry
{
    [Tooltip("Card label, e.g. \"Three-seat sofa\".")]
    public string displayName;

    [Tooltip("Small grey line under the name, e.g. \"Oak · Linen\" or \"6 pieces\".")]
    public string subtitle;

    public GameObject prefab;

    [Tooltip("Card image. Rendered automatically by the catalog builder.")]
    public Sprite thumbnail;
}
