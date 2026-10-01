using StardewValley;
using SObject = StardewValley.Object;

namespace JojaDrop.Services;

public sealed class ItemValueService
{
    private readonly StardewAcquisitionProfileProvider profiles;
    private readonly ValuationSimulationExporter exporter = new();
    private readonly FinalPointsCache finalPoints = new();
    private IReadOnlyDictionary<string, int>? cachedPoints;

    public ItemValueService(StardewAcquisitionProfileProvider profiles)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /// <summary>Get stable JojaDrop points for one supported ordinary object, or null if unsupported.</summary>
    public int? GetValue(Item item)
    {
        // Specialized Object subclasses (including furniture) need their own valuation rules.
        if (item.GetType() != typeof(SObject)
            || item is not SObject obj
            || obj.bigCraftable.Value
            || obj.IsRecipe
            || obj.questItem.Value
            || !obj.canBeShipped())
        {
            return null;
        }

        if (cachedPoints is null)
        {
            IReadOnlyDictionary<string, int> uniquePoints = exporter.Simulate(profiles.BuildAllObjects()).Entries
                .ToDictionary(entry => entry.QualifiedItemId, entry => entry.Points, StringComparer.Ordinal);
            cachedPoints = finalPoints.Get(uniquePoints, profiles.BuildProductionRecipes());
        }
        return cachedPoints.TryGetValue(obj.QualifiedItemId, out int points) && points > 0 ? points : null;
    }

    /// <summary>Discard points derived from resolved data assets after those assets change.</summary>
    public void InvalidateCache()
    {
        cachedPoints = null;
        finalPoints.Invalidate();
    }
}
