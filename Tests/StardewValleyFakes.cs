namespace StardewValley;

public sealed class Farmer
{
    public Farmer(int maxItems, params Item?[] items)
    {
        MaxItems = maxItems;
        Items = items.ToList();
    }

    public int MaxItems { get; }
    public List<Item?> Items { get; }
}

public class Item
{
    public Item(string qualifiedItemId, int stack, int quality = 0, int maxStackSize = 999)
    {
        QualifiedItemId = qualifiedItemId;
        Stack = stack;
        Quality = quality;
        MaxStackSize = maxStackSize;
    }

    public string QualifiedItemId { get; }
    public int Quality { get; }
    public int Stack { get; set; }
    public int MaxStackSize { get; }

    public bool canStackWith(Item other) => QualifiedItemId == other.QualifiedItemId && Quality == other.Quality;
    public int getRemainingStackSpace() => Math.Max(0, MaxStackSize - Stack);
    public int maximumStackSize() => MaxStackSize;
}

public static class ItemRegistry
{
    public static Item? Create(string qualifiedItemId, int amount, int quality, bool allowNull)
    {
        return string.IsNullOrWhiteSpace(qualifiedItemId) ? null : new(qualifiedItemId, amount, quality);
    }
}
