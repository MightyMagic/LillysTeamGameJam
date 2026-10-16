"""Procedural pixel-art generator for MOBILE GEAR SOLID prototype.

Writes PNG sprites into Assets/Resources/Sprites. Run from anywhere:
    python Tools/gen_art.py
Everything is drawn at 32 px per tile (PPU 32 in Unity).
"""
import os
import random
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Sprites")
os.makedirs(OUT, exist_ok=True)

T = 32
rng = random.Random(1987)


# ---------------------------------------------------------------- helpers
class C:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            if len(c) == 3:
                c = (c[0], c[1], c[2], 255)
            self.px[x, y] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return (0, 0, 0, 0)

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.set(xx, yy, c)

    def hline(self, x, y, w, c):
        self.rect(x, y, w, 1, c)

    def vline(self, x, y, h, c):
        self.rect(x, y, 1, h, c)

    def noise(self, x, y, w, h, base, amount=6, chance=1.0, seed=None):
        r = random.Random(seed) if seed is not None else rng
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                if r.random() < chance:
                    d = r.randint(-amount, amount)
                    self.set(xx, yy, clamp_col(base, d))

    def outline(self, c=(12, 14, 16)):
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] == 0:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] > 200:
                            self.set(x, y, c)
                            break

    def flip(self):
        self.img = self.img.transpose(Image.FLIP_LEFT_RIGHT)
        self.px = self.img.load()

    def save(self, name):
        self.img.save(os.path.join(OUT, name + ".png"))


def clamp_col(c, d):
    return tuple(max(0, min(255, v + d)) for v in c[:3])


def shade(c, d):
    return clamp_col(c, d)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


# ---------------------------------------------------------------- palette
FLOOR = (64, 68, 62)
FLOOR_D = (50, 54, 50)
GRIME = (42, 44, 38)
YEL = (156, 128, 52)
WALL_TOP = (20, 23, 26)
WALL_TOP_E = (38, 43, 47)
WALL_FACE = (56, 63, 64)
PIPE = (34, 38, 41)
PIPE_H = (78, 86, 88)
WOOD = (122, 92, 58)
WOOD_D = (84, 62, 40)
WOOD_L = (156, 120, 78)
CARD = (146, 116, 76)
METAL = (64, 70, 76)
METAL_D = (40, 44, 50)
METAL_L = (96, 104, 110)
OUTL = (12, 14, 16)


# ---------------------------------------------------------------- tiles
def floor_tile(name, seed, variant=0):
    c = C(T, T)
    r = random.Random(seed)
    c.noise(0, 0, T, T, FLOOR, 4, seed=seed)
    # concrete slab seams
    for i in range(T):
        c.set(i, 0, shade(FLOOR_D, -4))
        c.set(0, i, shade(FLOOR_D, -4))
        c.set(i, 16, shade(FLOOR, -6)) if variant == 1 else None
    # grime blobs
    for _ in range(r.randint(1, 3)):
        bx, by, rad = r.randint(2, 29), r.randint(2, 29), r.randint(2, 5)
        for y in range(by - rad, by + rad):
            for x in range(bx - rad, bx + rad):
                if (x - bx) ** 2 + (y - by) ** 2 < rad * rad and r.random() < 0.55:
                    c.set(x % T, y % T, clamp_col(GRIME, r.randint(-3, 3)))
    if variant == 2:  # crack
        x, y = r.randint(6, 24), 4
        while y < 28:
            c.set(x, y, (36, 38, 34))
            x += r.choice((-1, 0, 1))
            y += 1
    if variant == 3:  # worn yellow paint
        for y in range(T):
            for x in range(3, 7):
                if r.random() < 0.7:
                    c.set(x, y, clamp_col(YEL, r.randint(-14, 8)))
    if variant == 4:  # paint splotches
        for _ in range(14):
            x, y = r.randint(0, 31), r.randint(0, 31)
            c.rect(x, y, r.randint(1, 3), r.randint(1, 2), clamp_col(YEL, r.randint(-20, 0)))
    c.save(name)


def grate_tile():
    c = C(T, T)
    c.noise(0, 0, T, T, FLOOR, 4, seed=55)
    c.rect(8, 10, 16, 12, (30, 32, 32))
    for x in range(9, 23, 2):
        c.vline(x, 11, 10, (82, 88, 88))
    c.hline(8, 10, 16, (90, 96, 96))
    c.hline(8, 21, 16, (20, 22, 22))
    c.save("tile_grate")


def hazard_tile():
    c = C(T, T)
    c.noise(0, 0, T, T, FLOOR, 4, seed=77)
    for y in range(T):
        for x in range(T):
            if 12 <= y < 20:
                stripe = ((x + y) // 4) % 2 == 0
                col = clamp_col(YEL, rng.randint(-14, 6)) if stripe else (30, 30, 28)
                if rng.random() < 0.85:
                    c.set(x, y, col)
    c.save("tile_hazard")


def door_tile():
    c = C(T, T)
    c.noise(0, 0, T, T, shade(FLOOR, -6), 3, seed=88)
    c.rect(0, 0, T, 4, (40, 44, 46))
    c.rect(0, 28, T, 4, (40, 44, 46))
    for x in range(0, T, 4):
        c.rect(x, 1, 2, 2, YEL)
        c.rect(x + 2, 29, 2, 2, YEL)
    c.save("tile_door")


def wall_top():
    c = C(T, T)
    c.noise(0, 0, T, T, WALL_TOP, 2, seed=5)
    c.save("tile_wall_top")


def wall_face(name, kind, seed):
    c = C(T, T)
    r = random.Random(seed)
    # dark top cap (the wall's thickness seen from above)
    c.noise(0, 0, T, 9, WALL_TOP, 2, seed=seed)
    c.hline(0, 9, T, WALL_TOP_E)
    # face: lit concrete with vertical gradient
    for y in range(10, T):
        t = (y - 10) / 22
        base = mix(shade(WALL_FACE, 10), shade(WALL_FACE, -12), t)
        for x in range(T):
            c.set(x, y, clamp_col(base, r.randint(-3, 3)))
    c.hline(0, 31, T, (30, 34, 36))
    # panel seams
    c.vline(r.choice((7, 15, 23)), 10, 22, shade(WALL_FACE, -16))
    if kind == "pipes":
        for py in (13, 18):
            c.hline(0, py, T, PIPE)
            c.hline(0, py + 1, T, PIPE)
            c.hline(0, py - 1, T, PIPE_H)
            c.set(r.randint(4, 28), py, PIPE_H)
        c.rect(10, 12, 3, 9, PIPE)
    elif kind == "vpipe":
        x = r.randint(6, 22)
        c.rect(x, 10, 4, 22, PIPE)
        c.vline(x, 10, 22, PIPE_H)
        c.rect(x - 1, 20, 6, 2, (26, 28, 30))
    elif kind == "box":
        c.rect(9, 13, 14, 12, (44, 50, 52))
        c.rect(10, 14, 12, 10, (70, 78, 80))
        c.rect(14, 16, 4, 4, YEL)
        c.set(15, 17, OUTL)
        c.set(16, 18, OUTL)
        c.vline(16, 25, 7, PIPE)
    elif kind == "sign":
        c.rect(8, 14, 16, 8, (150, 120, 40))
        c.rect(9, 15, 14, 6, (190, 156, 60))
        for x in range(10, 22, 3):
            c.rect(x, 16, 2, 4, (40, 34, 20))
    c.save(name)


def vent_face():
    c = C(T, T)
    c.noise(0, 0, T, 9, WALL_TOP, 2, seed=9)
    c.hline(0, 9, T, WALL_TOP_E)
    for y in range(10, T):
        base = mix(shade(WALL_FACE, 10), shade(WALL_FACE, -12), (y - 10) / 22)
        c.hline(0, y, T, base)
    c.rect(6, 16, 20, 14, (70, 76, 78))
    c.rect(8, 18, 16, 12, (8, 8, 10))
    c.rect(9, 19, 14, 3, (20, 20, 22))
    c.save("tile_vent")


def exit_face():
    c = C(T, T)
    c.noise(0, 0, T, 9, WALL_TOP, 2, seed=19)
    c.hline(0, 9, T, WALL_TOP_E)
    for y in range(10, T):
        base = mix(shade(WALL_FACE, 10), shade(WALL_FACE, -12), (y - 10) / 22)
        c.hline(0, y, T, base)
    c.rect(4, 11, 24, 21, (34, 40, 40))
    c.rect(6, 13, 20, 19, (78, 92, 86))
    c.rect(6, 13, 9, 19, (70, 84, 78))
    c.vline(15, 13, 19, (40, 48, 46))
    c.rect(10, 20, 2, 4, (30, 34, 34))
    c.rect(19, 20, 2, 4, (30, 34, 34))
    c.rect(11, 10, 10, 3, (60, 220, 110))  # exit light
    c.save("tile_exit")


def locked_door(name, light):
    c = C(T, T)
    c.rect(0, 0, T, T, (44, 48, 52))
    c.rect(2, 2, 28, 28, (88, 96, 100))
    for y in range(4, 28, 4):
        c.hline(3, y, 26, (70, 78, 82))
    c.vline(15, 2, 28, (40, 44, 48))
    c.vline(16, 2, 28, (110, 118, 122))
    for x in range(2, 30, 6):
        c.rect(x, 2, 3, 2, YEL)
        c.rect(x + 3, 28, 3, 2, YEL)
    c.rect(12, 13, 8, 6, (24, 26, 28))
    c.rect(14, 15, 4, 2, light)
    c.save(name)


# ---------------------------------------------------------------- props
def crate():
    c = C(T, 40)
    # top face
    c.rect(2, 4, 28, 10, WOOD_L)
    c.noise(3, 5, 26, 8, WOOD_L, 6)
    for y in (7, 10):
        c.hline(3, y, 26, shade(WOOD_L, -18))
    # front face
    c.rect(2, 14, 28, 24, WOOD)
    c.noise(4, 16, 24, 20, WOOD, 6)
    c.rect(2, 14, 28, 3, WOOD_D)
    c.rect(2, 35, 28, 3, WOOD_D)
    c.rect(2, 14, 3, 24, WOOD_D)
    c.rect(27, 14, 3, 24, WOOD_D)
    for i in range(20):  # diagonal brace
        x = 5 + int(i * 22 / 20)
        y = 17 + int(i * 18 / 20)
        c.rect(x, y, 2, 2, WOOD_D)
    c.hline(2, 14, 28, shade(WOOD_L, 10))
    c.outline()
    c.save("prop_crate")


def box():
    c = C(T, T)
    c.rect(6, 8, 20, 7, shade(CARD, 18))
    c.vline(16, 8, 7, shade(CARD, -20))
    c.rect(6, 15, 20, 15, CARD)
    c.noise(7, 16, 18, 13, CARD, 5)
    c.rect(13, 15, 6, 5, (210, 200, 170))
    c.hline(6, 15, 20, shade(CARD, -28))
    c.outline()
    c.save("prop_box")


def barrel(name, col):
    c = C(T, 40)
    d, l = shade(col, -30), shade(col, 25)
    c.rect(6, 4, 20, 8, shade(col, 12))
    c.rect(8, 5, 16, 6, d)
    c.rect(14, 7, 4, 2, shade(col, -50))
    for y in range(12, 38):
        for x in range(6, 26):
            t = abs(x - 13) / 13
            c.set(x, y, clamp_col(mix(l, d, t), rng.randint(-4, 4)))
    c.hline(6, 18, 20, d)
    c.hline(6, 30, 20, d)
    c.rect(6, 11, 20, 1, shade(col, 35))
    if "red" in name:  # rust
        for _ in range(12):
            c.set(rng.randint(7, 24), rng.randint(13, 36), (150, 80, 40))
    c.outline()
    c.save(name)


def shelf():
    c = C(T, 52)
    c.rect(2, 4, 28, 46, (24, 26, 30))
    c.rect(2, 4, 2, 46, METAL_L)
    c.rect(28, 4, 2, 46, METAL)
    for y in (4, 18, 33, 47):
        c.rect(2, y, 28, 3, METAL_L)
        c.hline(2, y + 3, 28, METAL_D)
    cols = [(150, 60, 50), (70, 96, 64), (150, 118, 74), (80, 90, 110), (60, 72, 54)]
    for top in (8, 22, 37):
        x = 5
        while x < 26:
            w = rng.randint(4, 8)
            h = rng.randint(5, 9)
            col = rng.choice(cols)
            c.rect(x, top + (10 - h), min(w, 27 - x), h, col)
            c.hline(x, top + (10 - h), min(w, 27 - x), shade(col, 25))
            x += w + 1
    c.outline()
    c.save("prop_shelf")


def server_rack():
    c = C(T, 56)
    c.rect(3, 4, 26, 6, (44, 48, 54))
    c.rect(3, 10, 26, 44, (22, 25, 30))
    c.rect(3, 10, 26, 2, (60, 66, 72))
    c.vline(3, 10, 44, (50, 56, 62))
    for y in range(14, 50, 4):
        c.rect(6, y, 20, 3, (32, 36, 42))
        c.hline(6, y, 20, (44, 50, 56))
        for x in range(8, 24, 3):
            r = rng.random()
            col = (60, 230, 90) if r < 0.3 else (70, 160, 230) if r < 0.45 else (230, 160, 40) if r < 0.5 else None
            if col:
                c.set(x, y + 1, col)
    c.outline()
    c.save("prop_rack")


def locker():
    c = C(T, 52)
    c.rect(3, 4, 26, 6, (70, 84, 72))
    c.rect(3, 10, 26, 40, (58, 72, 60))
    c.vline(16, 10, 40, (36, 44, 38))
    for x in (6, 19):
        for y in range(13, 20, 2):
            c.hline(x, y, 7, (36, 44, 38))
        c.rect(x + 7, 28, 2, 5, (150, 150, 140))
    c.hline(3, 10, 26, (90, 106, 92))
    c.outline()
    c.save("prop_locker")


def weapon_rack():
    c = C(T, 48)
    c.rect(2, 4, 28, 42, (34, 30, 28))
    c.rect(2, 4, 28, 3, (70, 60, 50))
    c.rect(2, 40, 28, 6, (60, 50, 40))
    for x in (6, 13, 20, 26):
        c.rect(x, 9, 2, 30, (24, 24, 26))
        c.rect(x - 1, 22, 4, 6, (90, 54, 34))
        c.rect(x, 8, 2, 2, (80, 84, 88))
    c.outline()
    c.save("prop_weaponrack")


def desk():
    c = C(T, 40)
    c.rect(1, 12, 30, 8, (90, 96, 92))
    c.hline(1, 12, 30, (120, 128, 124))
    c.rect(1, 20, 30, 18, (60, 66, 66))
    c.rect(3, 22, 10, 14, (50, 56, 56))
    for y in (26, 31):
        c.hline(4, y, 8, (36, 40, 40))
    # monitor
    c.rect(14, 2, 14, 11, (40, 44, 46))
    c.rect(15, 3, 12, 8, (30, 70, 60))
    c.rect(16, 4, 6, 1, (90, 200, 160))
    c.rect(16, 6, 9, 1, (90, 200, 160))
    c.rect(5, 14, 7, 4, (210, 196, 150))  # papers
    c.outline()
    c.save("prop_desk")


def terminal(name, on):
    c = C(T, 48)
    c.rect(1, 20, 30, 8, (70, 76, 82))
    c.rect(1, 28, 30, 18, (44, 48, 54))
    c.rect(4, 2, 24, 18, (30, 34, 38))
    c.rect(6, 4, 20, 13, (20, 60, 40) if on else (40, 20, 20))
    scr = (110, 240, 150) if on else (230, 80, 70)
    for y in range(6, 15, 2):
        c.hline(7, y, rng.randint(6, 17), scr)
    c.rect(6, 21, 20, 5, (36, 40, 44))
    for x in range(7, 25, 2):
        c.set(x, 22, (110, 116, 120))
        c.set(x, 24, (110, 116, 120))
    c.rect(24, 32, 4, 3, scr)
    c.outline()
    c.save(name)


def panel(name, on):
    c = C(T, 56)
    c.rect(2, 4, 28, 6, (60, 66, 70))
    c.rect(2, 10, 28, 44, (74, 82, 86))
    c.rect(4, 12, 11, 40, (64, 72, 76))
    c.rect(17, 12, 11, 40, (64, 72, 76))
    c.vline(15, 10, 44, (40, 44, 48))
    c.vline(16, 10, 44, (40, 44, 48))
    c.rect(6, 15, 7, 6, (190, 156, 50))
    c.set(9, 16, OUTL); c.set(9, 17, OUTL); c.set(9, 19, OUTL)
    for i, y in enumerate(range(16, 40, 5)):
        col = (60, 230, 90) if on else (60, 40, 40)
        c.rect(19, y, 3, 2, col if i % 2 == 0 else ((230, 60, 50) if not on else (40, 60, 40)))
        c.rect(23, y, 3, 2, (30, 34, 36))
    c.rect(7, 40, 4, 8, (30, 34, 36))  # lever
    c.rect(6, 40 if on else 46, 6, 2, (200, 60, 50))
    c.outline()
    c.save(name)


def proto_case():
    c = C(T, 40)
    c.rect(4, 22, 24, 16, (54, 58, 64))  # pedestal
    c.hline(4, 22, 24, (90, 96, 102))
    c.rect(6, 8, 20, 14, (40, 76, 52))   # green hard case
    c.rect(6, 8, 20, 3, (70, 116, 80))
    c.rect(13, 5, 6, 3, (30, 34, 34))
    c.rect(9, 13, 3, 4, (200, 200, 190))
    c.rect(20, 13, 3, 4, (200, 200, 190))
    c.rect(14, 14, 4, 2, (240, 210, 70))
    c.outline()
    c.save("prop_prototype")


# ---------------------------------------------------------------- pickups / icons (16x16)
def small(name, fn):
    c = C(16, 16)
    fn(c)
    c.outline()
    c.save(name)


def keycard(c):
    c.rect(2, 4, 12, 8, (230, 230, 220))
    c.rect(2, 4, 12, 3, (200, 50, 50))
    c.rect(4, 8, 3, 3, (90, 90, 90))
    c.hline(8, 9, 5, (120, 120, 120))


def intel(c):
    c.rect(2, 3, 12, 10, (200, 170, 100))
    c.rect(2, 3, 5, 2, (220, 190, 120))
    c.rect(4, 6, 8, 1, (90, 70, 40))
    c.rect(4, 8, 6, 1, (90, 70, 40))
    c.rect(9, 10, 4, 2, (190, 40, 40))


def ammo(c):
    c.rect(2, 5, 12, 9, (70, 84, 56))
    c.rect(2, 5, 12, 2, (100, 116, 80))
    for x in (4, 7, 10):
        c.rect(x, 2, 2, 4, (210, 170, 70))


def medkit(c):
    c.rect(2, 4, 12, 10, (230, 230, 226))
    c.rect(6, 6, 4, 6, (210, 40, 40))
    c.rect(4, 8, 8, 2, (210, 40, 40))
    c.rect(6, 2, 4, 2, (120, 120, 120))


def decoy(c):
    c.rect(4, 5, 8, 9, (60, 66, 70))
    c.rect(5, 6, 6, 4, (30, 34, 36))
    c.rect(6, 7, 1, 1, (230, 60, 50))
    c.rect(7, 1, 1, 4, (150, 150, 150))
    c.rect(4, 11, 8, 1, (90, 96, 100))


def camera_sprite():
    c = C(24, 16)
    c.rect(0, 5, 5, 6, (50, 54, 58))     # mount
    c.rect(4, 3, 14, 10, (200, 204, 200))
    c.rect(4, 3, 14, 2, (230, 232, 230))
    c.rect(4, 11, 14, 2, (150, 154, 150))
    c.rect(18, 4, 4, 8, (40, 44, 48))
    c.rect(19, 6, 3, 4, (230, 40, 40))
    c.outline()
    c.save("prop_camera")


def lamp():
    c = C(T, 8)
    c.rect(1, 2, 30, 4, (60, 64, 66))
    c.rect(3, 3, 26, 2, (236, 244, 220))
    c.save("prop_lamp")


def marks():
    c = C(16, 16)
    c.rect(6, 1, 4, 10, (255, 60, 50))
    c.rect(6, 12, 4, 3, (255, 60, 50))
    c.outline((40, 0, 0))
    c.save("fx_alert")
    c = C(16, 16)
    pts = [(5, 2, 6, 2), (10, 3, 2, 4), (8, 7, 3, 2), (7, 8, 2, 3), (7, 12, 2, 2), (4, 3, 2, 2)]
    for x, y, w, h in pts:
        c.rect(x, y, w, h, (255, 210, 60))
    c.outline((50, 40, 0))
    c.save("fx_suspect")
    c = C(16, 16)  # search
    c.rect(3, 3, 7, 7, (120, 200, 255))
    c.rect(5, 5, 3, 3, (0, 0, 0, 0))
    c.rect(9, 9, 2, 2, (120, 200, 255))
    c.rect(11, 11, 3, 3, (120, 200, 255))
    c.outline((10, 30, 50))
    c.save("fx_search")
    c = C(8, 8)  # interactable marker diamond
    for y in range(8):
        w = 4 - abs(y - 3.5) + 0.5
        for x in range(8):
            if abs(x - 3.5) < w:
                c.set(x, y, (120, 240, 255))
    c.save("fx_marker")
    c = C(8, 8)  # bullet / muzzle
    c.rect(2, 2, 4, 4, (255, 230, 150))
    c.rect(3, 3, 2, 2, (255, 255, 255))
    c.save("fx_flash")
    c = C(4, 4)
    c.rect(0, 0, 4, 4, (255, 255, 255))
    c.save("fx_white")


# ---------------------------------------------------------------- characters
def character(prefix, pal, armed_player):
    skin, hair, body, body_d, legs, boots, accent = (
        pal["skin"], pal["hair"], pal["body"], pal["body_d"], pal["legs"], pal["boots"], pal["accent"])
    for d in ("down", "up", "side"):
        for f in range(4):
            c = C(T, T)
            step = (0, 1, 0, -1)[f]
            bob = 1 if f in (1, 3) else 0
            # legs
            if d == "side":
                c.rect(13 + step * 2, 22, 4, 7, legs)
                c.rect(15 - step * 2, 22, 4, 7, shade(legs, -12))
                c.rect(13 + step * 2, 28, 5, 2, boots)
                c.rect(15 - step * 2, 28, 5, 2, shade(boots, -8))
            else:
                c.rect(12, 22, 4, 7 + (step if step > 0 else 0), legs)
                c.rect(17, 22, 4, 7 + (-step if step < 0 else 0), shade(legs, -10))
                c.rect(12, 28 + max(step, 0), 4, 2, boots)
                c.rect(17, 28 + max(-step, 0), 4, 2, boots)
            oy = -bob
            # torso
            if d == "side":
                c.rect(12, 14 + oy, 9, 9, body)
                c.rect(12, 14 + oy, 9, 2, shade(body, 14))
                c.rect(12, 19 + oy, 9, 2, accent)  # belt
                c.rect(11, 15 + oy, 2, 6, body_d)  # backpack
            else:
                c.rect(11, 14 + oy, 11, 9, body)
                c.rect(11, 14 + oy, 11, 2, shade(body, 14))
                c.rect(11, 20 + oy, 11, 2, accent)
                if d == "down":
                    c.rect(14, 16 + oy, 5, 3, body_d)  # vest pouches
                else:
                    c.rect(13, 15 + oy, 7, 5, body_d)  # backpack
            # arms + gun
            if d == "down":
                c.rect(9, 15 + oy, 2, 7, body_d)
                c.rect(22, 15 + oy, 2, 6, body_d)
                c.rect(9, 21 + oy, 2, 2, skin)
                c.rect(21, 20 + oy, 3, 2, skin)
                c.rect(21, 21 + oy, 2, 5, (30, 30, 34))  # pistol pointing down
            elif d == "up":
                c.rect(9, 15 + oy, 2, 7, body_d)
                c.rect(22, 15 + oy, 2, 7, body_d)
                c.rect(9, 21 + oy, 2, 2, skin)
                c.rect(22, 21 + oy, 2, 2, skin)
            else:
                c.rect(16, 16 + oy, 6, 3, body_d)
                c.rect(21, 16 + oy, 2, 3, skin)
                c.rect(22, 16 + oy, 6 if armed_player else 5, 2, (30, 30, 34))
                if armed_player:
                    c.rect(27, 16 + oy, 3, 2, (50, 50, 56))  # silencer
            # head
            hy = 6 + oy
            if d == "down":
                c.rect(12, hy, 9, 8, skin)
                c.rect(12, hy, 9, 3, hair)
                c.rect(12, hy, 1, 6, hair)
                c.rect(20, hy, 1, 6, hair)
                if pal["helmet"]:
                    c.rect(11, hy - 1, 11, 4, hair)
                    c.hline(11, hy + 3, 11, shade(hair, -15))
                c.rect(13, hy + 4, 7, 2, pal["eyes"])
                c.rect(15, hy + 7, 3, 1, shade(skin, -40))
                if pal["band"]:
                    c.rect(12, hy + 2, 9, 1, pal["band"])
            elif d == "up":
                c.rect(12, hy, 9, 8, hair)
                c.rect(13, hy + 1, 7, 2, shade(hair, 15))
                if pal["helmet"]:
                    c.rect(11, hy - 1, 11, 5, hair)
                    c.rect(13, hy, 7, 2, shade(hair, 15))
                if pal["band"]:
                    c.rect(12, hy + 2, 9, 1, pal["band"])
                    c.rect(10, hy + 3, 2, 4, pal["band"])
            else:
                c.rect(13, hy, 8, 8, skin)
                c.rect(13, hy, 8, 3, hair)
                c.rect(13, hy, 3, 6, hair)
                if pal["helmet"]:
                    c.rect(12, hy - 1, 10, 4, hair)
                c.rect(18, hy + 4, 3, 2, pal["eyes"])
                if pal["band"]:
                    c.rect(13, hy + 2, 8, 1, pal["band"])
                    c.rect(10, hy + 2, 3, 1, pal["band"])
                    c.rect(9, hy + 3, 2, 3, pal["band"])
            if pal.get("boss"):
                boss_details(c, d, hy, oy, pal, f)
            c.outline()
            c.save(f"{prefix}_{d}_{f}")
    # dead / downed sprite
    c = C(T, T)
    c.rect(5, 18, 8, 7, skin)
    c.rect(5, 18, 8, 3, hair)
    c.rect(13, 18, 11, 8, body)
    c.rect(13, 23, 11, 2, accent)
    c.rect(24, 19, 6, 3, legs)
    c.rect(24, 23, 6, 3, shade(legs, -10))
    c.rect(29, 19, 2, 3, boots)
    c.rect(29, 23, 2, 3, boots)
    c.outline()
    c.save(f"{prefix}_dead")


def boss_details(c, d, hy, oy, pal, f):
    """Legendary-soldier look: black bandana with long tails, eyepatch, beard, harness straps."""
    band, beard, patch, harness = pal["band"], pal["beard"], (14, 14, 16), pal["harness"]
    flow = (0, 1, 0, -1)[f]  # bandana tails sway while walking
    if d == "down":
        # harness straps crossing the chest
        for i in range(6):
            c.set(12 + i, 15 + oy + i, harness)
            c.set(20 - i, 15 + oy + i, harness)
        c.rect(13, hy + 4, 7, 2, pal["skin"])          # clear the default eye band
        c.rect(13, hy + 4, 3, 2, patch)                # eyepatch (his right eye)
        c.hline(12, hy + 3, 9, patch)                  # strap
        c.set(18, hy + 4, (230, 226, 210))
        c.set(18, hy + 5, (30, 32, 36))                # good eye
        c.rect(13, hy + 6, 7, 2, beard)                # beard + moustache
        c.set(15, hy + 6, shade(beard, -25)); c.set(16, hy + 6, shade(beard, -25))
        c.rect(14, hy + 8, 5, 1, beard)
        c.rect(11, hy, 11, 2, band)                    # bandana
        c.rect(21, hy + 1, 2, 2, band)                 # knot tails peeking out
        c.rect(22, hy + 3 + flow, 1, 3, band)
    elif d == "up":
        for i in range(6):
            c.set(12 + i, 15 + oy + i, harness)
            c.set(20 - i, 15 + oy + i, harness)
        c.rect(11, hy, 11, 3, band)
        c.rect(15, hy + 3, 3, 2, band)                 # knot
        c.rect(14 + flow, hy + 5, 2, 6, band)          # long tails down the back
        c.rect(17 - flow, hy + 5, 2, 5, shade(band, 10))
    else:
        c.vline(14, 14 + oy, 7, harness)
        c.rect(18, hy + 4, 3, 2, pal["skin"])
        c.set(19, hy + 4, (230, 226, 210))
        c.set(20, hy + 4, (30, 32, 36))
        c.rect(17, hy + 6, 4, 2, beard)
        c.rect(16, hy + 7, 2, 1, beard)
        c.rect(13, hy, 8, 2, band)
        c.rect(9, hy + 1, 4, 2, band)                  # tails fly behind
        c.rect(6, hy + 2 + flow, 4, 1, band)
        c.rect(7, hy + 3 - flow, 3, 1, shade(band, 10))


PLAYER = dict(skin=(200, 152, 112), hair=(46, 34, 28), body=(58, 70, 50), body_d=(38, 46, 34),
              legs=(48, 56, 42), boots=(26, 26, 24), accent=(90, 80, 50), eyes=(20, 22, 26),
              helmet=False, band=(40, 44, 40))
BOSS = dict(skin=(192, 144, 104), hair=(64, 52, 40), body=(82, 78, 54), body_d=(56, 54, 38),
            legs=(70, 68, 48), boots=(30, 28, 24), accent=(104, 84, 54), eyes=(192, 144, 104),
            helmet=False, band=(26, 26, 30), boss=True, beard=(86, 72, 58), harness=(126, 100, 62))


# ---------------------------------------------------------------- portraits for dialogue (64x64)
def ellipse(c, cx, cy, rx, ry, col, ymin=-99, ymax=999):
    for y in range(int(cy - ry), int(cy + ry) + 1):
        if y < ymin or y > ymax:
            continue
        for x in range(int(cx - rx), int(cx + rx) + 1):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                c.set(x, y, col)


def portrait_boss():
    c = C(64, 64)
    skin, hair, band, beard = (192, 144, 104), (64, 52, 40), (26, 26, 30), (86, 72, 58)
    suit, strap = (82, 78, 54), (126, 100, 62)
    ellipse(c, 32, 64, 27, 14, suit)                    # shoulders
    ellipse(c, 32, 64, 9, 12, shade(suit, -18))         # collar shadow
    for i in range(16):                                 # harness
        c.rect(14 + i, 52 + i // 2, 2, 2, strap)
        c.rect(48 - i, 52 + i // 2, 2, 2, strap)
    c.rect(26, 40, 12, 12, shade(skin, -22))            # neck
    ellipse(c, 32, 28, 14, 17, skin)                    # head
    ellipse(c, 38, 30, 8, 13, shade(skin, -14))         # cheek shading
    ellipse(c, 32, 15, 15, 8, hair, ymax=19)            # hair
    c.rect(17, 18, 3, 12, hair); c.rect(44, 18, 3, 12, hair)
    c.rect(16, 16, 32, 5, band)                         # bandana
    c.hline(17, 16, 30, (60, 60, 66))
    c.rect(46, 17, 5, 4, band)                          # knot + tails
    for i in range(10):
        c.rect(50 + i // 2, 19 + i, 3, 1, band)
    for i in range(8):
        c.rect(47 + i // 3, 21 + i, 2, 1, shade(band, 12))
    c.rect(35, 24, 7, 1, hair)                          # brow
    c.rect(22, 24, 7, 1, hair)
    ellipse(c, 25, 28, 4.5, 3.5, (14, 14, 16))          # eyepatch
    for i in range(14):                                 # strap
        c.set(17 + i // 2, 24 + (i % 2), (14, 14, 16)) if i < 6 else c.set(29 + i, 23 - (i - 6) // 3, (14, 14, 16))
    c.rect(36, 27, 5, 2, (226, 220, 204))               # good eye
    c.rect(38, 27, 2, 2, (36, 40, 46))
    c.vline(32, 27, 7, shade(skin, -34))                # nose
    c.rect(31, 34, 3, 1, shade(skin, -40))
    ellipse(c, 32, 40, 12, 7, beard, ymin=36)           # beard
    c.rect(26, 35, 12, 3, beard)                        # moustache
    c.rect(28, 39, 8, 1, (52, 40, 34))                  # mouth
    for _ in range(40):
        x, y = rng.randint(21, 43), rng.randint(36, 46)
        if c.get(x, y)[:3] == beard:
            c.set(x, y, shade(beard, rng.randint(-14, 10)))
    c.vline(42, 30, 4, shade(skin, -40))                # scar
    c.outline()
    c.save("portrait_boss")


def portrait_major():
    c = C(64, 64)
    skin, hair, suit = (206, 166, 132), (176, 176, 170), (98, 74, 54)
    ellipse(c, 32, 64, 27, 14, suit)
    c.rect(27, 50, 10, 14, (230, 230, 222))             # shirt
    c.rect(30, 52, 4, 12, (150, 40, 36))                # tie
    c.rect(26, 40, 12, 11, shade(skin, -22))
    ellipse(c, 32, 27, 13, 16, skin)
    ellipse(c, 37, 29, 7, 12, shade(skin, -12))
    ellipse(c, 32, 14, 14, 7, hair, ymax=17)            # swept grey hair
    c.rect(19, 15, 3, 9, hair); c.rect(42, 15, 3, 9, hair)
    for x0 in (22, 34):                                 # glasses
        c.rect(x0, 25, 8, 5, (40, 40, 44))
        c.rect(x0 + 1, 26, 6, 3, (176, 200, 210))
        c.set(x0 + 2, 26, (240, 250, 255))
    c.hline(30, 26, 4, (40, 40, 44))
    c.rect(24, 32, 16, 0, skin)
    c.vline(32, 29, 5, shade(skin, -34))
    c.rect(25, 35, 14, 3, hair)                         # moustache
    c.rect(28, 39, 8, 1, (110, 70, 60))
    c.hline(22, 21, 6, shade(skin, -30)); c.hline(36, 21, 6, shade(skin, -30))  # brows / wrinkles
    c.rect(44, 22, 3, 10, (40, 40, 44))                 # headset
    c.rect(41, 34, 6, 2, (40, 40, 44))
    c.outline()
    c.save("portrait_major")


def hide_box():
    """The cardboard box. Deliberately brighter and taped so it pops out of the scenery."""
    for name, slit in (("prop_hidebox", False), ("player_box", True)):
        c = C(T, T)
        top, front = (196, 158, 98), (170, 132, 80)
        c.rect(3, 6, 26, 8, top)
        c.vline(16, 6, 8, shade(top, -30))
        c.rect(3, 14, 26, 16, front)
        c.noise(4, 15, 24, 14, front, 5)
        c.rect(3, 14, 26, 1, shade(front, -35))
        c.rect(3, 19, 26, 3, (60, 110, 160))            # blue tape band
        c.rect(6, 23, 7, 5, (230, 226, 210))            # shipping label
        c.hline(7, 25, 5, (60, 60, 60))
        for i in range(4):                              # "this side up" arrows
            c.set(21 + i, 26 - i, (40, 30, 20)); c.set(21 - i + 6, 26 - i, (40, 30, 20))
        if slit:
            c.rect(14, 16, 8, 2, (10, 10, 12))          # eye slit — someone is inside
            c.set(19, 16, (230, 226, 210))
        c.outline((255, 220, 120) if not slit else OUTL)
        c.save(name)


def trap_item(c):
    c.rect(3, 6, 10, 8, (70, 76, 80))
    c.rect(4, 7, 8, 3, (30, 34, 36))
    c.rect(5, 8, 2, 1, (240, 60, 50))
    c.rect(9, 8, 2, 1, (240, 200, 60))
    c.rect(11, 1, 1, 6, (170, 170, 170))
    c.rect(3, 12, 10, 1, (100, 106, 110))
GUARD = dict(skin=(196, 150, 112), hair=(54, 58, 64), body=(86, 92, 102), body_d=(60, 66, 74),
             legs=(60, 64, 72), boots=(24, 24, 26), accent=(40, 40, 44), eyes=(40, 30, 26),
             helmet=True, band=None)


# ---------------------------------------------------------------- UI icons (64x64, white)
def ui_icons():
    W = (240, 240, 236)
    c = C(64, 64)  # pistol
    c.rect(10, 20, 38, 9, W)
    c.rect(48, 22, 8, 5, W)
    c.rect(12, 29, 12, 4, W)
    c.rect(14, 33, 10, 16, W)
    c.rect(26, 29, 6, 6, W)
    c.rect(28, 29, 3, 3, (0, 0, 0, 0))
    c.save("ui_gun")
    c = C(64, 64)  # hand
    for i, (x, h) in enumerate(((18, 20), (25, 26), (32, 28), (39, 24))):
        c.rect(x, 34 - h + 8, 6, h, W)
    c.rect(18, 30, 27, 18, W)
    c.rect(44, 28, 6, 12, W)
    c.rect(46, 24, 5, 8, W)
    c.rect(20, 48, 22, 6, W)
    c.save("ui_hand")
    c = C(64, 64)  # decoy / noise
    c.rect(24, 26, 16, 22, W)
    c.rect(30, 14, 3, 12, W)
    for r in (14, 20):
        for a in range(-40, 41, 4):
            import math
            ang = math.radians(a)
            c.set(int(32 + r * 1.2 * math.cos(ang)) + 4, int(36 + r * math.sin(ang)), W)
            c.set(int(32 - r * 1.2 * math.cos(ang)) - 4, int(36 + r * math.sin(ang)), W)
    c.save("ui_decoy")
    c = C(64, 64)  # circle for joystick/buttons
    import math
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - 31.5, y - 31.5)
            if d < 31:
                a = 255 if d > 28 else 70
                c.set(x, y, (255, 255, 255, a))
    c.save("ui_ring")
    c = C(64, 64)
    for y in range(64):
        for x in range(64):
            if math.hypot(x - 31.5, y - 31.5) < 31:
                c.set(x, y, (255, 255, 255, 255))
    c.save("ui_disc")



# ---------------------------------------------------------------- HUD pixel icons (16x16, shown scaled up)
HEART = ["..XX....XX..",
         ".XXXX..XXXX.",
         "XXXXXXXXXXXX",
         "XXXXXXXXXXXX",
         "XXXXXXXXXXXX",
         ".XXXXXXXXXX.",
         "..XXXXXXXX..",
         "...XXXXXX...",
         "....XXXX....",
         ".....XX....."]


def hud_icons():
    for name, col, hi in (("ui_heart", (226, 52, 48), (255, 150, 140)), ("ui_heart_empty", (60, 30, 32), (90, 50, 52))):
        c = C(16, 16)
        for y, row in enumerate(HEART):
            for x, ch in enumerate(row):
                if ch == "X":
                    c.set(x + 2, y + 3, col)
        c.rect(4, 4, 2, 2, hi)
        c.set(6, 4, hi)
        c.outline((10, 4, 4))
        c.save(name)
    c = C(16, 16)  # ammo: three cartridges
    for i, x in enumerate((3, 7, 11)):
        c.rect(x, 6, 3, 8, (196, 150, 60))        # brass case
        c.vline(x, 6, 8, (236, 196, 110))
        c.rect(x, 2, 3, 4, (150, 150, 158))       # bullet
        c.set(x + 1, 1, (190, 190, 198))
        c.hline(x, 12, 3, (130, 96, 40))
    c.outline()
    c.save("ui_ammo")
    c = C(16, 16)  # objective pointer arrow (points right)
    for x in range(12):
        h = 6 - abs(x - 6) if x > 6 else 2
        if x <= 6:
            c.rect(x + 1, 6, 1, 4, (255, 214, 72))
        else:
            c.rect(x + 1, 8 - (12 - x), 1, 2 * (12 - x), (255, 214, 72))
    c.outline((60, 40, 0))
    c.save("ui_arrow")
    c = C(16, 16)  # objective marker above the target (points down)
    for y in range(7):
        c.rect(2 + y, 3 + y, 12 - 2 * y, 1, (255, 214, 72))
    c.rect(6, 0, 4, 3, (255, 214, 72))
    c.outline((60, 40, 0))
    c.save("ui_target")


# ---------------------------------------------------------------- run
floor_tile("tile_floor_0", 1, 0)
floor_tile("tile_floor_1", 2, 1)
floor_tile("tile_floor_2", 3, 2)
floor_tile("tile_floor_3", 4, 3)
floor_tile("tile_floor_4", 5, 4)
grate_tile()
hazard_tile()
door_tile()
wall_top()
wall_face("tile_wall_0", "plain", 11)
wall_face("tile_wall_1", "pipes", 12)
wall_face("tile_wall_2", "vpipe", 13)
wall_face("tile_wall_3", "box", 14)
wall_face("tile_wall_4", "sign", 15)
vent_face()
exit_face()
locked_door("prop_door_locked", (230, 50, 40))
crate()
box()
barrel("prop_barrel_red", (124, 56, 42))
barrel("prop_barrel_blue", (62, 82, 98))
shelf()
server_rack()
locker()
weapon_rack()
desk()
terminal("prop_terminal_on", True)
terminal("prop_terminal_off", False)
panel("prop_panel_on", True)
panel("prop_panel_off", False)
proto_case()
small("item_keycard", keycard)
small("item_intel", intel)
small("item_ammo", ammo)
small("item_medkit", medkit)
small("item_decoy", decoy)
camera_sprite()
lamp()
marks()
character("player", BOSS, True)   # Big Boss-style main hero
portrait_boss()
portrait_major()
hide_box()
small("item_trap", trap_item)
character("guard", GUARD, False)
ui_icons()
hud_icons()

# contact sheet for quick review (not imported: written outside Assets)
files = sorted(f for f in os.listdir(OUT) if f.endswith(".png"))
sheet = Image.new("RGBA", (12 * 70, ((len(files) + 11) // 12) * 70), (30, 30, 34, 255))
for i, f in enumerate(files):
    im = Image.open(os.path.join(OUT, f))
    s = max(1, min(64 // im.width, 64 // im.height))
    im = im.resize((im.width * s, im.height * s), Image.NEAREST)
    sheet.alpha_composite(im, ((i % 12) * 70 + 3, (i // 12) * 70 + 3))
sheet.save(os.path.join(ROOT, "Tools", "contact_sheet.png"))
print(len(files), "sprites written to", OUT)
