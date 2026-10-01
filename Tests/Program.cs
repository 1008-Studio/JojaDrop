using JojaDrop.Models;
using JojaDrop.Services;
using StardewValley;

var calculator = new UpgradeCalculator();
var roller = new UpgradeRoller();
var inventoryPlanner = new InventoryBatchPlanner();

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
    double batchActual = calculator.CalculateChance(1, 1, source, target);
    if (Math.Abs(actual - expected) > 1e-12 || Math.Abs(batchActual - actual) > 1e-12 || actual <= 0 || actual > 1)
        throw new InvalidOperationException($"Incorrect probability for {source} -> {target}: {actual}.");
}

foreach (var (sourceCount, targetCount, sourceValue, targetValue, expected) in new[]
{
    (1, 1, 1000, 2000, 0.5),
    (2, 2, 1000, 2000, 0.5),
    (3, 2, 1000, 2000, 0.75),
    (4, 1, 1000, 2000, 1d)
})
{
    double actual = calculator.CalculateChance(sourceCount, targetCount, sourceValue, targetValue);
    if (Math.Abs(actual - expected) > 1e-12 || actual < 0 || actual > 1)
        throw new InvalidOperationException($"Incorrect batch probability for {sourceCount}x{sourceValue} -> {targetCount}x{targetValue}: {actual}.");
}

foreach (int count in new[] { 0, -1, int.MinValue })
{
    ExpectBatchException<ArgumentOutOfRangeException>(count, 1, "sourceCount");
    ExpectBatchException<ArgumentOutOfRangeException>(1, count, "targetCount");
}

if (calculator.IsBatchTargetValueValid(3, 1, 2, 4)
    || !calculator.IsBatchTargetValueValid(3, 1, 2, 6)
    || !calculator.IsBatchTargetValueValid(3, 2, 2, 4))
{
    throw new InvalidOperationException("Batch target validation must compare total values.");
}

if (Math.Abs(calculator.CalculateChance(23, 100, 2, 4) - 0.115d) > 1e-12
    || calculator.IsBatchTargetValueValid(23, 1, 2, 4)
    || !calculator.IsBatchTargetValueValid(23, 12, 2, 4)
    || !calculator.IsBatchTargetValueValid(23, 100, 2, 4)
    || !calculator.IsBatchTargetValueValid(int.MaxValue, int.MaxValue, int.MaxValue - 1, int.MaxValue))
{
    throw new InvalidOperationException("Target batches must be independent of source quantity and use safe totals.");
}

foreach (var (sourceCount, sourceValue, targetValue, expected) in new[]
{
    (23, 2, 1, 46),
    (23, 2, 3, 16),
    (23, 2, 4, 12),
    (23, 2, 100, 1),
    (1, 100, 3, 34)
})
{
    if (!calculator.TryGetMinimumTargetQuantity(sourceCount, sourceValue, targetValue, out int actual) || actual != expected)
        throw new InvalidOperationException("Incorrect minimum target quantity.");
}

if (calculator.TryGetMinimumTargetQuantity(23, 2, 0, out _)
    || calculator.TryGetMinimumTargetQuantity(int.MaxValue, int.MaxValue, 1, out _)
    || Math.Abs(calculator.CalculateChance(23, 46, 2, 1) - 1d) > 1e-12
    || Math.Abs(calculator.CalculateChance(23, 16, 2, 3) - 46d / 48d) > 1e-12)
{
    throw new InvalidOperationException("Cheaper targets must use safe minimum quantities.");
}

foreach (int source in new[] { 0, -1, int.MinValue })
    ExpectException<ArgumentOutOfRangeException>(source, 1000, "sourceValue");

foreach (int target in new[] { 0, -1, int.MinValue })
    ExpectException<ArgumentOutOfRangeException>(1000, target, "targetValue");

// Target probability filter: range configuration.
if (!TargetProbabilityFilter.TryGetRange(TargetFilterMode.X2, out double x2Min, out double x2Max)
    || Math.Abs(x2Min - 0.4) > 1e-12 || Math.Abs(x2Max - 0.6) > 1e-12)
{
    throw new InvalidOperationException("The x2 filter must cover 40%..60%.");
}

if (TargetProbabilityFilter.TryGetRange(TargetFilterMode.All, out _, out _))
    throw new InvalidOperationException("The All mode must not define a probability range.");

foreach (TargetFilterMode mode in new[] { TargetFilterMode.X2, TargetFilterMode.X3, TargetFilterMode.X5, TargetFilterMode.X10 })
{
    if (!TargetProbabilityFilter.TryGetRange(mode, out double min, out double max) || !(min < max))
        throw new InvalidOperationException($"Filter {mode} must expose an ordered probability range.");

    double center = TargetProbabilityFilter.GetCenter(mode)!.Value;
    if (center < min || center > max || !TargetProbabilityFilter.Matches(mode, center))
        throw new InvalidOperationException($"Filter {mode} must accept its own center {center}.");
}

// The All mode never filters probabilities.
foreach (double chance in new[] { 0d, 0.37d, 1d, double.NaN })
{
    if (!TargetProbabilityFilter.Matches(TargetFilterMode.All, chance))
        throw new InvalidOperationException("The All mode must keep every probability.");
}

// Invalid probabilities never match a probability filter.
foreach (TargetFilterMode mode in new[] { TargetFilterMode.X2, TargetFilterMode.X3, TargetFilterMode.X5, TargetFilterMode.X10 })
{
    if (TargetProbabilityFilter.Matches(mode, double.NaN)
        || TargetProbabilityFilter.Matches(mode, 0d)
        || TargetProbabilityFilter.Matches(mode, 1d)
        || TargetProbabilityFilter.Matches(mode, calculator.CalculateChance(200, 100)))
    {
        throw new InvalidOperationException($"Filter {mode} must reject zero, one, invalid and non-upgrade probabilities.");
    }
}

// x2: 40%..60% inclusive, everything outside excluded.
foreach (var (sourceValue, targetValue, expected) in new[]
{
    (39, 100, false),
    (40, 100, true),
    (45, 100, true),
    (50, 100, true),
    (60, 100, true),
    (61, 100, false)
})
{
    ExpectFilter(TargetFilterMode.X2, calculator.CalculateChance(sourceValue, targetValue), expected);
}

// x3: center 1/3 with the shared relative range.
ExpectFilter(TargetFilterMode.X3, calculator.CalculateChance(1, 3), true);
foreach (var (sourceValue, targetValue, expected) in new[]
{
    (26, 100, false),
    (27, 100, true),
    (33, 100, true),
    (40, 100, true),
    (41, 100, false)
})
{
    ExpectFilter(TargetFilterMode.X3, calculator.CalculateChance(sourceValue, targetValue), expected);
}

// x5: center 20%.
ExpectFilter(TargetFilterMode.X5, calculator.CalculateChance(20, 100), true);
foreach (var (sourceValue, targetValue, expected) in new[]
{
    (15, 100, false),
    (16, 100, true),
    (24, 100, true),
    (25, 100, false)
})
{
    ExpectFilter(TargetFilterMode.X5, calculator.CalculateChance(sourceValue, targetValue), expected);
}

// x10: center 10%.
ExpectFilter(TargetFilterMode.X10, calculator.CalculateChance(10, 100), true);
foreach (var (sourceValue, targetValue, expected) in new[]
{
    (7, 100, false),
    (8, 100, true),
    (12, 100, true),
    (13, 100, false)
})
{
    ExpectFilter(TargetFilterMode.X10, calculator.CalculateChance(sourceValue, targetValue), expected);
}

// Filtering can only remove candidates, never add unsupported ones.
double[] candidateChances = [0.05, 0.39, 0.4, 0.5, 0.6, 0.61, 0.9, 1d];
if (candidateChances.Count(chance => TargetProbabilityFilter.Matches(TargetFilterMode.X2, chance)) != 3
    || candidateChances.Count(chance => TargetProbabilityFilter.Matches(TargetFilterMode.All, chance)) != candidateChances.Length)
{
    throw new InvalidOperationException("The probability filter must only narrow the existing candidate list.");
}

// The filtered pipeline output stays an ordered subset of the unfiltered output
// (candidates sorted by value ascending, exactly like TargetItemProvider caches them).
int[] candidateValues = [50, 150, 200, 250, 500, 1000];
double[] sortedCandidateChances = candidateValues
    .Select(value => calculator.CalculateChance(100, value))
    .ToArray();
ExpectFilteredSequence(TargetFilterMode.All, sortedCandidateChances, [1d, 4d / 6, 0.5, 0.4, 0.2, 0.1]);
ExpectFilteredSequence(TargetFilterMode.X2, sortedCandidateChances, [0.5, 0.4]);
ExpectFilteredSequence(TargetFilterMode.X3, sortedCandidateChances, [0.4]);
ExpectFilteredSequence(TargetFilterMode.X5, sortedCandidateChances, [0.2]);
ExpectFilteredSequence(TargetFilterMode.X10, sortedCandidateChances, [0.1]);
ExpectFilteredSequence(TargetFilterMode.X5, [0.5, 0.4], []);
ExpectFilteredSequence(TargetFilterMode.X2, [], []);

// End-to-end filter expectations for a 1000g source: every shown target really sits
// near the multiplier the button promises (chance ≈ 1 / N).
if (!Enum.GetValues<TargetFilterMode>().SequenceEqual(
    new[] { TargetFilterMode.All, TargetFilterMode.X2, TargetFilterMode.X3, TargetFilterMode.X5, TargetFilterMode.X10 }))
{
    throw new InvalidOperationException("Unexpected target filter modes.");
}

int[] sampleTargetValues = [1000, 1500, 2000, 2500, 3334, 5000, 10000, 20000, 100000];
double[] sampleChances = sampleTargetValues.Select(value => calculator.CalculateChance(1000, value)).ToArray();
foreach ((TargetFilterMode mode, int[] expected) in new[]
{
    (TargetFilterMode.All, sampleTargetValues),
    (TargetFilterMode.X2, new[] { 2000, 2500 }),
    (TargetFilterMode.X3, new[] { 2500, 3334 }),
    (TargetFilterMode.X5, new[] { 5000 }),
    (TargetFilterMode.X10, new[] { 10000 })
})
{
    int[] actual = sampleTargetValues
        .Where((_, index) => TargetProbabilityFilter.Matches(mode, sampleChances[index]))
        .ToArray();
    if (!actual.SequenceEqual(expected))
        throw new InvalidOperationException($"Unexpected {mode} targets for a 1000g source: [{string.Join(", ", actual)}].");
}

// Display text used by the UI tooltips.
if (TargetProbabilityFilter.GetChanceText(TargetFilterMode.X2) != "50%"
    || TargetProbabilityFilter.GetChanceText(TargetFilterMode.X3) != "33%"
    || TargetProbabilityFilter.GetChanceText(TargetFilterMode.X5) != "20%"
    || TargetProbabilityFilter.GetChanceText(TargetFilterMode.X10) != "10%"
    || TargetProbabilityFilter.GetChanceText(TargetFilterMode.All) != "")
{
    throw new InvalidOperationException("Unexpected filter chance display text.");
}


for (int i = 0; i < 10; i++)
{
    if (roller.Roll(0d))
        throw new InvalidOperationException("Chance 0 must always fail.");

    if (!roller.Roll(1d))
        throw new InvalidOperationException("Chance 1 must always succeed.");
}

ExpectRollException(-0.01d);
ExpectRollException(1.01d);

if (!new UpgradeRoller(new FixedRandom(0.49d)).Roll(0.5d)
    || new UpgradeRoller(new FixedRandom(0.5d)).Roll(0.5d)
    || !new UpgradeRoller(new FixedRandom(0.09d)).Roll(0.1d)
    || new UpgradeRoller(new FixedRandom(0.1d)).Roll(0.1d))
{
    throw new InvalidOperationException("UpgradeRoller must use a strict less-than comparison.");
}

foreach (UpgradeTransactionStatus status in Enum.GetValues<UpgradeTransactionStatus>())
{
    bool expected = status == UpgradeTransactionStatus.Success;
    if (new UpgradeTransactionResult(status).IsSuccess != expected)
        throw new InvalidOperationException($"Incorrect transaction result for {status}.");
}

InventoryBatchSlot[] singleStack = [new(5, 0, 10)];
ExpectInventoryPlan(inventoryPlanner.Plan(singleStack, 5, 4),
    [new(0, 5)], [new(0, 4)]);
if (!singleStack.SequenceEqual([new InventoryBatchSlot(5, 0, 10)]))
    throw new InvalidOperationException("Inventory planning must not mutate its input slots.");

ExpectInventoryPlan(inventoryPlanner.Plan([new(2, 0, 10), new(3, 0, 10)], 4, 4),
    [new(0, 2), new(1, 2)], [new(0, 4)]);

ExpectInventoryStatus(inventoryPlanner.Plan([new(2, 0, 10), new(3, 0, 10)], 6, 1),
    InventoryBatchPlanStatus.InsufficientSource);

ExpectInventoryPlan(inventoryPlanner.Plan([new(0, 3, 0), new(2, 0, 5)], 2, 5),
    [new(1, 2)], [new(0, 3), new(1, 2)]);

ExpectInventoryStatus(inventoryPlanner.Plan([new(1, 0, 5)], 1, 6),
    InventoryBatchPlanStatus.InsufficientOutputCapacity);
ExpectInventoryStatus(inventoryPlanner.Plan([new(2, 0, 10)], 0, 1), InventoryBatchPlanStatus.InvalidQuantity);
ExpectInventoryStatus(inventoryPlanner.Plan([new(2, 0, 10)], 1, 0), InventoryBatchPlanStatus.InvalidQuantity);
ExpectInventoryPlan(inventoryPlanner.PlanRemoval([new(2, 0, 10), new(3, 0, 10)], 4),
    [new(0, 2), new(1, 2)], []);

ExpectInventoryPlan(inventoryPlanner.Plan([new(2, 0, 10)], 2, 1), [new(0, 2)], [new(0, 1)]);
ExpectInventoryStatus(inventoryPlanner.Plan([new(1, 0, 10)], 2, 1), InventoryBatchPlanStatus.InsufficientSource);

var transactionService = new UpgradeTransactionService(calculator, item => item.Value);
Item legacySource = new("source", 1);
var legacyPlayer = new Farmer(1, legacySource);
UpgradeTransactionResult legacyTransaction = transactionService.Apply(legacyPlayer, legacySource, new Item("target", 1), success: true);
if (!legacyTransaction.IsSuccess || legacyPlayer.Items[0] is not { QualifiedItemId: "target", Stack: 1 })
    throw new InvalidOperationException("The legacy one-to-one transaction must consume the source and create one target.");
if (transactionService.Apply(legacyPlayer, legacySource, new Item("target", 1), success: true).Status != UpgradeTransactionStatus.SourceMissing)
    throw new InvalidOperationException("A completed transaction must not commit twice.");

Item sourceA = new("source", 2);
Item sourceB = new("source", 3);
var batchPlayer = new Farmer(3, sourceA, sourceB, null);
UpgradeTransactionResult batchSuccess = transactionService.Apply(batchPlayer, sourceA, new Item("target", 1) { Value = 4 }, 3, 1, success: true);
if (!batchSuccess.IsSuccess || batchPlayer.Items[0] is not { QualifiedItemId: "target", Stack: 1 } || sourceB.Stack != 2
    || batchPlayer.Items.Count(item => item is { QualifiedItemId: "target" }) != 1
    || batchPlayer.Items.Count(item => item is not null) != 2)
{
    throw new InvalidOperationException("A successful batch must consume q across stacks and create exactly one target.");
}

Item failedSourceA = new("source", 2);
Item failedSourceB = new("source", 3);
var failedPlayer = new Farmer(2, failedSourceA, failedSourceB);
UpgradeTransactionResult batchFailure = transactionService.Apply(failedPlayer, failedSourceA, new Item("target", 1) { Value = 4 }, 3, 1, success: false);
if (!batchFailure.IsSuccess || failedPlayer.Items[0] is not null || failedSourceB.Stack != 2)
    throw new InvalidOperationException("A failed batch must consume q without creating any output.");

Item insufficientSource = new("source", 2);
var insufficientPlayer = new Farmer(1, insufficientSource);
if (transactionService.Apply(insufficientPlayer, insufficientSource, new Item("target", 1) { Value = 4 }, 3, 1, success: true).Status
    != UpgradeTransactionStatus.InsufficientQuantity || insufficientSource.Stack != 2)
{
    throw new InvalidOperationException("An insufficient source batch must not mutate inventory.");
}

Item fullSource = new("source", 3);
var fullPlayer = new Farmer(2, fullSource, new Item("other", 999));
if (transactionService.Apply(fullPlayer, fullSource, new Item("target", 1), 1, 1, success: true).Status
    != UpgradeTransactionStatus.InventoryFull || fullSource.Stack != 3)
{
    throw new InvalidOperationException("A full inventory must reject the batch before source removal.");
}

Item[] multiStackSources =
[
    new Item("source", 999) { Value = 2 },
    new Item("source", 999) { Value = 2 },
    new Item("source", 999) { Value = 2 },
    new Item("source", 999) { Value = 2 },
    new Item("source", 4) { Value = 2 }
];
var multiStackPlayer = new Farmer(5, multiStackSources);
UpgradeTransactionResult multiStackTransaction = transactionService.Apply(multiStackPlayer, multiStackSources[0],
    new Item("target", 1) { Value = 8000 }, 4000, 1, success: true);
if (!multiStackTransaction.IsSuccess
    || multiStackPlayer.Items.Count(item => item is not null) != 1
    || multiStackPlayer.Items.Count(item => item is { QualifiedItemId: "target", Stack: 1 }) != 1)
{
    throw new InvalidOperationException("A batch spanning many source stacks must still create exactly one target item.");
}

Item invalidTargetSource = new("source", 2);
var invalidTargetPlayer = new Farmer(1, invalidTargetSource);
if (transactionService.Apply(invalidTargetPlayer, invalidTargetSource, targetPreview: null, 2, 1, success: true).Status
    != UpgradeTransactionStatus.InvalidTarget || invalidTargetSource.Stack != 2)
{
    throw new InvalidOperationException("An invalid target must not mutate inventory.");
}

if (transactionService.Apply(new Farmer(1, new Item("source", 1)), new Item("source", 1), new Item("target", 1), 0, 1, success: true).Status
    != UpgradeTransactionStatus.InvalidQuantity)
{
    throw new InvalidOperationException("Invalid batch quantities must return InvalidQuantity.");
}

if (transactionService.Apply(new Farmer(1, new Item("source", 1)), new Item("source", 1), new Item("target", 1), 1, 0, success: true).Status
    != UpgradeTransactionStatus.InvalidQuantity)
{
    throw new InvalidOperationException("Invalid output quantities must return InvalidQuantity.");
}

// One operation may only ever produce one output item: any r > 1 is rejected
// before any inventory mutation, on both the success and the failure path.
Item singleOutputSource = new("source", 2);
var singleOutputPlayer = new Farmer(2, singleOutputSource, null);
foreach (bool rollResult in new[] { true, false })
{
    if (transactionService.Apply(singleOutputPlayer, singleOutputSource, new Item("target", 1) { Value = 4 }, 2, 2, rollResult).Status
        != UpgradeTransactionStatus.InvalidQuantity || singleOutputSource.Stack != 2)
    {
        throw new InvalidOperationException("Output quantities above one must return InvalidQuantity without mutating inventory.");
    }
}

Item lowerValueSource = new("source", 3) { Value = 2 };
var lowerValuePlayer = new Farmer(1, lowerValueSource);
if (transactionService.Apply(lowerValuePlayer, lowerValueSource, new Item("target", 1) { Value = 4 }, 3, 1, success: true).Status
    != UpgradeTransactionStatus.InvalidTarget || lowerValueSource.Stack != 3)
{
    throw new InvalidOperationException("Transaction must reject lower-value target batches without mutation.");
}

Item cheapTargetSource = new("source", 23) { Value = 2 };
var cheapTargetPlayer = new Farmer(1, cheapTargetSource);
if (transactionService.Apply(cheapTargetPlayer, cheapTargetSource, new Item("target", 1) { Value = 1 }, 23, 1, success: true).Status
    != UpgradeTransactionStatus.InvalidTarget || cheapTargetSource.Stack != 23)
{
    throw new InvalidOperationException("A target that cannot cover the source batch alone must be rejected without mutation.");
}

// Final flow: ten source items spread across three stacks must resolve to
// exactly one selected target item of a single type.
Item[] flowSources =
[
    new Item("source", 1) { Value = 10 },
    new Item("source", 5) { Value = 10 },
    new Item("source", 4) { Value = 10 }
];
var flowPlayer = new Farmer(4, flowSources[0], flowSources[1], flowSources[2], null);
UpgradeTransactionResult flowSuccess = transactionService.Apply(flowPlayer, flowSources[0],
    new Item("target", 1) { Value = 100 }, 10, 1, success: true);
if (!flowSuccess.IsSuccess
    || flowPlayer.Items.Count(item => item is { QualifiedItemId: "target", Stack: 1 }) != 1
    || flowPlayer.Items.Any(item => item is not null && item.QualifiedItemId != "target"))
{
    throw new InvalidOperationException("A multi-stack source batch must yield exactly one target item.");
}

// The same flow failing consumes every source item and creates no output at all.
Item[] flowFailureSources = [new Item("source", 5) { Value = 10 }, new Item("source", 5) { Value = 10 }];
var flowFailurePlayer = new Farmer(2, flowFailureSources);
UpgradeTransactionResult flowFailure = transactionService.Apply(flowFailurePlayer, flowFailureSources[0],
    new Item("target", 1) { Value = 100 }, 10, 1, success: false);
if (!flowFailure.IsSuccess || flowFailurePlayer.Items.Any(item => item is not null))
    throw new InvalidOperationException("A failed flow must consume the whole batch and create no output.");

// The single-output guard runs before target validation: r > 1 with a null target
// reports InvalidQuantity, proving the invariant is checked first.
if (transactionService.Apply(new Farmer(1, new Item("source", 1)), new Item("source", 1),
        targetPreview: null, 1, 2, success: true).Status != UpgradeTransactionStatus.InvalidQuantity)
{
    throw new InvalidOperationException("The single-output guard must run before target validation.");
}

// Single-output chance/validity math: one target item must cover the whole source
// batch, so the chance is q * S / T and never has to be clamped by extra outputs.
foreach ((int sourceCount, int sourceValue, int targetValue, bool valid, double chance) in new[]
{
    (1, 10, 1000, true, 0.01),
    (10, 10, 1000, true, 0.1),
    (100, 10, 1000, true, 1d),
    (101, 10, 1000, false, 1d)
})
{
    bool actualValid = calculator.IsBatchTargetValueValid(sourceCount, 1, sourceValue, targetValue);
    double actualChance = calculator.CalculateChance(sourceCount, 1, sourceValue, targetValue);
    if (actualValid != valid || Math.Abs(actualChance - chance) > 1e-12)
        throw new InvalidOperationException($"Unexpected single-output math for {sourceCount}x{sourceValue} -> 1x{targetValue}.");
}

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

void ExpectFilter(TargetFilterMode mode, double chance, bool expected)
{
    bool actual = TargetProbabilityFilter.Matches(mode, chance);
    if (actual != expected)
        throw new InvalidOperationException($"Filter {mode} for chance {chance:R}: expected {expected}, got {actual}.");
}

void ExpectFilteredSequence(TargetFilterMode mode, double[] chances, double[] expected)
{
    double[] actual = chances.Where(chance => TargetProbabilityFilter.Matches(mode, chance)).ToArray();
    if (!actual.SequenceEqual(expected))
        throw new InvalidOperationException($"Unexpected {mode} candidate list: [{string.Join(", ", actual)}].");
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

void ExpectBatchException<TException>(int sourceCount, int targetCount, string parameter) where TException : ArgumentException
{
    try
    {
        calculator.CalculateChance(sourceCount, targetCount, 1000, 2000);
    }
    catch (TException exception) when (exception.ParamName == parameter)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} for counts {sourceCount}, {targetCount}.");
}

void ExpectInventoryPlan(InventoryBatchPlanResult result, InventoryBatchRemoval[] removals, InventoryBatchInsertion[] insertions)
{
    if (!result.IsSuccess || result.Plan is null
        || !result.Plan.SourceRemovals.SequenceEqual(removals)
        || !result.Plan.OutputInsertions.SequenceEqual(insertions))
    {
        throw new InvalidOperationException($"Unexpected inventory plan: {result.Status}.");
    }
}

void ExpectInventoryStatus(InventoryBatchPlanResult result, InventoryBatchPlanStatus expected)
{
    if (result.Status != expected || result.Plan is not null)
        throw new InvalidOperationException($"Expected inventory plan status {expected}, got {result.Status}.");
}

sealed class FixedRandom(double value) : Random
{
    public override double NextDouble() => value;
}
