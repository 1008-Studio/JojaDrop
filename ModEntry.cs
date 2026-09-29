using JojaDrop.Integration;
using JojaDrop.Services;
using StardewModdingAPI;

namespace JojaDrop;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var upgradeCalculator = new UpgradeCalculator();
        var integration = new InventoryIntegration(helper, Monitor, new ItemValueService(), upgradeCalculator);
        integration.RegisterEvents();
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }
}
