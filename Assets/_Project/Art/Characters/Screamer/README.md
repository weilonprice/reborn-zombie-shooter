# Screamer

A distended throat and an unhinged jaw. Built to be picked out of a crowd.

Generated, not modelled. Source: [`ArtSource/Enemies/build_enemies.py`](../../../../../ArtSource/Enemies/build_enemies.py),
function `Screamer()`. Re-export with:

```bash
blender --background --python ArtSource/Enemies/build_enemies.py -- Screamer
```

## Files

- `Screamer.fbx` — Unity import. Carries **no animation clips**; it borrows the
  normal zombie's, which is only possible because the whole roster shares one
  skeleton. See [the kit README](../../../../../ArtSource/Enemies/README.md).
- `Screamer_Palette.png` — the sixteen-cell palette texture. The mesh has a single
  material that samples it, so each enemy is one draw call.

## Budget

| | |
|---|---|
| Vertices | 1080 |
| Triangles | 1992 |
| Bones | 18 |
| Materials | 1 |
| Height | 2.075 m |
| Half-width at the chest | 0.597 m |

Half-width matters: the hit zone authored in `ArenaBuilder.AddLimbHitbox` is sized
to it, and the walking CharacterController is narrower than both.
