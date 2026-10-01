using JojaDrop.Integration;
using JojaDrop.Services;
using StardewModdingAPI;

namespace JojaDrop;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var upgradeCalculator = new UpgradeCalculator();
        var upgradeRoller = new UpgradeRoller();
        var itemValues = new ItemValueService();
        var transactionService = new UpgradeTransactionService(upgradeCalculator, itemValues.GetValue);
        var integration = new InventoryIntegration(helper, Monitor, itemValues, upgradeRoller, transactionService);
        integration.RegisterEvents();
        helper.ConsoleCommands.Add("jojadrop_balance_export", "Export current Data/Objects points balance CSV.",
            (_, _) => ExportBalance(helper));
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }

    private void ExportBalance(IModHelper helper)
    {
        try
        {
            var exporter = new ValuationSimulationExporter();
            ValuationSimulationReport report = exporter.Simulate(new StardewAcquisitionProfileProvider().BuildAllObjects());
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
}
