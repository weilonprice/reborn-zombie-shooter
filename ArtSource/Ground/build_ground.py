"""
Restore the approved dirt/grass/stone artwork to its stable Unity asset path.

Ground_Source.png is the master and is never modified. Two things happen on the
way to the engine, both of which the eye will not catch but the renderer will:

RESIZE TO A POWER OF TWO. The master is 1254x1254. Unity's default npotScale is
ToNearest, so it was silently resampling to 1024 on import - meaning the pixels
shipping in the build were not the pixels in the repository, and nothing said so.
Doing the resize here makes the asset honest and keeps full texture compression.

CLOSE THE TILE. Measured, the master's opposite edges differ by about 2.2x a
normal interior step, so at 3.75 repeats across the arena every join is a faint
seam. The last rows and columns are cross-faded into the opposite edge over a
64px margin, which makes the wrap exact. Mirrored source keeps the grain and the
stone shapes plausible through the fade rather than smearing them.

The destination .meta is left alone so the texture GUID, and every material and
scene reference to it, survives.

    python3 build_ground.py
"""
from pathlib import Path
import numpy as np
from PIL import Image

SIZE = 1024
MARGIN = 64

source = Path(__file__).resolve().parent / "Ground_Source.png"
destination = source.parent.parent.parent / "Assets/_Project/Art/Ground/Ground_Albedo.png"
destination.parent.mkdir(parents=True, exist_ok=True)


def seam_ratio(a):
    """Edge mismatch, measured against a neighbouring interior line."""
    h, w, _ = a.shape
    col = np.abs(a[:, 0] - a[:, -1]).mean() / max(np.abs(a[:, w // 2] - a[:, w // 2 + 1]).mean(), .01)
    row = np.abs(a[0] - a[-1]).mean() / max(np.abs(a[h // 2] - a[h // 2 + 1]).mean(), .01)
    return col, row


def close_edges(a, margin):
    """
    Cross-fade each trailing margin into the mirrored opposite edge, so the last
    line equals the first and the wrap is exact rather than merely close.
    """
    out = a.copy()
    ramp = np.linspace(0, 1, margin)

    tail = out[:, -margin:]
    head = out[:, :margin][:, ::-1]
    out[:, -margin:] = tail * (1 - ramp)[None, :, None] + head * ramp[None, :, None]

    tail = out[-margin:, :]
    head = out[:margin, :][::-1, :]
    out[-margin:, :] = tail * (1 - ramp)[:, None, None] + head * ramp[:, None, None]

    return out


image = Image.open(source).convert("RGB")
print(f"master {image.width}x{image.height}")
before = seam_ratio(np.asarray(image, dtype=float))
print(f"  seam before: horizontal {before[0]:.1f}x  vertical {before[1]:.1f}x")

resized = np.asarray(image.resize((SIZE, SIZE), Image.LANCZOS), dtype=float)
closed = close_edges(resized, MARGIN)
after = seam_ratio(closed)
print(f"  seam after:  horizontal {after[0]:.1f}x  vertical {after[1]:.1f}x")

Image.fromarray(np.clip(closed, 0, 255).astype(np.uint8)).save(destination)
lum = 0.2126 * closed[..., 0] + 0.7152 * closed[..., 1] + 0.0722 * closed[..., 2]
print(f"Restored {destination} at {SIZE}x{SIZE}")
print(f"  luminance mean={lum.mean():.1f} spread={np.percentile(lum,98)-np.percentile(lum,2):.1f} "
      f"(characters render at ~56, so bodies stay dark against it)")
