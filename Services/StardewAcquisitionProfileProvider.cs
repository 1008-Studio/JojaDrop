using JojaDrop.Models;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Shops;

namespace JojaDrop.Services;

/// <summary>Adapts the resolved Stardew 1.6 content assets into acquisition-index facts.</summary>
public sealed class StardewAcquisitionProfileProvider
{
    private readonly AcquisitionProfileIndexer indexer = new();

    public AcquisitionProfile Build(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            throw new ArgumentException("A qualified item ID is required.", nameof(qualifiedItemId));
        return indexer.Build(qualifiedItemId, ReadData());
    }

    private static AcquisitionIndexData ReadData()
    {
        Dictionary<string, string> fishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
        Dictionary<string, ShopData> shops = Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops");
        return new AcquisitionIndexData(
            fishData.Select(entry => ParseFish(QualifyObjectId(entry.Key), entry.Value)).ToArray(),
            Game1.locationData.SelectMany(location => (location.Value.Fish ?? Enumerable.Empty<SpawnFishData>())
                .Select(spawn => ToFishSpawn(location.Key, spawn))).ToArray(),
            Game1.cropData.Select(crop => ToCrop(crop.Key, crop.Value)).ToArray(),
            shops.SelectMany(shop => (shop.Value.Items ?? Enumerable.Empty<ShopItemData>())
                .Select(item => ToShopOffer(shop.Key, shop.Value, item))).ToArray(),
            Game1.locationData.SelectMany(location => (location.Value.Forage ?? Enumerable.Empty<SpawnForageData>())
                .Select(spawn => ToForageSpawn(location.Key, spawn))).ToArray(),
            Game1.objectData.Select(item => new ItemMetadata(QualifyObjectId(item.Key), item.Value.Category,
                item.Value.ContextTags?.ToArray() ?? Array.Empty<string>())).ToArray());
    }

    private static FishingDefinition ParseFish(string itemId, string raw)
    {
        string[] fields = raw.Split('/');
        if (fields.Length < 2)
            return new FishingDefinition(itemId, null, null, null, null, Array.Empty<string>(), null);
        if (string.Equals(fields[1], "trap", StringComparison.OrdinalIgnoreCase))
        {
            return new FishingDefinition(itemId, null, "trap", ParseDouble(fields, 2), null,
                Array.Empty<string>(), null);
        }

        return new FishingDefinition(itemId, ParseInt(fields, 1), Get(fields, 2), ParseDouble(fields, 10),
            Get(fields, 7), SplitPairs(Get(fields, 5)), ParseInt(fields, 12));
    }

    private static FishingSpawn ToFishSpawn(string locationId, SpawnFishData spawn) => new(QualifyObjectId(spawn.ItemId),
        locationId, spawn.Chance, spawn.Season?.ToString(), spawn.Condition, spawn.MinFishingLevel,
        spawn.MinDistanceFromShore, spawn.IsBossFish, spawn.CatchLimit, spawn.RequireMagicBait);

    private static CropDefinition ToCrop(string seedId, CropData crop) => new(QualifyObjectId(seedId),
        QualifyObjectId(crop.HarvestItemId), crop.DaysInPhase?.Sum() ?? 0,
        crop.Seasons?.Select(season => season.ToString()).ToArray() ?? Array.Empty<string>(), crop.RegrowDays,
        crop.HarvestMinStack, crop.HarvestMaxStack, crop.ExtraHarvestChance,
        crop.HarvestMaxIncreasePerFarmingLevel, crop.NeedsWatering, crop.IsPaddyCrop, crop.IsRaised,
        crop.PlantableLocationRules?.Count > 0);

    private static ShopOffer ToShopOffer(string shopId, ShopData shop, ShopItemData item) => new(shopId,
        QualifyObjectId(item.ItemId), item.Price, item.AvailableStock,
        shop.Currency == 0, item.TradeItemId, item.TradeItemAmount, item.Condition);

    private static ForageSpawn ToForageSpawn(string locationId, SpawnForageData spawn) => new(QualifyObjectId(spawn.ItemId),
        locationId, spawn.Chance, spawn.Season?.ToString(), spawn.Condition);

    private static string QualifyObjectId(string itemId) => string.IsNullOrWhiteSpace(itemId)
        ? itemId
        : ItemRegistry.ManuallyQualifyItemId(itemId, ItemRegistry.type_object, overrideIfQualified: false);

    private static string? Get(IReadOnlyList<string> fields, int index) => index < fields.Count ? fields[index] : null;
    private static int? ParseInt(IReadOnlyList<string> fields, int index) => int.TryParse(Get(fields, index), out int value) ? value : null;
    private static double? ParseDouble(IReadOnlyList<string> fields, int index) => double.TryParse(Get(fields, index),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : null;
    private static IReadOnlyList<string> SplitPairs(string? value) => string.IsNullOrWhiteSpace(value)
        ? Array.Empty<string>()
        : value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
