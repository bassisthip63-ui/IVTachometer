# IV Tachometer art: a rev-counter arc that wraps the lower left of the radar, outside the SpeedoMeter mod's ring.
# Canvas = 2.6 x the speedometer ring radius, centred on the radar. Original art in the style of bassisthip63-ui's draft.
import math, sys
from PIL import Image, ImageDraw, ImageFilter
out = sys.argv[1]
C = 1024; SS = 4; K = C / 2.6          # pixels per ring radius
cx = cy = C / 2
A0, A1 = 258.0, 186.0                  # 0 rpm near the bottom, max near the left (clockwise, like the speedometer)
RIM_OUT, RIM_IN = 1.240, 1.228         # rim, in ring radii
TICK_IN = 1.075                        # ticks run from the rim inward to here
N = 9; RED_FROM = 6.5 / 8.0
def ang(f): return math.radians(A0 + (A1 - A0) * f)
def P(a, r): return ((cx + math.cos(a) * r * K) * SS, (cy - math.sin(a) * r * K) * SS)
def red_at(t):
    a = (255, 92, 40); b = (226, 22, 22); t = max(0., min(1., t)); return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
WHITE = (244, 248, 255)
def rim(d, f0, f1, col, alpha):
    steps = max(2, int(abs(f1 - f0) * 600))
    for i in range(steps):
        fa = f0 + (f1 - f0) * i / steps; fb = f0 + (f1 - f0) * (i + 1.05) / steps; fm = (fa + fb) / 2
        d.polygon([P(ang(fa), RIM_OUT), P(ang(fb), RIM_OUT), P(ang(fb), RIM_IN), P(ang(fa), RIM_IN)], fill=col(fm) + (alpha(fm),))
def tick(d, f, long, colour):
    a = ang(f); half = math.radians(0.50 if long else 0.38)
    rin = TICK_IN if long else TICK_IN + (RIM_IN - TICK_IN) * 0.35
    d.polygon([P(a - half, RIM_IN + 0.01), P(a + half, RIM_IN + 0.01), P(a + half * 0.85, rin), P(a - half * 0.85, rin)], fill=colour + (255,))
def finish(img):
    img = img.resize((C, C), Image.LANCZOS)
    a = img.split()[3].filter(ImageFilter.GaussianBlur(3)).point(lambda v: int(v * 0.35))
    sh = Image.new('RGBA', img.size, (10, 14, 22, 0)); sh.putalpha(a)
    base = Image.new('RGBA', img.size, (0, 0, 0, 0)); base.alpha_composite(sh, (2, 3)); base.alpha_composite(img); return base
fade = 0.07
img = Image.new('RGBA', (C * SS, C * SS), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
rim(d, 0.0, RED_FROM, lambda f: WHITE, lambda f: 255 if f < RED_FROM - fade else int(255 * (RED_FROM - f) / fade))
for k in range(N):
    f = k / (N - 1)
    if f < RED_FROM: tick(d, f, k % 2 == 0, WHITE)
finish(img).save(out + '/tach_arc.png')
img = Image.new('RGBA', (C * SS, C * SS), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
rim(d, RED_FROM - fade, 1.0, lambda f: red_at((f - RED_FROM) / (1 - RED_FROM)), lambda f: 255 if f >= RED_FROM else int(255 * (f - (RED_FROM - fade)) / fade))
for k in range(N):
    f = k / (N - 1)
    if f >= RED_FROM: tick(d, f, k % 2 == 0, red_at((f - RED_FROM) / (1 - RED_FROM)))
finish(img).save(out + '/tach_arc_red.png')
# needle: points up (outward); its inner end sits at the bottom of the image
NW, NH = 64, 200
img = Image.new('RGBA', (NW * SS, NH * SS), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
m = NW / 2; hw = 20; base = NH - hw - 2          # the inner end is a rounded cap, centred on (m, base)
d.polygon([(m * SS, 4 * SS), ((m + hw) * SS, base * SS), ((m - hw) * SS, base * SS)], fill=(232, 30, 22, 255))
d.ellipse([(m - hw) * SS, (base - hw) * SS, (m + hw) * SS, (base + hw) * SS], fill=(232, 30, 22, 255))
# a slightly brighter red down one side for a little depth (still all red)
d.polygon([(m * SS, 10 * SS), (m * SS, base * SS), ((m - hw * 0.75) * SS, base * SS)], fill=(250, 60, 44, 255))
img = img.resize((NW, NH), Image.LANCZOS)
a = img.split()[3].filter(ImageFilter.GaussianBlur(2.5)).point(lambda v: int(v * 0.5))
sh = Image.new('RGBA', img.size, (10, 14, 22, 0)); sh.putalpha(a)
b = Image.new('RGBA', img.size, (0, 0, 0, 0)); b.alpha_composite(sh, (1, 2)); b.alpha_composite(img); b.save(out + '/tach_arc_needle.png')
print('ok')
