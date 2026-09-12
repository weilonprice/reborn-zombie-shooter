# Weapon animation set

All weapon FBX exports use the same runtime vocabulary so the loadout can swap models without special case animation code.

## Shared clips

- `Idle` — looping ready pose and subtle breathing sway.
- `Equip` — raise the weapon into the aiming position.
- `Unequip` — lower the weapon when changing slots.
- `Fire` — recoil and recovery; fired by `Weapon.Fired`.
- `Reload` — full reload motion; fired by the weapon reload events.
- `Charge` — looping charge pose used by energy weapons while the trigger is held.
- `Inspect` — short readable inspection flourish for future inspect input.
- `Melee` — close range weapon bash for future alternate fire.

## Weapon-specific mechanical clips

| Weapon | Additional clip |
| --- | --- |
| Pistol | `SlideCycle` |
| Shotgun | `Pump` |
| AssaultRifle | `BoltCycle` |
| SniperRifle | `BoltCycle` |
| Flamethrower | `Ignite` |
| TeslaCoil | `Discharge` |
| GrenadeLauncher | `DrumCycle` |
| SMG | `BoltCycle` |
| NailGun | `DriverCycle` |
| SiphonRifle | `Drain` |

The clips are transform-only prop actions authored on each weapon root. Unity imports them from the FBX and the generated per-weapon controller exposes the exact set above. `WeaponAnimator` drives the shared clips from gameplay events, while `WeaponVisuals` keeps muzzle, eject, and off-hand sockets in sync with the animated root.
