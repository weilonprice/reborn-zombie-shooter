# Normal Zombie

Stylized, original infected civilian for Reborn. The proportions are intentionally chunky and readable from the project’s top-down camera: hunched torso, oversized hands and boots, strong face planes, muted sage skin, faded petrol work shirt, charcoal trousers, and warm wound/eye accents.

## Files

- `NormalZombie.fbx` — Unity-friendly skeletal export with baked animation clips.
- `NormalZombie.glb` — portable glTF export with embedded palette texture and animations.
- `NormalZombie_Palette.png` — small palette texture used by the GLB material.

The editable Blender source and the generation script live in `ArtSource/NormalZombie/`.

## Detail budget

This is the **baseline** archetype — spawn weight 10, never falls off — so most of the sixty
enemies alive at wave 15 are this mesh. The camera sits about 23m out, which makes it roughly
eighty pixels tall on a 1080p screen.

`build_zombie.py` therefore builds at two resolutions, chosen with `ZOMBIE_DETAIL`:

| | Spheres | Tubes | Bevel | Result |
|---|---|---|---|---|
| `horde` (default) | 8 × 5 | 6 | 1 | what ships |
| `hero` | 16 × 10 | 12 | 2 | the original, for renders or a close-up |

Resolution is lowered **at generation time** rather than by decimating a finished mesh —
primitives built coarse stay clean, decimated ones do not. Call sites that ask for more
detail positionally (the cranium asks for 24 × 16) are clamped to the budget rather than
edited, so the authored intent stays readable and `hero` still produces exactly what was
written.

Note that a Unity `LODGroup` would achieve nothing here: the top-down camera never changes
distance, so every zombie would sit on the same LOD level forever. Lower resolution is the
whole fix.

`ArenaBuilder.TuneForHorde` handles the rest on the Unity side — shadow casting off,
two-bone skinning, no per-frame bounds recalculation, and animator culling that keeps the
state machine ticking while skipping transform writes nothing can see. Those matter more than
the polygon count, because skinned meshes do not batch and the GPU Resident Drawer does not
touch them: every horde member is its own draw call and its own skinning pass, and a
shadow-casting one pays both twice.

## Rig and clips

**This rig is shared by the whole roster, and its clips are the only clips in the
game.** Eleven other archetypes plus the boss borrow everything below, so the bone
names, the rest pose and the armature name `NormalZombie_Rig` are fixed. Generic
rigs bind curves by transform path, and `frame()` keyframes bone LOCATION as well
as rotation — so renaming the armature breaks every archetype at once, and moving
a bone's rest position would snap every archetype back to these proportions.
See [`ArtSource/Enemies/README.md`](../../../../../ArtSource/Enemies/README.md).

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

## Palette UVs

The character bakes its colours into `NormalZombie_Palette.png` — sixteen cells,
one per material — and ships a single material that samples it. The bake removes
the smart-project UV layer rather than deselecting it: `active_render` does not
survive an FBX round trip, and for most of this character's life both layers
shipped, so Unity sampled the sixteen-cell palette with a 6,382-point unwrap and
scattered every colour on the model. Every render of this zombie before
2026-09-12 was of that artifact. See debt 12 in `ROADMAP.txt`.
