using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Builds evidenced acquisition profiles from already-resolved game-data facts.</summary>
public sealed class AcquisitionProfileIndexer
{
    public AcquisitionProfile Build(string qualifiedItemId, AcquisitionIndexData data)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            throw new ArgumentException("A qualified item ID is required.", nameof(qualifiedItemId));
        ArgumentNullException.ThrowIfNull(data);

        var routes = new List<AcquisitionRoute>();
        AddFishingRoutes(routes, qualifiedItemId, data);
        AddFarmingRoutes(routes, qualifiedItemId, data);
        AddShopRoutes(routes, qualifiedItemId, data.ShopOffers);
        AddForageRoutes(routes, qualifiedItemId, data);
        AddGeodeRoutes(routes, qualifiedItemId, data.GeodeDrops);
        AddMachineRoutes(routes, qualifiedItemId, data.MachineProductions);
        AddRecipeRoutes(routes, qualifiedItemId, data.Recipes);

        if (routes.Count == 0)
        {
            routes.Add(new AcquisitionRoute(AcquisitionKind.Unknown,
                new AcquisitionMetrics(null, null, null, null, null, null, null), AcquisitionConfidence.Low,
                new[] { new AcquisitionEvidence("Acquisition index", "No matching data-defined route") }));
        }

        return new AcquisitionProfile(qualifiedItemId, routes);
    }

    private static void AddFishingRoutes(List<AcquisitionRoute> routes, string itemId, AcquisitionIndexData data)
    {
        FishingDefinition? fish = data.Fish.FirstOrDefault(entry => entry.ItemId == itemId);
        foreach (FishingSpawn spawn in data.FishSpawns.Where(entry => entry.ItemId == itemId))
        {
            int? level = Max(fish?.MinFishingLevel, spawn.MinFishingLevel);
            var evidence = new List<AcquisitionEvidence>
            {
                new("Data/Locations", $"{spawn.LocationId}; chance={Format(spawn.Chance)}; season={spawn.Season ?? "any"}", spawn.Condition)
            };
            if (fish is not null)
            {
                evidence.Add(new AcquisitionEvidence("Data/Fish",
                    $"difficulty={Format(fish.Difficulty)}; behavior={fish.Behavior ?? "unknown"}; catchChance={Format(fish.CatchChance)}; "
                    + $"weather={fish.Weather ?? "any"}; time={string.Join(",", fish.TimeWindows)}; level={Format(level)}"));
            }
            if (spawn.IsBossFish || spawn.CatchLimit.HasValue || spawn.RequiresMagicBait)
            {
                evidence.Add(new AcquisitionEvidence("Data/Locations",
                    $"boss={spawn.IsBossFish}; catchLimit={Format(spawn.CatchLimit)}; magicBait={spawn.RequiresMagicBait}"));
            }

            routes.Add(new AcquisitionRoute(AcquisitionKind.Fishing,
                new AcquisitionMetrics(Percent(fish?.Difficulty), InversePercent(spawn.Chance), Level(level),
                    Distance(spawn.MinDistanceFromShore), RestrictionCount(spawn.Condition, spawn.Season, fish?.Weather,
                        fish?.TimeWindows.Count > 0, spawn.RequiresMagicBait),
                    spawn.IsBossFish || spawn.CatchLimit == 1 ? 100 : null, 0, spawn.Chance),
                fish is null ? AcquisitionConfidence.Medium : AcquisitionConfidence.High, evidence));
        }
    }

    private static void AddFarmingRoutes(List<AcquisitionRoute> routes, string itemId, AcquisitionIndexData data)
    {
        foreach (CropDefinition crop in data.Crops.Where(entry => entry.HarvestItemId == itemId))
        {
            ShopOffer[] seedOffers = data.ShopOffers.Where(offer => offer.ItemId == crop.SeedItemId).ToArray();
            var evidence = new List<AcquisitionEvidence>
            {
                new("Data/Crops", $"seed={crop.SeedItemId}; growthDays={crop.GrowthDays}; seasons={string.Join(",", crop.Seasons)}; "
                    + $"regrowDays={crop.RegrowDays}; yield={crop.HarvestMinStack}-{crop.HarvestMaxStack}; "
                    + $"extraHarvestChance={crop.ExtraHarvestChance:R}; levelYield={crop.HarvestMaxIncreasePerFarmingLevel:R}; "
                    + $"watering={crop.NeedsWatering}; paddy={crop.IsPaddyCrop}; raised={crop.IsRaised}; "
                    + $"plantingRules={crop.HasPlantingRestrictions}")
            };
            foreach (ShopOffer offer in seedOffers)
                evidence.Add(ShopEvidence("Data/Shops", offer, "seed"));

            routes.Add(new AcquisitionRoute(AcquisitionKind.Farming,
                new AcquisitionMetrics(0, null, seedOffers.Length == 0 ? null : 0, Days(crop.GrowthDays),
                    RestrictionCount(null, crop.Seasons.Count == 1 ? crop.Seasons[0] : null, null,
                        crop.NeedsWatering || crop.IsRaised || crop.HasPlantingRestrictions, false),
                    null, crop.RegrowDays >= 0 ? 100 : 50), AcquisitionConfidence.High, evidence));
        }
    }

    private static void AddShopRoutes(List<AcquisitionRoute> routes, string itemId, IEnumerable<ShopOffer> offers)
    {
        foreach (ShopOffer offer in offers.Where(entry => entry.ItemId == itemId))
        {
            bool unlimited = offer.AvailableStock < 0;
            routes.Add(new AcquisitionRoute(AcquisitionKind.Shop,
                new AcquisitionMetrics(0, unlimited ? 0 : 100, offer.Condition is null ? 0 : 50, 0,
                    RestrictionCount(offer.Condition, null, null, false, !offer.IsGoldCurrency || !string.IsNullOrWhiteSpace(offer.TradeItemId)),
                    null, unlimited ? 100 : 0), AcquisitionConfidence.High,
                new[] { ShopEvidence("Data/Shops", offer, "item") }));
        }
    }

    private static void AddForageRoutes(List<AcquisitionRoute> routes, string itemId, AcquisitionIndexData data)
    {
        foreach (ForageSpawn spawn in data.Forage.Where(entry => entry.ItemId == itemId))
        {
            routes.Add(new AcquisitionRoute(AcquisitionKind.Foraging,
                new AcquisitionMetrics(0, InversePercent(spawn.Chance), null, 0,
                    RestrictionCount(spawn.Condition, spawn.Season, null, false, false), null, 0, spawn.Chance), AcquisitionConfidence.High,
                new[] { new AcquisitionEvidence("Data/Locations", $"{spawn.LocationId}; chance={Format(spawn.Chance)}; season={spawn.Season ?? "any"}", spawn.Condition) }));
        }

        if (data.Forage.All(entry => entry.ItemId != itemId)
            && data.Items.FirstOrDefault(entry => entry.QualifiedItemId == itemId)?.ContextTags.Contains("forage_item") == true)
        {
            routes.Add(new AcquisitionRoute(AcquisitionKind.Foraging,
                new AcquisitionMetrics(null, null, null, null, null, null, null), AcquisitionConfidence.Low,
                new[] { new AcquisitionEvidence("Data/Objects", "context tag: forage_item; no matching location spawn data") }));
        }
    }

    private static void AddGeodeRoutes(List<AcquisitionRoute> routes, string itemId, IEnumerable<GeodeDrop> drops)
    {
        foreach (GeodeDrop drop in drops.Where(entry => entry.ItemId == itemId))
        {
            routes.Add(new AcquisitionRoute(AcquisitionKind.Geode,
                new AcquisitionMetrics(null, InversePercent(drop.Chance), null, null,
                    RestrictionCount(drop.Condition, null, null, false, false), null, 0, drop.Chance), AcquisitionConfidence.High,
                new[] { new AcquisitionEvidence("Data/Objects", $"geode={drop.GeodeItemId}; chance={Format(drop.Chance)}; output={drop.MinStack}-{drop.MaxStack}", drop.Condition) }));
        }
    }

    private static void AddMachineRoutes(List<AcquisitionRoute> routes, string itemId, IEnumerable<MachineProduction> productions)
    {
        foreach (MachineProduction production in productions.Where(entry => entry.ItemId == itemId))
        {
            routes.Add(new AcquisitionRoute(AcquisitionKind.Machine,
                new AcquisitionMetrics(null, null, null, ProductionTime(production.MinutesUntilReady, production.DaysUntilReady),
                    RestrictionCount(production.Condition, null, null, production.Inputs.Count > 1, false), null, null),
                AcquisitionConfidence.High,
                new[] { new AcquisitionEvidence("Data/Machines",
                    $"machine={production.MachineItemId}; input={FormatInputs(production.Inputs)}; output={production.MinOutput}-{production.MaxOutput}; "
                    + $"minutes={production.MinutesUntilReady}; days={production.DaysUntilReady}", production.Condition) }));
        }
    }

    private static void AddRecipeRoutes(List<AcquisitionRoute> routes, string itemId, IEnumerable<RecipeProduction> recipes)
    {
        foreach (RecipeProduction recipe in recipes.Where(entry => entry.ItemId == itemId))
        {
            routes.Add(new AcquisitionRoute(recipe.Kind,
                new AcquisitionMetrics(null, null, null, Math.Min(AcquisitionMetrics.Maximum, recipe.Inputs.Count * 10),
                    RestrictionCount(recipe.UnlockCondition, null, null, recipe.Inputs.Count > 1, false), null, null),
                AcquisitionConfidence.High,
                new[] { new AcquisitionEvidence(recipe.Kind == AcquisitionKind.Crafting ? "Data/CraftingRecipes" : "Data/CookingRecipes",
                    $"recipe={recipe.RecipeId}; input={FormatInputs(recipe.Inputs)}; output={recipe.OutputQuantity}", recipe.UnlockCondition) }));
        }
    }

    private static AcquisitionEvidence ShopEvidence(string source, ShopOffer offer, string role)
    {
        string currency = offer.IsGoldCurrency && string.IsNullOrWhiteSpace(offer.TradeItemId)
            ? "gold"
            : $"non-gold trade={offer.TradeItemId ?? "unknown"} x{offer.TradeItemAmount}";
        return new AcquisitionEvidence(source,
            $"{role}; shop={offer.ShopId}; price={offer.Price}; currency={currency}; stock={offer.AvailableStock}", offer.Condition);
    }

    private static int? Percent(int? value) => value is >= 0 ? Math.Min(AcquisitionMetrics.Maximum, value.Value) : null;
    private static int? InversePercent(double? chance) => chance is >= 0 and <= 1
        ? AcquisitionMetrics.Maximum - (int)Math.Round(chance.Value * AcquisitionMetrics.Maximum) : null;
    private static int? Level(int? value) => value is >= 0 ? Math.Min(AcquisitionMetrics.Maximum, value.Value * 10) : null;
    private static int? Distance(int? value) => value is >= 0 ? Math.Min(AcquisitionMetrics.Maximum, value.Value * 10) : null;
    private static int? Days(int value) => value >= 0 ? Math.Min(AcquisitionMetrics.Maximum, value * 5) : null;
    private static int? ProductionTime(int minutes, int days) => minutes < 0 || days < 0
        ? null
        : Math.Min(AcquisitionMetrics.Maximum, minutes / 10 + days * AcquisitionMetrics.Maximum);
    private static int? Max(int? first, int? second) => first is null ? second : second is null ? first : Math.Max(first.Value, second.Value);
    private static int RestrictionCount(string? condition, string? season, string? weather, bool extra, bool specialCurrency)
    {
        int count = (string.IsNullOrWhiteSpace(condition) ? 0 : 1) + (string.IsNullOrWhiteSpace(season) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(weather) ? 0 : 1) + (extra ? 1 : 0) + (specialCurrency ? 1 : 0);
        return Math.Min(AcquisitionMetrics.Maximum, count * 20);
    }

    private static string Format<T>(T? value) => value?.ToString() ?? "unknown";
    private static string FormatInputs(IEnumerable<ProductionInput> inputs) => string.Join(", ", inputs.Select(input =>
        $"{(input.IsCategory ? "category=" : string.Empty)}{input.ItemId} x{input.Quantity}"));
}
