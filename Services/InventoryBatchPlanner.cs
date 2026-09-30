namespace JojaDrop.Services;

public enum InventoryBatchPlanStatus
{
    Success,
    InvalidQuantity,
    InsufficientSource,
    InsufficientOutputCapacity,
    SourceMissing
}

public readonly record struct InventoryBatchSlot(int SourceQuantity, int OutputCapacity, int ReleasedOutputCapacity);

public readonly record struct InventoryBatchRemoval(int SlotIndex, int Quantity);

public readonly record struct InventoryBatchInsertion(int SlotIndex, int Quantity);

public sealed record InventoryBatchPlan(
    IReadOnlyList<InventoryBatchRemoval> SourceRemovals,
    IReadOnlyList<InventoryBatchInsertion> OutputInsertions);

public readonly record struct InventoryBatchPlanResult(InventoryBatchPlanStatus Status, InventoryBatchPlan? Plan)
{
    public bool IsSuccess => Status == InventoryBatchPlanStatus.Success;
}

/// <summary>Plans batch inventory changes without mutating any inventory state.</summary>
public sealed class InventoryBatchPlanner
{
    public InventoryBatchPlanResult Plan(IReadOnlyList<InventoryBatchSlot> slots, int sourceQuantity, int outputQuantity)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (outputQuantity < 1)
            return new(InventoryBatchPlanStatus.InvalidQuantity, null);

        InventoryBatchPlanResult removalPlan = PlanRemoval(slots, sourceQuantity);
        if (!removalPlan.IsSuccess || removalPlan.Plan is null)
            return removalPlan;

        InventoryBatchPlan plan = removalPlan.Plan;
        var outputCapacities = slots.Select(slot => slot.OutputCapacity).ToArray();
        foreach (InventoryBatchRemoval removal in plan.SourceRemovals)
        {
            if (removal.Quantity == slots[removal.SlotIndex].SourceQuantity)
                outputCapacities[removal.SlotIndex] += slots[removal.SlotIndex].ReleasedOutputCapacity;
        }

        int remainingOutput = outputQuantity;
        var insertions = new List<InventoryBatchInsertion>();
        for (int index = 0; index < outputCapacities.Length && remainingOutput > 0; index++)
        {
            int quantity = Math.Min(outputCapacities[index], remainingOutput);
            if (quantity == 0)
                continue;

            insertions.Add(new(index, quantity));
            remainingOutput -= quantity;
        }

        return remainingOutput == 0
            ? new(InventoryBatchPlanStatus.Success, new(plan.SourceRemovals, insertions))
            : new(InventoryBatchPlanStatus.InsufficientOutputCapacity, null);
    }

    public InventoryBatchPlanResult PlanRemoval(IReadOnlyList<InventoryBatchSlot> slots, int sourceQuantity)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (sourceQuantity < 1)
            return new(InventoryBatchPlanStatus.InvalidQuantity, null);

        if (slots.Any(slot => slot.SourceQuantity < 0 || slot.OutputCapacity < 0 || slot.ReleasedOutputCapacity < 0))
            throw new ArgumentOutOfRangeException(nameof(slots), "Inventory slot quantities and capacities cannot be negative.");

        int remainingSource = sourceQuantity;
        var removals = new List<InventoryBatchRemoval>();
        for (int index = 0; index < slots.Count && remainingSource > 0; index++)
        {
            int quantity = Math.Min(slots[index].SourceQuantity, remainingSource);
            if (quantity == 0)
                continue;

            removals.Add(new(index, quantity));
            remainingSource -= quantity;
        }

        if (remainingSource > 0)
            return new(InventoryBatchPlanStatus.InsufficientSource, null);

        return new(InventoryBatchPlanStatus.Success, new(removals, Array.Empty<InventoryBatchInsertion>()));
    }
}
