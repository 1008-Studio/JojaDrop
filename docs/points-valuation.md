# Points valuation model

`PointsValuationEngine` is pure: it accepts an `AcquisitionProfile` and a
canonical base value (for ordinary objects, a future caller may use
`Data/Objects.Price`). It never reads player state, `sellToStorePrice`, weather,
season, luck, professions, or installed-item aggregates.

For each route, known metrics are normalized from `0..100` to `0..1`; missing
metrics are neutral. If a route has an explicit availability probability, its
scarcity is `clamp(-ln(max(p, 1e-9)) / -ln(0.001), 0, 1)`. This yields about
0.10 at 50%, 0.33 at 10%, 0.67 at 1%, and 1.00 at 0.1%.

```text
S = 1.25D + 1.50R + A + 0.75E + 0.75C + 1.25U - F
M = clamp(exp(0.55S), 0.80, 10.0)
RoutePoints = BaseValue * M
```

The selected value is the least expensive high- or medium-confidence route.
Low-confidence routes stay in the breakdown but cannot lower the result. If no
reliable route exists, points equal the canonical base value. This makes a
guessed easy route unable to collapse the value of a rare item.

Two rejected alternatives were: linear `R = 1 - p`, which barely distinguishes
1% from 0.1%, and an unconstrained additive multiplier, which cannot naturally
reach 2x–8x for difficult items. The selected logarithmic rarity and clamped
exponential multiplier provide that separation without global normalization or
unbounded growth.
