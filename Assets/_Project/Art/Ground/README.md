# Arena ground

`Ground_Albedo.png` — 1024×1024, seamless, tiled every 12 m by `ArenaBuilder`
(7.5 repeats across the 90 m arena).

Generated, not painted:

```bash
python3 ArtSource/Ground/build_ground.py
```

## Why it looks like this

It is deliberately **the quietest surface in the project**. The floor fills most
of a 61-degree screen, so every bit of contrast it carries is contrast competing
with the horde — and reading the horde is the game.

| | mean luminance | spread |
|---|---|---|
| characters | ~56 | wide |
| ground | 89 | 23 |

Three rules it is built to:

- **Value sits just above the characters and barely moves**, so a silhouette
  always separates from it.
- **Hue is cool** where the characters are warm and green, so they separate by
  temperature too — which still works when a dark archetype crosses a shadow.
- **Tone is quantised into five steps**, not a continuous gradient. Everything
  else in the project is flat-shaded low-poly, and photographic grain fights it.

The faint grid is slab seams, three to a tile — a seam every four metres. It says
*paved*, which is what the shacks and storefronts are standing on, and gives the
eye a scale reference on a surface that otherwise has none.

## What was tried and rejected

The first pass used continuous noise and long meandering crack lines. At twenty
metres the cracks did not read as cracks, they read as **scribbled hair** across
the whole arena — the exact noise the design was meant to avoid. Cracks are now
short, nearly straight and sparse, at half the contrast.

The first dirt pass drifted far enough into tan that the olive archetypes stopped
separating from it. It is now weaker, cooler, and rarer.

Judge changes with `ArtSource/Ground/preview_ground.py`, which renders the floor
under five real characters from the game's own camera. A flat swatch will not
tell you whether an enemy reads against it.
