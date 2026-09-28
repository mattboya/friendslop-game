"""Stage original, seamless material maps for the festival's near-camera surfaces."""
import json
import os
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / os.environ.get("FESTIVAL_ASSET_STAGE", "artifacts/asset-staging/manual-surfaces")
OUT = STAGE / "Resources"
SOURCE = STAGE / "ArtSource"
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
SIZE = 1024
Y, X = np.mgrid[:SIZE, :SIZE]


def noise(cells, seed):
    """Periodic smooth noise: opposite edges sample the same lattice."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells, cells), dtype=np.float32)
    u = np.arange(SIZE, dtype=np.float32) * cells / SIZE
    i = np.floor(u).astype(np.int32)
    f = u - i
    f = f * f * (3 - 2 * f)
    x0, x1 = i % cells, (i + 1) % cells
    a = grid[np.ix_(x0, x0)]
    b = grid[np.ix_(x1, x0)]
    c = grid[np.ix_(x0, x1)]
    d = grid[np.ix_(x1, x1)]
    return (a * (1-f[:, None]) + b * f[:, None]) * (1-f[None, :]) + \
        (c * (1-f[:, None]) + d * f[:, None]) * f[None, :]


def write(name, rgb):
    rgba = np.empty((SIZE, SIZE, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb, 0, 1)
    rgba[:, :, 3] = 1
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=True)
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = str(OUT / (name + ".png"))
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def neutral(field):
    return np.repeat(field[:, :, None], 3, axis=2)


def canvas():
    warp = np.sin(X * np.pi / 4) ** 8
    weft = np.sin(Y * np.pi / 4) ** 8
    field = .91 + .016*warp + .012*weft + (noise(16, 11)-.5)*.045
    field += (noise(96, 17)-.5)*.018
    write("FestivalCanvas", neutral(field))


def wood():
    bend = (noise(8, 23)-.5)*28 + (noise(32, 29)-.5)*7
    rings = np.sin((X+bend)*np.pi/17)
    field = .90 + .055*rings + (noise(64, 31)-.5)*.025
    for kx, ky in ((197, 300), (714, 790), (913, 135)):
        dx = (X-kx+SIZE/2) % SIZE-SIZE/2
        dy = (Y-ky+SIZE/2) % SIZE-SIZE/2
        radius = (dx/34)**2 + (dy/85)**2
        field -= .10*np.exp(-radius*2.6)*np.cos(np.sqrt(radius)*12)
    write("FestivalWood", neutral(field))


def bark():
    shift = (noise(8, 37)-.5)*17 + (noise(32, 41)-.5)*5
    fissures = np.sin((X+shift)*np.pi/10)
    narrow = np.power(np.maximum(0, -fissures), 9)
    field = .85 - .18*narrow + (noise(64, 43)-.5)*.07
    field += (noise(128, 47)-.5)*.035
    write("FestivalBark", neutral(field))


def leaf():
    field = .88 + (noise(16, 53)-.5)*.12 + (noise(128, 59)-.5)*.07
    rgb = np.stack((field*.95, field, field*.90), axis=2)
    write("FestivalLeaf", rgb)


def dirt():
    field = .89 + (noise(8, 61)-.5)*.075 + (noise(64, 67)-.5)*.055
    field += (noise(256, 71)-.5)*.045
    rgb = np.stack((field, field*.95, field*.88), axis=2)
    write("FestivalDirt", rgb)


def ground():
    broad = noise(8, 73)
    mid = noise(64, 79)
    fine = noise(256, 83)
    brown = np.clip((broad-.54)*1.6, 0, 1)
    field = (mid-.5)*.075 + (fine-.5)*.045
    rgb = np.stack((.36+.08*brown+field,
                    .46-.04*brown+field,
                    .33-.06*brown+field*.75), axis=2)
    rng = np.random.default_rng(89)
    for _ in range(7000):
        px, py = rng.integers(0, SIZE, size=2)
        length = int(rng.integers(3, 9))
        lean = int(rng.integers(-2, 3))
        tint = float(rng.uniform(-.09, .08))
        for step in range(length):
            xx = (px + lean*step//max(1, length)) % SIZE
            yy = (py + step) % SIZE
            rgb[xx, yy] += (tint*.7, tint, tint*.55)
    write("FestivalGround", rgb)


def main():
    canvas(); wood(); bark(); leaf(); dirt(); ground()
    names = ["FestivalCanvas", "FestivalWood", "FestivalBark",
             "FestivalLeaf", "FestivalDirt", "FestivalGround"]
    (SOURCE / "surface-manifest.json").write_text(json.dumps({
        "source": "Original deterministic procedural maps; no external assets",
        "runtimeDirectory": "Assets/Festival/Art/Resources",
        "size": SIZE,
        "textures": [name + ".png" for name in names],
    }, indent=2) + "\n")
    print("FESTIVAL SURFACES STAGED", STAGE)


if __name__ == "__main__":
    main()
