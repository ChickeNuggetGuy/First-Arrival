# Unconscious units and body items

Inspired by the original UFO: Enemy Unknown rules described at
https://www.ufopaedia.org/Unconscious.

## Rules

- Stun at or above current health causes unconsciousness. Zero health is death.
- Equipment drops on collapse. The unit stops acting, occupying cells and
  contributing sight.
- Each team turn applies bleeding, then removes one stun from its living units,
  including carried bodies. A unit wakes when stun falls below health.
- Recovery tries the body's cell, then adjacent cells clockwise from north,
  checking the full footprint and ground support. Blocked recovery retries later.
- Natural recovery restores effective maximum time units. `ApplyStimulant()`
  removes four stun; a dose that enables recovery gives zero time units. An
  earlier insufficient dose does not penalize later natural recovery.
- Bodies can be picked up, carried and thrown. A carried unit wakes near its
  carrier and its body item disappears.
- Health-damaging explosions destroy existing bodies; direct fire cannot target
  them, and stun-only explosions leave them intact.

## Ownership and components

`GridObjectCondition` is added at initialization to non-scenery objects with both
health and time units. It is available as `unit.Condition` and through
`TryGetGridObjectNode<GridObjectCondition>()`. Doors do not gain consciousness.
The component owns `Conscious`, `Unconscious`, and `Dead` state; `IsActive` remains
battle participation, so stored campaign units are not confused with casualties.

**The original GridObject and its components are never replaced.** Inactive units
stay in their team roster. `UnitBodyItem.LinkedUnit` refers to that original object;
its stats, wounds, inventory component, team, name and other components remain
accessible there. Each unit has a persistent `UnitId`, and its body saves only that
ID and display metadata. No unit data is embedded or copied inside an item.

Unit data is saved once in its team's roster. Ground and carried inventories save
body IDs alongside other item state. `GridObjectManager.RestoreBodyLinks()` joins
them after every team and the ground inventories have loaded, including bodies
carried by the other team. It rejects orphaned and duplicate proxies. A body held
by the inventory cursor is returned to its source before a world save.

Bodies occupy a 2 × 3 inventory shape, never stack, and currently use weight 4 in
the existing abstract inventory/throw-cost scale. The floor uses a simple capsule
placeholder and the inventory uses a body silhouette. `ItemData.CreateBodyData`
contains these defaults; it does not duplicate the unit model or components.

## Combat and integration

The Stun Rod (`Data/Items/Stun_Rod_Item.tres`, ID 113) is registered in the item
database and available with the normal starting equipment. It deals 40 stun for
24 time units and 16 stamina. These are initial tuning values, not a reproduction
of the original game's randomized weapon rolls.

Melee and ranged action definitions expose `dealsStunDamage`. Ranged stun uses the
configured ammunition damage. Explosions can add stun through their `affectedStats`
entry for `Enums.Stat.Stun`. Existing damaging weapons retain health damage.

Environmental effects can call `unit.Condition.ApplyStun(amount)`; medical actions
can call `unit.Condition.ApplyStimulant()` and use the linked unit's existing
`Health.HealFatalWound(bodyPart)` API. This feature supplies these integration
points, not a new smoke simulation or medikit interface.

The health bar displays stun as a gray overlay and reports its value in the
hover text. Body inventory tooltips identify the unit and whether it is alive.

## Mission recovery

Successful full-field recovery includes unconscious player soldiers. Extraction
recovery includes a body only if its ground/carrier location is in the extraction
area. Dead bodies are not returned as living soldiers, and bodies are never sold
or added to stackable equipment recovery. Unconscious enemies are not counted as
kills; an alien-containment/research reward system is outside this change.

Evacuation clears tactical unconsciousness and stun in the recovered save, while
preserving health and wounds. The live battle object is not mutated by previewing
mission recovery. This prevents a returned soldier entering the next battle with
an orphaned body link.

Large units use the same footprint-safe waking rule as other units, rather than
reproducing the original game's large-alien inability to wake or its floating,
cloning and ghost-inventory bugs.

## Verification

Run `python3 Tests/run_unit_condition_regression.py --godot /path/to/Godot` using
the .NET Godot version matching this project. The fixture exercises state
thresholds, real turn processing, equipment drops, blocked/carried waking, wounds,
explosions, cross-team save/load and mission recovery. Existing door/navigation
and action-performance regressions cover the shared action and grid changes.
