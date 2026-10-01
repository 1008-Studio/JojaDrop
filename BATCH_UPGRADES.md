# Batch upgrades

Choose a source quantity `q`. `S` and `T` are the unit values of the selected source and target. One attempt always outputs exactly one target item, so the target quantity is fixed at `r = 1`.

One batch of `q` source items with a single target item on output makes exactly one roll:

`p = min(1, (q * S) / T)`

The target must be worth at least the whole source batch (`T >= q * S`), so the chance never exceeds 100%. A target that fails this check is rejected when picked, and the source quantity is capped at `T / S` while a target is selected. Failure consumes all `q` source items and creates no output. Success consumes `q` and creates exactly one target item. `q = 1` is the original one-to-one upgrade (`p = S / T`).

The menu shows the source quantity, unit/total values, chance, and multiplier. It snapshots `q` at roulette start and revalidates source stock, target validity, capacity, and quantities before inventory changes.

In multiplayer, batch upgrades are host-only. Farmhands are blocked from opening the mutating menu until a host-authoritative protocol exists.

Manual checks: test `1->1`, `2->1`, the source cap at `T / S`, a full inventory, multiple compatible source stacks, source/target changes, inventory changes while the roulette spins, close/reopen, mouse, and controller navigation.
