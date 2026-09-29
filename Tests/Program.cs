using JojaDrop.Services;

var calculator = new UpgradeCalculator();
var roller = new UpgradeRoller();

foreach (var (source, target, expected) in new[]
{
    (1000, 2000, 0.5),
    (1000, 5000, 0.2),
    (1000, 10000, 0.1),
    (1, 3, 1d / 3),
    (1, int.MaxValue, 1d / int.MaxValue),
    (int.MaxValue - 1, int.MaxValue, (double)(int.MaxValue - 1) / int.MaxValue)
})
{
    double actual = calculator.CalculateChance(source, target);
    if (Math.Abs(actual - expected) > 1e-12 || actual <= 0 || actual > 1)
        throw new InvalidOperationException($"Incorrect probability for {source} -> {target}: {actual}.");
}

foreach (int source in new[] { 0, -1, int.MinValue })
    ExpectException<ArgumentOutOfRangeException>(source, 1000, "sourceValue");

foreach (int target in new[] { 0, -1, int.MinValue })
    ExpectException<ArgumentOutOfRangeException>(1000, target, "targetValue");

ExpectException<ArgumentException>(1000, 1000, "targetValue");
ExpectException<ArgumentException>(1000, 500, "targetValue");
ExpectException<ArgumentException>(int.MaxValue, int.MaxValue, "targetValue");

for (int i = 0; i < 10; i++)
{
    if (roller.Roll(0d))
        throw new InvalidOperationException("Chance 0 must always fail.");

    if (!roller.Roll(1d))
        throw new InvalidOperationException("Chance 1 must always succeed.");
}

ExpectRollException(-0.01d);
ExpectRollException(1.01d);

void ExpectException<TException>(int source, int target, string parameter) where TException : ArgumentException
{
    try
    {
        calculator.CalculateChance(source, target);
    }
    catch (TException exception) when (exception.ParamName == parameter)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} for {source} -> {target}.");
}

void ExpectRollException(double chance)
{
    try
    {
        roller.Roll(chance);
    }
    catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "chance")
    {
        return;
    }

    throw new InvalidOperationException($"Expected ArgumentOutOfRangeException for chance {chance}.");
}
