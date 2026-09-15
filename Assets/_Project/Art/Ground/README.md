# Arena ground

`Ground_Albedo.png` — 1024×1024, tiled every **24 m** by `ArenaBuilder`
(3.75 repeats across the 90 m arena).

The artwork is tan packed dirt, muted olive grass and grey stone. The master and
the prompt that produced it live in
[`ArtSource/Ground`](../../../../ArtSource/Ground); `build_ground.py` restores this
file from it. **Do not edit this PNG** — it is a build output, and the destination
`.meta` is deliberately preserved so the texture GUID and every reference to it
survive a restore.

Two things the build step does on the way in, neither of which the eye catches:

- **Resizes to a power of two.** The master is 1254², and Unity's default
  `npotScale` is `ToNearest` — so it was silently resampling to 1024 on import,
  meaning the pixels shipping in a build were not the pixels in the repository.
- **Closes the tile.** The master's opposite edges differed by about 2.2× a normal
  interior step, which at 3.75 repeats is a faint seam at every join. The trailing
  rows and columns are cross-faded into the mirrored opposite edge over a 64 px
  margin, taking the measured mismatch to zero.

## Readability

The floor fills most of a 61° screen, so its contrast competes with the horde —
and reading the horde is the game.

| | mean luminance | spread |
|---|---|---|
| characters | ~56 | wide |
| ground | 128 | 58 |

Bodies separate on **value**: they are dark silhouettes on a bright floor, and by
a wider margin than the generated concrete this replaced (which sat at 89).

The trade is internal contrast — spread 58 against the old 23. That is a busier
surface to pick a crawler out of, and **no wave of crawlers has been played on
it.** Since 2026-09-13 the map also has no buildings, so the floor is most of what
is on screen.

Judge changes with `ArtSource/Ground/preview_ground.py`, which renders the floor
under five real characters from the game's own camera. A flat swatch will not tell
you whether an enemy reads against it.
