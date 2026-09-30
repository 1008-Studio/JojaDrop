# Batch upgrades

Choose source quantity `q` and target quantity `r`, with `1 <= r <= q`. `S` and `T` are the unit values of the selected source and target.

One batch makes exactly one roll:

`p = min(1, (q * S) / (r * T))`

Failure consumes all `q` source items and creates no output. Success consumes `q` and creates `r` target items. `q=1, r=1` is the original one-to-one upgrade.

The menu shows quantities, unit/total values, batch chance, and multiplier. It snapshots q/r at roulette start and revalidates source stock, target validity, capacity, and quantities before inventory changes.

In multiplayer, batch upgrades are host-only. Farmhands are blocked from opening the mutating menu until a host-authoritative protocol exists.

Manual checks: test `1->1`, `2->1`, `2->2`, maximum source quantity, a full inventory, multiple compatible source stacks, source/target changes, inventory changes while the roulette spins, close/reopen, mouse, and controller navigation.
