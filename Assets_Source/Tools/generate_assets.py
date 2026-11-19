"""
PROJECT APEX 2.0 — original asset generator.

Generates every texture, decal, advertising board, façade, branding image and loading artwork used
by the game. All output is original (procedural); text is rendered with the Windows system font
Bahnschrift into images (the font file itself is not redistributed).

Usage:  python generate_assets.py            (writes into Assets_Source/ and UnityGame/Assets/Resources/)
"""
import math
import os
import random
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC_TEX = os.path.join(ROOT, "Assets_Source", "Textures")
SRC_BRAND = os.path.join(ROOT, "Assets_Source", "Branding")
RES_TEX = os.path.join(ROOT, "UnityGame", "Assets", "Resources", "Textures")
RES_UI = os.path.join(ROOT, "UnityGame", "Assets", "Resources", "UI")
FONT_PATH = r"C:\Windows\Fonts\bahnschrift.ttf"

RED = (227, 13, 28)
RED_DEEP = (140, 5, 15)
WHITE = (247, 247, 250)
BLACK = (6, 6, 8)
CHARCOAL = (19, 20, 23)

rng = np.random.default_rng(20261120)
random.seed(20261120)


def font(size, style="Bold"):
    f = ImageFont.truetype(FONT_PATH, size)
    try:
        f.set_variation_by_name(style)
    except Exception:
        pass
    return f


def save(img, name, ui=False, brand=False):
    folder = RES_UI if ui else RES_TEX
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path, optimize=True)
    copy_dir = SRC_BRAND if (ui or brand) else SRC_TEX
    os.makedirs(copy_dir, exist_ok=True)
    shutil.copyfile(path, os.path.join(copy_dir, name + ".png"))
    print("  ", os.path.relpath(path, ROOT), img.size)


# ---------------------------------------------------------------- noise helpers

def fbm(w, h, octaves=5, scale=64.0, seed=0):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        s = max(1, int(scale / (2 ** o)))
        gw, gh = w // s + 2, h // s + 2
        g = r.random((gh, gw)).astype(np.float32)
        # tileable: wrap edges
        g[:, -1] = g[:, 0]
        g[-1, :] = g[0, :]
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((gw * s, gh * s), Image.BICUBIC)
        arr = np.asarray(img, np.float32)[:h, :w] / 255.0
        out += arr * amp
        total += amp
        amp *= 0.5
    return out / total


def normal_from_height(h, strength=2.0):
    gy, gx = np.gradient(h)
    nx, ny = -gx * strength, -gy * strength
    nz = np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / l, ny / l, nz / l], -1)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")


def tileable_noise(w, h, seed, scale=1.0):
    r = np.random.default_rng(seed)
    return r.random((h, w)).astype(np.float32) * scale


# ---------------------------------------------------------------- surfaces

def asphalt():
    w = h = 1024
    base = fbm(w, h, 6, 256, 1)
    grain = tileable_noise(w, h, 2)
    stones = (tileable_noise(w, h, 3) > 0.985).astype(np.float32)
    stones = np.asarray(Image.fromarray((stones * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)), np.float32) / 255
    v = 0.36 + base * 0.12 + grain * 0.07 + stones * 0.12
    # rubbered racing line darker toward the middle of u (across road), subtle
    u = np.linspace(0, 1, w)[None, :]
    rubber = np.exp(-((u - 0.5) ** 2) / 0.02) * 0.05 + np.exp(-((u - 0.28) ** 2) / 0.004) * 0.03 + np.exp(-((u - 0.72) ** 2) / 0.004) * 0.03
    v = v - rubber
    patches = fbm(w, h, 3, 512, 9)
    v = v * (0.92 + (patches > 0.62) * 0.08 * -1 + 0.08)
    col = np.stack([v, v, v * 1.03], -1)
    img = Image.fromarray(np.clip(col * 255, 0, 255).astype(np.uint8), "RGB")
    save(img, "asphalt")
    hgt = base * 0.6 + grain * 0.4 + stones * 0.8
    save(normal_from_height(hgt, 3.0), "asphalt_n")


def concrete():
    w = h = 1024
    base = fbm(w, h, 6, 256, 11)
    grain = tileable_noise(w, h, 12)
    stains = fbm(w, h, 4, 512, 13)
    v = 0.62 + base * 0.12 + grain * 0.05 - (stains > 0.6) * 0.06
    # formwork seams every 256 px
    seam = np.zeros((h, w), np.float32)
    seam[:, ::256] = 0.18
    seam[::512, :] = 0.12
    seam = np.asarray(Image.fromarray((seam * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255
    v -= seam
    col = np.stack([v, v * 0.99, v * 0.96], -1)
    save(Image.fromarray(np.clip(col * 255, 0, 255).astype(np.uint8), "RGB"), "concrete")
    save(normal_from_height(base + grain * 0.3 - seam * 2, 2.0), "concrete_n")


def paving():
    w = h = 1024
    tile = 128
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    noise = fbm(w, h, 5, 128, 21)
    hgt = np.zeros((h, w), np.float32)
    for y in range(0, h, tile // 2):
        off = (tile // 2) if (y // (tile // 2)) % 2 else 0
        for x in range(-tile, w, tile):
            shade = random.randint(118, 150)
            tint = (shade, shade - 4, shade - 10)
            d.rectangle([x + off + 2, y + 2, x + off + tile - 3, y + tile // 2 - 3], fill=tint)
    arr = np.asarray(img, np.float32) / 255
    arr = arr * (0.85 + noise[..., None] * 0.3)
    gaps = (arr.sum(-1) < 0.05).astype(np.float32)
    arr[gaps > 0] = [0.2, 0.19, 0.17]
    img = Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8), "RGB")
    save(img, "paving")
    hgt = 1 - gaps * 0.8 + noise * 0.3
    save(normal_from_height(np.asarray(Image.fromarray((hgt * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5)), np.float32) / 255, 3.0), "paving_n")


def kerb():
    w, h = 256, 512
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for i in range(4):
        y0 = i * h // 4
        d.rectangle([0, y0, w, y0 + h // 4], fill=RED if i % 2 == 0 else WHITE)
    arr = np.asarray(img, np.float32) / 255
    n = fbm(w, h, 4, 64, 31)[..., None]
    arr = arr * (0.88 + n * 0.2)
    save(Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8), "RGB"), "kerb")


def chequer():
    w, h = 512, 128
    img = Image.new("RGB", (w, h), WHITE)
    d = ImageDraw.Draw(img)
    s = 64
    for y in range(0, h, s):
        for x in range(0, w, s):
            if (x // s + y // s) % 2 == 0:
                d.rectangle([x, y, x + s - 1, y + s - 1], fill=(15, 15, 17))
    save(img, "chequer")


def carbon():
    w = h = 256
    arr = np.zeros((h, w), np.float32)
    s = 16
    for y in range(h):
        for x in range(w):
            cx, cy = (x // s), (y // s)
            twill = ((cx + cy) // 2) % 2
            fx, fy = (x % s) / s, (y % s) / s
            stripe = 0.5 + 0.5 * math.sin((fx if twill else fy) * math.pi * 6)
            shade = (0.55 if twill else 0.35) + stripe * 0.15
            arr[y, x] = shade
    col = np.stack([arr * 0.30, arr * 0.31, arr * 0.34], -1)
    save(Image.fromarray(np.clip(col * 255, 0, 255).astype(np.uint8), "RGB"), "carbon")


def tyre_sidewall():
    w, h = 512, 128
    n = fbm(w, h, 4, 32, 41)
    arr = 0.09 + n * 0.04
    col = np.stack([arr, arr, arr * 1.05], -1)
    img = Image.fromarray(np.clip(col * 255, 0, 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    # yellow tyre-brand band on the sidewall (v ≈ 0.12 / 0.88 of the profile)
    for y in (14, h - 18):
        d.rectangle([0, y, w, y + 3], fill=(200, 160, 30))
    f = font(13)
    for x in range(0, w, 128):
        d.text((x + 8, 20), "KAIROS", font=f, fill=(170, 140, 40))
        d.text((x + 8, h - 38), "KAIROS", font=f, fill=(170, 140, 40))
    save(img, "tyre_sidewall")


def mesh_grille():
    w = h = 128
    img = Image.new("RGB", (w, h), (8, 8, 9))
    d = ImageDraw.Draw(img)
    r = 10
    for row in range(-1, h // r + 2):
        for col in range(-1, w // r + 2):
            cx = col * r * 1.75 + (row % 2) * r * 0.875
            cy = row * r * 1.5
            pts = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(30, 390, 60)]
            d.polygon(pts, outline=(55, 56, 60))
    save(img, "mesh_grille")


def brake_disc():
    w, h = 512, 256
    arr = np.full((h, w), 0.55, np.float32)
    n = fbm(w, h, 4, 64, 51)
    arr += n * 0.1
    img = Image.fromarray(np.clip(np.stack([arr, arr, arr * 1.02], -1) * 255, 0, 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    for k in range(24):  # curved slots
        x = k * w / 24
        d.line([(x, 40), (x + 18, 120)], fill=(70, 70, 72), width=3)
    for k in range(48):
        x = k * w / 48 + 6
        d.ellipse([x - 3, 150, x + 3, 156], fill=(40, 40, 42))
    save(img, "brake_disc")


def fence():
    w = h = 128
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = 32
    for i in range(-w, w * 2, s):
        d.line([(i, 0), (i + h, h)], fill=(200, 205, 210, 255), width=3)
        d.line([(i, h), (i + h, 0)], fill=(200, 205, 210, 255), width=3)
    d.rectangle([0, 0, w, 4], fill=(150, 152, 155, 255))
    save(img, "fence")


def water():
    w = h = 512
    n1 = fbm(w, h, 5, 128, 61)
    n2 = fbm(w, h, 4, 64, 62)
    v = n1 * 0.6 + n2 * 0.4
    col = np.stack([0.15 + v * 0.15, 0.32 + v * 0.2, 0.42 + v * 0.22], -1)
    save(Image.fromarray(np.clip(col * 255, 0, 255).astype(np.uint8), "RGB"), "water")


# ---------------------------------------------------------------- branding / decals

BRANDS = [
    ("VORTEX", "OIL", (255, 196, 0), (12, 12, 14)),
    ("KAIROS", "TYRES", (255, 255, 255), (200, 20, 30)),
    ("NOVA", "TELECOM", (90, 200, 255), (10, 22, 60)),
    ("HELIX", "WATCHES", (230, 210, 160), (18, 18, 20)),
    ("STRATOS", "ENERGY", (20, 20, 20), (255, 120, 0)),
    ("PORTO VELA", "GRAND PRIX", (255, 255, 255), (0, 90, 160)),
    ("MERIDIAN", "BANK", (255, 255, 255), (0, 120, 80)),
    ("APEX", "", (255, 255, 255), RED),
]


def brand_panel(w, h, idx, transparent=False):
    name, sub, fg, bg = BRANDS[idx]
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0) if transparent else bg + (255,))
    d = ImageDraw.Draw(img)
    if transparent:
        d.rounded_rectangle([4, 4, w - 5, h - 5], radius=int(h * 0.12), fill=bg + (255,))
    fsize = int(h * (0.52 if sub else 0.66))
    f = font(fsize, "Bold")
    tw = d.textlength(name, font=f)
    while tw > w * 0.86:
        fsize -= 2
        f = font(fsize, "Bold")
        tw = d.textlength(name, font=f)
    y = h * (0.12 if sub else 0.13)
    # small emblem
    tx = (w - tw) / 2
    if idx == 7:
        tx += h * 0.3
        draw_chevron(d, (tx - h * 0.42, h * 0.5), h * 0.55, (255, 255, 255, 255))
    d.text((tx, y), name, font=f, fill=fg + (255,))
    if sub:
        fs = font(int(h * 0.22), "SemiBold")
        sw = d.textlength(sub, font=fs)
        d.text(((w - sw) / 2, h * 0.68), sub, font=fs, fill=fg + (220,))
    return img


def draw_chevron(d, center, size, fill):
    cx, cy = center
    s = size
    outer = [(cx - s * 0.55, cy + s * 0.5), (cx, cy - s * 0.55), (cx + s * 0.55, cy + s * 0.5), (cx + s * 0.3, cy + s * 0.5), (cx, cy - s * 0.12), (cx - s * 0.3, cy + s * 0.5)]
    d.polygon(outer, fill=fill)


def sponsors_atlas():
    cw, ch = 512, 160
    atlas = Image.new("RGBA", (cw * 4, ch * 2), (0, 0, 0, 0))
    # cells laid out 4 x 2, each cell 512 x 160 (exactly one logo)
    for i in range(8):
        panel = brand_panel(cw, ch, i, transparent=True)
        atlas.alpha_composite(panel, ((i % 4) * cw, (i // 4) * ch))
    save(atlas, "sponsors")


def digits():
    w, h = 1280, 192
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = font(170, "Bold")
    for i in range(10):
        t = str(i)
        tw = d.textlength(t, font=f)
        d.text((i * 128 + (128 - tw) / 2, -8), t, font=f, fill=(255, 255, 255, 255))
    save(img, "digits")


def adboard():
    # 4096 x 256 strip of 8 sponsor panels (2:1 each); u along the track (13.5 m per repeat)
    w, h = 4096, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 255))
    order = [7, 0, 2, 1, 5, 3, 4, 6]
    pw = w // len(order)
    for k, idx in enumerate(order):
        p = brand_panel(pw, h, idx)
        img.alpha_composite(p, (k * pw, 0))
    d = ImageDraw.Draw(img)
    for k in range(len(order) + 1):
        d.line([(k * pw, 0), (k * pw, h)], fill=(0, 0, 0, 255), width=6)
    save(img.convert("RGB"), "adboard")


def banner_start():
    w, h = 2048, 256
    img = Image.new("RGB", (w, h), (10, 10, 12))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 12], fill=RED)
    d.rectangle([0, h - 12, w, h], fill=RED)
    f = font(120, "Bold")
    t = "PROJECT APEX  ·  PORTO VELA GRAND PRIX"
    tw = d.textlength(t, font=f)
    d.text(((w - tw) / 2, 52), t, font=f, fill=WHITE)
    save(img, "banner_start")


def logo_sign():
    w, h = 2048, 280
    img = Image.new("RGB", (w, h), (8, 8, 10))
    d = ImageDraw.Draw(img)
    draw_chevron(d, (190, 150), 230, RED)
    f = font(200, "Bold")
    d.text((360, 20), "PROJECT APEX", font=f, fill=WHITE)
    fs = font(48, "SemiBold")
    d.text((1700, 190), "GT3 SERIES", font=fs, fill=(170, 172, 178))
    save(img, "logo_sign")


def timing_tower():
    w, h = 256, 2048
    img = Image.new("RGB", (w, h), (6, 6, 8))
    d = ImageDraw.Draw(img)
    f = font(64, "Bold")
    fs = font(56, "SemiBold")
    d.rectangle([0, 0, w, 160], fill=RED)
    d.text((26, 40), "LAP", font=f, fill=WHITE)
    codes = ["VAR", "YOU", "MOR", "ASA", "LIN", "NOV", "HEL", "STR", "KAI", "MER"]
    for i, c in enumerate(codes):
        y = 200 + i * 182
        d.rectangle([0, y, 70, y + 150], fill=(240, 240, 242))
        d.text((14, y + 36), str(i + 1), font=f, fill=BLACK)
        d.text((90, y + 40), c, font=fs, fill=WHITE)
    save(img, "timing_tower")


def brakeboards():
    w, h = 768, 256
    img = Image.new("RGB", (w, h), WHITE)
    d = ImageDraw.Draw(img)
    f = font(150, "Bold")
    for i, t in enumerate(["150", "100", "50"]):
        x0 = i * 256
        d.rectangle([x0 + 6, 6, x0 + 249, 249], outline=BLACK, width=10)
        tw = d.textlength(t, font=f)
        d.text((x0 + (256 - tw) / 2, 30), t, font=f, fill=BLACK)
    save(img, "brakeboards")


def crowd():
    w, h = 1024, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    palette = [(220, 30, 40), (240, 240, 240), (30, 60, 160), (250, 200, 30), (40, 40, 45), (20, 140, 80), (240, 120, 20), (130, 40, 150), (90, 160, 220)]
    skin = [(240, 200, 170), (200, 150, 110), (140, 95, 65), (90, 60, 40)]
    x = 0
    while x < w:
        bw = random.randint(16, 24)
        top = random.randint(70, 110)
        col = random.choice(palette)
        d.rounded_rectangle([x, top + 28, x + bw, h], radius=6, fill=col + (255,))
        hr = bw * 0.38
        hx = x + bw / 2
        d.ellipse([hx - hr, top, hx + hr, top + hr * 2.2], fill=random.choice(skin) + (255,))
        if random.random() < 0.15:  # flag / raised arm
            d.line([(hx + 6, top + 30), (hx + 14, top - 10)], fill=random.choice(skin) + (255,), width=4)
            if random.random() < 0.5:
                d.rectangle([hx + 14, top - 30, hx + 40, top - 12], fill=random.choice([RED, WHITE, (255, 200, 0)]) + (255,))
        x += bw - random.randint(2, 6)
    save(img, "crowd")


def facade(idx):
    w = h = 512
    bases = [(165, 160, 152), (72, 78, 88), (196, 160, 120)]
    base = bases[idx]
    img = Image.new("RGB", (w, h), base)
    emit = Image.new("RGB", (w, h), (0, 0, 0))
    d = ImageDraw.Draw(img)
    de = ImageDraw.Draw(emit)
    n = fbm(w, h, 4, 128, 70 + idx)
    # 4 bays x 4 floors per tile (1 u = 4 m bay → texture covers 4 bays? UV: u per 4 m; tile = 1 bay)
    bays, floors = 2, 4
    bw, fh = w // bays, h // floors
    for f in range(floors):
        for b in range(bays):
            x0, y0 = b * bw, f * fh
            if idx == 1:   # glass office
                d.rectangle([x0 + 6, y0 + 10, x0 + bw - 6, y0 + fh - 14], fill=(40, 52, 66))
                d.line([(x0 + bw // 2, y0 + 10), (x0 + bw // 2, y0 + fh - 14)], fill=(90, 96, 104), width=4)
            elif idx == 2:  # old town shutters
                d.rectangle([x0 + bw * 0.28, y0 + fh * 0.2, x0 + bw * 0.72, y0 + fh * 0.8], fill=(40, 34, 30))
                d.rectangle([x0 + bw * 0.2, y0 + fh * 0.2, x0 + bw * 0.28, y0 + fh * 0.8], fill=(60, 110, 80))
                d.rectangle([x0 + bw * 0.72, y0 + fh * 0.2, x0 + bw * 0.8, y0 + fh * 0.8], fill=(60, 110, 80))
                d.rectangle([x0 + bw * 0.18, y0 + fh * 0.8, x0 + bw * 0.82, y0 + fh * 0.84], fill=(220, 210, 190))
            else:  # residential
                d.rectangle([x0 + bw * 0.2, y0 + fh * 0.22, x0 + bw * 0.8, y0 + fh * 0.75], fill=(35, 40, 48))
                d.rectangle([x0 + bw * 0.15, y0 + fh * 0.75, x0 + bw * 0.85, y0 + fh * 0.8], fill=(205, 200, 192))
            if random.random() < 0.38:
                warm = random.choice([(255, 200, 130), (255, 225, 170), (230, 240, 255)])
                if idx == 1:
                    de.rectangle([x0 + 6, y0 + 10, x0 + bw - 6, y0 + fh - 14], fill=warm)
                elif idx == 2:
                    de.rectangle([x0 + bw * 0.28, y0 + fh * 0.2, x0 + bw * 0.72, y0 + fh * 0.8], fill=warm)
                else:
                    de.rectangle([x0 + bw * 0.2, y0 + fh * 0.22, x0 + bw * 0.8, y0 + fh * 0.75], fill=warm)
        d.line([(0, f * fh), (w, f * fh)], fill=tuple(max(0, c - 30) for c in base), width=4)
    arr = np.asarray(img, np.float32) * (0.9 + n[..., None] * 0.2)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    # facades are applied with v = height / 3.4 m (one floor per v unit): rescale so one tile = 1 floor x 1 bay
    save(img, f"facade_{idx}")
    save(emit, f"facade_{idx}_e")


def glass_band():
    w, h = 512, 256
    img = Image.new("RGB", (w, h), (30, 38, 48))
    d = ImageDraw.Draw(img)
    for x in range(0, w, 64):
        d.rectangle([x, 0, x + 6, h], fill=(70, 74, 80))
        if random.random() < 0.6:
            d.rectangle([x + 10, 30, x + 58, h - 30], fill=(250, 220, 170))
    d.rectangle([0, 0, w, 16], fill=(80, 84, 90))
    d.rectangle([0, h - 16, w, h], fill=(80, 84, 90))
    save(img, "glass_band")


def palm():
    w, h = 256, 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(w // 2, 0), (w // 2, h)], fill=(70, 90, 40, 255), width=6)
    for i in range(40):
        t = i / 40
        y = int(20 + t * (h - 40))
        ln = int((1 - t * 0.6) * w * 0.48)
        col = (40 + random.randint(0, 30), 110 + random.randint(0, 40), 40, 255)
        d.line([(w // 2, y), (w // 2 - ln, y + 30)], fill=col, width=7)
        d.line([(w // 2, y), (w // 2 + ln, y + 30)], fill=col, width=7)
    save(img, "palm")


def particles():
    s = 128
    yy, xx = np.mgrid[0:s, 0:s]
    r = np.sqrt((xx - s / 2 + 0.5) ** 2 + (yy - s / 2 + 0.5) ** 2) / (s / 2)
    n = fbm(s, s, 4, 32, 90)
    a = np.clip((1 - r) * 1.4, 0, 1) ** 1.5 * (0.6 + n * 0.6)
    smoke = np.stack([np.ones_like(a), np.ones_like(a), np.ones_like(a), np.clip(a, 0, 1)], -1)
    save(Image.fromarray((smoke * 255).astype(np.uint8), "RGBA"), "smoke")
    a2 = np.clip(1 - r, 0, 1) ** 3
    spark = np.stack([np.ones_like(a2), np.ones_like(a2) * 0.85, np.ones_like(a2) * 0.6, a2], -1)
    save(Image.fromarray((spark * 255).astype(np.uint8), "RGBA"), "spark")
    a3 = np.clip(1 - r, 0, 1) ** 1.6
    flame = np.stack([np.ones_like(a3), 0.45 + a3 * 0.5, a3 * 0.4, a3], -1)
    save(Image.fromarray((np.clip(flame, 0, 1) * 255).astype(np.uint8), "RGBA"), "flame")
    w, h = 64, 64
    u = np.linspace(-1, 1, w)[None, :].repeat(h, 0)
    a4 = np.clip(1 - np.abs(u) ** 2, 0, 1)
    skid = np.stack([np.ones_like(a4), np.ones_like(a4), np.ones_like(a4), a4], -1)
    save(Image.fromarray((skid * 255).astype(np.uint8), "RGBA"), "skid")


# ---------------------------------------------------------------- UI / key art

def logo():
    w, h = 1600, 520
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # emblem: stacked red chevrons forming an "A" apex
    cx, cy = 250, 270
    for k, (s, a) in enumerate([(420, 255), (300, 255)]):
        col = RED + (a,) if k == 0 else (255, 255, 255, 255)
        draw_chevron(d, (cx, cy + k * 52), s * (1 if k == 0 else 0.62), col)
    f = font(250, "Bold")
    d.text((500, 52), "APEX", font=f, fill=WHITE + (255,))
    fs = font(78, "SemiBold")
    d.text((510, 330), "PROJECT", font=fs, fill=(190, 192, 198, 255))
    tw = d.textlength("PROJECT", font=fs)
    d.rectangle([510 + tw + 24, 362, 1520, 370], fill=RED + (255,))
    fv = font(54, "SemiBold")
    d.text((1395, 395), "2.0", font=fv, fill=(150, 152, 160, 255))
    save(img, "logo", ui=True)
    icon = Image.new("RGBA", (256, 256), BLACK + (255,))
    di = ImageDraw.Draw(icon)
    di.rectangle([0, 220, 256, 256], fill=RED + (255,))
    draw_chevron(di, (128, 120), 190, RED + (255,))
    draw_chevron(di, (128, 146), 112, (255, 255, 255, 255))
    save(icon, "icon_app", ui=True)


def loading_art():
    w, h = 1920, 1080
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # dusk sky gradient with sun glow
    t = yy / h
    sky = np.stack([0.05 + (1 - t) * 0.1, 0.05 + (1 - t) * 0.05, 0.08 + (1 - t) * 0.08], -1)
    sun = np.exp(-(((xx - w * 0.72) / 520) ** 2 + ((yy - h * 0.46) / 180) ** 2))
    sky += sun[..., None] * np.array([0.95, 0.42, 0.18])
    img = Image.fromarray(np.clip(sky * 255, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img)
    horizon = int(h * 0.52)
    # skyline silhouettes
    x = 0
    while x < w:
        bw = random.randint(30, 110)
        bh = random.randint(40, 260) * (1.4 if abs(x - w * 0.3) < 200 else 1)
        d.rectangle([x, horizon - bh, x + bw, horizon], fill=(14, 14, 20, 255))
        for wy in range(int(horizon - bh) + 8, horizon - 6, 14):
            for wx in range(x + 6, x + bw - 6, 12):
                if random.random() < 0.18:
                    d.rectangle([wx, wy, wx + 4, wy + 6], fill=(255, 190, 120, 200))
        x += bw + random.randint(0, 8)
    # road in perspective
    vp = (w * 0.62, horizon)
    d.polygon([(vp[0] - 6, horizon), (vp[0] + 6, horizon), (w * 1.25, h), (-w * 0.1, h)], fill=(22, 22, 26, 255))
    for k in range(14):
        z0, z1 = k / 14, (k + 0.45) / 14
        def lerp(a, b, s):
            return a + (b - a) * s
        y0, y1 = lerp(horizon, h, z0 ** 2), lerp(horizon, h, z1 ** 2)
        x0l, x1l = lerp(vp[0], -w * 0.1, z0 ** 2), lerp(vp[0], -w * 0.1, z1 ** 2)
        d.polygon([(x0l, y0), (x0l + 8 * z0 * 10, y0), (x1l + 8 * z1 * 10, y1), (x1l, y1)], fill=(230, 20, 30, 255) if k % 2 == 0 else (240, 240, 240, 255))
    # light trails
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for k in range(9):
        col = (255, 40 + k * 8, 40, 200) if k % 3 else (255, 240, 220, 180)
        pts = []
        for s in np.linspace(0, 1, 40):
            px = vp[0] + (w * (0.9 + k * 0.05) - vp[0]) * s ** 1.6
            py = horizon + (h * (0.7 + k * 0.03) - horizon) * s ** 1.9 - math.sin(s * math.pi) * 40
            pts.append((px, py))
        g.line(pts, fill=col, width=3 + k)
    glow = glow.filter(ImageFilter.GaussianBlur(3))
    img.alpha_composite(glow)
    # red diagonal slashes
    sl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    s = ImageDraw.Draw(sl)
    s.polygon([(w * 0.83, 0), (w * 0.9, 0), (w * 0.62, h), (w * 0.55, h)], fill=RED + (90,))
    s.polygon([(w * 0.93, 0), (w * 0.95, 0), (w * 0.67, h), (w * 0.65, h)], fill=(255, 255, 255, 40))
    img.alpha_composite(sl)
    save(img.convert("RGB"), "loading_bg", ui=True)


def main():
    print("PROJECT APEX asset generation")
    asphalt(); concrete(); paving(); kerb(); chequer(); carbon(); tyre_sidewall(); mesh_grille(); brake_disc()
    fence(); water(); sponsors_atlas(); digits(); adboard(); banner_start(); logo_sign(); timing_tower(); brakeboards()
    crowd(); glass_band(); palm(); particles()
    for i in range(3):
        facade(i)
    logo(); loading_art()
    print("done")


if __name__ == "__main__":
    main()
