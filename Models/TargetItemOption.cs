using StardewValley;

namespace JojaDrop.Models;

/// <summary>A preview-only item that can be selected as an upgrade target.</summary>
public sealed record TargetItemOption(Item PreviewItem, int Value, double BatchChance, double BatchMultiplier)
{
    public string QualifiedItemId => PreviewItem.QualifiedItemId;

    public string DisplayName => PreviewItem.DisplayName;
}
