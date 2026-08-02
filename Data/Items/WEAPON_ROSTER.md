# September 2001 Weapon Roster

This catalog uses real military weapons, or military prototypes, that existed by the campaign start in September 2001. Progression tiers describe game availability and acquisition sophistication, not the weapon's manufacturing date.

## Progression

| Tier | ID | Item | Class | Intended role |
|---|---:|---|---|---|
| Starting | 0 | M67 Fragmentation Grenade | Explosive | Limited area damage |
| Starting | 1 | KA-BAR Combat Knife | Melee | Fast, light melee backup |
| Starting | 2 | M1911A1 Service Pistol | Sidearm | Hard-hitting but inaccurate starter pistol |
| Starting | 110 | Entrenching Tool | Melee | Cheap, heavier starter melee |
| Starting | 130 | M3A1 Submachine Gun | SMG | Inaccurate close-range automatic weapon |
| Starting | 150 | AKM Assault Rifle | Assault rifle | Powerful, affordable, difficult to control |
| Standard | 3 | M16A2 Service Rifle | Assault rifle | Accurate general-purpose rifle with burst fire |
| Standard | 111 | Military Machete | Melee | Balanced field blade |
| Standard | 120 | M9 Service Pistol | Sidearm | Accurate standard sidearm |
| Standard | 131 | MP5A3 | SMG | Precise close-quarters weapon |
| Standard | 140 | Mossberg 500 | Shotgun | Pump-action close-range burst damage |
| Standard | 160 | M14 Designated Marksman Rifle | Marksman rifle | Midpoint between rifle and sniper weapon |
| Standard | 170 | M249 Squad Automatic Weapon | Light machine gun | Squad suppression |
| Advanced | 112 | Tactical Breaching Axe | Melee | Slow, high-damage melee |
| Advanced | 121 | Glock 17 | Sidearm | Light and fast modern pistol |
| Advanced | 132 | UMP45 | SMG | Modern high-damage SMG |
| Advanced | 141 | Benelli M4 | Shotgun | Fast semiautomatic combat shotgun |
| Advanced | 151 | M4A1 Carbine | Assault rifle | Compact modular automatic carbine |
| Advanced | 152 | G36K Carbine | Assault rifle | Accurate carbine with integrated optics |
| Advanced | 161 | M24 Sniper Weapon System | Sniper rifle | Slow, high-damage precision weapon |
| Advanced | 171 | M240B Machine Gun | General-purpose machine gun | Heavy 7.62 mm sustained fire |
| Experimental | 122 | Mk 23 Mod 0 | Sidearm | Elite special-operations pistol |
| Experimental | 133 | P90 Personal Defense Weapon | PDW | Compact, controllable high-volume fire |
| Experimental | 153 | F2000 Prototype | Assault rifle | Cutting-edge 2001 bullpup |
| Experimental | 162 | PSG1 Precision Rifle | Sniper rifle | Fast semiautomatic precision fire |

## Data conventions

- `AvailableAtCampaignStart` is enforced by purchasing and starting-equipment
  lists. An item with `RequiredResearch` can only be purchased after that exact
  project is complete and an `UnlockItemsResult` explicitly grants its item ID.
  `ResearchTier` remains organizational metadata.
- `damage` is per projectile or shotgun pellet.
- `accuracy` is added to the unit's ranged-accuracy stat. Snap and automatic modes deliberately carry larger penalties than aimed fire.
- `attackCount` is the number of projectiles or pellets produced by one action.
- `timeUnitCost` and `staminaCost` apply once per trigger pull. They are independent of projectile count, allowing shotguns and automatic weapons to be balanced correctly.
- `weight` remains inventory burden and still affects throwing actions.
- Item IDs are permanent save-data identifiers. Add new items with new IDs; do not renumber existing entries.
