using JojaDrop.Services;

var calculator = new UpgradeCalculator();

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
    ExpectException<ArgumentOutOfRangeException>(source, 2000, "sourceValue");

foreach (int target in new[] { 0, -1, int.MinValue })
    ExpectException<ArgumentOutOfRangeException>(1000, target, "targetValue");

ExpectException<ArgumentException>(1000, 1000, "targetValue");
ExpectException<ArgumentException>(2000, 1000, "targetValue");
ExpectException<ArgumentException>(int.MaxValue, int.MaxValue, "targetValue");

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
