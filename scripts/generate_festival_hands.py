"""Original always-visible first-person arm source for the festival prototype."""
import bpy
import json
import os
from pathlib import Path
from mathutils import Vector, Euler

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


def part(name, pos, size, color, kind="sphere", rotation=(0, 0, 0), poses=None):
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    elif kind == "tube":
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=1, depth=1, location=pos, rotation=rotation)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1, location=pos, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    unit_vertices = [v.co.copy() for v in obj.data.vertices]
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = kind != "cube"
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in uv.data:
        loop.uv = ((color + .5) / 8, .5)
    if poses:
        obj.shape_key_add(name="Basis")
        for pose, (target_pos, target_size, target_rotation) in poses.items():
            key = obj.shape_key_add(name=pose)
            orientation = Euler(target_rotation).to_matrix()
            offset = Vector(target_pos)-Vector(pos)
            for vertex, unit in zip(key.data, unit_vertices):
                vertex.co = offset + orientation @ Vector(tuple(unit[i]*target_size[i] for i in range(3)))
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


def segment_transform(a, b, radius):
    a, b = Vector(a), Vector(b)
    return ((a+b)/2, (radius, radius, (b-a).length/2+radius*.45),
            (b-a).to_track_quat("Z", "Y").to_euler())


def finger_chain(label, paths, radius):
    base = paths["Rest"]
    for segment in range(3):
        position, size, rotation = segment_transform(base[segment],base[segment+1],radius)
        poses = {name:segment_transform(points[segment],points[segment+1],radius)
                 for name,points in paths.items() if name!="Rest"}
        part(label+str(segment),position,size,0,rotation=rotation,poses=poses)


def hands(shape):
    width = (.85, 1, 1.16)[shape]
    # The FBX forward-axis conversion mirrors source X in the Unity camera view.
    for side, x in (("L", 1), ("R", -1)):
        part("tapered forearm " + side, (x*.36, -.55, -.36), (.082*width, .30, .095), 0)
        part("wrist " + side, (x*.34, -.75, -.30), (.075*width, .09, .075), 0)
        # A smaller palm leaves the articulated phalanges visible from the camera.
        part("palm " + side, (x*.32, -.798, -.26), (.095*width, .091, .061), 0)
        def point(dx,dy,dz):
            return (x*(.32+dx*width),-.84+dy,-.268+dz)
        for finger in range(4):
            across=(finger-1.5)*.045
            level=.067-finger*.044
            rest=[point(across,.005,.022),point(across,-.075,.008),
                  point(across,-.105,-.020),point(across,-.087,-.049)]
            rod=[point(.069,.027,level),point(.070,-.040,level),
                 point(.012,-.067,level),point(-.035,-.035,level)]
            paths={"Rest":rest,"Rod"+side:rod}
            if side=="R":
                paths["BagR"]=[point(across,.006,.047),point(across,-.054,.027),
                               point(across,-.054,-.027),point(across,-.005,-.052)]
                # The tin centre is 14 cm forward in source space; fingers wrap its rim.
                paths["TinR"]=[point(.072,.027,level),point(.138,-.029,level),
                               point(.150,-.120,level),point(.106,-.203,level)]
                paths["PaperR"]=[point(across,.005,.016),point(across,-.069,-.005),
                                 point(across,-.096,-.034),point(across,-.077,-.045)]
            finger_chain("Articulated finger "+side+str(finger),paths,.021*width)
        rest=[point(-.070,.028,.006),point(-.116,-.005,.015),
              point(-.130,-.055,.015),point(-.113,-.085,.003)]
        paths={"Rest":rest,"Rod"+side:[point(-.070,.025,.010),point(-.079,-.023,.006),
                                      point(-.044,-.063,.012),point(.004,-.070,.028)]}
        if side=="R":
            paths["BagR"]=[point(-.070,.020,.005),point(-.110,-.022,.002),
                            point(-.070,-.054,-.003),point(-.026,-.060,-.002)]
            paths["TinR"]=[point(-.070,.026,.010),point(-.129,-.024,.012),
                            point(-.147,-.096,.025),point(-.119,-.150,.038)]
            paths["PaperR"]=[point(-.070,.020,.005),point(-.106,-.025,.016),
                              point(-.092,-.068,.004),point(-.051,-.075,-.012)]
        finger_chain("Opposing thumb "+side,paths,.026*width)


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

grip_names = ["RodL", "RodR", "BagR", "TinR", "PaperR"]
for obj in bpy.data.objects:
    if obj.name.startswith("HandsSkin_"):
        assert obj.data.shape_keys is not None, "Hand grip shapes missing"
        assert set(grip_names).issubset(obj.data.shape_keys.key_blocks.keys()), "Incomplete hand grip set"

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
    "gripShapes": ["RodL", "RodR", "BagR", "TinR", "PaperR"],
    "visible": "Whenever the local player camera is active in a round",
}, indent=2) + "\n")
print("FESTIVAL FIRST PERSON HANDS EXPORTED")
