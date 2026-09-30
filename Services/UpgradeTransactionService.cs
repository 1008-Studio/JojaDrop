using StardewValley;

namespace JojaDrop.Services;

public sealed class UpgradeTransactionService
{
    private readonly InventoryBatchService inventory;
    private readonly UpgradeCalculator calculator;
    private readonly Func<Item, int?> getValue;

    public UpgradeTransactionService(UpgradeCalculator calculator, Func<Item, int?> getValue, InventoryBatchService? inventory = null)
    {
        this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
        this.inventory = inventory ?? new InventoryBatchService();
    }

    public UpgradeTransactionResult Apply(Farmer player, Item sourceItem, Item? targetPreview, bool success)
    {
        return Apply(player, sourceItem, targetPreview, 1, 1, success);
    }

    public UpgradeTransactionResult Apply(Farmer player, Item sourceItem, Item? targetPreview,
        int sourceQuantity, int outputQuantity, bool success)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(sourceItem);

        if (sourceQuantity < 1 || outputQuantity < 1)
            return new(UpgradeTransactionStatus.InvalidQuantity);

        int? sourceValue = getValue(sourceItem);
        int? targetValue = targetPreview is null ? null : getValue(targetPreview);
        if (!sourceValue.HasValue || sourceValue.Value <= 0 || !targetValue.HasValue || targetValue.Value <= sourceValue.Value
            || !calculator.IsBatchTargetValueValid(
                sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value))
        {
            return new(UpgradeTransactionStatus.InvalidTarget);
        }

        if (!success)
        {
            InventoryBatchPlanResult removalPlan = inventory.PlanRemoval(player, sourceItem, sourceQuantity);
            if (!removalPlan.IsSuccess || removalPlan.Plan is null)
                return new(MapPlanStatus(removalPlan.Status));

            return TryApplyPlan(player, sourceItem, targetItem: null, removalPlan.Plan, Array.Empty<Item>())
                ? new(UpgradeTransactionStatus.Success)
                : new(UpgradeTransactionStatus.TransactionFailed);
        }

        if (!TryCreateTarget(targetPreview, 1, out Item? targetTemplate) || targetTemplate is null)
            return new(UpgradeTransactionStatus.InvalidTarget);

        InventoryBatchPlanResult planResult = inventory.Plan(player, sourceItem, targetTemplate, sourceQuantity, outputQuantity);
        if (!planResult.IsSuccess || planResult.Plan is null)
            return new(MapPlanStatus(planResult.Status));

        if (!TryCreateTargets(targetPreview, planResult.Plan.OutputInsertions, out List<Item>? targets) || targets is null)
            return new(UpgradeTransactionStatus.InvalidTarget);

        return TryApplyPlan(player, sourceItem, targetTemplate, planResult.Plan, targets)
            ? new(UpgradeTransactionStatus.Success)
            : new(UpgradeTransactionStatus.TransactionFailed);
    }

    private static UpgradeTransactionStatus MapPlanStatus(InventoryBatchPlanStatus status)
    {
        return status switch
        {
            InventoryBatchPlanStatus.InvalidQuantity => UpgradeTransactionStatus.InvalidQuantity,
            InventoryBatchPlanStatus.SourceMissing => UpgradeTransactionStatus.SourceMissing,
            InventoryBatchPlanStatus.InsufficientSource => UpgradeTransactionStatus.InsufficientQuantity,
            InventoryBatchPlanStatus.InsufficientOutputCapacity => UpgradeTransactionStatus.InventoryFull,
            _ => UpgradeTransactionStatus.TransactionFailed
        };
    }

    private static bool TryApplyPlan(Farmer player, Item sourceItem, Item? targetItem, InventoryBatchPlan plan, IReadOnlyList<Item> targets)
    {
        if (plan.OutputInsertions.Count != targets.Count)
            return false;

        Item?[] originalItems = player.Items.Select(item => (Item?)item).ToArray();
        int[] originalStacks = originalItems.Select(item => item?.Stack ?? 0).ToArray();

        try
        {
            foreach (InventoryBatchRemoval removal in plan.SourceRemovals)
            {
                if (removal.SlotIndex < 0 || removal.SlotIndex >= player.Items.Count)
                    throw new InvalidOperationException();

                Item? source = player.Items[removal.SlotIndex];
                if (source is null || !sourceItem.canStackWith(source) || source.Stack < removal.Quantity)
                    throw new InvalidOperationException();

                source.Stack -= removal.Quantity;
                if (source.Stack == 0)
                    player.Items[removal.SlotIndex] = null;
            }

            for (int index = 0; index < plan.OutputInsertions.Count; index++)
            {
                InventoryBatchInsertion insertion = plan.OutputInsertions[index];
                if (targetItem is null || insertion.SlotIndex < 0 || insertion.SlotIndex >= player.Items.Count)
                    throw new InvalidOperationException();

                Item? existing = player.Items[insertion.SlotIndex];
                if (existing is null)
                {
                    if (targets[index].Stack != insertion.Quantity)
                        throw new InvalidOperationException();

                    player.Items[insertion.SlotIndex] = targets[index];
                    continue;
                }

                if (!existing.canStackWith(targetItem) || existing.getRemainingStackSpace() < insertion.Quantity)
                    throw new InvalidOperationException();

                existing.Stack += insertion.Quantity;
            }

            return true;
        }
        catch (Exception)
        {
            RestoreInventory(player, originalItems, originalStacks);
            return false;
        }
    }

    private static void RestoreInventory(Farmer player, IReadOnlyList<Item?> items, IReadOnlyList<int> stacks)
    {
        for (int index = 0; index < items.Count; index++)
            player.Items[index] = items[index];

        for (int index = 0; index < items.Count; index++)
        {
            if (items[index] is Item item)
                item.Stack = stacks[index];
        }
    }

    private static bool TryCreateTargets(Item? targetPreview, IReadOnlyList<InventoryBatchInsertion> insertions, out List<Item>? targets)
    {
        targets = new(insertions.Count);
        foreach (InventoryBatchInsertion insertion in insertions)
        {
            if (!TryCreateTarget(targetPreview, insertion.Quantity, out Item? target) || target is null)
            {
                targets = null;
                return false;
            }

            targets.Add(target);
        }

        return true;
    }

    private static bool TryCreateTarget(Item? targetPreview, int amount, out Item? target)
    {
        target = null;
        if (amount < 1 || targetPreview is null || string.IsNullOrWhiteSpace(targetPreview.QualifiedItemId))
            return false;

        try
        {
            target = ItemRegistry.Create(targetPreview.QualifiedItemId, amount, quality: targetPreview.Quality, allowNull: true);
            return target is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
