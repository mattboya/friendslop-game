"""Renders the people review sheets from the runtime FestivalCharacter.fbx in headless Blender.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python scripts/render_people_sheets.py
    ... --python scripts/render_people_sheets.py -- --only roles,roles-back --out artifacts/people-sheets-try

The visibility rules copy FestivalCharacter.KeepMesh and the colours copy FestivalAppearance: change them here when those change.
"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FBX = ROOT / "Assets/Festival/Art/Resources/FestivalCharacter.fbx"
OUT = ROOT / "artifacts/people-sheets"

# FestivalAppearance's palettes (sRGB), in its order.
SKIN = [(.32,.19,.15),(.43,.25,.18),(.55,.34,.23),(.68,.44,.30),(.77,.54,.37),(.84,.65,.48),(.91,.76,.61),(.96,.84,.71)]
HAIR = [(.10,.07,.07),(.23,.12,.09),(.41,.23,.12),(.66,.43,.19),(.85,.68,.35),(.85,.83,.73),(.78,.29,.13),(.22,.77,.69),(.81,.31,.66),(.39,.39,.85)]
CLOTH = [(.94,.32,.42),(.19,.79,.69),(.95,.67,.24),(.64,.40,.84),(.26,.49,.84),(.83,.78,.48),(.27,.36,.29),(.80,.48,.31),(.93,.83,.68),(.18,.22,.33)]
ACCENT = [(.99,.74,.20),(.22,.92,.78),(.98,.45,.58),(.52,.37,.93),(.95,.94,.72),(.33,.73,.98),(.91,.49,.24),(.30,.36,.40)]
# FestivalAppearance.SleeveTint and palette slot 7 for the roles that fix them.
ROLE_SLEEVE = {"Friend": (.99,.44,.22), "Security": (.30,.48,.72)}
ROLE_ACCENT = {"Medic": (.20,.92,.71)}

BASE = dict(gender=0, shape=1, face=1, shirt=1, pants=1, shoes=0, hairstyle=1, skin=4, haircolor=2, shirtcolor=1, pantscolor=9,
            shoecolor=1, accent=0)
WOMAN = dict(gender=1, face=4)
BODY_SHOT = dict(gap=1.35, target_z=1.05, vis_h=2.95, label_z=-.3, w=1800, h=900)
HEAD_SHOT = dict(gap=.65, target_z=1.8, vis_h=.86, label_z=1.43, w=1800, h=420)
EXPRESSIONS = [({}, False, "neutral"), ({"Smile": 1}, False, "Smile 100"), ({"Smile": .65}, False, "dance 65"),
               ({"Concern": .85}, False, "Concern 85"), ({"Smile": -.8, "Concern": -.6, "Blink": .4}, False, "scowl (turned)"),
               ({"Blink": 1}, False, "Blink"), ({"WideIntoxicatedEyes": 1}, False, "Wide"), ({}, True, "red eyes")]
ROLES = ["Security", "Medic", "Friend", "Vendor0", "Vendor1", "Vendor2"]


def people(looks, labels=None, **common):
    return [dict(BASE, **common, **look, **({"label": labels[i]} if labels else {})) for i, look in enumerate(looks)]


def sheets():
    bodies = [dict(shape=s) for s in range(3)] + [dict(WOMAN, shape=s) for s in range(3)]
    body_labels = [f"g{g} {n}" for g in (0, 1) for n in ("lanky", "average", "stocky")]
    shirts = [dict(shirt=v, shirtcolor=v) for v in range(4)]
    accessories = [dict(accessory=v, accent=a) for v, a in enumerate((1, 2, 3, 6))]
    hair = [dict(hairstyle=v, haircolor=c) for v, c in enumerate((0, 3, 6, 8))]
    faces = [dict(face=f, hairstyle=h, skin=s, haircolor=c) for f, h, s, c in
             zip(range(6), (0, 1, 2, 3, 0, 1), (1, 3, 5, 7, 2, 6), (0, 2, 4, 6, 8, 1))]
    expressions = [dict(keys=keys, red=red) for keys, red, _ in EXPRESSIONS]
    headgear = ([dict(headgear=v, hairstyle=v, accent=v) for v in range(4)]
                + [dict(WOMAN, headgear=v, hairstyle=3 - v, accent=4 + v) for v in range(4)])
    return {
        "bodies": dict(BODY_SHOT, actors=people(bodies, body_labels)),
        "bodies-side": dict(BODY_SHOT, view="front", actors=people(bodies, yaw=90)),
        "shirts-front": dict(BODY_SHOT, actors=people(shirts, [f"Shirt_{v}" for v in range(4)])),
        "shirts-back": dict(BODY_SHOT, view="back", actors=people(shirts)),
        "pants": dict(BODY_SHOT, view="three", actors=people(
            [dict(pants=v, pantscolor=c) for v, c in enumerate((9, 4, 0, 3))], [f"Pants_{v}" for v in range(4)])),
        "shoes": dict(BODY_SHOT, view="three", actors=people(
            [dict(shoes=v, shoecolor=c) for v, c in enumerate((1, 0, 2, 3))], [f"Shoes_{v}" for v in range(4)])),
        "headgear": dict(BODY_SHOT, view="three", actors=people(
            headgear, [f"Headgear_{v}" for v in range(4)] + [f"W Headgear_{v}" for v in range(4)])),
        "sunglasses": dict(BODY_SHOT, view="three", actors=people(
            [dict(sunglasses=v, accent=a) for v, a in enumerate((0, 2, 3, 5))], [f"Sunglasses_{v}" for v in range(4)])),
        "facialhair": dict(BODY_SHOT, view="three", actors=people(
            [dict(facialhair=v, haircolor=c) for v, c in enumerate((0, 2, 4, 5))], [f"FacialHair_{v}" for v in range(4)])),
        "accessories": dict(BODY_SHOT, view="three", actors=people(accessories, [f"Accessory_{v}" for v in range(4)])),
        "accessories-back": dict(BODY_SHOT, view="back", actors=people(accessories)),
        "hair-men": dict(BODY_SHOT, view="three", actors=people(hair, [f"M Hairstyle_{v}" for v in range(4)])),
        "hair-women": dict(BODY_SHOT, view="three", actors=people(hair, [f"W Hairstyle_{v}" for v in range(4)], **WOMAN)),
        "hair-back": dict(BODY_SHOT, view="back", actors=people(hair) + people(hair, **WOMAN)),
        "faces-men": dict(HEAD_SHOT, actors=people(faces, [f"Face_0_{f}" for f in range(6)])),
        "faces-women": dict(HEAD_SHOT, actors=people(faces, [f"Face_1_{f}" for f in range(6)], gender=1)),
        "expr-scruffy": dict(HEAD_SHOT, w=2200, actors=people(expressions, [n for _, _, n in EXPRESSIONS])),
        "expr-deadpan": dict(HEAD_SHOT, w=2200, actors=people(expressions, [n for _, _, n in EXPRESSIONS], **WOMAN)),
        "roles": dict(BODY_SHOT, view="three", actors=people([dict(role=r) for r in ROLES], ROLES)),
        "roles-back": dict(BODY_SHOT, view="back", actors=people([dict(role=r) for r in ROLES])),
    }


def parse(argv):
    # Blender passes the arguments after its own '--' through to the script.
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    fbx, out, only, dry = FBX, OUT, None, False
    while args:
        flag = args.pop(0)
        if flag == "--dry-run":
            dry = True
        elif flag in ("--out", "--only", "--fbx") and args:
            value = args.pop(0)
            if flag == "--out":
                out = Path(value) if Path(value).is_absolute() else ROOT / value
            elif flag == "--fbx":
                fbx = Path(value)
            else:
                only = [n for n in value.split(",") if n]
        else:
            raise SystemExit(f"render_people_sheets: unknown argument {flag!r} (use --out DIR, --only a,b, --fbx PATH, --dry-run)")
    every = sheets()
    unknown = [n for n in only or [] if n not in every]
    if unknown:
        raise SystemExit(f"render_people_sheets: no sheet {', '.join(unknown)}; the sheets are {', '.join(every)}")
    return fbx, out, [(n, every[n]) for n in (only or every)], dry


def keep(name, a):
    if name.startswith("Body_"):
        return name == f"Body_{a['gender']}_{a['shape']}"
    if name.startswith("Face_"):
        return name == f"Face_{a['gender']}_{a['face']}"
    for slot in ("Shirt", "Pants", "Shoes", "Headgear", "Sunglasses", "FacialHair", "Hairstyle", "Accessory"):
        if name.startswith(slot + "_"):
            return name == f"{slot}_{a.get(slot.lower(), -1)}"
    if name.startswith("HairTop_"):
        return name == f"HairTop_{a.get('hairstyle', -1)}" and a.get("headgear", -1) < 0
    if name == "HairUnderHat":
        return a.get("headgear", -1) in (2, 3)
    if name.startswith("Role_"):
        return name == "Role_" + a.get("role", "Attendee")
    return False


def render(fbx, path, spec):
    import bpy
    from mathutils import Vector

    def linear(c):
        return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)

    def actor(a, x):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(fbx))
        new = [o for o in bpy.data.objects if o not in before]
        root = [o for o in new if o.type == "ARMATURE"][0]
        while root.parent:
            root = root.parent
        root.location.x += x
        # FestivalAppearance.Scale is (x, height, depth); Blender's up is z.
        sc = [(.87, 1.07, .88), (1, 1, 1), (1.13, .94, 1.12)][a["shape"]]
        root.scale = (root.scale.x * sc[0], root.scale.y * sc[2], root.scale.z * sc[1])
        root.rotation_euler.z += math.radians(a.get("yaw", 0))
        role = a.get("role", "Attendee")
        cols = [SKIN[a["skin"]], ROLE_SLEEVE.get(role, CLOTH[a["shirtcolor"]]), CLOTH[a["pantscolor"]], CLOTH[a["shoecolor"]],
                (.98, .95, .82), (.035, .04, .08), HAIR[a["haircolor"]], ROLE_ACCENT.get(role, ACCENT[a["accent"]])]
        mats = []
        for c in cols:
            m = bpy.data.materials.new("palette")
            m.diffuse_color = (*linear(c), 1)
            mats.append(m)
        # A garment's slot 7 is its seams: 18% of the way from the shirt colour to the accent (CreatePalette garment).
        seam = bpy.data.materials.new("garment accent")
        seam.diffuse_color = (*linear(tuple(s + (c - s) * .18 for s, c in zip(cols[1], cols[7]))), 1)
        sclera = bpy.data.materials.new("sclera")
        sclera.diffuse_color = (*linear((1, .46, .44) if a.get("red") else (.98, .95, .88)), 1)
        for o in new:
            if o.type != "MESH":
                continue
            base = o.name.split(".")[0]
            o.hide_render = not keep(base, a)
            if o.hide_render:
                continue
            if o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:
                    if k.name == "Basis":
                        continue
                    k.slider_min = -1
                    k.value = 1 if k.name == f"Fit_{a['gender']}_{a['shape']}" else a.get("keys", {}).get(k.name, 0)
            # The palette is eight colour columns: each face's first UV picks its slot, as in the game.
            uv = o.data.uv_layers.active
            old = [p.material_index for p in o.data.polygons]
            slot = [min(7, int(uv.data[p.loop_indices[0]].uv.x * 8)) for p in o.data.polygons]
            face = base.startswith("Face_")
            garment = base.startswith(("Shirt_", "Pants_"))
            o.data.materials.clear()
            for m in mats + [sclera, seam]:
                o.data.materials.append(m)
            for p, i, m0 in zip(o.data.polygons, slot, old):
                p.material_index = 8 if face and m0 == 1 else 9 if garment and i == 7 else i

    def label(text, x, z, cam):
        cu = bpy.data.curves.new("label", "FONT")
        cu.body, cu.align_x, cu.size = text, "CENTER", .16
        ob = bpy.data.objects.new("label", cu)
        bpy.context.scene.collection.objects.link(ob)
        ob.location, ob.rotation_euler = (x, 0, z), cam.rotation_euler
        m = bpy.data.materials.new("label")
        m.diffuse_color = (.9, .9, .9, 1)
        cu.materials.append(m)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.show_cavity, sh.cavity_type, sh.show_shadows = "STUDIO", "MATERIAL", True, "BOTH", True
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("review")
    scene.world.color = (.05, .06, .08)
    sh.background_type = "WORLD"
    cd = bpy.data.cameras.new("review")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("review", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    gap, n = spec["gap"], len(spec["actors"])
    for i, a in enumerate(spec["actors"]):
        actor(a, (i - (n - 1) / 2) * gap)
    tz = spec["target_z"]
    # The people face -Y.
    d = {"front": (0, -8, 0), "three": (3.5, -8, 1.5), "back": (0, 8, 0), "side": (8, 0, 0)}[spec.get("view", "front")]
    cam.location = Vector((d[0], d[1], tz + d[2]))
    cam.rotation_euler = (Vector((0, 0, tz)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    w, h = spec["w"], spec["h"]
    cd.ortho_scale = max(n * gap, spec["vis_h"] * w / h)
    for i, a in enumerate(spec["actors"]):
        if a.get("label"):
            label(a["label"], (i - (n - 1) / 2) * gap, spec["label_z"], cam)
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    fbx, out, chosen, dry = parse(sys.argv)
    for name, spec in chosen:
        path = out / f"{name}.png"
        print(f"people sheet {name}: {len(spec['actors'])} people, {spec.get('view', 'front')} view -> {path}")
        if not dry:
            out.mkdir(parents=True, exist_ok=True)
            render(fbx, path, spec)
