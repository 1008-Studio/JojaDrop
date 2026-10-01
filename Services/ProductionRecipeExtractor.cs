using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Converts resolved deterministic Stardew production facts into final-price recipes.</summary>
public sealed class ProductionRecipeExtractor
{
    public IReadOnlyList<ProductionRecipe> Extract(AcquisitionIndexData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Recipes.Where(recipe => recipe.Inputs.Count > 0 && recipe.Inputs.All(input => !input.IsCategory))
            .Select(recipe => new ProductionRecipe($"Data/{(recipe.Kind == AcquisitionKind.Crafting ? "CraftingRecipes" : "CookingRecipes")}:{recipe.RecipeId}",
                recipe.Inputs.Select(input => new ProductionIngredient(input.ItemId, input.Quantity)),
                new ProductionOutput(recipe.ItemId, recipe.OutputQuantity)))
            .Concat(data.MachineProductions.Where(machine => !machine.IsRandomOutput && !machine.HasCustomOutputMethod
                    && machine.MinOutput == machine.MaxOutput && machine.Inputs.Count > 0 && machine.Inputs.All(input => !input.IsCategory))
                .Select(machine => new ProductionRecipe($"Data/Machines:{machine.MachineItemId}",
                    machine.Inputs.Select(input => new ProductionIngredient(input.ItemId, input.Quantity)),
                    new ProductionOutput(machine.ItemId, machine.MinOutput))))
            .ToArray();
    }
}
