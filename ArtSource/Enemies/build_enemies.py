"""
The infected roster: eleven archetypes and the boss, on one shared skeleton.

Run with:
    blender --background --python build_enemies.py
    blender --background --python build_enemies.py -- Screamer Bloater   (subset)

Every archetype is the same bone layout wearing a different body, because the
animation clips are shared - see enemy_kit.py for why that is not negotiable.
What separates them is mass and attachments across the shoulders and back,
which is the part a 61-degree camera actually shows.
"""
import bpy, sys, os, json
from math import sin, cos, radians
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from enemy_kit import *
import enemy_kit

BASE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(BASE, '../../Assets/_Project/Art/Characters'))


def palette(skin_rgb, cloth_rgb, trim_rgb):
    return {
        'skin':  mat(f'Skin {skin_rgb}', skin_rgb),
        'cloth': mat(f'Cloth {cloth_rgb}', cloth_rgb),
        'trim':  mat(f'Trim {trim_rgb}', trim_rgb),
        'bone':  mat('Bone', (.72, .69, .58)),
        'metal': mat('Metal', (.30, .31, .33), .55),
        'mouth': mat('Mouth', (.045, .035, .03)),
        'wound': mat('Wound', (.25, .085, .065)),
        'sick':  mat('Sick bile', (.44, .49, .22)),
    }


def body(p, girth=1.0, limb=1.0, head=1.0, shoulder=1.0, ragged=True, legs=True):
    """The common infected frame. Every archetype starts here and then deviates."""
    g, l = girth, limb
    tube('Hips', [(0, 0, .89), (0, 0, 1.04), (0, -.01, 1.11)],
         [(.22*g, .14*g), (.24*g, .15*g), (.20*g, .13*g)], p['cloth'], 'Pelvis', n=12)
    tube('Torso', [(0, 0, 1.04), (0, -.04, 1.18), (0, -.09, 1.38), (0, -.13, 1.52), (0, -.15, 1.58)],
         [(.20*g, .14*g), (.205*g, .14*g), (.28*g, .17*g), (.32*g, .17*g), (.23*g, .12*g)],
         p['cloth'], 'Spine', 'Chest', ragged=ragged, n=14)
    ell('Shoulder mass', (0, .005, 1.48), (.29*shoulder, .155*shoulder, .145*shoulder),
        p['cloth'], 'Chest')
    box('Belt', (0, -.012, 1.02), (.45*g, .31*g, .055), p['trim'], 'Pelvis', .035)
    for _sg in (-1, 1):
        o = box('Collar', (_sg*.105, -.277*g, 1.55), (.12, .035, .15), p['trim'], 'Chest', .012)
        o.rotation_euler[1] = _sg * -.43
    box('Placket', (0, -.225*g, 1.26), (.05, .028, .32), p['trim'], 'Spine', .01)
    ell('Sternum', (0, -.250*g, 1.50), (.105, .045, .14), p['skin'], 'Chest')
    tube('Neck', [(0, -.16, 1.53), (0, -.21, 1.68), (0, -.24, 1.77)],
         [.12, .11, .115], p['skin'], 'Neck', n=10)
    ell('Cranium', (0, -.235, 1.90), (.145*head, .165*head, .175*head), p['skin'], 'Head', 12, 8)
    ell('Jaw', (0, -.30, 1.83), (.105*head, .10*head, .075*head), p['skin'], 'Head', 10, 6)
    for sg in (-1, 1):
        ell('Eye socket', (sg*.065, -.355*head, 1.915), (.030, .022, .034), p['mouth'], 'Head', 8, 6)
        # Legs
        if legs: tube('Leg', [(sg*.14, 0, 1.0), (sg*.16, -.01, .84), (sg*.17, -.035, .59),
                     (sg*.175, -.02, .48), (sg*.18, .01, .22)],
             [(.125*l, .135*l), (.13*l, .13*l), (.105*l, .10*l), (.09*l, .095*l), (.083*l, .085*l)],
             p['cloth'], f'Thigh.{"L" if sg > 0 else "R"}',
                         f'Shin.{"L" if sg > 0 else "R"}', ragged=ragged, n=10)
        if legs:
            box('Boot', (sg*.18, -.145, .075), (.125*l, .27*l, .10*l), p['trim'],
                f'Foot.{"L" if sg > 0 else "R"}', .02)
        # Arms. Radii match the zombie's sleeve rather than the skin beneath it - the
        # sleeve is what the camera sees, and building to the skin left the roster looking
        # like a set of wire armatures next to the baseline.
        upper = f'UpperArm.{"L" if sg > 0 else "R"}'
        fore = f'Forearm.{"L" if sg > 0 else "R"}'
        hand = f'Hand.{"L" if sg > 0 else "R"}'
        tube('Upper arm', [(sg*.29, -.12, 1.51), (sg*.38, -.145, 1.36), (sg*.455, -.165, 1.22)],
             [.138*l, .130*l, .114*l], p['cloth'], upper, n=10)
        ell('Elbow', (sg*.465, -.16, 1.20), (.086*l, .089*l, .082*l), p['skin'], fore)
        tube('Forearm', [(sg*.46, -.17, 1.19), (sg*.49, -.24, 1.06), (sg*.51, -.30, .95)],
             [.100*l, .094*l, .082*l], p['skin'], fore, n=10)
        ell('Palm', (sg*.515, -.33, .876), (.091*l, .062*l, .105*l), p['skin'], hand)
        for i in range(3):
            x = sg * (.462 + i*.048)
            tube('Finger', [(x, -.350, .822), (x, -.398, .775), (x, -.432, .800)],
                 [.023*l, .020*l, .015*l], p['skin'], hand, n=6)


# ---------------------------------------------------------------- archetypes

def Runner(p):
    """Lean and stripped down. Reads as a body with nothing left on it."""
    body(p, girth=.86, limb=.88, head=.95, shoulder=.88)
    for sg in (-1, 1):
        for i in range(4):   # exposed ribs, the only thing breaking up a thin torso
            ell('Rib', (sg*.10, -.20 + .012*i, 1.42 - i*.055), (.075, .045, .016), p['bone'], 'Chest', 8, 4)
    ell('Sunken belly', (0, -.145, 1.17), (.135, .075, .13), p['skin'], 'Spine')


def Brute(p):
    """All mass across the shoulders, head sunk between them. Widest plan view."""
    body(p, girth=1.35, limb=1.45, head=.85, shoulder=1.75, ragged=False)
    ell('Back slab', (0, .10, 1.46), (.40, .21, .24), p['skin'], 'Chest')
    for sg in (-1, 1):
        ell('Trapezius', (sg*.24, -.02, 1.60), (.19, .17, .13), p['skin'], 'Chest')
        ell('Deltoid', (sg*.36, -.09, 1.48), (.15, .15, .155), p['skin'], f'UpperArm.{"L" if sg > 0 else "R"}')
        ell('Knuckle', (sg*.545, -.345, .80), (.075, .08, .06), p['bone'], f'Hand.{"L" if sg > 0 else "R"}', 8, 6)


def Boss(p):
    """The brute plus a crown of bone. The only archetype that breaks the skyline."""
    Brute(p)
    for i in range(5):       # back spines - visible from directly above, unlike height
        t = i / 4
        ell('Dorsal spine', (0, .12 - t*.02, 1.62 + t*.16), (.045, .055, .09 + t*.05), p['bone'], 'Chest', 8, 5)
    for sg in (-1, 1):
        for i in range(3):
            o = ell('Crown horn', (sg*(.09 + i*.045), -.19 + i*.03, 2.02 + i*.012),
                    (.028, .030, .085 - i*.012), p['bone'], 'Head', 8, 5)
            o.rotation_euler[1] = sg * (.25 + i*.22)
    ell('Jaw tusk L', (.075, -.36, 1.80), (.028, .032, .065), p['bone'], 'Head', 8, 5)
    ell('Jaw tusk R', (-.075, -.36, 1.80), (.028, .032, .065), p['bone'], 'Head', 8, 5)


def Bloater(p):
    """A sphere with limbs. Silhouette is the whole counterplay: do not stand near it."""
    body(p, girth=.95, limb=1.05, head=.82, shoulder=.95, ragged=False)
    ell('Distended abdomen', (0, -.09, 1.16), (.40, .38, .33), p['skin'], 'Spine', 14, 10)
    ell('Gas sac', (0, -.20, 1.24), (.25, .20, .19), p['sick'], 'Spine', 12, 8)
    for i in range(7):       # split seams showing the pressure inside
        a = radians(i * 51.4)
        ell('Split seam', (cos(a)*.30, -.20 + sin(a)*.09, 1.16 + sin(a)*.28),
            (.055, .035, .045), p['wound'], 'Spine', 8, 5)


def Armored(p):
    """A flat plate where the chest should be. Hard edges against a roster of soft ones."""
    body(p, girth=1.10, limb=1.14, head=.90, shoulder=1.20, ragged=False)
    box('Chest plate', (0, -.245, 1.34), (.52, .085, .46), p['metal'], 'Chest', .03)
    box('Plate ridge', (0, -.295, 1.34), (.10, .05, .46), p['metal'], 'Chest', .02)
    box('Belly plate', (0, -.215, 1.06), (.42, .075, .20), p['metal'], 'Spine', .025)
    for sg in (-1, 1):
        box('Pauldron', (sg*.325, -.10, 1.55), (.24, .30, .14), p['metal'], 'Chest', .03)
    box('Helmet', (0, -.245, 1.94), (.32, .34, .26), p['metal'], 'Head', .035)
    box('Visor slit', (0, -.415, 1.92), (.24, .04, .045), p['mouth'], 'Head', .008)


def Sapper(p):
    """Carries the charge on its back, where the camera can see it. Goes for walls."""
    body(p, girth=1.0, limb=1.0, head=.86, shoulder=1.05)
    box('Satchel', (0, .175, 1.30), (.34, .21, .34), p['trim'], 'Spine', .03)
    for i in range(3):
        ell('Charge', (-.10 + i*.10, .265, 1.30), (.045, .05, .13), p['metal'], 'Spine', 8, 6)
    ell('Blasting cap', (0, .27, 1.49), (.05, .05, .055), p['wound'], 'Spine', 8, 6)
    for sg in (-1, 1):
        box('Shoulder strap', (sg*.145, -.12, 1.44), (.075, .30, .24), p['trim'], 'Chest', .012)
    box('Head wrap', (0, -.235, 1.93), (.32, .33, .20), p['trim'], 'Head', .03)


def Leaper(p):
    """Coiled haunches, long reach. Wide low stance rather than a tall one."""
    body(p, girth=.94, limb=1.0, head=.88, shoulder=1.0)
    for sg in (-1, 1):
        ell('Haunch', (sg*.185, -.02, .92), (.155, .21, .225), p['skin'], f'Thigh.{"L" if sg > 0 else "R"}')
        ell('Calf', (sg*.175, -.03, .46), (.095, .12, .15), p['skin'], f'Shin.{"L" if sg > 0 else "R"}')
        box('Splayed foot', (sg*.185, -.20, .06), (.155, .34, .075), p['skin'],
            f'Foot.{"L" if sg > 0 else "R"}', .02)
        for i in range(3):
            ell('Claw', (sg*(.13 + i*.055), -.35, .045), (.022, .06, .022), p['bone'],
                f'Foot.{"L" if sg > 0 else "R"}', 6, 4)


def Screamer(p):
    """A throat built to be seen. The one archetype you are asked to pick out of a crowd."""
    body(p, girth=.93, limb=.95, head=1.0, shoulder=.95)
    ell('Distended throat', (0, -.255, 1.66), (.175, .175, .20), p['sick'], 'Neck', 12, 8)
    ell('Throat membrane', (0, -.315, 1.63), (.115, .10, .13), p['wound'], 'Neck', 10, 6)
    ell('Unhinged jaw', (0, -.315, 1.775), (.125, .135, .115), p['mouth'], 'Head', 10, 8)
    for i in range(6):
        a = radians(30 + i * 24)
        ell('Tooth', (cos(a)*.10, -.365, 1.775 + sin(a)*.085), (.016, .022, .026), p['bone'], 'Head', 6, 4)
    for sg in (-1, 1):
        ell('Strained cord', (sg*.085, -.275, 1.55), (.028, .045, .085), p['wound'], 'Neck', 8, 5)


def Revenant(p):
    """Skeletal and half-wrapped. Has to look like something that could get back up."""
    body(p, girth=.88, limb=.86, head=.92, shoulder=.92)
    for sg in (-1, 1):
        for i in range(5):
            ell('Bare rib', (sg*.11, -.175 + .015*i, 1.47 - i*.052), (.10, .055, .018), p['bone'], 'Chest', 8, 4)
    ell('Spine column', (0, .045, 1.24), (.045, .05, .22), p['bone'], 'Spine', 8, 6)
    for i in range(6):       # shroud, torn into bands rather than a solid sheet
        tube('Shroud band', [(-.22 + i*.088, .08, 1.44), (-.20 + i*.088, .06, 1.02 + .04*sin(i))],
             [.055, .022], p['trim'], 'Spine', n=6)
    for sg in (-1, 1):
        ell('Ember socket', (sg*.065, -.365, 1.915), (.032, .022, .036), p['sick'], 'Head', 8, 6)


def Crawler(p):
    """Nothing below the knee. Small, low, and arrives in numbers."""
    body(p, girth=.96, limb=.86, head=.92, shoulder=1.0, ragged=True, legs=False)
    # legs=False, because it has none. An earlier pass added the stumps on top of a full
    # pair of legs and the crawler stood upright like everything else on the sheet.
    for sg in (-1, 1):
        tube('Stump', [(sg*.15, -.02, 1.00), (sg*.165, -.05, .78), (sg*.17, -.07, .66)],
             [.11, .085, .055], p['wound'], f'Thigh.{"L" if sg > 0 else "R"}', n=8)
        ell('Stump cap', (sg*.17, -.075, .645), (.06, .06, .035), p['bone'],
            f'Thigh.{"L" if sg > 0 else "R"}', 8, 6)
        # It pulls itself along, so the forearms are the heaviest thing on it.
        tube('Dragging forearm', [(sg*.46, -.17, 1.20), (sg*.50, -.26, 1.05), (sg*.52, -.32, .93)],
             [.10, .095, .085], p['skin'], f'Forearm.{"L" if sg > 0 else "R"}', n=10)
        for i in range(3):
            ell('Finger', (sg*(.47 + i*.045), -.40, .82), (.020, .055, .020), p['bone'],
                f'Hand.{"L" if sg > 0 else "R"}', 6, 4)


def Spitter(p):
    """The gland rides high on the back, which is exactly what a 61-degree camera sees."""
    body(p, girth=.98, limb=1.0, head=.88, shoulder=1.05)
    ell('Bile gland', (0, .155, 1.47), (.28, .22, .26), p['sick'], 'Chest', 12, 8)
    ell('Gland membrane', (0, .215, 1.52), (.17, .13, .15), p['wound'], 'Chest', 10, 6)
    for sg in (-1, 1):
        tube('Feed duct', [(sg*.10, .12, 1.42), (sg*.13, -.05, 1.60), (sg*.09, -.22, 1.68)],
             [.035, .030, .024], p['sick'], 'Chest', 'Neck', n=6)
    ell('Dripping maw', (0, -.325, 1.80), (.105, .11, .085), p['sick'], 'Head', 10, 6)


def RangedZombie(p):
    """Asymmetric: one arm built to throw, and a bag of what it throws."""
    body(p, girth=1.0, limb=1.02, head=.90, shoulder=1.08)
    ell('Throwing shoulder', (.315, -.06, 1.53), (.175, .165, .165), p['skin'], 'UpperArm.L')
    tube('Overgrown forearm', [(.46, -.17, 1.20), (.50, -.25, 1.05), (.52, -.31, .94)],
         [.115, .105, .092], p['skin'], 'Forearm.L', n=10)
    ell('Throwing fist', (.525, -.345, .845), (.095, .105, .105), p['skin'], 'Hand.L', 8, 6)
    box('Debris satchel', (-.20, .10, 1.16), (.26, .18, .26), p['trim'], 'Spine', .025)
    for i in range(4):
        a = radians(i * 90)
        ell('Rubble', (-.20 + cos(a)*.085, .165, 1.22 + sin(a)*.075), (.045, .04, .045),
            p['metal'], 'Spine', 6, 4)


ARCHETYPES = {
    # Twelve bodies that have to be told apart at eighty pixels, so hue does as much work as
    # shape. Two rules, both learned from rendering the first three against the baseline:
    #
    #   Skin stays GREEN-DOMINANT (g clearly above r and b). The first pass used
    #   near-neutral values and the brute came out looking like a beige statue - the roster
    #   stopped reading as one infection the moment green stopped leading.
    #
    #   Skin stays DARK. These archetypes expose far more skin than the baseline zombie,
    #   which is mostly shirt, so values that look right on a head and two hands blow out
    #   when they cover a whole back.
    #
    # Cloth carries the identity: each one a different corner of the wheel.
    'Runner':       (Runner,       ((0.144, 0.202, 0.151), (0.223, 0.086, 0.065), (0.115, 0.043, 0.036))),
    'Brute':        (Brute,        ((0.151, 0.194, 0.144), (0.180, 0.144, 0.058), (0.094, 0.072, 0.029))),
    'Boss':         (Boss,         ((0.173, 0.187, 0.137), (0.194, 0.043, 0.058), (0.101, 0.022, 0.029))),
    'Bloater':      (Bloater,      ((0.187, 0.223, 0.122), (0.245, 0.216, 0.065), (0.130, 0.108, 0.036))),
    'Armored':      (Armored,      ((0.137, 0.187, 0.144), (0.079, 0.115, 0.173), (0.043, 0.058, 0.094))),
    'Sapper':       (Sapper,       ((0.151, 0.202, 0.151), (0.259, 0.194, 0.086), (0.137, 0.101, 0.043))),
    'Leaper':       (Leaper,       ((0.130, 0.202, 0.166), (0.043, 0.151, 0.144), (0.022, 0.079, 0.072))),
    'Screamer':     (Screamer,     ((0.166, 0.223, 0.166), (0.065, 0.065, 0.173), (0.036, 0.036, 0.094))),
    'Revenant':     (Revenant,     ((0.137, 0.158, 0.137), (0.209, 0.202, 0.180), (0.108, 0.108, 0.094))),
    'Crawler':      (Crawler,      ((0.144, 0.202, 0.151), (0.144, 0.094, 0.058), (0.072, 0.050, 0.029))),
    'Spitter':      (Spitter,      ((0.158, 0.209, 0.130), (0.108, 0.166, 0.065), (0.058, 0.086, 0.029))),
    'RangedZombie': (RangedZombie, ((0.151, 0.194, 0.144), (0.245, 0.122, 0.050), (0.130, 0.065, 0.022))),
}


wanted = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(ARCHETYPES)
reports = []
for name in wanted:
    if name not in ARCHETYPES:
        print(f'SKIP unknown archetype {name}'); continue
    fn, colours = ARCHETYPES[name]
    reset()
    fn(palette(*colours))
    reports.append(export(name, OUT))
    print(f'BUILT {name}: {reports[-1]}')

path = os.path.join(BASE, 'roster_report.json')
existing = {}
if os.path.exists(path):
    existing = {r['name']: r for r in json.load(open(path)).get('archetypes', [])}
for r in reports: existing[r['name']] = r
with open(path, 'w') as f:
    json.dump({'archetypes': [existing[k] for k in sorted(existing)]}, f, indent=1)
print('ROSTER_REPORT_WRITTEN', path)
