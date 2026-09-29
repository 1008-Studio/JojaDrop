using StardewValley;
using SObject = StardewValley.Object;

namespace JojaDrop.Services;

public sealed class ItemValueService
{
    /// <summary>Get the current player's sale value for one ordinary object, or null if unsupported.</summary>
    public int? GetValue(Item item)
    {
        // Specialized Object subclasses (including furniture) need their own valuation rules.
        if (item.GetType() != typeof(SObject)
            || item is not SObject obj
            || obj.bigCraftable.Value
            || obj.IsRecipe
            || obj.questItem.Value
            || !obj.canBeShipped())
        {
            return null;
        }

        int value = obj.sellToStorePrice(Game1.player.UniqueMultiplayerID);
        return value > 0 ? value : null;
    }
}
