using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Builds everything the furniture catalogue needs from the imported packs:
///
///  1. Catalog items (Assets/Catalog/Items): a small wrapper prefab per piece
///     that scales the pack model to real-world size and turns it so its front
///     faces +Z, with the footprint centred on the origin and the base at y = 0.
///     The packs were authored at very different scales (some 1.6-2.3x too big)
///     and facing different ways; the wrappers fix that without touching them.
///  2. Room sets (Assets/Catalog/RoomSets): complete arrangements made of the
///     item wrappers, placed as one piece. A set's front (the side the viewer
///     stands on) is +Z.
///  3. Thumbnails (Assets/Catalog/Thumbnails) for every card.
///  4. The FurnitureCatalog asset that the catalogue UI reads.
///
/// Run Tools > AR Furniture Designer > Build Furniture Catalog. It is safe to
/// run again: everything it generates is rebuilt from the tables below.
/// </summary>
public static class FurnitureCatalogBuilder
{
    private const string RootFolder = "Assets/Catalog";
    private const string ItemsFolder = RootFolder + "/Items";
    private const string SetsFolder = RootFolder + "/RoomSets";
    private const string ThumbnailsFolder = RootFolder + "/Thumbnails";
    public const string CatalogPath = RootFolder + "/FurnitureCatalog.asset";

    private const int ThumbnailSize = 256;

    // Source folders
    private const string F = "Assets/Furniture/Prefabs/";
    private const string M = "Assets/Modern Furniture/Prefabs/";
    private const string B = "Assets/MinimalistBedroom/Prefabs/Interior/";
    private const string D = "Assets/diningRoom/diningRoom/prefabs/";
    private const string K = "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/";
    private const string P = "Assets/Brick Project Studio/_BPS Basic Assets/_Prefabs/Basic Asset/Furniture_Props/";

    /// <summary>The Fast Food kit is modelled about 1.6x real size; this one factor fixes all of it.</summary>
    private const float FastFood = 0.62f;

    // Rotation that turns each model's front toward +Z, from the survey renders.
    private const float FrontPlusZ = 0f;
    private const float FrontMinusZ = 180f;
    private const float FrontPlusX = -90f;

    //=========================================================
    // Item table
    //=========================================================

    private sealed class ItemDef
    {
        public string id;      // asset name, also used by room sets
        public string name;    // card label
        public string source;  // pack prefab
        public float yaw;      // turns the model's front to +Z
        public float scale;    // uniform scale to real-world size
        public ItemDef(string id, string name, string source, float yaw, float scale)
        {
            this.id = id; this.name = name; this.source = source; this.yaw = yaw; this.scale = scale;
        }
    }

    private static readonly ItemDef[] Items =
    {
        // Living room seating
        new ItemDef("Sofa_Classic",        "Classic sofa",            F + "Couch.prefab",          FrontPlusZ,  0.60f),
        new ItemDef("Sofa_ThreeSeat",      "Three-seat sofa",         F + "3Seat.prefab",          FrontMinusZ, 0.80f),
        new ItemDef("Sofa_Chesterfield",   "Chesterfield sofa",       F + "3Seat3.prefab",         FrontMinusZ, 0.53f),
        new ItemDef("Sofa_Loveseat",       "Loveseat",                F + "DoubleSeat.prefab",     FrontPlusZ,  0.63f),
        new ItemDef("Sofa_Club",           "Club loveseat",           F + "Fotel.prefab",          FrontMinusZ, 0.69f),
        new ItemDef("Chair_Leather",       "Leather armchair",        F + "Fotel3.prefab",         FrontPlusZ,  0.62f),
        new ItemDef("Chair_Tan",           "Tan armchair",            F + "Fotel4.prefab",         FrontPlusZ,  0.59f),
        new ItemDef("Chair_LoungeFrame",   "Lounge chair",            M + "Single Frame chair.prefab", FrontPlusZ, 0.42f),
        new ItemDef("Chair_Sculpted",      "Sculpted chair",          M + "Triangle Chair.prefab", FrontPlusX,  0.50f),
        new ItemDef("Chair_Curve",         "Curve accent chair",      M + "Curve Chair.prefab",    FrontPlusZ,  0.41f),

        // Living room tables and storage
        new ItemDef("Table_CoffeeRound",   "Round coffee table",      F + "RoundTable.prefab",     FrontPlusZ,  0.55f),
        new ItemDef("Table_CoffeeMetal",   "Industrial coffee table", F + "MetalTable.prefab",     FrontPlusZ,  0.71f),
        new ItemDef("Table_CoffeeGlass",   "Glass coffee table",      M + "Coffee Table.prefab",   FrontPlusZ,  0.50f),
        new ItemDef("Table_CoffeeOval",    "Oval glass table",        M + "Round Table.prefab",    FrontPlusZ,  0.52f),
        new ItemDef("Table_Side",          "Round side table",        B + "Stool_01.prefab",       FrontPlusZ,  1.45f),
        new ItemDef("Storage_TvConsole",   "TV console",              B + "TvStand_01.prefab",     FrontPlusX,  0.85f),
        new ItemDef("Decor_Tv",            "65\" TV",                 B + "Tv_01.prefab",          FrontPlusX,  1.40f),
        new ItemDef("Storage_Bookshelf",   "Cube bookshelf",          M + "Book Rack.prefab",      FrontPlusX,  1.05f),
        new ItemDef("Storage_Sideboard",   "Walnut chest",            F + "SmallCloset.prefab",    FrontPlusZ,  0.83f),

        // Lighting, rugs, plants, art
        new ItemDef("Light_Tripod",        "Tripod floor lamp",       B + "StandingLamp_01.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Light_Arc",           "Arc table lamp",          M + "Lamp.prefab",           FrontPlusZ,  1.00f),
        new ItemDef("Light_Globe",         "Globe lamp",              B + "NightLight_01.prefab",  FrontPlusZ,  1.40f),
        new ItemDef("Rug_Woven",           "Woven rug",               B + "Rug_01.prefab",         FrontPlusZ,  0.66f),
        new ItemDef("Plant_Leafy",         "Leafy plant",             B + "Plant_a_01.prefab",     FrontPlusZ,  1.00f),
        new ItemDef("Plant_Maple",         "Japanese maple",          D + "plants/redTree.prefab", FrontPlusZ,  1.00f),
        new ItemDef("Plant_Bamboo",        "Bamboo planter",          D + "plants/bambooPot.prefab", FrontPlusZ, 0.85f),
        new ItemDef("Plant_Grass",         "Tall grass planter",      D + "plants/plantPot.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Plant_Planter",       "Ceramic planter",         B + "Pot_a_01.prefab",       FrontPlusZ,  1.00f),
        new ItemDef("Decor_Canvas",        "Canvas art",              B + "Painting_01.prefab",    FrontPlusX,  1.00f),

        // Bedroom
        new ItemDef("Bed_Platform",        "Platform bed",            B + "Bed_01.prefab",         FrontPlusX,  0.92f),
        new ItemDef("Storage_OpenWardrobe","Open wardrobe",           B + "Cupboard_a_01.prefab",  FrontPlusX,  0.92f),
        new ItemDef("Storage_Wardrobe",    "Two-door wardrobe",       F + "BigCloset.prefab",      FrontPlusZ,  0.71f),
        new ItemDef("Storage_NightWalnut", "Walnut nightstand",       F + "SmallCloset.prefab",    FrontPlusZ,  0.57f),
        new ItemDef("Storage_NightOak",    "Oak nightstand",          F + "closet2.prefab",        FrontPlusZ,  0.56f),
        new ItemDef("Storage_Vanity",      "Vanity",                  F + "MirrorComode.prefab",   FrontPlusZ,  0.68f),
        new ItemDef("Storage_Dresser",     "Dressing table",          F + "MirrorComode2.prefab",  FrontPlusZ,  0.74f),
        new ItemDef("Decor_FloorMirror",   "Floor mirror",            B + "WallMirror_01.prefab",  FrontPlusX,  0.73f),
        new ItemDef("Decor_Laundry",       "Laundry basket",          B + "LaundryBasket_01.prefab", FrontPlusZ, 0.85f),

        // Dining
        new ItemDef("Dining_Table6",       "Dining table for 6",      D + "table/brownWoodTable.prefab", FrontPlusZ, 1.08f),
        new ItemDef("Dining_Table8",       "Dining table for 8",      D + "table/RectangleTable.prefab", FrontPlusZ, 0.95f),
        new ItemDef("Dining_TableSquare",  "Square table",            D + "table/joinedLegsTable.prefab", FrontPlusZ, 1.07f),
        new ItemDef("Dining_TableRound",   "Round table",             D + "table/circleTable.prefab", FrontPlusZ, 1.25f),
        new ItemDef("Dining_TableBistro",  "Bistro table",            D + "table/squareTable.prefab", FrontPlusZ, 1.25f),
        new ItemDef("Dining_TablePedestal","Pedestal table",          D + "table/cylinderTable.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Dining_ChairBeige",   "Upholstered chair",       D + "chair/chairBeige.prefab", FrontPlusZ, 0.87f),
        new ItemDef("Dining_ChairRed",     "Velvet dining chair",     D + "chair/curveBackChair.prefab", FrontPlusZ, 0.86f),
        new ItemDef("Dining_ChairSpindle", "Spindle chair",           D + "chair/fourVerticalBackrestChair.prefab", FrontPlusZ, 0.83f),
        new ItemDef("Dining_ChairWhite",   "White frame chair",       D + "chair/verticalBackrestChair.prefab", FrontPlusZ, 0.67f),
        new ItemDef("Dining_ChairFarm",    "Farmhouse chair",         D + "chair/simpleChair.prefab", FrontPlusZ, 0.86f),

        // Office
        new ItemDef("Office_Desk",         "Executive desk",          P + "BPS_Table_01.prefab",   FrontPlusZ,  0.77f),
        new ItemDef("Office_Chair",        "Office chair",            P + "BPS_Chair_01.prefab",   FrontMinusZ, 0.75f),

        // Cafe (Fast Food Restaurant Kit)
        new ItemDef("Cafe_ChairBirch",     "Bentwood chair",          K + "Furniture/Seating/FFK_Chair_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_ChairWalnut",    "Walnut bentwood chair",   K + "Furniture/Seating/FFK_Chair_01_02.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_ChairIndustrial","Industrial chair",        K + "Furniture/Seating/FFK_Chair_02_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_StoolOrange",    "Counter stool, coral",    K + "Furniture/Seating/FFK_Stool_02_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_StoolTeal",      "Counter stool, teal",     K + "Furniture/Seating/FFK_Stool_02_02.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_StoolMetal",     "Metal counter stool",     K + "Furniture/Seating/FFK_Stool_03_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_BoothCoral",     "Booth seat, coral",       K + "Furniture/Seating/FFK_Bench_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_BoothTeal",      "Booth seat, teal",        K + "Furniture/Seating/FFK_Bench_02_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_BoothGraphite",  "Booth seat, graphite",    K + "Furniture/Seating/FFK_Bench_03_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_Table4",         "Café table",              K + "Furniture/Tables/FFK_Table_03_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_Table4Dark",     "Café table, walnut",      K + "Furniture/Tables/FFK_Table_03_02.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_TableLong",      "Long café table",         K + "Furniture/Tables/FFK_Table_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_TableHigh",      "High round table",        K + "Furniture/Tables/FFK_Table_06_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_TableHighRect",  "High table",              K + "Furniture/Tables/FFK_Table_04_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_Counter",        "Service counter",         K + "Furniture/Misc/FFK_RegisterTable_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_PrepCounter",    "Prep counter",            K + "Furniture/Misc/FFK_TableFreestanding_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_WasteStation",   "Waste station",           K + "Furniture/Misc/FFK_TrashBin_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_Kiosk",          "Self-order kiosk",        K + "Props/Electronics/FFK_Kiosk_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_Display",        "Pastry display",          K + "Props/Misc/FFK_Display_01_01.prefab", FrontPlusZ, FastFood),
        new ItemDef("Cafe_DrinkStation",   "Drink station",           K + "Props/Misc/FFK_DrinkMachine_01_01.prefab", FrontPlusZ, FastFood),

        // Table-top accessories: used inside room sets only (too small to place on the floor).
        new ItemDef("Acc_Centerpiece",     "Centerpiece",             D + "decor/Centerpiece Variant.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Acc_CenterPlant",     "Table plant",             D + "decor/CenterpiecePlant Variant.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Acc_Vases",           "Vases",                   D + "decor/vases.prefab",    FrontPlusZ,  1.00f),
        new ItemDef("Acc_Candlestick",     "Candlestick",             D + "decor/candlestick.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Acc_PlaceSetting",    "Place setting",           D + "decor/tableware 1.prefab", FrontPlusZ, 0.60f),
        new ItemDef("Acc_Spheres",         "Gold spheres",            D + "decor/goldeSphere.prefab", FrontPlusZ, 1.00f),
        new ItemDef("Acc_Books",           "Books",                   B + "Book_a_01.prefab",      FrontPlusZ,  1.00f),
        new ItemDef("Acc_Vase",            "Vase",                    B + "Vase_a_01.prefab",      FrontPlusZ,  1.60f),
        new ItemDef("Acc_TrayMeal",        "Meal tray",               K + "Props/Tray Setup/Tray_01_Setup_02.prefab", FrontPlusZ, FastFood),
        new ItemDef("Acc_TrayBurger",      "Burger tray",             K + "Props/Tray Setup/Tray_01_Setup_04.prefab", FrontPlusZ, FastFood),
        new ItemDef("Acc_Register",        "POS terminal",            K + "Props/Electronics/FFK_Register.prefab", FrontPlusZ, FastFood),
    };

    //=========================================================
    // Spec table: type, materials, estimated weight at real size
    // (shown on the captured spec sheet; sizes are measured live)
    //=========================================================

    private static readonly Dictionary<string, (string type, string material, float kg)> Specs =
        new Dictionary<string, (string, string, float)>
    {
        { "Sofa_Classic",         ("Sofa", "Burgundy leather, carved wood trim", 68f) },
        { "Sofa_ThreeSeat",       ("Sofa", "Oxblood leather, button-tufted seat", 75f) },
        { "Sofa_Chesterfield",    ("Chesterfield sofa", "Tufted burgundy leather, rolled arms", 85f) },
        { "Sofa_Loveseat",        ("Loveseat", "Brown leather, bentwood arms", 48f) },
        { "Sofa_Club",            ("Club loveseat", "Tan aniline leather, walnut base", 55f) },
        { "Chair_Leather",        ("Armchair", "Oxblood leather, bentwood frame", 28f) },
        { "Chair_Tan",            ("Armchair", "Distressed tan leather, bentwood frame", 27f) },
        { "Chair_LoungeFrame",    ("Lounge chair", "White leather, chrome tube frame", 14f) },
        { "Chair_Sculpted",       ("Accent chair", "Black faceted shell, steel base", 16f) },
        { "Chair_Curve",          ("Accent chair", "Gloss white composite", 9f) },
        { "Table_CoffeeRound",    ("Coffee table", "Mahogany, carved pedestal", 24f) },
        { "Table_CoffeeMetal",    ("Coffee table", "Blackened steel, smoked glass", 30f) },
        { "Table_CoffeeGlass",    ("Coffee table", "Tempered glass, black steel", 26f) },
        { "Table_CoffeeOval",     ("Coffee table", "Tinted glass, chrome base", 22f) },
        { "Table_Side",           ("Side table", "White lacquer, steel legs", 5f) },
        { "Storage_TvConsole",    ("TV console", "Oak veneer", 32f) },
        { "Decor_Tv",             ("Television", "65-inch LED panel, aluminium stand", 23f) },
        { "Storage_Bookshelf",    ("Bookshelf", "Black powder-coated steel", 35f) },
        { "Storage_Sideboard",    ("Chest of drawers", "Walnut, brass handles", 38f) },
        { "Light_Tripod",         ("Floor lamp", "Linen shade, black steel tripod", 4f) },
        { "Light_Arc",            ("Table lamp", "Brushed aluminium", 2f) },
        { "Light_Globe",          ("Table lamp", "Opal glass globe, chrome stem", 1.5f) },
        { "Rug_Woven",            ("Rug", "Woven wool blend, sand", 9f) },
        { "Plant_Leafy",          ("Plant", "Live foliage, nursery pot", 6f) },
        { "Plant_Maple",          ("Plant", "Japanese maple, stone pot", 18f) },
        { "Plant_Bamboo",         ("Planter", "Bamboo, timber trough", 40f) },
        { "Plant_Grass",          ("Planter", "Ornamental grass, timber pot", 12f) },
        { "Plant_Planter",        ("Planter", "Matte ceramic", 11f) },
        { "Decor_Canvas",         ("Wall art", "Acrylic on canvas, timber frame", 5f) },

        { "Bed_Platform",         ("Bed", "Oak slatted headboard, cotton linen", 70f) },
        { "Storage_OpenWardrobe", ("Wardrobe", "Oak veneer, louvred doors", 95f) },
        { "Storage_Wardrobe",     ("Wardrobe", "Mahogany, brass handles", 110f) },
        { "Storage_NightWalnut",  ("Nightstand", "Walnut, brass handles", 16f) },
        { "Storage_NightOak",     ("Nightstand", "Weathered oak, brass pulls", 15f) },
        { "Storage_Vanity",       ("Vanity", "Mahogany, oval mirror", 30f) },
        { "Storage_Dresser",      ("Dressing table", "Walnut, tilting mirror", 34f) },
        { "Decor_FloorMirror",    ("Floor mirror", "Glass, white timber frame", 14f) },
        { "Decor_Laundry",        ("Laundry basket", "Cotton liner, copper wire", 1.5f) },

        { "Dining_Table6",        ("Dining table", "Mahogany, inlaid top", 55f) },
        { "Dining_Table8",        ("Dining table", "Black lacquer, block base", 90f) },
        { "Dining_TableSquare",   ("Dining table", "Black ash", 30f) },
        { "Dining_TableRound",    ("Dining table", "White laminate, copper legs", 25f) },
        { "Dining_TableBistro",   ("Bistro table", "Birch, tapered legs", 14f) },
        { "Dining_TablePedestal", ("Pedestal table", "Terracotta laminate, cast-iron base", 20f) },
        { "Dining_ChairBeige",    ("Dining chair", "Beige bouclé, walnut legs", 7f) },
        { "Dining_ChairRed",      ("Dining chair", "Crimson velvet, oak legs", 7.5f) },
        { "Dining_ChairSpindle",  ("Dining chair", "Black-stained ash, white seat pad", 6f) },
        { "Dining_ChairWhite",    ("Dining chair", "White lacquer, black seat pad", 6f) },
        { "Dining_ChairFarm",     ("Dining chair", "Solid pine", 6.5f) },

        { "Office_Desk",          ("Desk", "Black oak veneer", 45f) },
        { "Office_Chair",         ("Office chair", "Black leather and mesh, aluminium base", 15f) },

        { "Cafe_ChairBirch",      ("Café chair", "Bent birch plywood, steel legs", 4.5f) },
        { "Cafe_ChairWalnut",     ("Café chair", "Bent walnut plywood, steel legs", 4.5f) },
        { "Cafe_ChairIndustrial", ("Café chair", "Ash seat, black steel frame", 6f) },
        { "Cafe_StoolOrange",     ("Counter stool", "Coral vinyl, chrome base", 9f) },
        { "Cafe_StoolTeal",       ("Counter stool", "Teal vinyl, chrome base", 9f) },
        { "Cafe_StoolMetal",      ("Counter stool", "Ash seat, brushed steel", 6f) },
        { "Cafe_BoothCoral",      ("Booth seat", "Coral vinyl, steel legs", 40f) },
        { "Cafe_BoothTeal",       ("Booth seat", "Teal vinyl, steel legs", 40f) },
        { "Cafe_BoothGraphite",   ("Booth seat", "Graphite fabric, violet vinyl back", 45f) },
        { "Cafe_Table4",          ("Café table", "Birch laminate, steel pedestal", 20f) },
        { "Cafe_Table4Dark",      ("Café table", "Walnut laminate, steel pedestal", 20f) },
        { "Cafe_TableLong",       ("Café table", "Birch laminate, steel legs", 30f) },
        { "Cafe_TableHigh",       ("High table", "Birch laminate, steel column", 24f) },
        { "Cafe_TableHighRect",   ("High table", "Birch laminate, steel column", 26f) },
        { "Cafe_Counter",         ("Service counter", "Oak cladding, quartz top", 120f) },
        { "Cafe_PrepCounter",     ("Prep counter", "Stainless steel, oak top", 85f) },
        { "Cafe_WasteStation",    ("Waste station", "Oak cladding, stainless inserts", 55f) },
        { "Cafe_Kiosk",           ("Order kiosk", "Touchscreen, aluminium stand", 35f) },
        { "Cafe_Display",         ("Display case", "Glass, black steel, LED lighting", 45f) },
        { "Cafe_DrinkStation",    ("Drink dispenser", "Stainless steel", 60f) },

        { "Acc_Centerpiece",      ("Decor", "Carved wood bowl", 2f) },
        { "Acc_CenterPlant",      ("Decor", "Ceramic vase, faux plant", 1f) },
        { "Acc_Vases",            ("Decor", "Glazed ceramic", 3f) },
        { "Acc_Candlestick",      ("Decor", "Black iron", 0.6f) },
        { "Acc_PlaceSetting",     ("Tableware", "Porcelain", 1f) },
        { "Acc_Spheres",          ("Decor", "Gilded metal", 1.2f) },
        { "Acc_Books",            ("Decor", "Hardcover books", 1f) },
        { "Acc_Vase",             ("Decor", "Matte ceramic", 0.8f) },
        { "Acc_TrayMeal",         ("Meal tray", "Plastic tray, paper packaging", 1f) },
        { "Acc_TrayBurger",       ("Meal tray", "Plastic tray, paper packaging", 0.8f) },
        { "Acc_Register",         ("POS terminal", "Touchscreen", 3f) },
    };

    private static readonly Dictionary<string, string> SetTypes = new Dictionary<string, string>
    {
        { "Living", "Living room set" }, { "Bedroom", "Bedroom set" }, { "Dining", "Dining set" },
        { "Office", "Office set" }, { "Café", "Café set" },
    };

    //=========================================================
    // Catalogue tabs (room sets come first, then single items)
    //=========================================================

    private static readonly (string tab, string[] ids)[] ItemTabs =
    {
        ("Living Room", new[]
        {
            "Sofa_Classic", "Sofa_ThreeSeat", "Sofa_Chesterfield", "Sofa_Loveseat", "Sofa_Club",
            "Chair_Leather", "Chair_Tan", "Chair_LoungeFrame", "Chair_Sculpted", "Chair_Curve",
            "Table_CoffeeRound", "Table_CoffeeGlass", "Table_CoffeeOval", "Table_CoffeeMetal", "Table_Side",
            "Storage_TvConsole", "Decor_Tv", "Storage_Bookshelf", "Storage_Sideboard",
            "Light_Tripod", "Light_Arc", "Rug_Woven", "Decor_Canvas",
            "Plant_Maple", "Plant_Leafy", "Plant_Bamboo", "Plant_Grass", "Plant_Planter",
        }),
        ("Bedroom", new[]
        {
            "Bed_Platform", "Storage_NightWalnut", "Storage_NightOak", "Storage_Wardrobe", "Storage_OpenWardrobe",
            "Storage_Dresser", "Storage_Vanity", "Decor_FloorMirror", "Table_Side", "Light_Globe",
            "Light_Tripod", "Rug_Woven", "Decor_Laundry", "Plant_Leafy",
        }),
        ("Dining", new[]
        {
            "Dining_Table6", "Dining_Table8", "Dining_TableRound", "Dining_TableSquare", "Dining_TableBistro",
            "Dining_TablePedestal", "Dining_ChairBeige", "Dining_ChairRed", "Dining_ChairWhite",
            "Dining_ChairSpindle", "Dining_ChairFarm", "Plant_Bamboo", "Plant_Maple",
        }),
        ("Office", new[]
        {
            "Office_Desk", "Office_Chair", "Dining_Table8", "Storage_Bookshelf", "Chair_LoungeFrame",
            "Chair_Sculpted", "Table_CoffeeOval", "Light_Arc", "Plant_Grass", "Plant_Bamboo", "Plant_Planter",
        }),
        ("Café", new[]
        {
            "Cafe_Table4", "Cafe_Table4Dark", "Cafe_TableLong", "Cafe_TableHigh", "Cafe_TableHighRect",
            "Cafe_ChairBirch", "Cafe_ChairWalnut", "Cafe_ChairIndustrial",
            "Cafe_StoolOrange", "Cafe_StoolTeal", "Cafe_StoolMetal",
            "Cafe_BoothCoral", "Cafe_BoothTeal", "Cafe_BoothGraphite",
            "Cafe_Counter", "Cafe_PrepCounter", "Cafe_Display", "Cafe_DrinkStation", "Cafe_Kiosk", "Cafe_WasteStation",
        }),
    };

    //=========================================================
    // Room sets
    //=========================================================

    private sealed class SetDef
    {
        public string id;
        public string name;
        public string room;
        public Action<SetLayout> layout;
        public SetDef(string id, string name, string room, Action<SetLayout> layout)
        {
            this.id = id; this.name = name; this.room = room; this.layout = layout;
        }
    }

    private static readonly SetDef[] Sets =
    {
        new SetDef("Set_ClassicLounge", "Classic lounge", "Living", s =>
        {
            Vector3 table = s.Size("Table_CoffeeRound");
            Vector3 sofa = s.Size("Sofa_Classic");
            Vector3 chair = s.Size("Chair_Leather");
            s.Place("Rug_Woven", 0f, 0f);
            Placed t = s.Place("Table_CoffeeRound", 0f, 0.05f);
            float sofaZ = 0.05f - (table.z * 0.5f + 0.42f + sofa.z * 0.5f);
            s.Place("Sofa_Classic", 0f, sofaZ);
            float chairX = table.x * 0.5f + 0.42f + chair.z * 0.5f;
            s.Place("Chair_Leather", -chairX, 0.15f, 90f);
            s.Place("Chair_Leather", chairX, 0.15f, -90f);
            s.Place("Light_Tripod", -(sofa.x * 0.5f + 0.35f), sofaZ - 0.05f);
            s.Place("Plant_Maple", sofa.x * 0.5f + 0.45f, sofaZ - 0.05f);
            s.On(t, "Acc_Vases", 0f, 0f);
        }),

        new SetDef("Set_ModernLiving", "Modern living", "Living", s =>
        {
            Vector3 table = s.Size("Table_CoffeeGlass");
            Vector3 sofa = s.Size("Sofa_ThreeSeat");
            s.Place("Rug_Woven", 0f, 0.1f);
            Placed t = s.Place("Table_CoffeeGlass", 0f, 0.1f);
            float sofaZ = 0.1f - (table.z * 0.5f + 0.45f + sofa.z * 0.5f);
            s.Place("Sofa_ThreeSeat", 0f, sofaZ);
            // Two lounge chairs across the table, angled toward the sofa.
            s.Place("Chair_LoungeFrame", -0.85f, 1.25f, 155f);
            s.Place("Chair_LoungeFrame", 0.85f, 1.25f, -155f);
            Placed side = s.Place("Table_Side", sofa.x * 0.5f + 0.35f, sofaZ);
            s.On(side, "Light_Arc", 0f, 0f, 200f);
            s.Place("Light_Tripod", -(sofa.x * 0.5f + 0.35f), sofaZ);
            s.Place("Plant_Bamboo", 0f, sofaZ - sofa.z * 0.5f - 0.3f);
            s.On(t, "Acc_Spheres", -0.2f, 0f);
            s.On(t, "Acc_Books", 0.35f, 0.05f, 90f);
        }),

        new SetDef("Set_MediaLounge", "Media lounge", "Living", s =>
        {
            Vector3 console = s.Size("Storage_TvConsole");
            Vector3 sofa = s.Size("Sofa_Chesterfield");
            Vector3 table = s.Size("Table_CoffeeMetal");
            // TV wall on the left, sofa on the right, both seen side-on from the front.
            float consoleX = -1.45f;
            Placed stand = s.Place("Storage_TvConsole", consoleX, 0f, 90f);
            s.On(stand, "Decor_Tv", 0f, 0f, 90f);
            s.Place("Rug_Woven", 0.15f, 0f, 90f);
            s.Place("Table_CoffeeMetal", 0.15f, 0f, 90f);
            s.Place("Sofa_Chesterfield", 0.15f + table.z * 0.5f + 0.45f + sofa.z * 0.5f, 0f, -90f);
            s.Place("Plant_Grass", consoleX, console.x * 0.5f + 0.3f);
            s.Place("Light_Tripod", consoleX, -(console.x * 0.5f + 0.3f));
            s.Place("Chair_Tan", 0.1f, -(sofa.x * 0.5f + 0.25f), 0f);
        }),

        new SetDef("Set_MinimalBedroom", "Minimalist bedroom", "Bedroom", s =>
        {
            Vector3 bed = s.Size("Bed_Platform");
            Vector3 side = s.Size("Table_Side");
            float bedZ = -0.2f;
            float headZ = bedZ - bed.z * 0.5f;
            s.Place("Rug_Woven", 0f, bedZ + bed.z * 0.25f);
            s.Place("Bed_Platform", 0f, bedZ);
            float standX = bed.x * 0.5f + side.x * 0.5f + 0.08f;
            Placed left = s.Place("Table_Side", -standX, headZ + side.z * 0.5f + 0.15f);
            Placed right = s.Place("Table_Side", standX, headZ + side.z * 0.5f + 0.15f);
            s.On(left, "Light_Globe", 0f, 0f);
            s.On(right, "Light_Globe", 0f, 0f);
            s.Place("Light_Tripod", standX + 0.6f, headZ + 0.35f);
            s.Place("Plant_Leafy", -(standX + 0.65f), headZ + 0.4f);
            s.Place("Decor_Laundry", standX + 0.55f, bedZ + bed.z * 0.45f);
        }),

        new SetDef("Set_ClassicBedroom", "Classic bedroom suite", "Bedroom", s =>
        {
            Vector3 bed = s.Size("Bed_Platform");
            Vector3 night = s.Size("Storage_NightWalnut");
            Vector3 wardrobe = s.Size("Storage_Wardrobe");
            Vector3 dresser = s.Size("Storage_Dresser");
            float bedZ = -0.3f;
            float headZ = bedZ - bed.z * 0.5f;
            s.Place("Bed_Platform", 0f, bedZ);
            float standX = bed.x * 0.5f + night.x * 0.5f + 0.06f;
            Placed left = s.Place("Storage_NightWalnut", -standX, headZ + night.z * 0.5f + 0.1f);
            Placed right = s.Place("Storage_NightWalnut", standX, headZ + night.z * 0.5f + 0.1f);
            s.On(left, "Light_Globe", 0f, 0f);
            s.On(right, "Acc_Vase", 0f, 0f);
            float wallX = standX + night.x * 0.5f + 0.25f;
            s.Place("Storage_Wardrobe", -(wallX + wardrobe.z * 0.5f), bedZ + 0.35f, 90f);
            s.Place("Storage_Dresser", wallX + dresser.z * 0.5f, bedZ + 0.45f, -90f);
            s.Place("Rug_Woven", 0f, bedZ + bed.z * 0.5f + 0.35f);
        }),

        new SetDef("Set_FamilyDining", "Family dining for 6", "Dining", s =>
        {
            Vector3 table = s.Size("Dining_Table6");
            Vector3 chair = s.Size("Dining_ChairBeige");
            Placed t = s.Place("Dining_Table6", 0f, 0f);
            float sideZ = table.z * 0.5f + chair.z * 0.5f - 0.1f;
            float endX = table.x * 0.5f + chair.z * 0.5f - 0.05f;
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                s.Place("Dining_ChairBeige", x, -sideZ, 0f);
                s.Place("Dining_ChairBeige", x, sideZ, 180f);
            }
            s.Place("Dining_ChairBeige", -endX, 0f, 90f);
            s.Place("Dining_ChairBeige", endX, 0f, -90f);
            s.On(t, "Acc_Centerpiece", 0f, 0f);
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                s.On(t, "Acc_PlaceSetting", x, -(table.z * 0.5f - 0.2f));
                s.On(t, "Acc_PlaceSetting", x, table.z * 0.5f - 0.2f);
            }
            s.Place("Plant_Maple", table.x * 0.5f + 0.55f, -(table.z * 0.5f + 0.75f));
        }),

        new SetDef("Set_ModernDining", "Modern dining for 8", "Dining", s =>
        {
            Vector3 table = s.Size("Dining_Table8");
            Vector3 chair = s.Size("Dining_ChairWhite");
            Placed t = s.Place("Dining_Table8", 0f, 0f);
            float sideZ = table.z * 0.5f + chair.z * 0.5f - 0.12f;
            float endX = table.x * 0.5f + chair.z * 0.5f - 0.05f;
            foreach (float x in new[] { -0.75f, 0f, 0.75f })
            {
                s.Place("Dining_ChairWhite", x, -sideZ, 0f);
                s.Place("Dining_ChairWhite", x, sideZ, 180f);
            }
            s.Place("Dining_ChairWhite", -endX, 0f, 90f);
            s.Place("Dining_ChairWhite", endX, 0f, -90f);
            s.On(t, "Acc_CenterPlant", 0f, 0f);
            s.On(t, "Acc_Candlestick", -0.35f, 0f);
            s.On(t, "Acc_Candlestick", 0.35f, 0f);
            s.Place("Plant_Bamboo", 0f, -(sideZ + chair.z * 0.5f + 0.45f));
        }),

        new SetDef("Set_Bistro", "Bistro for four", "Dining", s =>
        {
            Vector3 table = s.Size("Dining_TableRound");
            Vector3 chair = s.Size("Dining_ChairRed");
            Placed t = s.Place("Dining_TableRound", 0f, 0f);
            float r = table.x * 0.5f + chair.z * 0.5f - 0.12f;
            // Chairs on the diagonals so all four are visible from the front.
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                Vector3 p = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, r);
                s.Place("Dining_ChairRed", p.x, p.z, angle + 180f);
            }
            s.On(t, "Acc_Vases", 0f, 0f);
            s.Place("Plant_Grass", 0f, -(r + 0.45f));
        }),

        new SetDef("Set_ExecutiveOffice", "Executive office", "Office", s =>
        {
            Vector3 desk = s.Size("Office_Desk");
            Vector3 chair = s.Size("Office_Chair");
            Vector3 shelf = s.Size("Storage_Bookshelf");
            Placed d = s.Place("Office_Desk", 0f, 0f);
            s.Place("Office_Chair", 0f, -(desk.z * 0.5f + chair.z * 0.5f - 0.05f), 0f);
            s.Place("Chair_LoungeFrame", -0.45f, desk.z * 0.5f + 0.55f, 180f);
            s.Place("Chair_LoungeFrame", 0.45f, desk.z * 0.5f + 0.55f, 180f);
            s.Place("Storage_Bookshelf", -0.35f, -(desk.z * 0.5f + chair.z + 0.45f + shelf.z * 0.5f));
            s.Place("Plant_Grass", desk.x * 0.5f + 0.45f, -(desk.z * 0.5f + 0.45f));
            s.On(d, "Light_Arc", -desk.x * 0.5f + 0.25f, -0.15f, 160f);
            s.On(d, "Acc_Books", desk.x * 0.5f - 0.3f, -0.1f, 90f);
        }),

        new SetDef("Set_MeetingRoom", "Meeting room for 6", "Office", s =>
        {
            Vector3 table = s.Size("Dining_Table8");
            Vector3 chair = s.Size("Office_Chair");
            Placed t = s.Place("Dining_Table8", 0f, 0f);
            float sideZ = table.z * 0.5f + chair.z * 0.5f - 0.05f;
            foreach (float x in new[] { -0.75f, 0f, 0.75f })
            {
                s.Place("Office_Chair", x, -sideZ, 0f);
                s.Place("Office_Chair", x, sideZ, 180f);
            }
            s.On(t, "Acc_CenterPlant", 0f, 0f);
            s.Place("Plant_Bamboo", 0f, -(sideZ + chair.z * 0.5f + 0.45f));
            s.Place("Plant_Planter", table.x * 0.5f + 0.6f, -(sideZ + 0.2f));
        }),

        new SetDef("Set_Reception", "Reception lounge", "Office", s =>
        {
            Vector3 table = s.Size("Table_CoffeeOval");
            Placed t = s.Place("Table_CoffeeOval", 0f, 0f);
            float r = table.x * 0.5f + 0.5f;
            s.Place("Chair_Sculpted", -r, -0.15f, 90f);
            s.Place("Chair_Sculpted", r, -0.15f, -90f);
            s.Place("Chair_LoungeFrame", 0f, -(table.z * 0.5f + 0.6f), 0f);
            s.Place("Plant_Maple", -(r + 0.5f), -(table.z * 0.5f + 0.7f));
            s.Place("Storage_Bookshelf", r + 0.45f, -(table.z * 0.5f + 0.75f));
            s.On(t, "Acc_Spheres", 0f, 0f);
        }),

        new SetDef("Set_CafeTable", "Café table for four", "Café", s =>
        {
            Vector3 table = s.Size("Cafe_Table4");
            Vector3 chair = s.Size("Cafe_ChairBirch");
            Placed t = s.Place("Cafe_Table4", 0f, 0f);
            float dz = table.z * 0.5f + chair.z * 0.5f - 0.12f;
            float dx = table.x * 0.5f + chair.z * 0.5f - 0.12f;
            s.Place("Cafe_ChairBirch", 0f, -dz, 0f);
            s.Place("Cafe_ChairBirch", 0f, dz, 180f);
            s.Place("Cafe_ChairBirch", -dx, 0f, 90f);
            s.Place("Cafe_ChairBirch", dx, 0f, -90f);
            s.On(t, "Acc_TrayMeal", -0.12f, -0.12f);
            s.On(t, "Acc_TrayBurger", 0.12f, 0.15f, 180f);
            s.Place("Plant_Bamboo", 0f, -(dz + chair.z * 0.5f + 0.5f));
        }),

        new SetDef("Set_DinerBooth", "Diner booth", "Café", s =>
        {
            Vector3 table = s.Size("Cafe_TableLong");
            Vector3 booth = s.Size("Cafe_BoothCoral");
            // Seen side-on: booths on the left and right, table between them.
            Placed t = s.Place("Cafe_TableLong", 0f, 0f, 90f);
            float x = table.z * 0.5f + booth.z * 0.5f - 0.05f;
            s.Place("Cafe_BoothCoral", -x, 0f, 90f);
            s.Place("Cafe_BoothCoral", x, 0f, -90f);
            s.On(t, "Acc_TrayMeal", -0.12f, -0.35f, 90f);
            s.On(t, "Acc_TrayBurger", 0.12f, 0.35f, -90f);
        }),

        new SetDef("Set_CounterBar", "Counter bar", "Café", s =>
        {
            Vector3 table = s.Size("Cafe_TableHigh");
            Vector3 stool = s.Size("Cafe_StoolOrange");
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                Placed t = s.Place("Cafe_TableHigh", x, 0f);
                float r = table.x * 0.5f + stool.x * 0.5f - 0.05f;
                s.Place("Cafe_StoolOrange", x - r, 0.12f);
                s.Place("Cafe_StoolTeal", x + r, 0.12f);
                s.On(t, "Acc_TrayBurger", 0f, 0f);
            }
        }),

        new SetDef("Set_OrderPoint", "Order point", "Café", s =>
        {
            Vector3 counter = s.Size("Cafe_Counter");
            Vector3 kiosk = s.Size("Cafe_Kiosk");
            Placed c = s.Place("Cafe_Counter", 0f, 0f);
            s.On(c, "Acc_Register", -counter.x * 0.25f, 0f);
            s.On(c, "Cafe_Display", counter.x * 0.2f, 0f);
            s.Place("Cafe_Kiosk", -(counter.x * 0.5f + kiosk.x * 0.5f + 0.35f), 0.25f);
            s.Place("Cafe_DrinkStation", counter.x * 0.5f + 0.55f, -0.05f);
            s.Place("Plant_Bamboo", 0f, -(counter.z * 0.5f + 0.55f));
        }),
    };

    //=========================================================
    // Menu / batch entry points
    //=========================================================

    [MenuItem("Tools/AR Furniture Designer/Build Furniture Catalog")]
    private static void BuildMenu()
    {
        try
        {
            string summary = BuildAll(null);
            EditorUtility.DisplayDialog("AR Furniture Designer", summary, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>Batch mode: -executeMethod FurnitureCatalogBuilder.BuildBatch [-previewOut folder]</summary>
    public static void BuildBatch()
    {
        string previewOut = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-previewOut")
                previewOut = args[i + 1];
        }
        Debug.Log("[CatalogBuilder] " + BuildAll(previewOut));
    }

    /// <param name="previewFolder">Optional folder (outside Assets) for large room-set previews.</param>
    public static string BuildAll(string previewFolder)
    {
        BuiltinMaterialConverter.Convert(BuiltinMaterialConverter.DefaultFolders);

        EnsureFolder(ItemsFolder);
        EnsureFolder(SetsFolder);
        EnsureFolder(ThumbnailsFolder);

        // 1. Item wrappers
        Dictionary<string, GameObject> items = new Dictionary<string, GameObject>();
        Dictionary<string, Bounds> itemBounds = new Dictionary<string, Bounds>();
        int step = 0;
        int total = Items.Length + Sets.Length;
        List<string> problems = new List<string>();
        foreach (ItemDef def in Items)
        {
            EditorUtility.DisplayProgressBar("Furniture catalog", "Item: " + def.name, step++ / (float)total);
            GameObject wrapper = BuildItemWrapper(def, out Bounds bounds);
            if (wrapper == null)
            {
                problems.Add(def.source);
                continue;
            }
            items[def.id] = wrapper;
            itemBounds[def.id] = bounds;
        }

        // 2. Room sets
        Dictionary<string, GameObject> sets = new Dictionary<string, GameObject>();
        Dictionary<string, int> setPieceCounts = new Dictionary<string, int>();
        Dictionary<string, Bounds> setBounds = new Dictionary<string, Bounds>();
        foreach (SetDef def in Sets)
        {
            EditorUtility.DisplayProgressBar("Furniture catalog", "Room set: " + def.name, step++ / (float)total);
            GameObject set = BuildRoomSet(def, items, itemBounds, out int pieces, out Bounds bounds);
            if (set == null)
                continue;
            sets[def.id] = set;
            setPieceCounts[def.id] = pieces;
            setBounds[def.id] = bounds;
        }

        // 3. Thumbnails and 4. catalogue asset
        EditorUtility.DisplayProgressBar("Furniture catalog", "Rendering thumbnails", 1f);
        FurnitureCatalog catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FurnitureCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.categories.Clear();

        CatalogCategory setTab = new CatalogCategory { displayName = "Room Sets" };
        foreach (SetDef def in Sets)
        {
            if (!sets.TryGetValue(def.id, out GameObject set))
                continue;
            Sprite setThumb = RenderThumbnail(set, def.id, 150f, 34f, 0.05f);
            AssignThumbnail(set, setThumb);
            setTab.entries.Add(new CatalogEntry
            {
                displayName = def.name,
                subtitle = def.room + "  ·  " + setPieceCounts[def.id] + " pieces",
                prefab = set,
                thumbnail = setThumb,
            });
        }
        catalog.categories.Add(setTab);

        Dictionary<string, Sprite> itemThumbs = new Dictionary<string, Sprite>();
        foreach ((string tab, string[] ids) in ItemTabs)
        {
            CatalogCategory category = new CatalogCategory { displayName = tab };
            foreach (string id in ids)
            {
                if (!items.TryGetValue(id, out GameObject prefab))
                    continue;
                if (!itemThumbs.TryGetValue(id, out Sprite thumb))
                {
                    thumb = RenderThumbnail(prefab, id, 148f, 20f, 0.07f);
                    itemThumbs[id] = thumb;
                    AssignThumbnail(prefab, thumb);
                }
                category.entries.Add(new CatalogEntry
                {
                    displayName = ItemName(id),
                    subtitle = SizeLabel(itemBounds[id].size),
                    prefab = prefab,
                    thumbnail = thumb,
                });
            }
            catalog.categories.Add(category);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(previewFolder))
            WritePreviews(previewFolder, sets, setBounds);

        EditorUtility.ClearProgressBar();
        string summary = string.Format("Catalog built: {0} items, {1} room sets, {2} tabs.", items.Count, sets.Count, catalog.categories.Count);
        if (problems.Count > 0)
            summary += "\nMissing source prefabs (skipped):\n" + string.Join("\n", problems);
        return summary;
    }

    //=========================================================
    // Item wrappers
    //=========================================================

    private static GameObject BuildItemWrapper(ItemDef def, out Bounds bounds)
    {
        bounds = default;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(def.source);
        if (source == null)
        {
            Debug.LogWarning("[CatalogBuilder] Missing source prefab: " + def.source);
            return null;
        }

        GameObject root = new GameObject(def.id);
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            Transform mt = model.transform;
            Quaternion authoredRotation = mt.localRotation;
            Vector3 authoredScale = mt.localScale;
            mt.SetParent(root.transform, false);
            mt.localPosition = Vector3.zero;
            mt.localRotation = Quaternion.Euler(0f, def.yaw, 0f) * authoredRotation;
            mt.localScale = authoredScale * def.scale;

            // Centre the footprint on the origin with the base on y = 0.
            Bounds b = PrefabThumbnailRenderer.WorldBounds(root);
            mt.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            bounds = PrefabThumbnailRenderer.WorldBounds(root);

            FurnitureSpec spec = root.AddComponent<FurnitureSpec>();
            spec.displayName = def.name;
            if (Specs.TryGetValue(def.id, out (string type, string material, float kg) s))
            {
                spec.type = s.type;
                spec.material = s.material;
                spec.weightKg = s.kg;
            }
            else
            {
                spec.type = "Furniture";
                Debug.LogWarning("[CatalogBuilder] No spec entry for " + def.id);
            }

            return PrefabUtility.SaveAsPrefabAsset(root, ItemsFolder + "/" + def.id + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    //=========================================================
    // Room sets
    //=========================================================

    private sealed class Placed
    {
        public Vector3 position;
        public float top;
    }

    /// <summary>Layout helper handed to each room set's recipe.</summary>
    private sealed class SetLayout
    {
        private readonly Transform root;
        private readonly Dictionary<string, GameObject> items;
        private readonly Dictionary<string, Bounds> bounds;
        public int pieces;
        public float weightKg;
        public readonly List<string> missing = new List<string>();
        /// <summary>Main material of each piece, most-used first once sorted (for the set's summary line).</summary>
        public readonly Dictionary<string, int> materials = new Dictionary<string, int>();

        public SetLayout(Transform root, Dictionary<string, GameObject> items, Dictionary<string, Bounds> bounds)
        {
            this.root = root; this.items = items; this.bounds = bounds;
        }

        /// <summary>Item size in metres before rotation: x = width, y = height, z = depth (front to back).</summary>
        public Vector3 Size(string id)
        {
            return bounds.TryGetValue(id, out Bounds b) ? b.size : Vector3.one * 0.5f;
        }

        /// <summary>Places an item on the floor with its footprint centred at (x, z), front turned by yaw degrees.</summary>
        public Placed Place(string id, float x, float z, float yaw = 0f)
        {
            return PlaceAt(id, new Vector3(x, 0f, z), yaw);
        }

        /// <summary>Places an item on top of another, offset (dx, dz) from its centre in set space.</summary>
        public Placed On(Placed support, string id, float dx, float dz, float yaw = 0f)
        {
            if (support == null)
                return null;
            return PlaceAt(id, new Vector3(support.position.x + dx, support.top, support.position.z + dz), yaw);
        }

        private Placed PlaceAt(string id, Vector3 position, float yaw)
        {
            if (!items.TryGetValue(id, out GameObject prefab))
            {
                missing.Add(id);
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            pieces++;

            if (Specs.TryGetValue(id, out (string type, string material, float kg) spec))
            {
                weightKg += spec.kg;
                // "Oxblood leather, bentwood frame" -> "Oxblood leather"
                string main = spec.material.Split(',')[0].Trim();
                materials.TryGetValue(main, out int count);
                materials[main] = count + 1;
            }
            return new Placed { position = position, top = position.y + Size(id).y };
        }
    }

    private static GameObject BuildRoomSet(SetDef def, Dictionary<string, GameObject> items, Dictionary<string, Bounds> itemBounds,
        out int pieces, out Bounds bounds)
    {
        pieces = 0;
        bounds = default;
        GameObject root = new GameObject(def.id);
        try
        {
            GameObject content = new GameObject("Layout");
            content.transform.SetParent(root.transform, false);

            SetLayout layout = new SetLayout(content.transform, items, itemBounds);
            def.layout(layout);
            if (layout.missing.Count > 0)
                Debug.LogWarning("[CatalogBuilder] " + def.id + " skipped missing items: " + string.Join(", ", layout.missing));
            if (layout.pieces == 0)
                return null;

            // Centre the whole arrangement's footprint on the origin.
            Bounds b = PrefabThumbnailRenderer.WorldBounds(root);
            content.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            bounds = PrefabThumbnailRenderer.WorldBounds(root);
            pieces = layout.pieces;

            // The set's own details: summed weight and its three most-used materials.
            List<KeyValuePair<string, int>> byUse = new List<KeyValuePair<string, int>>(layout.materials);
            byUse.Sort((a, c) => c.Value.CompareTo(a.Value));
            List<string> top = new List<string>();
            for (int i = 0; i < byUse.Count && i < 3; i++)
                top.Add(byUse[i].Key);

            FurnitureSpec spec = root.AddComponent<FurnitureSpec>();
            spec.displayName = def.name;
            spec.type = SetTypes.TryGetValue(def.room, out string setType) ? setType : def.room + " set";
            spec.material = string.Join(", ", top);
            spec.weightKg = layout.weightKg;
            spec.isRoomSet = true;
            spec.pieceCount = pieces;

            return PrefabUtility.SaveAsPrefabAsset(root, SetsFolder + "/" + def.id + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    //=========================================================
    // Thumbnails and labels
    //=========================================================

    private static Sprite RenderThumbnail(GameObject prefab, string id, float yaw, float pitch, float padding)
    {
        Texture2D image = PrefabThumbnailRenderer.Render(prefab, ThumbnailSize, yaw, pitch, padding, false);
        string path = ThumbnailsFolder + "/" + id + ".png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = ThumbnailSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Stores the card image on the prefab's FurnitureSpec so the spec sheet can show it.</summary>
    private static void AssignThumbnail(GameObject prefab, Sprite thumbnail)
    {
        FurnitureSpec spec = prefab != null ? prefab.GetComponent<FurnitureSpec>() : null;
        if (spec == null)
            return;
        spec.thumbnail = thumbnail;
        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static string ItemName(string id)
    {
        foreach (ItemDef def in Items)
        {
            if (def.id == id)
                return def.name;
        }
        return id;
    }

    /// <summary>"W × D × H cm", rounded to whole centimetres.</summary>
    private static string SizeLabel(Vector3 size)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} × {1} × {2} cm",
            Mathf.RoundToInt(size.x * 100f), Mathf.RoundToInt(size.z * 100f), Mathf.RoundToInt(size.y * 100f));
    }

    private static void WritePreviews(string folder, Dictionary<string, GameObject> sets, Dictionary<string, Bounds> setBounds)
    {
        Directory.CreateDirectory(folder);
        foreach (KeyValuePair<string, GameObject> pair in sets)
        {
            Texture2D front = PrefabThumbnailRenderer.Render(pair.Value, 512, 150f, 34f, 0.04f, false);
            Texture2D top = PrefabThumbnailRenderer.Render(pair.Value, 512, 180f, 88f, 0.04f, false);
            Texture2D both = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
            both.SetPixels(0, 0, 512, 512, front.GetPixels());
            both.SetPixels(512, 0, 512, 512, top.GetPixels());
            both.Apply();
            File.WriteAllBytes(Path.Combine(folder, pair.Key + ".png"), both.EncodeToPNG());
            Object.DestroyImmediate(front);
            Object.DestroyImmediate(top);
            Object.DestroyImmediate(both);
            Bounds b = setBounds[pair.Key];
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[CatalogBuilder] {0}: {1:F2} x {2:F2} m footprint, {3:F2} m tall",
                pair.Key, b.size.x, b.size.z, b.size.y));
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
