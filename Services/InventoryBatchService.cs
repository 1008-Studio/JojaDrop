using StardewValley;

namespace JojaDrop.Services;

/// <summary>Builds non-mutating batch plans from the player's current inventory.</summary>
public sealed class InventoryBatchService
{
    private readonly InventoryBatchPlanner planner = new();

    public int GetCompatibleQuantity(Farmer player, Item sourceItem)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(sourceItem);

        List<InventoryBatchSlot> slots = BuildSlots(player, sourceItem, sourceItem, out bool sourcePresent);
        return sourcePresent ? slots.Sum(slot => slot.SourceQuantity) : 0;
    }

    public InventoryBatchPlanResult Plan(Farmer player, Item sourceItem, Item targetItem, int sourceQuantity, int outputQuantity)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(sourceItem);
        ArgumentNullException.ThrowIfNull(targetItem);

        if (sourceQuantity < 1 || outputQuantity < 1)
            return new(InventoryBatchPlanStatus.InvalidQuantity, null);

        List<InventoryBatchSlot> slots = BuildSlots(player, sourceItem, targetItem, out bool sourcePresent);
        return sourcePresent
            ? planner.Plan(slots, sourceQuantity, outputQuantity)
            : new(InventoryBatchPlanStatus.SourceMissing, null);
    }

    private static List<InventoryBatchSlot> BuildSlots(Farmer player, Item sourceItem, Item targetItem, out bool sourcePresent)
    {
        int targetStackSize = Math.Max(1, targetItem.maximumStackSize());
        int slotCount = Math.Max(0, player.MaxItems);
        var slots = new List<InventoryBatchSlot>(slotCount);
        sourcePresent = false;

        for (int index = 0; index < slotCount; index++)
        {
            Item? item = index < player.Items.Count ? player.Items[index] : null;
            if (item is null)
            {
                slots.Add(new(0, targetStackSize, 0));
                continue;
            }

            bool isSource = sourceItem.canStackWith(item);
            int sourceStack = isSource ? item.Stack : 0;
            sourcePresent |= ReferenceEquals(item, sourceItem) && sourceStack > 0;
            int outputCapacity = item.canStackWith(targetItem) ? Math.Max(0, item.getRemainingStackSpace()) : 0;
            slots.Add(new(sourceStack, outputCapacity, isSource ? targetStackSize : 0));
        }

        return slots;
    }
}
