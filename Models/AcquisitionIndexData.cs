namespace JojaDrop.Models;

/// <summary>Resolved game-data facts consumed by the pure acquisition indexer.</summary>
public sealed record AcquisitionIndexData(
    IReadOnlyList<FishingDefinition> Fish,
    IReadOnlyList<FishingSpawn> FishSpawns,
    IReadOnlyList<CropDefinition> Crops,
    IReadOnlyList<ShopOffer> ShopOffers,
    IReadOnlyList<ForageSpawn> Forage,
    IReadOnlyList<ItemMetadata> Items)
{
    public IReadOnlyList<GeodeDrop> GeodeDrops { get; init; } = Array.Empty<GeodeDrop>();
    public IReadOnlyList<MachineProduction> MachineProductions { get; init; } = Array.Empty<MachineProduction>();
    public IReadOnlyList<RecipeProduction> Recipes { get; init; } = Array.Empty<RecipeProduction>();

    public static AcquisitionIndexData Empty { get; } = new(
        Array.Empty<FishingDefinition>(), Array.Empty<FishingSpawn>(), Array.Empty<CropDefinition>(),
        Array.Empty<ShopOffer>(), Array.Empty<ForageSpawn>(), Array.Empty<ItemMetadata>());
}

public sealed record FishingDefinition(string ItemId, int? Difficulty, string? Behavior, double? CatchChance,
    string? Weather, IReadOnlyList<string> TimeWindows, int? MinFishingLevel);

public sealed record FishingSpawn(string ItemId, string LocationId, double? Chance, string? Season,
    string? Condition, int? MinFishingLevel, int? MinDistanceFromShore, bool IsBossFish,
    int? CatchLimit, bool RequiresMagicBait);

public sealed record CropDefinition(string SeedItemId, string HarvestItemId, int GrowthDays,
    IReadOnlyList<string> Seasons, int RegrowDays, int HarvestMinStack, int HarvestMaxStack,
    double ExtraHarvestChance, float HarvestMaxIncreasePerFarmingLevel, bool NeedsWatering,
    bool IsPaddyCrop, bool IsRaised, bool HasPlantingRestrictions);

public sealed record ShopOffer(string ShopId, string ItemId, int Price, int AvailableStock,
    bool IsGoldCurrency, string? TradeItemId, int TradeItemAmount, string? Condition);

public sealed record ForageSpawn(string ItemId, string LocationId, double? Chance, string? Season,
    string? Condition);

public sealed record ItemMetadata(string QualifiedItemId, int Category, IReadOnlyList<string> ContextTags);

public sealed record GeodeDrop(string GeodeItemId, string ItemId, double? Chance, int MinStack, int MaxStack,
    string? Condition);

public sealed record ProductionInput(string ItemId, int Quantity, bool IsCategory = false);

public sealed record MachineProduction(string MachineItemId, IReadOnlyList<ProductionInput> Inputs, string ItemId,
    int MinOutput, int MaxOutput, int MinutesUntilReady, int DaysUntilReady, string? Condition);

public sealed record RecipeProduction(AcquisitionKind Kind, string RecipeId, IReadOnlyList<ProductionInput> Inputs,
    string ItemId, int OutputQuantity, string? UnlockCondition);
