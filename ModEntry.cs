using JojaDrop.Integration;
using JojaDrop.Services;
using StardewModdingAPI;

namespace JojaDrop;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var integration = new InventoryIntegration(helper, Monitor, new ItemValueService());
        integration.RegisterEvents();
        Monitor.Log("JojaDrop loaded successfully.", LogLevel.Info);
    }
}
