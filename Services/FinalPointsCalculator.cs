using JojaDrop.Models;

namespace JojaDrop.Services;

/// <summary>Pure production-chain valuation layered over externally supplied intrinsic points.</summary>
public sealed class FinalPointsCalculator
{
    public const double CraftValueRetention = 0.90d;
    private const int MaximumPoints = int.MaxValue;

    /// <summary>Calculates final points using a lookup for U(item), without reading game or player state.</summary>
    public FinalPriceResult Calculate(string itemId, Func<string, int?> uniquePoints,
        IEnumerable<ProductionRecipe> recipes, Action<string>? diagnostic = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("An item ID is required.", nameof(itemId));
        ArgumentNullException.ThrowIfNull(uniquePoints);
        ArgumentNullException.ThrowIfNull(recipes);

        var recipesByOutput = new Dictionary<string, List<ProductionRecipe>>(StringComparer.Ordinal);
        foreach (ProductionRecipe recipe in recipes)
        {
            if (recipe is null)
                continue;

            if (!recipesByOutput.TryGetValue(recipe.Output.ItemId, out List<ProductionRecipe>? outputRecipes))
            {
                outputRecipes = new List<ProductionRecipe>();
                recipesByOutput.Add(recipe.Output.ItemId, outputRecipes);
            }

            outputRecipes.Add(recipe);
        }

        var reportedDiagnostics = new HashSet<string>(StringComparer.Ordinal);
        void Report(string message)
        {
            if (reportedDiagnostics.Add(message))
                diagnostic?.Invoke(message);
        }

        var memo = new Dictionary<string, FinalPriceResult>(StringComparer.Ordinal);
        return Calculate(itemId, uniquePoints, recipesByOutput, memo, new HashSet<string>(StringComparer.Ordinal), Report);
    }

    /// <summary>Dictionary convenience overload for a stable intrinsic-points snapshot.</summary>
    public FinalPriceResult Calculate(string itemId, IReadOnlyDictionary<string, int> uniquePoints,
        IEnumerable<ProductionRecipe> recipes, Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(uniquePoints);
        return Calculate(itemId, id => uniquePoints.TryGetValue(id, out int points) ? points : null, recipes, diagnostic);
    }

    private static FinalPriceResult Calculate(string itemId, Func<string, int?> uniquePoints,
        IReadOnlyDictionary<string, List<ProductionRecipe>> recipesByOutput, IDictionary<string, FinalPriceResult> memo,
        ISet<string> activePath, Action<string> report)
    {
        if (memo.TryGetValue(itemId, out FinalPriceResult? cached))
            return cached;

        int intrinsicPoints = GetIntrinsicPoints(itemId, uniquePoints, report);
        if (!activePath.Add(itemId))
        {
            report($"Final points cycle detected through '{itemId}'; using intrinsic points for this branch.");
            return new FinalPriceResult(itemId, intrinsicPoints, intrinsicPoints, intrinsicPoints, null, null, null);
        }

        try
        {
            double? cheapestCraftFloor = null;
            double? cheapestInputCost = null;
            ProductionRecipe? bestRecipe = null;
            if (recipesByOutput.TryGetValue(itemId, out List<ProductionRecipe>? recipes))
            {
                foreach (ProductionRecipe recipe in recipes)
                {
                    if (!TryCalculateCraftFloor(recipe, uniquePoints, recipesByOutput, memo, activePath, report,
                            out double inputCost, out double craftFloor))
                    {
                        continue;
                    }

                    if (!cheapestCraftFloor.HasValue || craftFloor < cheapestCraftFloor.Value)
                    {
                        cheapestCraftFloor = craftFloor;
                        cheapestInputCost = inputCost;
                        bestRecipe = recipe;
                    }
                }
            }

            double exactFinalPoints = Math.Max(intrinsicPoints, cheapestCraftFloor ?? intrinsicPoints);
            var result = new FinalPriceResult(itemId, intrinsicPoints, exactFinalPoints, ToPoints(exactFinalPoints),
                bestRecipe, cheapestInputCost, cheapestCraftFloor);
            memo[itemId] = result;
            return result;
        }
        finally
        {
            activePath.Remove(itemId);
        }
    }

    private static bool TryCalculateCraftFloor(ProductionRecipe recipe, Func<string, int?> uniquePoints,
        IReadOnlyDictionary<string, List<ProductionRecipe>> recipesByOutput, IDictionary<string, FinalPriceResult> memo,
        ISet<string> activePath, Action<string> report, out double inputCost, out double craftFloor)
    {
        inputCost = 0d;
        craftFloor = 0d;
        if (recipe.Output.Quantity < 1)
        {
            report($"Final points skipped '{recipe.SourceId}': output quantity must be positive.");
            return false;
        }

        foreach (ProductionIngredient ingredient in recipe.Ingredients)
        {
            if (ingredient is null || string.IsNullOrWhiteSpace(ingredient.ItemId) || ingredient.Quantity < 1)
            {
                report($"Final points skipped '{recipe.SourceId}': ingredient data is invalid.");
                return false;
            }

            if (!uniquePoints(ingredient.ItemId).HasValue)
            {
                report($"Final points skipped '{recipe.SourceId}': intrinsic points are unavailable for '{ingredient.ItemId}'.");
                return false;
            }

            FinalPriceResult ingredientPoints = Calculate(ingredient.ItemId, uniquePoints, recipesByOutput, memo,
                activePath, report);
            inputCost = SaturatingAdd(inputCost, ingredientPoints.ExactPoints * ingredient.Quantity);
        }

        craftFloor = Math.Min(MaximumPoints, CraftValueRetention * inputCost / recipe.Output.Quantity);
        return true;
    }

    private static int GetIntrinsicPoints(string itemId, Func<string, int?> uniquePoints, Action<string> report)
    {
        int? value = uniquePoints(itemId);
        if (!value.HasValue)
        {
            report($"Final points have no intrinsic value for '{itemId}'; using zero.");
            return 0;
        }

        if (value.Value < 0)
        {
            report($"Final points received negative intrinsic value for '{itemId}'; using zero.");
            return 0;
        }

        return value.Value;
    }

    private static double SaturatingAdd(double left, double right)
    {
        if (double.IsNaN(left) || double.IsNaN(right) || double.IsInfinity(left) || double.IsInfinity(right)
            || left > double.MaxValue - right)
        {
            return double.MaxValue;
        }

        return left + right;
    }

    private static int ToPoints(double value)
    {
        if (double.IsNaN(value) || value <= 0d)
            return 0;
        if (double.IsInfinity(value) || value >= MaximumPoints)
            return MaximumPoints;

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}

/// <summary>One final-price result; only <see cref="Points"/> is rounded for JojaDrop display and economy use.</summary>
public sealed record FinalPriceResult(string ItemId, int UniquePoints, double ExactPoints, int Points,
    ProductionRecipe? BestRecipe, double? RecipeInputCost, double? CraftFloor);
