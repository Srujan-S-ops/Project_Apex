"""
Downloads CC0 street-furniture models from Poly Haven (https://polyhaven.com, licence CC0 1.0) and converts
their maps to the project's URP layout.
  models   -> UnityGame/Assets/Resources/Props/<id>.fbx
  textures -> UnityGame/Assets/Resources/Textures/pbr/ph_<id>_{albedo,n,ms}.*
Usage: python fetch_polyhaven.py
"""
import io
import json
import os
import urllib.request

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "UnityGame", "Assets", "Resources", "Props")
TEX = os.path.join(ROOT, "UnityGame", "Assets", "Resources", "Textures", "pbr")
RAW = os.path.join(ROOT, "Assets_Source", "Downloaded", "polyhaven")
IDS = ["street_lamp_01", "street_lamp_02", "concrete_road_barrier", "utility_box_01", "planter_box_01", "fire_hydrant", "shrub_02"]
UA = {"User-Agent": "ProjectApexAssetFetcher/1.0"}


def get(url):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def pick(files, key, res="1k"):
    m = files.get(key, {}).get(res, {})
    for fmt in ("png", "jpg"):
        if fmt in m:
            return m[fmt]["url"]
    return None


def main():
    os.makedirs(MODELS, exist_ok=True)
    os.makedirs(TEX, exist_ok=True)
    os.makedirs(RAW, exist_ok=True)
    for pid in IDS:
        files = json.loads(get(f"https://api.polyhaven.com/files/{pid}"))
        fbx_url = files["fbx"]["1k"]["fbx"]["url"]
        data = get(fbx_url)
        open(os.path.join(MODELS, pid + ".fbx"), "wb").write(data)
        # map keys differ per asset (e.g. fire hydrant has a clean and an aged set; we use the clean one)
        diff = Image.open(io.BytesIO(get(pick(files, "Diffuse")))).convert("RGB")
        size = diff.size
        normal = Image.open(io.BytesIO(get(pick(files, "nor_gl")))).convert("RGB").resize(size)
        rough = np.asarray(Image.open(io.BytesIO(get(pick(files, "Rough")))).convert("L").resize(size), np.float32) / 255
        metal_url = pick(files, "Metal")
        metal = (np.asarray(Image.open(io.BytesIO(get(metal_url))).convert("L").resize(size), np.float32) / 255) if metal_url else np.zeros_like(rough)
        alpha_url = pick(files, "Alpha") or pick(files, "Opacity") if pid.startswith("shrub") else None
        if alpha_url:
            a = Image.open(io.BytesIO(get(alpha_url))).convert("L").resize(size)
            rgba = diff.copy()
            rgba.putalpha(a)
            rgba.save(os.path.join(TEX, f"ph_{pid}_albedo.png"))
        else:
            diff.save(os.path.join(TEX, f"ph_{pid}_albedo.jpg"), quality=92)
        normal.save(os.path.join(TEX, f"ph_{pid}_n.png"))
        ms = np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), 1 - rough], -1)
        Image.fromarray((ms * 255).astype(np.uint8), "RGBA").save(os.path.join(TEX, f"ph_{pid}_ms.png"))
        open(os.path.join(RAW, pid + ".json"), "w").write(json.dumps({"id": pid, "source": f"https://polyhaven.com/a/{pid}", "license": "CC0"}))
        print(f"  {pid}: fbx {len(data) // 1024} KB, maps {size}, alpha={'yes' if alpha_url else 'no'}")
    lic = os.path.join(ROOT, "Assets_Source", "Downloaded", "LICENSES.md")
    with open(lic, "a", encoding="utf-8") as f:
        f.write("\n## Poly Haven models (CC0 1.0, https://polyhaven.com)\n\n")
        for pid in IDS:
            f.write(f"- {pid} → `Resources/Props/{pid}.fbx`, `Resources/Textures/pbr/ph_{pid}_*`\n")
    print("done")


if __name__ == "__main__":
    main()
