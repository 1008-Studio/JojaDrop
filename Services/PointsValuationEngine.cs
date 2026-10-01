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
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (canonicalBaseValue < 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalBaseValue), "Canonical base value cannot be negative.");

        RouteValuationBreakdown[] routes = profile.Routes.Select(route => EvaluateRoute(route, canonicalBaseValue)).ToArray();
        RouteValuationBreakdown? selected = routes.Where(route => route.IsReliable)
            .OrderBy(route => route.Points).FirstOrDefault();
        if (selected is null)
        {
            return new ValuationBreakdown(profile, canonicalBaseValue, canonicalBaseValue, 1d, routes,
                "No high- or medium-confidence route; retained the canonical base value.");
        }

        return new ValuationBreakdown(profile, canonicalBaseValue, selected.Points, selected.Multiplier, routes,
            "Selected the cheapest high- or medium-confidence route.");
    }

    public double NormalizeProbabilityRarity(double chance)
    {
        if (double.IsNaN(chance) || double.IsInfinity(chance) || chance < 0d || chance > 1d)
            throw new ArgumentOutOfRangeException(nameof(chance), "Chance must be between zero and one.");

        return Math.Clamp(-Math.Log(Math.Max(chance, ProbabilityEpsilon)) / ProbabilityNormalizer, 0d, 1d);
    }

    private RouteValuationBreakdown EvaluateRoute(AcquisitionRoute route, int canonicalBaseValue)
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

        return new RouteValuationBreakdown(route, difficulty, scarcity, access, effort, restrictions, uniqueness,
            farmability, score, multiplier, ToPoints(canonicalBaseValue, multiplier), IsReliable(route.Confidence));
    }

    private static bool IsReliable(AcquisitionConfidence confidence) => confidence is AcquisitionConfidence.Medium or AcquisitionConfidence.High;
    private static double Normalize(int? value) => value.GetValueOrDefault() / (double)AcquisitionMetrics.Maximum;
    private static int ToPoints(int canonicalBaseValue, double multiplier) => canonicalBaseValue == 0
        ? 0
        : (int)Math.Min(int.MaxValue, Math.Round(canonicalBaseValue * multiplier, MidpointRounding.AwayFromZero));
}
