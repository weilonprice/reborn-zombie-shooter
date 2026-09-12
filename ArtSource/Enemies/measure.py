"""Luminance of each archetype against the baseline zombie, masked by real alpha."""
import sys, statistics, os
from PIL import Image
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'proof')
names = sys.argv[1:]
rows = []
for n in names:
    im = Image.open(os.path.join(BASE, n + '.png')).convert('RGBA')
    px = [p for p in im.getdata() if p[3] > 160]
    lum = sorted(0.2126*r + 0.7152*g + 0.0722*b for r, g, b, _ in px)
    rows.append((n, statistics.mean(lum), lum[len(lum)//10], lum[9*len(lum)//10], len(lum)))
base = rows[0][1]; base_lo = rows[0][2]
print(f"{'':<14}{'mean':>7}{'p10':>7}{'p90':>7}{'px':>8}   {'vs baseline':>22}")
for n, m, lo, hi, c in rows:
    flag = '' if n == names[0] else f"{(m-base)/base:+6.0%} mean {(lo-base_lo)/base_lo:+6.0%} dark"
    print(f"{n:<14}{m:>7.1f}{lo:>7.1f}{hi:>7.1f}{c:>8}   {flag:>22}")
if len(rows) > 1:
    print(f"\nroster mean {statistics.mean(r[1] for r in rows[1:]):.1f} vs baseline {base:.1f}")
