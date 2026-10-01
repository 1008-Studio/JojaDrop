using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Pure, bounded points valuation from a canonical base value and acquisition routes.</summary>
public sealed class PointsValuationEngine
{
    public const double MinimumMultiplier = 0.80d;
    public const double MaximumMultiplier = 10d;
    private const double ProbabilityNormalizer = 6.907755278982137d;
    private const double ProbabilityEpsilon = 0.000000001d;

    public ValuationBreakdown Evaluate(AcquisitionProfile profile, int canonicalBaseValue)
        => Evaluate(profile, canonicalBaseValue, Array.Empty<ValuationInput>());

    /// <summary>Values a profile using optional canonical values for its static production inputs.</summary>
    public ValuationBreakdown Evaluate(AcquisitionProfile profile, int canonicalBaseValue,
        IEnumerable<ValuationInput> availableInputs)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (canonicalBaseValue < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalBaseValue), "Canonical base value cannot be negative.");
        ArgumentNullException.ThrowIfNull(availableInputs);

        Dictionary<string, ValuationInput> catalog = availableInputs
            .Append(new ValuationInput(profile, canonicalBaseValue))
            .GroupBy(input => input.Profile.QualifiedItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        return Evaluate(profile, canonicalBaseValue, catalog, new Dictionary<string, ValuationBreakdown>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }

    private ValuationBreakdown Evaluate(AcquisitionProfile profile, int canonicalBaseValue,
        IReadOnlyDictionary<string, ValuationInput> catalog, IDictionary<string, ValuationBreakdown> cache,
        ISet<string> visiting)
    {
        if (cache.TryGetValue(profile.QualifiedItemId, out ValuationBreakdown? cached))
            return cached;

        visiting.Add(profile.QualifiedItemId);
        RouteValuationBreakdown[] routes = profile.Routes
            .Select(route => EvaluateRoute(route, canonicalBaseValue, catalog, cache, visiting)).ToArray();
        visiting.Remove(profile.QualifiedItemId);
        RouteValuationBreakdown? selected = routes.Where(route => route.IsReliable)
            .OrderBy(route => route.Points).FirstOrDefault();
        if (selected is null)
        {
            var fallback = new ValuationBreakdown(profile, canonicalBaseValue, canonicalBaseValue, 1d, routes,
                "No high- or medium-confidence route; retained the canonical base value.");
            cache[profile.QualifiedItemId] = fallback;
            return fallback;
        }

        double selectedMultiplier = canonicalBaseValue == 0 ? selected.Multiplier : selected.Points / (double)canonicalBaseValue;
        var result = new ValuationBreakdown(profile, canonicalBaseValue, selected.Points, selectedMultiplier, routes,
            "Selected the cheapest high- or medium-confidence route, including a verified production floor when available.");
        cache[profile.QualifiedItemId] = result;
        return result;
    }

    public double NormalizeProbabilityRarity(double chance)
    {
        if (double.IsNaN(chance) || double.IsInfinity(chance) || chance < 0d || chance > 1d)
            throw new ArgumentOutOfRangeException(nameof(chance), "Chance must be between zero and one.");

        return Math.Clamp(-Math.Log(Math.Max(chance, ProbabilityEpsilon)) / ProbabilityNormalizer, 0d, 1d);
    }

    private RouteValuationBreakdown EvaluateRoute(AcquisitionRoute route, int canonicalBaseValue,
        IReadOnlyDictionary<string, ValuationInput> catalog, IDictionary<string, ValuationBreakdown> cache,
        ISet<string> visiting)
    {
        AcquisitionMetrics metrics = route.Metrics;
        double difficulty = Normalize(metrics.Difficulty);
        double scarcity = metrics.AvailabilityChance.HasValue
            ? NormalizeProbabilityRarity(metrics.AvailabilityChance.Value)
            : Normalize(metrics.Scarcity);
        double access = Normalize(metrics.Access);
        double effort = Normalize(metrics.Effort);
        double restrictions = Normalize(metrics.Restrictions);
        double uniqueness = Normalize(metrics.Uniqueness);
        double farmability = Normalize(metrics.Farmability);
        double score = 1.25d * difficulty + 1.50d * scarcity + access + 0.75d * effort
            + 0.75d * restrictions + 1.25d * uniqueness - farmability;
        double multiplier = Math.Clamp(Math.Exp(0.55d * score), MinimumMultiplier, MaximumMultiplier);

        int points = ToPoints(canonicalBaseValue, multiplier);
        int? productionFloor = null;
        double? productionMultiplier = null;
        string? productionReason = null;
        if (route.Production is not null)
        {
            (productionFloor, productionMultiplier, productionReason) = ResolveProduction(route.Production, catalog, cache, visiting);
            if (productionFloor.HasValue)
            {
                productionMultiplier = ProductionModifier(effort, restrictions);
                points = Math.Max(points, ToPoints(productionFloor.Value, productionMultiplier!.Value));
            }
        }

        return new RouteValuationBreakdown(route, difficulty, scarcity, access, effort, restrictions, uniqueness,
            farmability, score, multiplier, points, IsReliable(route.Confidence), productionFloor,
            productionMultiplier, productionReason);
    }

    private (int? Floor, double? Multiplier, string? Reason) ResolveProduction(ProductionRelationship production,
        IReadOnlyDictionary<string, ValuationInput> catalog, IDictionary<string, ValuationBreakdown> cache,
        ISet<string> visiting)
    {
        if (!production.ExpectedOutput.HasValue)
            return (null, null, "Random or custom output has no deterministic expected output.");
        if (production.Inputs.Count == 0)
            return (null, null, "No static consumed inputs were supplied.");

        double inputCost = 0d;
        foreach (ProductionInput input in production.Inputs)
        {
            if (input.IsCategory)
                return (null, null, $"Category input '{input.ItemId}' cannot be resolved to one canonical item.");
            if (visiting.Contains(input.ItemId))
                return (null, null, $"Cycle detected through '{input.ItemId}'.");
            if (!catalog.TryGetValue(input.ItemId, out ValuationInput? dependency))
                return (null, null, $"Missing canonical input '{input.ItemId}'.");

            ValuationBreakdown inputValue = Evaluate(dependency.Profile, dependency.CanonicalBaseValue, catalog, cache, visiting);
            inputCost = Math.Min(int.MaxValue, inputCost + inputValue.Points * (double)input.Quantity);
        }

        int floor = (int)Math.Min(int.MaxValue, Math.Ceiling(inputCost / production.ExpectedOutput.Value));
        return (floor, null, "Static input cost divided by deterministic output quantity.");
    }

    private static bool IsReliable(AcquisitionConfidence confidence) => confidence is AcquisitionConfidence.Medium or AcquisitionConfidence.High;
    private static double Normalize(int? value) => value.GetValueOrDefault() / (double)AcquisitionMetrics.Maximum;
    private static double ProductionModifier(double effort, double restrictions)
        => Math.Clamp(1d + 0.25d * (effort + restrictions), 1d, 1.5d);
    private static int ToPoints(int canonicalBaseValue, double multiplier) => canonicalBaseValue == 0
        ? 0
        : (int)Math.Min(int.MaxValue, Math.Round(canonicalBaseValue * multiplier, MidpointRounding.AwayFromZero));
}
