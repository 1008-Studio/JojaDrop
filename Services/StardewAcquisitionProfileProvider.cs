using JojaDrop.Models;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Shops;

namespace JojaDrop.Services;

/// <summary>Adapts the resolved Stardew 1.6 content assets into acquisition-index facts.</summary>
public sealed class StardewAcquisitionProfileProvider
{
    private readonly AcquisitionProfileCache cache;

    public StardewAcquisitionProfileProvider(IReadOnlyDictionary<string, ItemValuationOverride>? overrides = null,
        Action<string>? diagnostic = null) => cache = new AcquisitionProfileCache(ReadData, overrides, diagnostic);

    public AcquisitionProfile Build(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            throw new ArgumentException("A qualified item ID is required.", nameof(qualifiedItemId));
        return cache.Build(qualifiedItemId);
    }

    /// <summary>Builds every currently resolved ordinary-object profile from one game-data snapshot.</summary>
    public IReadOnlyList<ValuationSimulationInput> BuildAllObjects()
    {
        return Game1.objectData.Select(item => new ValuationSimulationInput(item.Value.DisplayName,
            cache.Build(QualifyObjectId(item.Key)), Math.Max(0, item.Value.Price))).ToArray();
    }

    public void Invalidate() => cache.Invalidate();

    private static AcquisitionIndexData ReadData()
    {
        Dictionary<string, string> fishData = Game1.content.Load<Dictionary<string, string>>("Data/Fish");
        Dictionary<string, ShopData> shops = Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops");
        Dictionary<string, MachineData> machines = Game1.content.Load<Dictionary<string, MachineData>>("Data/Machines");
        Dictionary<string, string> craftingRecipes = Game1.content.Load<Dictionary<string, string>>("Data/CraftingRecipes");
        Dictionary<string, string> cookingRecipes = Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes");
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
                item.Value.ContextTags?.ToArray() ?? Array.Empty<string>())).ToArray())
        {
            GeodeDrops = Game1.objectData.SelectMany(geode => (geode.Value.GeodeDrops ?? Enumerable.Empty<StardewValley.GameData.Objects.ObjectGeodeDropData>())
                .Where(drop => !string.IsNullOrWhiteSpace(drop.ItemId))
                .Select(drop => new GeodeDrop(QualifyObjectId(geode.Key), QualifyObjectId(drop.ItemId), drop.Chance,
                    drop.MinStack, drop.MaxStack, drop.Condition))).ToArray(),
            MachineProductions = machines.SelectMany(machine => ToMachineProductions(machine.Key, machine.Value)).ToArray(),
            Recipes = craftingRecipes.SelectMany(recipe => ParseRecipe(recipe.Key, recipe.Value, AcquisitionKind.Crafting))
                .Concat(cookingRecipes.SelectMany(recipe => ParseRecipe(recipe.Key, recipe.Value, AcquisitionKind.Cooking))).ToArray()
        };
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

    private static IEnumerable<MachineProduction> ToMachineProductions(string machineItemId, MachineData machine)
    {
        foreach (MachineOutputRule rule in machine.OutputRules ?? Enumerable.Empty<MachineOutputRule>())
        {
            ProductionInput[] inputs = (rule.Triggers ?? Enumerable.Empty<MachineOutputTriggerRule>())
                .Where(trigger => !string.IsNullOrWhiteSpace(trigger.RequiredItemId))
                .Select(trigger => new ProductionInput(QualifyObjectId(trigger.RequiredItemId), Math.Max(1, trigger.RequiredCount))).Concat(
                    (machine.AdditionalConsumedItems ?? Enumerable.Empty<MachineItemAdditionalConsumedItems>())
                    .Where(input => !string.IsNullOrWhiteSpace(input.ItemId))
                    .Select(input => new ProductionInput(QualifyObjectId(input.ItemId), Math.Max(1, input.RequiredCount))))
                .ToArray();
            string? triggerCondition = string.Join(" && ", (rule.Triggers ?? Enumerable.Empty<MachineOutputTriggerRule>())
                .Select(trigger => trigger.Condition).Where(condition => !string.IsNullOrWhiteSpace(condition)));

            foreach (MachineItemOutput output in rule.OutputItem ?? Enumerable.Empty<MachineItemOutput>())
            {
                if (!TryQualifyStaticObjectId(output.ItemId, output.OutputMethod, out string outputId))
                    continue;

                yield return new MachineProduction(QualifyObjectId(machineItemId), inputs, outputId, output.MinStack,
                    output.MaxStack, rule.MinutesUntilReady, rule.DaysUntilReady, JoinConditions(triggerCondition, output.Condition),
                    IsRandomOutput: output.RandomItemId?.Count > 0);
            }
        }
    }

    private static IEnumerable<RecipeProduction> ParseRecipe(string recipeId, string raw, AcquisitionKind kind)
    {
        string[] fields = raw.Split('/');
        int unlockIndex = kind == AcquisitionKind.Crafting ? 4 : 3;
        int bigCraftableIndex = 3;
        if (fields.Length <= unlockIndex)
            yield break;

        bool isBigCraftable = kind == AcquisitionKind.Crafting && bool.TryParse(Get(fields, bigCraftableIndex), out bool parsed) && parsed;
        ProductionInput[] inputs = ParseItemPairs(Get(fields, 0), isOutput: false, isBigCraftable: false).ToArray();
        foreach (ProductionInput output in ParseItemPairs(Get(fields, 2), isOutput: true, isBigCraftable: isBigCraftable))
            yield return new RecipeProduction(kind, recipeId, inputs, output.ItemId, output.Quantity, Get(fields, unlockIndex));
    }

    private static IEnumerable<ProductionInput> ParseItemPairs(string? raw, bool isOutput, bool isBigCraftable)
    {
        string[] values = SplitPairs(raw).ToArray();
        for (int index = 0; index < values.Length; index += 2)
        {
            if (index == values.Length - 1 && isOutput)
            {
                if (TryQualifyRecipeItemId(values[index], isBigCraftable, out string outputItemId))
                    yield return new ProductionInput(outputItemId, 1);
                continue;
            }
            if (index + 1 >= values.Length || !int.TryParse(values[index + 1], out int quantity) || quantity < 1)
                continue;

            bool isCategory = !isOutput && int.TryParse(values[index], out int numericId) && numericId < 0;
            if (isCategory)
            {
                yield return new ProductionInput(values[index], quantity, true);
                continue;
            }
            if (TryQualifyRecipeItemId(values[index], isBigCraftable && isOutput, out string recipeItemId))
                yield return new ProductionInput(recipeItemId, quantity);
        }
    }

    private static string QualifyObjectId(string itemId) => string.IsNullOrWhiteSpace(itemId)
        ? itemId
        : ItemRegistry.ManuallyQualifyItemId(itemId, ItemRegistry.type_object, overrideIfQualified: false);

    private static bool TryQualifyStaticObjectId(string? itemId, string? outputMethod, out string qualifiedItemId)
    {
        qualifiedItemId = string.Empty;
        if (string.IsNullOrWhiteSpace(itemId) || !string.IsNullOrWhiteSpace(outputMethod) || itemId.Contains(' '))
            return false;

        qualifiedItemId = QualifyObjectId(itemId);
        return ItemRegistry.GetData(qualifiedItemId) is not null;
    }

    private static bool TryQualifyRecipeItemId(string itemId, bool isBigCraftable, out string qualifiedItemId)
    {
        qualifiedItemId = string.Empty;
        if (string.IsNullOrWhiteSpace(itemId) || itemId.Contains(' '))
            return false;

        qualifiedItemId = ItemRegistry.ManuallyQualifyItemId(itemId,
            isBigCraftable ? ItemRegistry.type_bigCraftable : ItemRegistry.type_object, overrideIfQualified: false);
        return true;
    }

    private static string? JoinConditions(string? first, string? second) => string.IsNullOrWhiteSpace(first)
        ? second
        : string.IsNullOrWhiteSpace(second) ? first : $"{first} && {second}";

    private static string? Get(IReadOnlyList<string> fields, int index) => index < fields.Count ? fields[index] : null;
    private static int? ParseInt(IReadOnlyList<string> fields, int index) => int.TryParse(Get(fields, index), out int value) ? value : null;
    private static double? ParseDouble(IReadOnlyList<string> fields, int index) => double.TryParse(Get(fields, index),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : null;
    private static IReadOnlyList<string> SplitPairs(string? value) => string.IsNullOrWhiteSpace(value)
        ? Array.Empty<string>()
        : value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
