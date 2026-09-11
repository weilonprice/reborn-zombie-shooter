# Normal Zombie

Stylized, original infected civilian for Reborn. The proportions are intentionally chunky and readable from the project’s top-down camera: hunched torso, oversized hands and boots, strong face planes, muted sage skin, faded petrol work shirt, charcoal trousers, and warm wound/eye accents.

## Files

- `NormalZombie.fbx` — Unity-friendly skeletal export with baked animation clips.
- `NormalZombie.glb` — portable glTF export with embedded palette texture and animations.
- `NormalZombie_Palette.png` — small palette texture used by the GLB material.

The editable Blender source and the generation script live in `ArtSource/NormalZombie/`.

## Rig and clips

The rig is `NormalZombie_Rig` with 18 bones. The mesh is one skinned object, so the character can be imported as one prefab and remains inexpensive to render in a horde.

| Clip | Frames | Length | Loop |
|---|---:|---:|---|
| `Chase` | 33 | 1.07 s | Yes |
| `Attack` | 39 | 1.27 s | No |
| `GetShot` | 18 | 0.57 s | No |
| `Stagger` | 46 | 1.50 s | No |
| `Death` | 55 | 1.80 s | No |

`Chase` keeps the root in place for Unity’s `ZombieAI` movement. The other clips include their authored reaction and body motion; `Death` settles to the floor and leaves the character at rest.

## Unity integration

`ArenaBuilder` now wires `NormalZombie.fbx` into `Assets/_Project/Prefabs/Zombie.prefab` automatically. It creates `NormalZombie.controller`, assigns the five clips, keeps root motion off so `ZombieAI` retains movement control, and adds `ZombieAnimator` to map chase, attack, hit, stagger, and death gameplay events to the authored states. Re-running **Tools → Zombie Shooter → Build Playable Arena** regenerates the integration from these source assets.

The prefab keeps its existing `Health`, `ZombieAI`, `DeathPop`, and `HitFlash` gameplay components. The imported model is a child visual, with the gameplay root remaining at the existing 1.9 m controller height.

The GLB is included for tools and future pipelines; it has one skin with 18 joints and all five animation names embedded.
