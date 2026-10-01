using System.Collections.ObjectModel;

namespace JojaDrop.Models;

/// <summary>One concrete, consumed ingredient in a deterministic production relationship.</summary>
public sealed record ProductionIngredient
{
    public ProductionIngredient(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An ingredient item ID is required.", nameof(itemId));
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Ingredient quantity must be positive.");

        ItemId = itemId;
        Quantity = quantity;
    }

    public string ItemId { get; }
    public int Quantity { get; }
}

/// <summary>The concrete item and quantity produced by one production relationship.</summary>
public sealed record ProductionOutput
{
    public ProductionOutput(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An output item ID is required.", nameof(itemId));
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Output quantity must be positive.");

        ItemId = itemId;
        Quantity = quantity;
    }

    public string ItemId { get; }
    public int Quantity { get; }
}

/// <summary>A validated, data-source-identified deterministic recipe for a single output item.</summary>
public sealed record ProductionRecipe
{
    public ProductionRecipe(string sourceId, IEnumerable<ProductionIngredient> ingredients, ProductionOutput output)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("A recipe source ID is required.", nameof(sourceId));
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(output);

        ProductionIngredient[] ingredientItems = ingredients.ToArray();
        if (ingredientItems.Length == 0 || ingredientItems.Any(ingredient => ingredient is null))
            throw new ArgumentException("A recipe needs at least one concrete ingredient.", nameof(ingredients));

        SourceId = sourceId;
        Ingredients = new ReadOnlyCollection<ProductionIngredient>(ingredientItems);
        Output = output;
    }

    /// <summary>Stable provenance, such as a crafting-recipe key or machine output-rule key.</summary>
    public string SourceId { get; }
    public IReadOnlyList<ProductionIngredient> Ingredients { get; }
    public ProductionOutput Output { get; }
}
