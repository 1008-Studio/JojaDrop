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
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }
}
