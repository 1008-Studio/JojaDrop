using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Pure single-output target eligibility, chance, and picker selection rules.</summary>
public static class TargetEconomics
{
    /// <summary>A target must differ from the source and be worth strictly more than the full source batch.</summary>
    public static bool IsEligible(int sourceQuantity, int sourceValue, string? sourceQualifiedItemId,
        int targetValue, string? targetQualifiedItemId)
    {
        return sourceQuantity > 0
            && sourceValue > 0
            && targetValue > 0
            && !string.IsNullOrWhiteSpace(sourceQualifiedItemId)
            && !string.IsNullOrWhiteSpace(targetQualifiedItemId)
            && !string.Equals(sourceQualifiedItemId, targetQualifiedItemId, StringComparison.Ordinal)
            && (long)targetValue > (long)sourceQuantity * sourceValue;
    }

    /// <summary>Gets p = q * S / T for an eligible one-output target.</summary>
    public static double CalculateChance(int sourceQuantity, int sourceValue, int targetValue)
    {
        Validate(sourceQuantity, sourceValue, targetValue);
        return (double)((long)sourceQuantity * sourceValue) / targetValue;
    }

    /// <summary>Gets the actual target-to-source-batch multiplier.</summary>
    public static double CalculateMultiplier(int sourceQuantity, int sourceValue, int targetValue)
    {
        Validate(sourceQuantity, sourceValue, targetValue);
        return (double)targetValue / ((long)sourceQuantity * sourceValue);
    }

    /// <summary>Filters discovered candidates using the active source batch and probability mode.</summary>
    public static IReadOnlyList<TargetCandidateOption> SelectTargets(string sourceQualifiedItemId, int sourceQuantity,
        int sourceValue, IEnumerable<TargetCandidate> candidates, TargetFilterMode filter)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => IsEligible(sourceQuantity, sourceValue, sourceQualifiedItemId,
                candidate.Value, candidate.QualifiedItemId))
            .Select(candidate => new TargetCandidateOption(candidate.QualifiedItemId, candidate.Value,
                CalculateChance(sourceQuantity, sourceValue, candidate.Value),
                CalculateMultiplier(sourceQuantity, sourceValue, candidate.Value)))
            .Where(candidate => TargetProbabilityFilter.Matches(filter, candidate.BatchChance))
            .ToArray();
    }

    private static void Validate(int sourceQuantity, int sourceValue, int targetValue)
    {
        if (sourceQuantity < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceQuantity), "Source quantity must be at least one.");
        if (sourceValue < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceValue), "Source value must be positive.");
        if (targetValue < 1)
            throw new ArgumentOutOfRangeException(nameof(targetValue), "Target value must be positive.");
    }
}

/// <summary>A pure, game-independent target candidate.</summary>
public sealed record TargetCandidate(string QualifiedItemId, int Value);

/// <summary>A pure eligible target with economics for the active source batch.</summary>
public sealed record TargetCandidateOption(string QualifiedItemId, int Value, double BatchChance, double BatchMultiplier);
