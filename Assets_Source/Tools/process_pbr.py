"""
PROJECT APEX 2.0 — PBR texture processing for the visual overhaul.

Converts the downloaded CC0 ambientCG material sets (Assets_Source/Downloaded/ambientcg, licence CC0 1.0)
into the URP Lit layout and generates the additional procedural maps the overhaul needs:
  <name>_albedo.jpg   base colour
  <name>_n.png        OpenGL-convention normal map (Unity convention)
  <name>_ms.png       URP metallic/smoothness map: R = metallic, A = smoothness (1 - roughness)
  <name>_ao.jpg       ambient occlusion (read from the G channel by URP)
Output: UnityGame/Assets/Resources/Textures/pbr
Usage:  python process_pbr.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets_Source", "Downloaded", "ambientcg")
OUT = os.path.join(ROOT, "UnityGame", "Assets", "Resources", "Textures", "pbr")
rng = np.random.default_rng(7)

# (source id, output name, size, roughness multiplier, metallic override or None)
SETS = [
    ("Asphalt031", "road", 2048, 1.0, 0.0),
    ("PavingStones138", "cobbles", 1024, 1.0, 0.0),
    ("Concrete034", "concrete", 1024, 1.0, 0.0),
    ("Facade001", "facade_glass", 1024, 0.8, None),
    ("Facade006", "facade_office", 1024, 1.0, None),
    ("Facade017", "facade_tower", 1024, 1.0, 0.0),
    ("Facade018A", "facade_brick", 1024, 1.0, None),
    ("Bricks105", "bricks", 1024, 1.0, 0.0),
    ("Metal049A", "brushed", 512, 1.0, None),
]


def load(path, size, mode):
    im = Image.open(path).convert(mode)
    w, h = im.size
    scale = size / max(w, h)
    return im.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name)
    if name.endswith(".jpg"):
        img.convert("RGB").save(p, quality=92)
    else:
        img.save(p, optimize=True)
    print("  ", os.path.relpath(p, ROOT), img.size)


def process_set(src_id, name, size, rough_mul, metal_override):
    base = os.path.join(SRC, src_id, f"{src_id}_2K-JPG")
    save(load(base + "_Color.jpg", size, "RGB"), f"{name}_albedo.jpg")
    save(load(base + "_NormalGL.jpg", size, "RGB"), f"{name}_n.png")
    rough = np.asarray(load(base + "_Roughness.jpg", size, "L"), np.float32) / 255.0
    smooth = np.clip(1.0 - rough * rough_mul, 0.0, 1.0)
    metal_path = base + "_Metalness.jpg"
    if metal_override is None and os.path.exists(metal_path):
        metal = np.asarray(load(metal_path, size, "L").resize(smooth.shape[::-1]), np.float32) / 255.0
    else:
        metal = np.full_like(smooth, metal_override or 0.0)
    ms = np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), smooth], -1)
    save(Image.fromarray((ms * 255).astype(np.uint8), "RGBA"), f"{name}_ms.png")
    ao_path = base + "_AmbientOcclusion.jpg"
    if os.path.exists(ao_path):
        save(load(ao_path, size, "L"), f"{name}_ao.jpg")


def fbm(w, h, octaves, scale, seed):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp = total = 0.0
    amp = 1.0
    for o in range(octaves):
        s = max(1, int(scale / 2 ** o))
        g = r.random((h // s + 2, w // s + 2)).astype(np.float32)
        g[:, -1] = g[:, 0]
        g[-1, :] = g[0, :]
        img = Image.fromarray((g * 255).astype(np.uint8)).resize(((w // s + 2) * s, (h // s + 2) * s), Image.BICUBIC)
        out += np.asarray(img, np.float32)[:h, :w] / 255.0 * amp
        total += amp
        amp *= 0.5
    return out / total


def road_wear():
    """Large-scale detail map (x2 multiply, 0.5 = neutral): patches, tar snakes, tyre wear. 1 tile = full width × 56 m."""
    w, h = 512, 2048
    v = np.full((h, w), 0.5, np.float32)
    big = fbm(w, h, 4, 512, 11)
    v += (big - 0.5) * 0.12
    # rubbered driving zones (darker bands across u)
    u = np.linspace(0, 1, w)[None, :]
    v -= (np.exp(-((u - 0.38) ** 2) / 0.01) + np.exp(-((u - 0.62) ** 2) / 0.01)) * 0.035
    img = Image.fromarray((np.clip(v, 0, 1) * 255).astype(np.uint8), "L")
    d = ImageDraw.Draw(img)
    # repaired patches (lighter rectangles with darker seams)
    for _ in range(9):
        x0, y0 = rng.integers(20, w - 200), rng.integers(0, h - 300)
        pw, ph = rng.integers(80, 220), rng.integers(120, 380)
        d.rectangle([x0, y0, x0 + pw, y0 + ph], fill=int(128 + rng.integers(6, 18)))
        d.rectangle([x0, y0, x0 + pw, y0 + ph], outline=88, width=3)
    # tar snakes (sealed cracks)
    for _ in range(26):
        x, y = float(rng.integers(0, w)), float(rng.integers(0, h))
        ang = rng.uniform(0, np.pi)
        pts = []
        for s in range(rng.integers(20, 70)):
            ang += rng.normal(0, 0.35)
            x += np.cos(ang) * 6
            y += np.sin(ang) * 6
            pts.append((x % w, y % h))
        for a, b in zip(pts, pts[1:]):
            if abs(a[0] - b[0]) < 50 and abs(a[1] - b[1]) < 50:
                d.line([a, b], fill=70, width=int(rng.integers(2, 5)))
    # oil drips on the grid-like centre
    for _ in range(40):
        x, y = rng.integers(0, w), rng.integers(0, h)
        r = rng.integers(4, 14)
        d.ellipse([x - r, y - r * 2, x + r, y + r * 2], fill=int(rng.integers(95, 115)))
    img = img.filter(ImageFilter.GaussianBlur(1.2))
    save(img.convert("RGB"), "road_wear.png")


def paint_flake():
    """Fine metallic-flake normal map for car paint (tiled densely)."""
    s = 512
    n = rng.normal(0, 1, (s, s, 2)).astype(np.float32) * 0.18
    n = np.asarray(Image.fromarray(((n[..., 0] * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255 - 0.5
    m = rng.normal(0, 1, (s, s)).astype(np.float32) * 0.18
    m = np.asarray(Image.fromarray(((m * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255 - 0.5
    nz = np.sqrt(np.clip(1 - n * n - m * m, 0, 1))
    img = np.stack([n * 0.5 + 0.5, m * 0.5 + 0.5, nz * 0.5 + 0.5], -1)
    save(Image.fromarray((img * 255).clip(0, 255).astype(np.uint8), "RGB"), "paint_flake_n.png")


def kerb_normal():
    """Raised kerb rumble profile: ridges across the strip every 0.5 m (v), bevelled edges (u)."""
    w, h = 256, 512
    v = np.linspace(0, 8 * np.pi, h)[:, None]
    height = (np.sin(v) * 0.5 + 0.5) ** 2 * 0.6 + fbm(w, h, 3, 32, 5) * 0.15
    height = np.repeat(height[:, :1], w, 1) if height.shape[1] == 1 else height
    gy, gx = np.gradient(height)
    nx, ny = -gx * 6, -gy * 6
    l = np.sqrt(nx ** 2 + ny ** 2 + 1)
    img = np.stack([nx / l, ny / l, 1 / l], -1) * 0.5 + 0.5
    save(Image.fromarray((img * 255).astype(np.uint8), "RGB"), "kerb_n.png")


def contact_shadow():
    """Soft elliptical ambient-occlusion blob placed under cars."""
    w, h = 128, 256
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx = (xx - w / 2 + 0.5) / (w / 2)
    dy = (yy - h / 2 + 0.5) / (h / 2)
    r = np.sqrt((dx * 1.05) ** 4 + dy ** 4) ** 0.5
    a = np.clip(1 - r, 0, 1) ** 1.6
    img = np.stack([np.zeros_like(a)] * 3 + [a * 0.85], -1)
    save(Image.fromarray((img * 255).astype(np.uint8), "RGBA"), "car_shadow.png")


def line_paint():
    """Worn road-marking paint: white with grit and wear (alpha = paint coverage)."""
    w, h = 64, 512
    n = fbm(w, h, 4, 32, 21)
    cover = np.clip((n - 0.18) * 3.0, 0, 1)
    img = np.stack([np.full_like(n, 0.93), np.full_like(n, 0.93), np.full_like(n, 0.9), cover], -1)
    save(Image.fromarray((img * 255).astype(np.uint8), "RGBA"), "line_paint.png")


def main():
    print("PBR processing")
    for s in SETS:
        process_set(*s)
    road_wear()
    paint_flake()
    kerb_normal()
    contact_shadow()
    line_paint()
    with open(os.path.join(ROOT, "Assets_Source", "Downloaded", "LICENSES.md"), "w", encoding="utf-8") as f:
        f.write("# Third-party assets\n\nAll material sets in `ambientcg/` come from https://ambientcg.com and are licensed\n"
                "**CC0 1.0 Universal** (public domain): free for commercial use, no attribution required.\n\n")
        for s in SETS:
            f.write(f"- {s[0]} → `Resources/Textures/pbr/{s[1]}_*`\n")
    print("done")


if __name__ == "__main__":
    main()


def foliage():
    """Tree canopy cluster (alpha cut-out) for crossed-quad street trees."""
    from PIL import ImageDraw as _D
    w = h = 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = _D.Draw(img)
    r2 = np.random.default_rng(31)
    for _ in range(2600):
        x, y = r2.normal(w / 2, w * 0.2), r2.normal(h * 0.45, h * 0.18)
        if (x - w / 2) ** 2 / (w * 0.42) ** 2 + (y - h * 0.45) ** 2 / (h * 0.4) ** 2 > 1:
            continue
        g = int(r2.integers(70, 140))
        col = (int(g * 0.45), g, int(g * 0.3), 255)
        rr = int(r2.integers(5, 13))
        d.ellipse([x - rr, y - rr * 0.7, x + rr, y + rr * 0.7], fill=col)
    d.line([(w / 2, h * 0.6), (w / 2, h)], fill=(70, 55, 40, 255), width=14)
    save(img, "foliage.png")


def signs():
    """Traffic-sign atlas (4 cells): no parking, speed limit 50, pedestrian crossing, keep right."""
    from PIL import ImageDraw as _D, ImageFont as _F
    cell = 256
    img = Image.new("RGBA", (cell * 4, cell), (0, 0, 0, 0))
    d = _D.Draw(img)
    f = _F.truetype(r"C:\Windows\Fonts\bahnschrift.ttf", 110)
    # no parking
    d.ellipse([8, 8, cell - 8, cell - 8], fill=(30, 70, 170, 255), outline=(210, 20, 30, 255), width=26)
    d.line([(50, 50), (cell - 50, cell - 50)], fill=(210, 20, 30, 255), width=26)
    # speed 50
    x0 = cell
    d.ellipse([x0 + 8, 8, x0 + cell - 8, cell - 8], fill=(250, 250, 250, 255), outline=(210, 20, 30, 255), width=26)
    tw = d.textlength("50", font=f)
    d.text((x0 + (cell - tw) / 2, 58), "50", font=f, fill=(15, 15, 15, 255))
    # pedestrian crossing (blue square, white triangle)
    x0 = cell * 2
    d.rectangle([x0 + 12, 12, x0 + cell - 12, cell - 12], fill=(30, 90, 190, 255))
    d.polygon([(x0 + cell / 2, 40), (x0 + cell - 40, cell - 50), (x0 + 40, cell - 50)], fill=(250, 250, 250, 255))
    d.ellipse([x0 + cell / 2 - 14, 110, x0 + cell / 2 + 14, 138], fill=(20, 20, 20, 255))
    d.line([(x0 + cell / 2, 138), (x0 + cell / 2 - 10, 185)], fill=(20, 20, 20, 255), width=12)
    # keep right
    x0 = cell * 3
    d.ellipse([x0 + 8, 8, x0 + cell - 8, cell - 8], fill=(30, 90, 190, 255))
    d.polygon([(x0 + 70, 70), (x0 + 175, 175), (x0 + 175, 110), (x0 + 195, 110), (x0 + 195, 195), (x0 + 110, 195), (x0 + 110, 175), (x0 + 160, 175), (x0 + 60, 80)], fill=(250, 250, 250, 255))
    save(img, "signs.png")


if __name__ == "__main__":
    foliage()
    signs()
