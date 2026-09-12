# Ability animations

Four skeletal clips authored on the project's existing 18-bone zombie and survivor rigs.
FBX is the Unity source; GLB includes the reference character for portable preview.
Editable Blender scenes and the reproducible generator live in `ArtSource/AbilityAnimations`.

| Character | Clip | Duration | Playback | Gameplay hook |
|---|---|---:|---|---|
| Screamer | Scream | 1.50 s | One shot, repeats every 3 s | `HordeAura.Screamed` |
| Revenant | GetUp | 2.20 s | Collapse, prone hold, push up, stand | `Revenant.Reviving` |
| Leaper | Leap | 0.65 s | Coil, tuck, reach, landing compression | `BarricadeLeaper.LeapStarted` |
| Player | Ultimate | 0.667 s | Loop throughout the ultimate magazine | `Weapon.UltimateActive` |

All clips use 60 fps. Root motion is disabled. The vault clip has no root translation;
`BarricadeLeaper` remains responsible for the complete vault arc. `PlayerController`
continues the 540 degrees/second world spin; the Ultimate pose adds no second root spin.

## Unity integration

The Screamer, Revenant and Leaper prefabs now use the existing skinned zombie, with
distinct proportions/tints and a `ZombieAnimator`. Their old primitive body markers and
shrinking `DeathPop` are removed so they can show the skeletal actions and death.

The ability clips occupy the zombie controller's base layer. Attack and flinch callbacks
cannot interrupt a signature action; death always can. Screamer howl and Revenant recovery
hold movement and attacks. The 12m aura remains continuous with a separate visual cadence.
Leapers cannot attack in mid-vault.

Ultimate owns the player's full body, disabling the masked upper-body layer during the
ability and restoring normal locomotion/aim afterwards. Death takes priority over it.

The Revenant still survives only one lethal hit per spawn and restores 45% health immediately.
It remains vulnerable while getting up. `Health.Heal` can restore zero HP only during the
death-interceptor callback; ordinary healing cannot revive a corpse. No death payout occurs
on the intercepted hit.

## Rebuild and checks

- Export: `Blender --background --python ArtSource/AbilityAnimations/build_ability_animations.py`.
- In Unity: **Tools > Zombie Shooter > Install Ability Animations** updates these three
  prefabs and both controllers without regenerating the arena.
- **Build Playable Arena** also includes the ability setup for future scene rebuilds.
- **Validate Ability Animations** runs integration checks in a temporary Play-mode scene,
  then restores the prior scene. Results are written to
  `ArtSource/AbilityAnimations/unity_validation.txt`; proof renders are in `proof`.

The checks cover imported durations, looping, bone bindings, aura cadence/radius, actual
revive health/death events, attack suppression, pool reuse, vault landing and the ultimate's
activation, cancellation, damage priority and death interruption.
