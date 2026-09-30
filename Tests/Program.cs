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
UpgradeTransactionResult batchSuccess = transactionService.Apply(batchPlayer, sourceA, new Item("target", 1), 3, 2, success: true);
if (!batchSuccess.IsSuccess || batchPlayer.Items[0] is not { QualifiedItemId: "target", Stack: 2 } || sourceB.Stack != 2)
    throw new InvalidOperationException("A successful batch transaction must consume q across stacks and create r targets.");

Item failedSourceA = new("source", 2);
Item failedSourceB = new("source", 3);
var failedPlayer = new Farmer(2, failedSourceA, failedSourceB);
UpgradeTransactionResult batchFailure = transactionService.Apply(failedPlayer, failedSourceA, new Item("target", 1), 3, 4, success: false);
if (!batchFailure.IsSuccess || failedPlayer.Items[0] is not null || failedSourceB.Stack != 2)
    throw new InvalidOperationException("A failed batch transaction must consume q without requiring output capacity.");

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

Item lowerValueSource = new("source", 3) { Value = 2 };
var lowerValuePlayer = new Farmer(1, lowerValueSource);
if (transactionService.Apply(lowerValuePlayer, lowerValueSource, new Item("target", 1) { Value = 4 }, 3, 1, success: true).Status
    != UpgradeTransactionStatus.InvalidTarget || lowerValueSource.Stack != 3)
{
    throw new InvalidOperationException("Transaction must reject lower-value target batches without mutation.");
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
