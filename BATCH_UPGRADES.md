# Batch upgrades

Choose a source quantity `q`. `S` and `T` are the unit values of the selected source and target. One attempt always outputs exactly one target item, so the target quantity is fixed at `r = 1`.

One batch of `q` source items with a single target item on output makes exactly one roll:

`p = (q * S) / T`

The target must be worth strictly more than the whole source batch (`T > q * S`), so valid chances are always below 100%. A target that fails this check is rejected; selecting a target never changes the user-selected source quantity. If a quantity change makes the selected target invalid, the target is cleared and the quantity is kept. The source item itself is never a target. Failure consumes all `q` source items and creates no output. Success consumes `q` and creates exactly one target item. `q = 1` is the original one-to-one upgrade (`p = S / T`).

**All** shows every eligible upgrade target, not every positive-value object. The x2/x3/x5/x10 filters are subsets of those targets and use the same whole-batch chance.

The menu shows the source quantity, unit/total values, chance, and multiplier. It snapshots `q` at roulette start and revalidates source stock, target validity, capacity, and quantities before inventory changes.

In multiplayer, batch upgrades are host-only. Farmhands are blocked from opening the mutating menu until a host-authoritative protocol exists.

Manual checks: test `1->1`, `2->1`, strict target values above the whole source batch, source/target changes, a full inventory, multiple compatible source stacks, inventory changes while the roulette spins, close/reopen, mouse, and controller navigation.
