# Spent weapon cases

Seven original low-poly spent-case meshes, authored/exported in Blender, plus 21 original
mono impact sounds created with modal synthesis. No downloaded recordings are used.
The existing weapon designs do not specify real calibres: the dimensions below are
artistic ammunition references chosen to distinguish the game's firearms.

| Weapon | Case body (length × diameter) | Appearance | Floor impact |
|---|---|---|---|
| Pistol | 23 × 12 mm | Straight, warm brass | Bright medium-weight tick |
| SMG | 19 × 10 mm | Smaller pale brass | Short, light, higher-pitched tinkle |
| Assault rifle | 45 × 10 mm | Bottleneck brass | Sharp metallic clink |
| Sniper rifle | 67 × 13 mm | Longer, darker bottleneck brass | Lower, longer brass ring |
| Shotgun | 70 × 20 mm | Red polymer hull, brass base | Damped plastic tap with base rattle |
| Grenade launcher | 48 × 42 mm | Wide olive-brass cup | Heavy hollow knock |
| Siphon rifle | 51 × 12 mm | Fictional nickel-plated bottleneck case | Brighter sustained metallic ring |

Flamethrower, Tesla coil and nail gun have `ShellsPerShot == 0` and eject no cases.
One case corresponds to one ammunition round, not one shotgun pellet or upgraded grenade
sub-projectile. Akimbo ejection alternates the actual model's ejection sockets.

All models have an open mouth, dark interior, extractor rim/groove and primer. They are
spent cases, so no bullet projectile remains in the mouth. FBX and GLB exports live in
`Assets/_Project/Art/Casings`; editable `.blend` files and the generator live here.
Unity bakes the FBX transforms into reusable meshes in the `Baked` subdirectory.

## Runtime

`CasingDefinition` assets in `Assets/_Project/Casings` store each mesh/material set, three
distinct impact clips, scale, bounce and volume. A common 3.2× display scale makes these
small props readable from the game camera while preserving their relative dimensions.

`Weapon` queues an ejection after each actual shot. `CasingEjector` runs after
`WeaponVisuals.LateUpdate`, so emission starts at the animated port after hand alignment.
Cases simulate gravity and tumbling with substepped sphere sweeps against solid scenery.
They ignore damageable actors and triggers, never block movement, and settle after a few
bounces. Sound is triggered by upward-facing surface contact, never by a timer at the shot.
Later bounces get quieter. Case lifetime is eight seconds with a short shrink-out.

The pool is bounded to 96 meshes and eight dedicated positional audio voices. Audio uses
three separately synthesized variants per type, avoids consecutive variant repetition,
adds small pitch variation and scales volume with impact speed. The separate voice pool
does not steal gunshot voices from `SfxPlayer`. The current arena has a hard floor; this
is not yet a wood/metal/dirt surface-material audio system.

The old generic `Shells` particle object is disabled and disconnected on installation.
The runtime retains its fallback for older scenes without a `CasingEjector`.

## Rebuild, tune and verify

1. Run Blender with `--background --python ArtSource/Casings/build_casings.py`.
2. In Unity choose **Tools > Zombie Shooter > Install Weapon Casings**. This updates
   the existing player and saves its scene; it does not regenerate the arena layout.
3. Future **Build Playable Arena** calls also wire the casing component.
4. Use **Validate Weapon Casings** in Arena for real-shot, floor-contact/audio,
   no-case weapon, akimbo, pause, pool-limit and expiry checks in Play mode.

Change per-type volume, bounce or display scale on the `CASE_*.asset` profiles. Existing
tuning on those fields survives reinstallation. The catalogue image shows all models at
one shared scale. `CasingSoundAudition.wav` plays three variations of each family in table
order: pistol, SMG, assault, sniper, shotgun, grenade, siphon. Pauses separate the families.

`unity_validation.txt` records the latest Unity checks. `audio_validation.json` records
duration, signal level and unique content hashes for all 21 sounds. Audio uniqueness and
clipping checks do not substitute for subjective mix tuning during play.
