namespace JojaDrop.Services;

public sealed class UpgradeCalculator
{
    /// <summary>Get a probability from 0 to 1. Invalid values and non-upgrades throw.</summary>
    public double CalculateChance(int sourceValue, int targetValue)
    {
        if (sourceValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceValue), "Source value must be positive.");

        if (targetValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetValue), "Target value must be positive.");

        if (targetValue <= sourceValue)
            throw new ArgumentException("Target value must exceed source value.", nameof(targetValue));

        return Math.Clamp((double)sourceValue / targetValue, 0d, 1d);
    }
}
