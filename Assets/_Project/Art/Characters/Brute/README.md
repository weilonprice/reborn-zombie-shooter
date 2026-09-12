# Brute

A slab of shoulders with the head sunk between them. The widest plan view on the roster.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Brute()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Brute
```

## Files

- `Brute.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Brute_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1028 |
| Triangles | 1904 |
| Bones | 18 |
| Materials | 1 |
| Height | 1.999 m |
| Half-width at the chest | 0.64 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
