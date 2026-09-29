# JojaDrop

JojaDrop is a SMAPI mod for Stardew Valley with an item upgrader concept: choose a source item and a more valuable target, with a future success chance of `sourceValue / targetValue`.

Milestone v0.1 provides a Stardew-style UI prototype and a read-only source item picker. Upgrading, item consumption, rewards, and random rolls are not implemented.

## Requirements

- Stardew Valley **1.6.14+** on Windows, Linux, macOS, or Steam Deck.
- [SMAPI **4.5.2+**](https://smapi.io/).
- For development: **.NET 8 SDK (8.0.200+) or newer** capable of targeting `net6.0`, and a local game installation with SMAPI installed. The newer SDK lets the build package's current C# analyzer run; the mod itself still targets the game's .NET 6 runtime.

The project follows the [SMAPI mod setup guide](https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started): an SDK-style C# class library targeting `net6.0`, with [Pathoschild.Stardew.ModBuildConfig 4.4.0](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig/4.4.0) providing game/SMAPI references, deployment, and release packaging. Requirements and package version were checked on 2026-09-29.

## Development

From the repository root:

```sh
dotnet build JojaDrop.csproj -c Release
```

The build package detects standard game installations, copies the mod to `Mods/JojaDrop`, and creates a release ZIP under `bin/Release`. The compiled assembly is under `bin/Release/net6.0`.

For a nonstandard game installation, pass the directory containing the Stardew Valley executable and installed SMAPI:

```sh
dotnet build JojaDrop.csproj -c Release -p:GamePath="/absolute/path/to/Stardew Valley"
```

On Windows, replace the path with the game's Windows directory. Keep local paths out of the project file. You can also configure `GamePath` in the build package's `stardewvalley.targets` file in your home directory, as described in its documentation.

To build a ZIP without automatically installing the mod:

```sh
dotnet build JojaDrop.csproj -c Release -p:GamePath="/absolute/path/to/Stardew Valley" -p:EnableModDeploy=false
```

Run the .NET 8 calculator checks independently of the game, SMAPI, and third-party test packages:

```sh
dotnet run --project Tests/JojaDrop.CalculatorChecks.csproj -c Release
```

The runner checks 15 valid, invalid, and boundary cases. It exits with code 0 on success and throws on a failed check.

Validation on 2026-09-29: Release compiled with SDK 8.0.408 against the actual Stardew Valley 1.6.15 and SMAPI 4.5.2 assemblies, with zero errors and warnings and the SMAPI analyzer enabled. All 15 calculator checks passed. Manifest consistency, release ZIP contents, whitespace, ignore rules, and layout geometry at five UI viewport sizes from 1920×1080 to 640×360 were checked. The game was not launched here; visual behavior, sounds, and controller input still need the manual in-game checks below.

### Project structure

```text
JojaDrop.csproj
manifest.json
ModEntry.cs
Integration/
    InventoryIntegration.cs
UI/
    UpgradeButton.cs
    UpgradeMenu.cs
    SourceItemMenu.cs
    MenuDrawing.cs
Services/
    ItemValueService.cs
    UpgradeCalculator.cs
Tests/
    JojaDrop.CalculatorChecks.csproj
    Program.cs
README.md
.gitignore
```

`ModEntry` wires up the mod. `InventoryIntegration` owns SMAPI rendering and input events. UI classes draw the button, upgrader, and source picker using game textures and drawing helpers. Price and probability rules live in `Services`. Asset files and target-option models will be added when needed.

### Value and probability rules

`ItemValueService.GetValue(Item)` returns the **sale value of one item**, not the entire stack. It supports ordinary `StardewValley.Object` instances and calls the game's `sellToStorePrice` for the current player, preserving the game's quality, profession, and profit-margin rules. Recipes, quest items, big craftables, non-shippable objects, specialized subclasses, and nonpositive prices return `null`. Unsupported items are dimmed in the picker. There are no invented fallback prices or item ID lists.

`UpgradeCalculator.CalculateChance(int, int)` is isolated from Stardew and returns a probability in `0..1`. Nonpositive inputs throw `ArgumentOutOfRangeException`; targets worth no more than the source throw `ArgumentException`. For valid upgrades it uses floating-point division and clamps the result. It performs no random roll. The menu's `0%` is a placeholder until target selection is implemented.

## Installation

1. Install SMAPI into your Stardew Valley installation.
2. Build the project. By default, the build package installs it automatically.
3. Alternatively, extract the generated release ZIP into the game's `Mods` directory, so the mod is located at `Mods/JojaDrop/manifest.json` and `Mods/JojaDrop/JojaDrop.dll`.
4. Launch the game through SMAPI. Its console should show `[JojaDrop] JojaDrop loaded successfully.`

Install only the packaged mod files, not the repository or game/SMAPI DLLs. No other mods are required.

### Try the prototype

1. Load a save and open the standard player menu (`E` by default), then select its **Inventory** tab.
2. Click the upward-arrow button on the right. Its hover tooltip reads **JojaDrop Upgrader**. You can also press **U** or click the controller's **right stick** while on the Inventory tab.
3. The JojaDrop panel opens with Your Item, Target Item, a `0%` placeholder, and a disabled Upgrade button.
4. Click **Your Item** and choose a supported inventory object. Its icon, display name, and sale value per item appear in the source slot. The original item and its stack stay in your backpack.
5. Close the picker with its cross, Esc, or controller B to cancel and return to JojaDrop. Close JojaDrop the same way to return to gameplay.

Directional controller navigation uses clickable components in both custom menus; A activates the focused component. The picker also supports shoulder buttons, mouse wheel, and Previous/Next buttons for pagination.

The inventory button is rendered through `RenderedActiveMenu` and activated through `ButtonPressed`; consumed input is suppressed. Vanilla inventory components and their navigation are not modified. Opening is blocked while the inventory cannot safely close, such as when holding an item. Layout uses UI-scaled viewport coordinates and is recalculated as the menu or viewport changes. At narrow widths the button moves below the inventory; if there is no free space outside it, use U/right stick. JojaDrop recenters on resizing, and the picker adjusts its grid and paginates. Extremely small UI viewports may require reducing the game's UI scale.

### Manual verification

- Confirm the load message and absence of JojaDrop errors in the SMAPI console.
- Confirm the button appears only on Inventory, and vanilla tabs, equipment, trash can, and item drag/drop still work.
- Check button hover, tooltip, and opening sound; try opening while holding an inventory item.
- Choose an object, verify its name and per-item value, and confirm its stack is unchanged after closing/reopening menus.
- Confirm unsupported items cannot be selected and Upgrade stays disabled.
- Check picker cancellation, close cross, Esc, and controller navigation/A/B.
- Try window resizing and different UI scales, including a narrow viewport and paginated inventory.

## Current status

Milestone v0.1

- [x] SMAPI mod foundation
- [x] Inventory Upgrade button
- [x] Upgrade menu shell
- [x] Source item selection
- [ ] Target item selection
- [ ] Upgrade probability
- [ ] Upgrade animation
- [ ] Success/failure
- [ ] Multiplayer

The probability calculator is implemented and checked in isolation; applying it to selected source/target items in the UI remains TODO. Multiplayer synchronization, persistent state, broader item valuation, and other mod integrations are future work.
