using System.Collections.Generic;

namespace EvuMagnets.Core;

public sealed class MagnetTierInfo
{
    public MagnetTierInfo(
        string id,
        string englishName,
        string englishDescription,
        string station,
        int stationLevel,
        float[] ranges,
        string craft,
        string upgrades,
        bool forged = true)
    {
        Id = id;
        EnglishName = englishName;
        EnglishDescription = englishDescription;
        Station = station;
        StationLevel = stationLevel;
        Ranges = ranges;
        Craft = craft;
        Upgrades = upgrades;
        Forged = forged;
    }

    public string Id { get; }

    public string PrefabName => PickupRules.PrefabPrefix + Id;

    public string Token => "item_" + PrefabName;

    public string EnglishName { get; }

    public string EnglishDescription { get; }

    public string Station { get; }

    public int StationLevel { get; }

    public float[] Ranges { get; }

    public string Craft { get; }

    public string Upgrades { get; }

    public bool Forged { get; }
}

public static class MagnetCast
{
    public const string Id = "cast";

    public const string PrefabName = "evu_cast_bloodgold";

    public const string Token = "item_" + PrefabName;

    public const string EnglishName = "Magnet Cast";

    public const string EnglishDescription =
        "A black-forged mold. Set it in the frost foundry with liquid frost, and a bloodgold magnet comes out.";

    public const string Station = "blackforge";

    public const int StationLevel = 1;

    public const string Craft = "Iron:10,Thunderstone:1,Gold:10,FrostCore:5";

    public const string Foundry = "piece_FrostFoundry";

    public const string OutputPrefab = "evu_magnet_bloodgold";

    public const float CookTime = 50f;
}

public static class MagnetCatalog
{
    public static IReadOnlyList<MagnetTierInfo> All { get; } = new[]
    {
        new MagnetTierInfo(
            "iron",
            "Iron Magnet",
            "A rough iron lodestone. It draws loose items in from a short way off.",
            "forge",
            1,
            new[] { 4f, 5f, 6f, 8f },
            "Iron:20,Thunderstone:1,Ectoplasm:5",
            "Iron:10,Thunderstone:1;Iron:20,Thunderstone:2;Iron:40,Thunderstone:4"),
        new MagnetTierInfo(
            "silver",
            "Silver Magnet",
            "Cold silver around a thunder stone. The pull reaches farther than iron.",
            "forge",
            2,
            new[] { 8f, 10f, 12f, 14f },
            "Iron:10,Silver:10,Thunderstone:1,Obsidian:5",
            "Silver:10,Thunderstone:1;Silver:20,Thunderstone:2;Silver:40,Thunderstone:4"),
        new MagnetTierInfo(
            "blackmetal",
            "Black Metal Magnet",
            "Dark green plains metal. It gathers drops from across a camp.",
            "forge",
            3,
            new[] { 14f, 16f, 18f, 20f },
            "Iron:10,BlackMetal:10,Thunderstone:1,Crystal:10",
            "BlackMetal:10,Thunderstone:1;BlackMetal:20,Thunderstone:2;BlackMetal:40,Thunderstone:4"),
        new MagnetTierInfo(
            "flametal",
            "Flametal Magnet",
            "Hot orange flametal, still unfinished. It pulls in everything nearby.",
            "blackforge",
            1,
            new[] { 20f, 22f, 24f, 28f },
            "Iron:10,Flametal:10,Thunderstone:1,Eitr:5",
            "Flametal:10,Thunderstone:1;Flametal:20,Thunderstone:2;Flametal:40,Thunderstone:4"),
        new MagnetTierInfo(
            "bloodgold",
            "Bloodgold Magnet",
            "Gold rimmed with frost. The frost foundry casts it, and it draws items in from a long way across the snow.",
            "",
            1,
            new[] { 36f },
            "",
            "",
            false),
    };

    public static bool TryGet(string? prefabName, out MagnetTierInfo? tier)
    {
        tier = null;
        if (prefabName == null || !PickupRules.IsMagnet(prefabName))
        {
            return false;
        }

        var id = prefabName.Substring(PickupRules.PrefabPrefix.Length);
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Id == id)
            {
                tier = All[i];
                return true;
            }
        }

        return false;
    }
}
