using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Snapshot cache for final points; its owner invalidates it with the resolved data assets.</summary>
public sealed class FinalPointsCache
{
    private readonly FinalPointsCalculator calculator = new();
    private IReadOnlyDictionary<string, int>? points;

    public IReadOnlyDictionary<string, int> Get(IReadOnlyDictionary<string, int> uniquePoints,
        IReadOnlyList<ProductionRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(uniquePoints);
        ArgumentNullException.ThrowIfNull(recipes);
        return points ??= uniquePoints.Keys.ToDictionary(id => id,
            id => calculator.Calculate(id, uniquePoints, recipes).Points, StringComparer.Ordinal);
    }

    public void Invalidate() => points = null;
}
