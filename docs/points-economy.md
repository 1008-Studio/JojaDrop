# Points economy

## 1. Final formula

JojaDrop now uses stable `pts` for supported ordinary objects. The canonical
base is `Data/Objects.Price`, not the current player's sell price.

For each acquisition route, known metrics are normalized from `0..100` to
`0..1` and scored as follows:

```text
R = clamp(-ln(max(availabilityChance, 1e-9)) / -ln(0.001), 0, 1)
S = 1.25D + 1.50R + A + 0.75E + 0.75C + 1.25U - F
M = clamp(exp(0.55S), 0.80, 10.0)
RoutePoints = round(BasePrice * M)
```

For deterministic machine, crafting, and cooking routes:

```text
InputCost = sum(inputPoints * quantity)
ProductionFloor = ceil(InputCost / expectedOutput)
ProductionModifier = clamp(1 + 0.25 * (Effort + Restrictions), 1, 1.5)
ProcessedRoutePoints = round(ProductionFloor * ProductionModifier)
RoutePoints = max(RoutePoints, ProcessedRoutePoints)
```

## 2. Metrics

`Difficulty`, `Scarcity`, `Access`, `Effort`, `Restrictions`, `Uniqueness`,
and `Farmability` are each bounded to `0..100`. `AvailabilityChance`, when
known, supplies logarithmic scarcity; otherwise the indexed scarcity metric is
used. Missing metrics remain neutral rather than guessed.

## 3. Acquisition routes

Profiles can retain Fishing, Farming, Foraging, Mining, Geode, MonsterDrop,
Machine, Crafting, Cooking, Shop, Quest/Event, and Unknown routes. The current
data indexer discovers data-defined fishing, farming, foraging, geode, machine,
crafting, cooking, and shop routes. Every route records confidence and evidence.

## 4. Multiple-route policy

The final value is the cheapest High- or Medium-confidence route. A real
unlimited shop route may therefore cap an item. Low-confidence routes are shown
in diagnostics but cannot lower an item's value. A production route cannot fall
below verified static inputs; an independently cheaper legitimate route may win.

## 5. Mod compatibility

The provider reads resolved SMAPI assets, so ordinary objects and routes added
through Content Patcher, Stardew Valley Expanded, or another data-driven mod are
indexed by their string IDs. It caches one resolved snapshot and rebuilds it
after invalidation of `Data/Objects`, `Fish`, `Locations`, `Crops`, `Shops`,
`Machines`, or either recipe asset.

## 6. Fallback

An unknown or code-only item receives an Unknown/Low-confidence profile and
falls back to its canonical base; it never crashes valuation. A diagnostic is
logged once per cache generation. `config.json` may add a manual fallback route
only for such items:

```json
{
  "ItemOverrides": {
    "(O)Some.Mod.Item": { "Source": "Mining", "Difficulty": 0.8 }
  }
}
```

Metric override values are normalized `0..1`. Overrides do not replace or
compete with data-defined routes.

## 7. Known limitations

JojaDrop still supports only exact, shippable `StardewValley.Object` items;
tools, furniture, big craftables, recipes, quest items, and specialized object
subclasses are excluded. Code-only monster/mining/quest routes remain Unknown.
Category ingredients, random outputs, and custom machine output methods retain
evidence but do not get an invented production floor. Conditions are recorded,
not evaluated for every possible save state.

## 8. Representative valuation examples

These deterministic regression examples exercise the general formula; none is
a per-item price override. Values are rounded points from the listed base and
route metrics.

| Example | Base | Points | Why |
| --- | ---: | ---: | --- |
| Common fish | 75 | 99 | Common 50% fishing route |
| Difficult fish | 750 | 5,142 | Difficulty, access, restrictions, 5% route |
| Legend | 7,500 | 75,000 | Boss/limited fish reaches the shared 10× cap |
| Common crop | 35 | 31 | Renewable basic crop |
| Expensive crop | 750 | 1,393 | Seasonal, slow farming route |
| Regrow crop | 750 | 1,058 | Same route with stronger farmability |
| Common forage | 100 | 109 | 50% forage route |
| Rare forage | 100 | 188 | 1% forage route |
| Copper-related resource | 75 | 123 | Low mining difficulty/rarity |
| Iron-related resource | 150 | 387 | Medium mining fixture |
| Gold-related resource | 400 | 1,626 | Higher mining fixture |
| Iridium-related resource | 1,000 | 6,950 | High access and rarity fixture |
| Common monster drop | 50 | 68 | 50% drop fixture |
| Rare monster drop | 50 | 117 | 1% drop fixture |
| Mineral/geode result | 100 | 173 | 1% geode result |
| Artisan product | 2,300 | 3,333+ | Machine effort; static inputs can raise it further |
| Unlimited shop item | 500 | 400 | Easy unlimited shop route |
| Mass-farmable item | 100 | 80 | High farmability floor multiplier |
| Difficult low-price item | 10 | 100 | Shared 10× cap, not a special exception |

## 9. Upgrade economy unchanged

```text
Probability formula: unchanged
Eligibility formula: unchanged
Output quantity: unchanged
Failure consumption: unchanged
RNG: unchanged
Stardew drop rates: unchanged
Stardew crop yields: unchanged
```

The unchanged equations now read `targetPoints > q * sourcePoints` and
`chance = (q * sourcePoints) / targetPoints`. A success consumes `q` source
items and creates exactly one target; a failure consumes `q` and creates none.
