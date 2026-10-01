using JojaDrop.Models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using SObject = StardewValley.Object;

namespace JojaDrop.Services;

/// <summary>Builds and caches the supported object items which can be used as upgrade targets.</summary>
public sealed class TargetItemProvider
{
    private readonly ItemValueService itemValues;
    private readonly UpgradeCalculator upgradeCalculator;
    private readonly IMonitor? monitor;
    private IReadOnlyList<CachedTarget>? cachedTargets;
    private IDictionary<string, ObjectData>? cachedObjectData;

    public TargetItemProvider(ItemValueService itemValues, UpgradeCalculator upgradeCalculator, IMonitor? monitor = null)
    {
        this.itemValues = itemValues ?? throw new ArgumentNullException(nameof(itemValues));
        this.upgradeCalculator = upgradeCalculator ?? throw new ArgumentNullException(nameof(upgradeCalculator));
        this.monitor = monitor;
    }

    /// <summary>Get preview-only targets with a positive value, optionally narrowed by upgrade probability.</summary>
    /// <param name="sourceItem">The selected source item whose unit value seeds the 1 → 1 preview math.</param>
    /// <param name="sourceQuantity">The selected source quantity; probability filters compare the target
    /// against the total source value (unit value × quantity), not against one unit.</param>
    /// <param name="filterMode">Optional probability filter. The unfiltered All mode is unchanged.</param>
    public IReadOnlyList<TargetItemOption> GetTargets(Item sourceItem, int sourceQuantity, TargetFilterMode filterMode = TargetFilterMode.All)
    {
        ArgumentNullException.ThrowIfNull(sourceItem);

        int? sourceValue = itemValues.GetValue(sourceItem);
        if (!sourceValue.HasValue || sourceValue.Value <= 0)
            return Array.Empty<TargetItemOption>();

        // The filter must price the whole selected batch: Bait x20 @ 1g filters
        // against 20g, so its x2 targets sit around 40g. The total is recomputed
        // on every call, so a quantity change is picked up when the picker reopens.
        int totalSourceValue = upgradeCalculator.GetTotalSourceValue((sourceValue.Value, Math.Max(1, sourceQuantity)));

        IEnumerable<TargetItemOption> options = GetCachedTargets()
            .Where(target => target.Value > 0)
            .Select(target => new TargetItemOption(
                target.PreviewItem,
                target.Value,
                upgradeCalculator.CalculateChance(sourceValue.Value, target.Value),
                (double)target.Value / sourceValue.Value));

        // The probability filter only narrows the existing candidate list; validity rules,
        // sorting, the 1 → 1 preview math and the unfiltered All mode stay exactly as before.
        if (filterMode != TargetFilterMode.All)
            options = options.Where(option => TargetProbabilityFilter.Matches(filterMode,
                upgradeCalculator.CalculateChance(totalSourceValue, option.Value)));

        return options.ToArray();
    }

    /// <summary>Discard the cached game-data scan, for example after object data is invalidated.</summary>
    public void InvalidateCache()
    {
        cachedTargets = null;
        cachedObjectData = null;
    }

    private IReadOnlyList<CachedTarget> GetCachedTargets()
    {
        IDictionary<string, ObjectData> objectData = Game1.objectData;
        if (cachedTargets is not null && ReferenceEquals(cachedObjectData, objectData))
            return cachedTargets;

        var targets = new List<CachedTarget>();
        foreach ((string objectId, ObjectData definition) in objectData)
        {
            if (definition is null)
                continue;

            try
            {
                // Object-data keys are unqualified. Force the object type so custom data cannot
                // be interpreted as another ItemRegistry type.
                string qualifiedItemId = ItemRegistry.ManuallyQualifyItemId(
                    objectId,
                    ItemRegistry.type_object,
                    overrideIfQualified: true
                );
                ParsedItemData? parsed = ItemRegistry.GetData(qualifiedItemId);
                if (parsed is null
                    || parsed.IsErrorItem
                    || parsed.ItemType.Identifier != ItemRegistry.type_object
                    || parsed.RawData is not ObjectData)
                {
                    continue;
                }

                // allowNull makes an invalid definition a skipped entry rather than an error item.
                Item? preview = ItemRegistry.Create(qualifiedItemId, amount: 1, quality: 0, allowNull: true);
                if (preview is not SObject obj
                    || obj.bigCraftable.Value
                    || obj.IsRecipe
                    || obj.questItem.Value)
                {
                    continue;
                }

                int? value = itemValues.GetValue(preview);
                if (!value.HasValue || value.Value <= 0)
                    continue;

                targets.Add(new CachedTarget(preview, value.Value));
            }
            catch (Exception ex)
            {
                monitor?.Log($"Skipped invalid object target '{objectId}': {ex.Message}", LogLevel.Trace);
            }
        }

        cachedObjectData = objectData;
        cachedTargets = targets
            .OrderBy(target => target.Value)
            .ThenBy(target => target.PreviewItem.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return cachedTargets;
    }

    private sealed record CachedTarget(Item PreviewItem, int Value);
}
