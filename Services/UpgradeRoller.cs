namespace JojaDrop.Services;

public sealed class UpgradeRoller
{
    private readonly Random random;

    public UpgradeRoller(Random? random = null)
    {
        this.random = random ?? Random.Shared;
    }

    public bool Roll(double chance)
    {
        if (double.IsNaN(chance) || chance < 0d || chance > 1d)
            throw new ArgumentOutOfRangeException(nameof(chance), "Chance must be between 0 and 1.");

        return random.NextDouble() < chance;
    }
}
