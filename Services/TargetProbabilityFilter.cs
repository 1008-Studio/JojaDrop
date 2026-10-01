using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>
/// Central configuration for the target probability filters. The UI only deals with
/// <see cref="TargetFilterMode"/>; every numeric center, range and epsilon lives here so the
/// probability thresholds are never duplicated or hard-coded in presentation code.
/// </summary>
public static class TargetProbabilityFilter
{
    /// <summary>
    /// Relative tolerance applied to every filter center. For x2 this reproduces the
    /// 40%..60% window around the central 50% upgrade chance.
    /// </summary>
    public const double RangeTolerance = 0.2;

    /// <summary>Tolerance used when comparing a probability against the range bounds.</summary>
    private const double Epsilon = 1e-12;

    /// <summary>Get the central upgrade probability (1 → 1) a filter mode targets, or null for <see cref="TargetFilterMode.All"/>.</summary>
    public static double? GetCenter(TargetFilterMode mode) => mode switch
    {
        TargetFilterMode.X2 => 0.5,
        TargetFilterMode.X3 => 1.0 / 3.0,
        TargetFilterMode.X5 => 0.2,
        TargetFilterMode.X10 => 0.1,
        _ => null
    };

    /// <summary>Get the inclusive probability range a filter mode accepts, or false when the mode does not filter.</summary>
    public static bool TryGetRange(TargetFilterMode mode, out double minChance, out double maxChance)
    {
        double? center = GetCenter(mode);
        if (center is not { } value)
        {
            minChance = 0;
            maxChance = 0;
            return false;
        }

        minChance = value * (1 - RangeTolerance);
        maxChance = value * (1 + RangeTolerance);
        return true;
    }

    /// <summary>
    /// Returns whether an upgrade probability belongs to the filter mode. <see cref="TargetFilterMode.All"/>
    /// accepts everything; invalid or out-of-range probabilities are rejected for every other mode.
    /// </summary>
    public static bool Matches(TargetFilterMode mode, double chance)
    {
        if (mode == TargetFilterMode.All)
            return true;

        if (double.IsNaN(chance) || !TryGetRange(mode, out double minChance, out double maxChance))
            return false;

        return chance >= minChance - Epsilon && chance <= maxChance + Epsilon;
    }

    /// <summary>Get the rounded central chance as a display string (for example "50%"), or an empty string for <see cref="TargetFilterMode.All"/>.</summary>
    public static string GetChanceText(TargetFilterMode mode)
    {
        double? center = GetCenter(mode);
        return center is { } value ? $"{value * 100:0}%" : "";
    }
}
