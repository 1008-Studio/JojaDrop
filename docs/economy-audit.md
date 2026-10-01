# Economy audit

Scope: the `skylak33/eco` branch, against the Stardew Valley 1.6.14+ contract in
`manifest.json`. This is an audit only; it does not change production behavior.

## 1. Current valuation flow

`Item` (inventory `StardewValley.Item`)
-> `SourceItemMenu` asks `ItemValueService.GetValue`
-> `UpgradeMenu` retains the selected item and its quantity `q`
-> `TargetItemProvider` scans `Game1.objectData`, creates `(O)` previews through
`ItemRegistry`, and values each through the same service
-> `TargetEconomics.SelectTargets` filters, calculates multiplier and chance
-> `TargetItemMenu` displays those preview values
-> `UpgradeMenu` revalidates, calculates the chance, and calls `UpgradeRoller.Roll`
-> after the roulette animation, `UpgradeTransactionService.Apply` revalidates and
commits the inventory plan.

The current unit valuation is deliberately narrow. `ItemValueService` returns
`obj.sellToStorePrice(Game1.player.UniqueMultiplayerID)` only when the runtime
type is **exactly** `StardewValley.Object`; it rejects big craftables, recipes,
quest items, unshippable items, specialized `Object` subclasses, and non-positive
sale prices. Thus the supported source and target type today is only an ordinary,
shippable `StardewValley.Object` (not all `Item`s). Value includes the game's
current player-dependent sale calculation (quality, professions, and profit
margin), rather than `Data/Objects.Price`.

The target cache is a scan of the currently loaded `Game1.objectData`, not a
hard-coded vanilla list. It is invalidated only when its owner calls
`TargetItemProvider.InvalidateCache` (no such invalidation wiring is present in
this branch). Candidates are sorted by unit value and display name.

## 2. Upgrade invariants

Verified in `TargetEconomics`, rechecked by `UpgradeMenu`, and enforced again by
`UpgradeTransactionService`:

```text
q = source quantity
S = source unit value
T = target unit value

eligible iff source and target have different nonblank qualified IDs
             and q > 0, S > 0, T > 0
             and T > q * S                 (strict; long arithmetic)

p = q * S / T
multiplier = T / (q * S)
```

For an eligible target, `p` is necessarily less than one. `UpgradeRoller` uses
`Random.NextDouble() < p` (strict less-than). The target filters (`x2`, `x3`,
`x5`, `x10`) are only views over this same batch chance; they do not alter the
economy. Increasing `q` revalidates the current target and clears it if it no
longer qualifies.

One operation has a fixed output quantity of exactly one. Both the UI and
transaction service reject any `outputQuantity != 1`. On success, the inventory
plan consumes exactly `q` stack-compatible source items and inserts exactly one
new target. On failure, it plans and performs the same source removal but inserts
nothing. Validation and capacity failures leave inventory unchanged; the
transaction has a rollback snapshot if its application fails part-way through.

The independent calculator still contains older general `targetCount` helpers,
but the live upgrade flow uses the single-output `TargetEconomics` rules above.

## 3. Available Stardew data

All observations below refer to data resolved by the running game, not files
read directly from the install. That distinction is required for Content Patcher
compatibility.

| Source | Available data / automatic inference | Content Patcher and modded IDs | Limits |
| --- | --- | --- | --- |
| `Data/Objects` / `Game1.objectData` | Object name/type/category, base price, edibility, context tags, custom fields, geode and artifact-spot metadata, and object identity. It can classify an ordinary object and identify some geode-capable metadata. | Yes for ordinary object definitions added or edited through the data asset; keys are strings. | Base price is not the current sell price and is not an acquisition cost. Object metadata alone does not prove how an item is acquired. |
| `Data/Fish` | Rod-fish difficulty/behavior, time ranges, weather, depth, base catch chance, depth penalty, and fishing-level requirement; crab-pot region and chance. | Yes for entries that follow the asset; string IDs work. | The definitive spawn list is in locations, not the legacy fish location/season fields. It does not by itself model bait, luck, equipment, map rules, or custom code. |
| `Data/Locations` | Fish and fish-area entries provide location, season, chance, condition, catch limit, boss flag, and requirement overrides. `Forage` provides location/season/chance/condition spawn routes; locations also expose artifact spots and weeds. | Yes for CP/SVE-style location additions and edits, including custom location IDs. | Conditions are game-state queries and must be evaluated in a concrete save/context. Spawn success also depends on valid map tiles and runtime logic. It is not a universal record of all drops. |
| `Data/Crops` / `Game1.cropData` | Seed ID -> harvest item ID, growth phases/days, seasons, regrow days, watering/paddy/planting restrictions, min/max yield, farming-level yield increase, and extra-harvest chance. | Yes; both seed and harvest IDs are strings, so custom IDs are first-class. | It does not guarantee seed acquisition or model fertilizer, skill, farm layout, greenhouse, or player time. Multiple crop definitions may produce one item. |
| `Data/Shops` | Item stock can expose gold price, quantity/limited stock, trade currency/item, conditions, and dynamic price/stock modifiers. It can establish a real unlimited, cheap gold route. | Yes for data-defined shops and items; string/qualified IDs can be resolved through `ItemRegistry`. | Availability is context-dependent; some shops/actions are special-cased. Trade and non-gold currencies must remain explicit, not converted to gold without a policy. |
| `Data/Monsters` | In 1.6 it provides monster definition metadata used by data-driven monsters. | Only for mods which actually use/extend this 1.6 asset. | It is not a complete, normalized item-drop ledger. Vanilla and arbitrary C# monsters can calculate drops in code; no safe generic item -> monster-drop inference follows solely from this asset. |
| `Data/Machines` | Machine ID -> ordered output rules, triggers, input queries, output item-query definitions, time and conditions. It can discover some input-to-output transformation routes. | Yes for data-defined machines and string item IDs. | Outputs may be conditional, random, preserve/copy an input, or come from arbitrary item queries. A route must retain conditions/evidence; do not flatten it to a guaranteed acquisition. |
| `Data/CraftingRecipes` | Ingredient quantities, output item/count, and recipe unlock requirement expose a crafting route. | Yes for data edits that preserve the recipe format and string IDs. | Does not establish how ingredients or recipe knowledge are acquired; recipe semantics are not enough to assign a final cost. |
| `Data/CookingRecipes` | Ingredient quantities, output item/count, and unlock requirement expose a cooking route. | Yes under the same condition. | Also needs a kitchen and recipe knowledge; ingredient routes are separate. |
| `ItemRegistry` and item definitions | Resolves qualified IDs, creates items, identifies the registered type, and supports object/custom string IDs without numeric-ID assumptions. Context tags expose category/type plus generated fish/machine/geode traits. | Strong for registered game-data item types and CP content. | A C# mod can register an `IItemDataDefinition` or create behavior outside these assets. `ItemRegistry` proves identity/type, not provenance. Context tags are descriptive and mod-extensible, not a complete acquisition graph. |
| Other relevant assets | `Data/LocationContexts`, item queries, game-state queries, festivals/shops, geode rules, and map/tile actions can add access restrictions or routes. | Usually yes when emitted through normal content loading. | Quest rewards, events, mail, NPC code, commands, and patches remain partly or wholly code-driven. |

Useful source references: [object data](https://wiki.stardewvalley.net/Modding:Objects),
[fish data](https://stardewvalleywiki.com/Modding:Fish_data),
[location data](https://stardewvalleywiki.com/Modding:Location_data),
[crop data](https://stardewvalleywiki.com/Modding:Crop_data),
[machine data](https://stardewvalleywiki.com/Modding:Machines),
[shops](https://stardewvalleywiki.com/Modding:Shops),
[context tags](https://stardewvalleywiki.com/Modding:Context_tags), and
[item registry/custom item types](https://wiki.stardewvalley.net/Modding:Items).

## 4. Mod compatibility

**Stardew Valley Expanded.** Data-driven SVE objects, crops, fish, locations,
forage, and shops will be visible if indexing reads the post-patch runtime assets
(`Game1.objectData`, `Game1.cropData`, `DataLoader`, or the corresponding
content-loaded data) after content is ready. Its bespoke conditions and any
code-driven acquisition remain partial routes, not facts to guess. No
SVE-specific IDs should be hard-coded.

**Content Patcher.** CP edits are compatible when the index reads the resolved
asset, registers cache invalidation for the relevant assets, and uses strings
plus `ItemRegistry` rather than numeric IDs or asset files. Rebuild indexes after
CP invalidation; otherwise the current target cache shows the branch's existing
staleness limitation. A CP condition that depends on save state should be kept as
evidence/restriction or evaluated at runtime, not treated as always available.

**Custom string IDs.** These are supported for ordinary data-defined objects and
their routes if IDs are kept as qualified/string identities end-to-end. Never
parse them as integers; qualify unqualified object IDs only when the data source
defines them as objects.

**Arbitrary C# mods.** Best effort only. Their registered item types can be
identified through `ItemRegistry`, but their acquisition logic may exist solely
in code, Harmony patches, events, or a private API. Do not manufacture routes
from price, name, or tags. Such items should receive an `Unknown`/low-confidence
profile unless a trustworthy public integration is added later.

## 5. Current limitations

* No points system or acquisition model exists yet.
* Runtime support is limited to exact ordinary `StardewValley.Object`, for both
  source and targets. Furniture, tools, weapons, clothing, rings, boots,
  big-craftables, object subclasses, recipes, quest items, and unshippable
  objects are unsupported.
* Values are player/runtime-sensitive sale prices, not stable catalog values.
* `TargetItemProvider` only scans object data and has no current content-asset
  invalidation event hookup.
* The route data above is incomplete by design for monster drops, quest/event
  rewards, code-defined machines/shops/items, and dynamic conditions.

## 6. Recommended architecture

Keep identity, acquisition evidence, valuation, and the existing batch math
separate:

```text
qualified item ID
  -> AcquisitionProfile (zero or more AcquisitionRoute records)
  -> points valuation policy / ValuationBreakdown
  -> existing ItemValueService boundary returns S or T
  -> unchanged TargetEconomics / roller / transaction invariants
```

`AcquisitionProfile` must own a collection of routes, never a single source
type. Each route should carry its kind, normalized metrics, confidence, and
machine-readable evidence (asset/key/rule/condition); an incomplete discovery is
a partial, low-confidence route rather than an optimistic cheap route. Build
profiles from independent indexers by scanning resolved data assets. Let a later
valuation policy decide how to combine routes conservatively (for example, do not
let a weakly inferred route lower points). Keep special currencies as typed
restrictions/costs, not an invented gold exchange rate.

For the smallest safe migration, retain the existing `ItemValueService.GetValue`
call boundary and change only its implementation after the profile/indexing and
points policy have tests. Add cache invalidation at the acquisition-index layer
for every indexed content asset, then invalidate target previews when points
change.

## 7. Places that will need modification

* New domain records and tests for profiles, routes, normalized metrics,
  confidence, evidence, and valuation breakdown.
* New data-indexing services for the resolved assets, their cache lifecycle, and
  Content Patcher/SMAPI asset invalidation.
* `ItemValueService` (or a replacement behind that same narrow call boundary) to
  return points for supported items after points valuation is implemented.
* `TargetItemProvider` cache invalidation and its display value once the service
  returns points.
* Value labels/tooltips/logging in `UpgradeMenu`, `SourceItemMenu`, and
  `TargetItemMenu` so they say points rather than gold when the semantics change.
* Test project links/fixtures for pure domain and indexer checks, plus production
  tests where game-data fakes are feasible.

## 8. Places that MUST NOT be modified

Do not change these behavioral rules while replacing the meaning of `S` and `T`:

* `TargetEconomics.IsEligible`: strict `T > q * S`, positive inputs, and
  non-self target rule.
* `TargetEconomics.CalculateChance`: exact `q * S / T`; nor the matching
  multiplier/filter semantics.
* `UpgradeRoller`: its `[0,1]` validation and strict `<` roll comparison.
* `UpgradeTransactionService`: one output only; successful and failed attempts
  both consume exactly `q`; only success produces one target; validation and
  rollback protections remain.
* The `UpgradeMenu` revalidation before roll and before commit, its one-roll
  guard, and deferred commit after the roulette animation.

The future change is a substitution of the semantic unit value supplied at the
existing boundary: sell-price `S`/`T` -> points `S`/`T`. It is not a probability,
target-selection, transaction, or UI-flow redesign.
