using JojaDrop.Integration;
using JojaDrop.Models;
using JojaDrop.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace JojaDrop;

public sealed class ModEntry : Mod
{
    private StardewAcquisitionProfileProvider profiles = null!;
    private ItemValueService itemValues = null!;
    private InventoryIntegration integration = null!;

    public override void Entry(IModHelper helper)
    {
        ModConfig config = helper.ReadConfig<ModConfig>();
        if (!File.Exists(Path.Combine(helper.DirectoryPath, "config.json")))
            helper.WriteConfig(config);
        profiles = new StardewAcquisitionProfileProvider(config.ItemOverrides,
            message => Monitor.Log($"Valuation: {message}", LogLevel.Warn));
        var upgradeCalculator = new UpgradeCalculator();
        var upgradeRoller = new UpgradeRoller();
        itemValues = new ItemValueService(profiles);
        var transactionService = new UpgradeTransactionService(upgradeCalculator, itemValues.GetValue);
        integration = new InventoryIntegration(helper, Monitor, itemValues, upgradeRoller, transactionService);
        integration.RegisterEvents();
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        helper.ConsoleCommands.Add("jojadrop_balance_export", "Export current Data/Objects points balance CSV.",
            (_, _) => ExportBalance(helper));
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }

    private void ExportBalance(IModHelper helper)
    {
        try
        {
            var exporter = new ValuationSimulationExporter();
            ValuationSimulationReport report = exporter.Simulate(profiles.BuildAllObjects());
            string directory = Path.Combine(helper.DirectoryPath, "balance-simulation");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "valuation.csv"), exporter.ToCsv(report));
            File.WriteAllText(Path.Combine(directory, "summary.txt"), exporter.ToSummary(report));
            Monitor.Log($"Balance export: {report.Statistics.Count} objects written to {directory}.", LogLevel.Info);
        }
        catch (Exception exception)
        {
            Monitor.Log($"Balance export failed: {exception}", LogLevel.Error);
        }
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (!e.NamesWithoutLocale.Any(IsValuationAsset))
            return;

        profiles.Invalidate();
        itemValues.InvalidateCache();
        integration.InvalidateValueCache();
        Monitor.Log("Valuation data cache invalidated after a resolved game-data asset changed.", LogLevel.Trace);
    }

    private static bool IsValuationAsset(IAssetName name) => name.IsEquivalentTo("Data/Objects")
        || name.IsEquivalentTo("Data/Fish")
        || name.IsEquivalentTo("Data/Locations")
        || name.IsEquivalentTo("Data/Crops")
        || name.IsEquivalentTo("Data/Shops")
        || name.IsEquivalentTo("Data/Machines")
        || name.IsEquivalentTo("Data/CraftingRecipes")
        || name.IsEquivalentTo("Data/CookingRecipes");
}
