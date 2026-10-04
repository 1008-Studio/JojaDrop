using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Converts resolved deterministic Stardew production facts into final-price recipes.</summary>
public sealed class ProductionRecipeExtractor
{
    public IReadOnlyList<ProductionRecipe> Extract(AcquisitionIndexData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var recipes = new List<ProductionRecipe>();

        foreach (RecipeProduction recipe in data.Recipes)
        {
            if (!ProductionOutputRules.IsValidRange(recipe.OutputQuantity, recipe.OutputQuantity)
                || string.IsNullOrWhiteSpace(recipe.ItemId)
                || recipe.Inputs.Count == 0
                || !recipe.Inputs.All(IsConcretePositiveInput))
            {
                continue;
            }

            recipes.Add(new ProductionRecipe(
                $"Data/{(recipe.Kind == AcquisitionKind.Crafting ? "CraftingRecipes" : "CookingRecipes")}:{recipe.RecipeId}",
                recipe.Inputs.Select(input => new ProductionIngredient(input.ItemId, input.Quantity)),
                new ProductionOutput(recipe.ItemId, recipe.OutputQuantity)));
        }

        foreach (MachineProduction machine in data.MachineProductions)
        {
            if (machine.IsRandomOutput || machine.HasCustomOutputMethod
                || !ProductionOutputRules.TryNormalizeMachineRange(
                    machine.MinOutput, machine.MaxOutput, out int minOutput, out int maxOutput)
                || minOutput != maxOutput
                || string.IsNullOrWhiteSpace(machine.ItemId)
                || machine.Inputs.Count == 0
                || !machine.Inputs.All(IsConcretePositiveInput))
            {
                continue;
            }

            recipes.Add(new ProductionRecipe(
                $"Data/Machines:{machine.MachineItemId}",
                machine.Inputs.Select(input => new ProductionIngredient(input.ItemId, input.Quantity)),
                new ProductionOutput(machine.ItemId, minOutput)));
        }

        return recipes.ToArray();
    }

    private static bool IsConcretePositiveInput(ProductionInput input) => input is not null
        && !input.IsCategory && input.Quantity > 0 && !string.IsNullOrWhiteSpace(input.ItemId);
}
