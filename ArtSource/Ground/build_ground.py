"""
Arena ground: worn asphalt for a survival town, generated seamless.

DESIGNED AGAINST THE CHARACTERS, NOT IN ISOLATION
-------------------------------------------------
The roster is dark and green-dominant - skin around luminance 0.12-0.20, cloth in
saturated corners of the wheel - and it renders at mean luminance ~56/255. The
ground fills most of a 61-degree screen, so anything competitive on value or hue
costs enemy readability, which in a twin-stick horde game is the whole game.

So the ground is deliberately the quietest surface in the project:
  * VALUE sits just below the characters and barely moves. Total spread is about
    40/255 where the characters span ~100, so a silhouette always separates.
  * HUE is near-neutral and cool, with warm dirt drifting through it. Cool ground
    under warm-and-green bodies separates by temperature as well as value, which
    keeps working when a dark archetype crosses a shadow.
  * FREQUENCY is mostly low, and QUANTISED. The characters and buildings are
    flat-shaded low-poly, so a continuous photographic grain fights them. Broad
    tonal steps read as worn concrete slabs and belong to the same drawing.
    The first pass used continuous noise plus long wandering crack lines; at
    twenty metres those did not read as cracks, they read as scribbled hair
    across the whole arena, which is the exact noise this was meant to avoid.

Seamless by construction: every noise layer is generated on a torus, so the
1024px tile repeats without a visible seam at any tiling rate.

    python3 build_ground.py
"""
import os
import numpy as np
from PIL import Image

SIZE = 1024
OUT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                   "../../Assets/_Project/Art/Ground"))

rng = np.random.default_rng(20260912)


def value_noise(cells):
    """Periodic value noise. Sampling a wrapped lattice is what makes it tile."""
    lattice = rng.random((cells, cells))
    ys, xs = np.meshgrid(np.linspace(0, cells, SIZE, endpoint=False),
                         np.linspace(0, cells, SIZE, endpoint=False), indexing="ij")
    y0, x0 = np.floor(ys).astype(int), np.floor(xs).astype(int)
    fy, fx = ys - y0, xs - x0
    # Smoothstep, so patches have soft shoulders instead of diamond creases.
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    y1, x1 = (y0 + 1) % cells, (x0 + 1) % cells
    y0, x0 = y0 % cells, x0 % cells
    top = lattice[y0, x0] * (1 - fx) + lattice[y0, x1] * fx
    bottom = lattice[y1, x0] * (1 - fx) + lattice[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def fbm(octaves, cells):
    total = np.zeros((SIZE, SIZE))
    amplitude, weight = 1.0, 0.0
    for i in range(octaves):
        total += value_noise(cells * 2 ** i) * amplitude
        weight += amplitude
        amplitude *= 0.5
    return total / weight


# ---- base surface -------------------------------------------------------
# Just under the characters' value, and cool. Authored in sRGB bytes because that
# is what a PNG holds and what the eye judges.
BASE = np.array([88.0, 90.0, 95.0])

# Quantised into slabs. Five steps is enough to read as varied concrete and few
# enough that each patch stays flat, which is what matches the shading elsewhere.
STEPS = 5
macro = fbm(3, 4)
slabs = np.floor(macro * STEPS) / (STEPS - 1.0)
micro = fbm(2, 40)                     # a whisper, only to stop the steps banding

albedo = np.repeat(BASE[None, None, :], SIZE, 0).repeat(SIZE, 1)
albedo += ((slabs - 0.5) * 30.0)[..., None]
albedo += ((micro - 0.5) * 5.0)[..., None]

# ---- dirt drift ---------------------------------------------------------
# Warm, low frequency, and never strong. Gives the cool asphalt somewhere to go
# without introducing a second thing the eye has to parse.
# Two steps, not a gradient, and weaker than the first pass - which drifted far
# enough into tan that the olive archetypes stopped separating from it.
DIRT = np.array([10.0, 5.0, -4.0])
dirt = np.clip((fbm(2, 3) - 0.60) * 3.0, 0, 1)
dirt = np.floor(dirt * 2.0) / 2.0
albedo += dirt[..., None] * DIRT

# ---- slab seams ---------------------------------------------------------
# A faint grid, three cells to the tile, which at the builder's 12m tiling is a
# seam every four metres. Two jobs: it says PAVED, which is what the shack-and-
# storefront buildings are standing on, and it gives the eye a scale reference on
# a surface that otherwise has none. Kept very low contrast - it should be read
# without being looked at.
SEAM_CELLS = 3
seam = np.zeros((SIZE, SIZE))
pitch = SIZE / SEAM_CELLS
wobble = fbm(2, 8) * 6.0 - 3.0
ys, xs = np.meshgrid(np.arange(SIZE), np.arange(SIZE), indexing="ij")
for axis, coord in ((0, ys), (1, xs)):
    offset = (coord + wobble) % pitch
    seam = np.maximum(seam, np.clip(1.6 - np.minimum(offset, pitch - offset), 0, 1))
albedo -= (seam * 9.0)[..., None]

# ---- cracks -------------------------------------------------------------
# Random walks across the torus. Wrapping the indices is what keeps the tile
# seamless; a straight line clipped at the edge would show as a seam.
# Short, nearly straight, and sparse. A crack in a slab runs a metre or two and
# stops; the long meandering version read as stray hairs lying on the arena.
cracks = np.zeros((SIZE, SIZE))
for _ in range(14):
    y, x = rng.integers(0, SIZE, 2).astype(float)
    heading = rng.random() * 2 * np.pi
    for _ in range(int(rng.integers(90, 220))):
        heading += (rng.random() - 0.5) * 0.06
        y = (y + np.sin(heading)) % SIZE
        x = (x + np.cos(heading)) % SIZE
        cracks[int(y), int(x)] = 1.0

crack_image = Image.fromarray((cracks * 255).astype(np.uint8)).filter(
    __import__("PIL.ImageFilter", fromlist=["ImageFilter"]).GaussianBlur(0.8))
cracks = np.asarray(crack_image, dtype=float) / 255.0
albedo -= (cracks * 15.0)[..., None]

# ---- grit ---------------------------------------------------------------
# Sparse, and both lighter and darker, so it reads as loose stone rather than
# as film grain over the whole arena.
speckle = rng.random((SIZE, SIZE))
albedo += np.where(speckle > 0.9985, 12.0, 0.0)[..., None]
albedo -= np.where(speckle < 0.0015, 11.0, 0.0)[..., None]

albedo = np.clip(albedo, 0, 255).astype(np.uint8)

os.makedirs(OUT, exist_ok=True)
Image.fromarray(albedo).save(os.path.join(OUT, "Ground_Albedo.png"))

luma = 0.2126 * albedo[..., 0] + 0.7152 * albedo[..., 1] + 0.0722 * albedo[..., 2]
print(f"wrote {OUT}/Ground_Albedo.png  {SIZE}x{SIZE}")
print(f"  luminance mean={luma.mean():.1f}  p2={np.percentile(luma,2):.1f}  "
      f"p98={np.percentile(luma,98):.1f}  spread={np.percentile(luma,98)-np.percentile(luma,2):.1f}")
print("  characters render at mean ~56 with a far wider spread, so bodies stay separable.")
