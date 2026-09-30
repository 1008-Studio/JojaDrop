namespace JojaDrop.Services;

public sealed class UpgradeCalculator
{
    /// <summary>
    /// Gets the probability for one batch roll: q source items of value S are spent; on success, r target
    /// items of value T are received, while failure receives none. q and r must be positive; callers should
    /// normally constrain r to q. One item of each reproduces the legacy source-value/target-value chance.
    /// </summary>
    public double CalculateChance(int sourceCount, int targetCount, int sourceValue, int targetValue)
    {
        if (sourceCount < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceCount), "Source count must be at least one.");

        if (targetCount < 1)
            throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be at least one.");

        ValidateValues(sourceValue, targetValue);

        return Math.Clamp((double)sourceCount * sourceValue / ((double)targetCount * targetValue), 0d, 1d);
    }

    /// <summary>Returns whether the target batch is worth at least as much as the source batch.</summary>
    public bool IsBatchTargetValueValid(int sourceCount, int targetCount, int sourceValue, int targetValue)
    {
        if (sourceCount < 1)
            throw new ArgumentOutOfRangeException(nameof(sourceCount), "Source count must be at least one.");

        if (targetCount < 1)
            throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be at least one.");

        ValidateValues(sourceValue, targetValue);
        return (long)targetCount * targetValue >= (long)sourceCount * sourceValue;
    }

    /// <summary>Gets the legacy one-source-to-one-target batch probability.</summary>
    public double CalculateChance(int sourceValue, int targetValue)
    {
        return CalculateChance(1, 1, sourceValue, targetValue);
    }

    private static void ValidateValues(int sourceValue, int targetValue)
    {
        if (sourceValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceValue), "Source value must be positive.");

        if (targetValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetValue), "Target value must be positive.");

        if (targetValue <= sourceValue)
            throw new ArgumentException("Target value must exceed source value.", nameof(targetValue));
    }
}
