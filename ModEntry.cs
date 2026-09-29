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
        var transactionService = new UpgradeTransactionService();
        var integration = new InventoryIntegration(helper, Monitor, new ItemValueService(), upgradeCalculator, upgradeRoller, transactionService);
        integration.RegisterEvents();
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }
}
