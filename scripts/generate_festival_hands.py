"""Original always-visible first-person arm source for the festival prototype."""
import bpy
import json
import os
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / os.environ.get("FESTIVAL_ASSET_STAGE", "artifacts/asset-staging/manual")
SOURCE = STAGE / "ArtSource"
OUT = STAGE / "Resources"
SOURCE.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

mat = bpy.data.materials.new("FestivalPalette")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Roughness"].default_value = .9
image = bpy.data.images.load(str(ROOT / "Assets/Festival/Art/Resources/FestivalPalette.png"))
texture = mat.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = image
texture.interpolation = "Closest"
mat.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
objects = []


def part(name, pos, size, color, kind="sphere", rotation=(0, 0, 0)):
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    elif kind == "tube":
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=1, depth=1, location=pos, rotation=rotation)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = kind != "cube"
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in uv.data:
        loop.uv = ((color + .5) / 8, .5)
    objects.append(obj)
    obj.select_set(False)


def group(name, build):
    start = len(objects)
    build()
    current = objects[start:]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in current:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = current[0]
    if len(current) > 1:
        bpy.ops.object.join()
    current[0].name = name
    current[0].data.name = name + "Mesh"
    current[0].select_set(False)


def hands(shape):
    width = (.85, 1, 1.16)[shape]
    for side, x in (("L", -1), ("R", 1)):
        part("tapered forearm " + side, (x*.36, -.55, -.36), (.082*width, .30, .095), 0)
        part("wrist " + side, (x*.34, -.75, -.30), (.075*width, .09, .075), 0)
        part("palm " + side, (x*.32, -.84, -.26), (.113*width, .135, .078), 0)
        part("thumb base " + side, (x*.215, -.82, -.25), (.048*width, .075, .055), 0)
        part("thumb tip " + side, (x*.19, -.91, -.23), (.036*width, .070, .038), 0)
        for finger in range(4):
            offset=(finger-1.5)*.049*width
            length=.059-(abs(finger-1.5)*.006)
            part("curled finger " + side + str(finger), (x*.32+offset, -.927, -.257),
                 (.028*width, length, .032), 0)


def sleeves(variant):
    # Sleeve tubes share the forearm axis (x=.36, z=-.36) and swallow its
    # rear tip at y=-.25, then run back and down toward the unseen shoulder.
    radius = (.12, .14, .105, .13)[variant]
    for side, x in (("L", -1), ("R", 1)):
        front, back = Vector((x * .365, -.31, -.37)), Vector((x * .40, .10, -.62))
        tilt = (back - front).to_track_quat("Z", "Y").to_euler()
        part("sleeve " + side, (front + back) / 2, (radius, radius, (back - front).length), 1, "tube", tilt)
        part("stitched sleeve cuff " + side, front + (back - front).normalized() * .035,
             (radius + .008, radius + .008, .045), 7, "tube", tilt)
        if variant in (1, 3):
            part("outer sleeve patch " + side, (front + back) / 2 + Vector((x * .012, -.014, -.016)),
                 (.060, .084, .008), 7, "cube", tilt)


for variant in range(3):
    group(f"HandsSkin_{variant}", lambda v=variant: hands(v))
for variant in range(4):
    group(f"HandsSleeve_{variant}", lambda v=variant: sleeves(v))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(OUT / "FestivalHands.fbx"), use_selection=True,
    object_types={"MESH"}, bake_anim=False, axis_forward="-Z", axis_up="Y",
    apply_unit_scale=True)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "FestivalHands.blend"))
(SOURCE / "hands-manifest.json").write_text(json.dumps({
    "source": "Original scripted Blender geometry; no external assets",
    "runtime": "Assets/Festival/Art/Resources/FestivalHands.fbx",
    "skinShapes": 3,
    "sleeveVariants": 4,
    "visible": "Whenever the local player camera is active in a round",
}, indent=2) + "\n")
print("FESTIVAL FIRST PERSON HANDS EXPORTED")
