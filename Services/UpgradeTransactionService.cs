using StardewValley;

namespace JojaDrop.Services;

public sealed class UpgradeTransactionService
{
    public UpgradeTransactionResult Apply(Farmer player, Item sourceItem, Item targetPreview, bool success)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(sourceItem);

        if (!ContainsSource(player, sourceItem))
            return new(UpgradeTransactionStatus.SourceMissing);

        if (!success)
        {
            RemoveOne(player, sourceItem);
            return new(UpgradeTransactionStatus.Success);
        }

        if (!TryCreateTarget(targetPreview, out Item? target))
            return new(UpgradeTransactionStatus.InvalidTarget);

        // A one-item source frees its own slot; otherwise the target must fit now.
        if (sourceItem.Stack > 1 && !player.couldInventoryAcceptThisItem(target))
            return new(UpgradeTransactionStatus.InventoryFull);

        int sourceStack = sourceItem.Stack;
        RemoveOne(player, sourceItem);

        if (player.addItemToInventory(target) is null)
            return new(UpgradeTransactionStatus.Success);

        RestoreSource(player, sourceItem, sourceStack);
        return new(UpgradeTransactionStatus.InventoryFull);
    }

    private static bool ContainsSource(Farmer player, Item sourceItem)
    {
        return sourceItem.Stack > 0 && player.Items.Any(item => ReferenceEquals(item, sourceItem));
    }

    private static void RemoveOne(Farmer player, Item sourceItem)
    {
        if (sourceItem.Stack > 1)
            sourceItem.Stack--;
        else
            player.removeItemFromInventory(sourceItem);
    }

    private static void RestoreSource(Farmer player, Item sourceItem, int sourceStack)
    {
        if (sourceStack > 1)
            sourceItem.Stack++;
        else
            player.addItemToInventory(sourceItem);
    }

    private static bool TryCreateTarget(Item? targetPreview, out Item? target)
    {
        target = null;
        if (targetPreview is null || string.IsNullOrWhiteSpace(targetPreview.QualifiedItemId))
            return false;

        try
        {
            target = ItemRegistry.Create(targetPreview.QualifiedItemId, amount: 1, quality: targetPreview.Quality, allowNull: true);
            return target is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
