"""Generate original, deliberately goofy modular characters in Blender 5.x.

Run: Blender --background --factory-startup --python scripts/generate_modular_character.py
"""
import bpy
import bmesh
import json
import math
import os
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / os.environ.get("FESTIVAL_ASSET_STAGE", "artifacts/asset-staging/manual")
SOURCE = STAGE / "ArtSource"
OUT = STAGE / "Resources"
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FACE_STYLE = args[args.index("--face-style") + 1] if "--face-style" in args else "both"
if FACE_STYLE not in {"both", "current", "scruffy", "deadpan"}:
    raise ValueError("--face-style must be both, current, scruffy, or deadpan")
if FACE_STYLE in {"scruffy", "deadpan"}:
    SOURCE = ROOT / "ArtSource/FaceComparisons" / FACE_STYLE
    OUT = SOURCE
SOURCE.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


clear()
mat = bpy.data.materials.new("FestivalPalette")
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Roughness"].default_value = 0.9
image = bpy.data.images.load(str(ROOT / "Assets/Festival/Art/Resources/FestivalPalette.png"))
texture = mat.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = image
texture.interpolation = "Closest"
mat.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
eye_mat=bpy.data.materials.new("FestivalEyeWhite")
eye_mat.diffuse_color=(.98,.95,.88,1)
eye_mat.use_nodes=True
eye_mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value=(.98,.95,.88,1)

# Character looks toward Blender -Y, which the FBX exporter maps to Unity +Z.
bones = {
    "Hips": ((0, 0, .88), None),
    "Spine": ((0, 0, 1.13), "Hips"),
    "Head": ((0, 0, 1.65), "Spine"),
    "ArmL": ((-.36, 0, 1.4), "Spine"),
    "ArmR": ((.36, 0, 1.4), "Spine"),
    "ForearmL": ((-.48, 0, 1.05), "ArmL"),
    "ForearmR": ((.48, 0, 1.05), "ArmR"),
    "HandL": ((-.52, -.02, .78), "ForearmL"),
    "HandR": ((.52, -.02, .78), "ForearmR"),
    "LegL": ((-.19, 0, .84), "Hips"),
    "LegR": ((.19, 0, .84), "Hips"),
    "ShinL": ((-.2, 0, .46), "LegL"),
    "ShinR": ((.2, 0, .46), "LegR"),
}
arm = bpy.data.armatures.new("FestivalRig")
rig = bpy.data.objects.new("FestivalRig", arm)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for name, (head, parent) in bones.items():
    bone = arm.edit_bones.new(name)
    bone.head = head
    bone.tail = Vector(head) + Vector((0, 0, .18))
    if parent:
        bone.parent = arm.edit_bones[parent]
bpy.ops.object.mode_set(mode="OBJECT")
rig.select_set(False)

parts = []
groups = []


def piece(label, position, size, color, bone, kind="sphere", sides=20, rotation=(0, 0, 0)):
    if kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    elif kind == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=1, depth=1, location=position)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=sides, ring_count=12, radius=1, location=position)
    obj = bpy.context.object
    obj.name = label
    # `size` is a half extent for every primitive. Blender cubes have unit
    # dimensions, spheres have unit radii, and cylinders have unit depth.
    obj.scale = ((size[0] * 2, size[1] * 2, size[2] * 2) if kind == "cube"
                 else (size[0], size[1], size[2] * 2) if kind == "cylinder"
                 else size)
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if kind == "cube":
        bevel = obj.modifiers.new("Authored soft edge", "BEVEL")
        bevel.width = min(min(size) * .48, .035)
        bevel.segments = 3
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        normal = obj.modifiers.new("Face normals", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normal.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = kind != "cube"
    obj.data.materials.append(eye_mat if label=="eye" else mat)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
    for loop in uv.data:
        loop.uv = ((color + .1 + loop.uv.x * .8) / 8, loop.uv.y)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
    if label in {"eye", "iris", "pupil", "catchlight"}:
        obj.vertex_groups.new(name="FaceBlink").add(list(range(len(obj.data.vertices))),1,"REPLACE")
        obj.vertex_groups.new(name={"eye":"IntoxSclera", "iris":"IntoxIris", "pupil":"IntoxPupil", "catchlight":"IntoxCatchlight"}[label]).add(list(range(len(obj.data.vertices))),1,"REPLACE")
    parts.append(obj)
    obj.select_set(False)



def surface(label, vertices, faces, color, bone, weights=None):
    mesh = bpy.data.meshes.new(label + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(label, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new()
    for poly in mesh.polygons:
        poly.use_smooth = True
        for loop_index in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = ((color + .1 + ((co.x * 1.7) % 1) * .8) / 8, co.z % 1)
    if weights is None:
        obj.vertex_groups.new(name=bone).add(list(range(len(vertices))), 1, "REPLACE")
    else:
        for index, values in enumerate(weights):
            for name, value in values.items():
                if value <= .0001:
                    continue
                group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
                group.add([index], value, "REPLACE")
    if label=="sculpted eyelid":
        obj.vertex_groups.new(name="FaceBlink").add(list(range(len(vertices))),1,"REPLACE")
        obj.vertex_groups.new(name="IntoxUpperLid").add(list(range(len(vertices))),1,"REPLACE")
    if label=="lower eyelid":
        obj.vertex_groups.new(name="IntoxLowerLid").add(list(range(len(vertices))),1,"REPLACE")
    parts.append(obj)
    return obj


def refine(obj):
    bpy.context.view_layer.objects.active=obj
    modifier=obj.modifiers.new("Curvature refinement", "SUBSURF")
    modifier.levels=1
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def loft(label, profiles, color, bone, center=(0, 0), sides=28, pleats=0, power=1):
    # Explicit connected rings: (height, width radius, depth radius, forward offset).
    vertices = []
    for z, rx, ry, cy in profiles:
        for i in range(sides):
            angle = math.tau * i / sides
            fold = 1 + pleats * math.cos(angle * 10)
            vertices.append((center[0] + rx * math.copysign(abs(math.cos(angle))**power, math.cos(angle)) * fold,
                             center[1] + cy + ry * math.copysign(abs(math.sin(angle))**power, math.sin(angle)) * fold, z))
    faces = []
    for row in range(len(profiles)-1):
        for i in range(sides):
            j = (i+1) % sides
            faces.append((row*sides+i, row*sides+j, (row+1)*sides+j, (row+1)*sides+i))
    faces.extend((tuple(reversed(range(sides))), tuple((len(profiles)-1)*sides+i for i in range(sides))))
    return surface(label, vertices, faces, color, bone)


def cord(label, points, radius, color, bone, sides=8):
    vertices = []
    for i, point in enumerate(points):
        direction = Vector(points[min(i+1,len(points)-1)]) - Vector(points[max(0,i-1)])
        direction.normalize()
        tangent = direction.cross(Vector((0,1,0)))
        if tangent.length < .01:
            tangent = direction.cross(Vector((0,0,1)))
        tangent.normalize()
        normal = direction.cross(tangent).normalized()
        for j in range(sides):
            angle = math.tau*j/sides
            vertices.append(tuple(Vector(point)+radius*(math.cos(angle)*tangent+math.sin(angle)*normal)))
    faces = []
    for row in range(len(points)-1):
        for j in range(sides):
            k=(j+1)%sides
            faces.append((row*sides+j,row*sides+k,(row+1)*sides+k,(row+1)*sides+j))
    faces.extend((tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))))
    return surface(label, vertices, faces, color, bone)


_skin = []


def skin_tree():
    """Head skin of the average body; every head shape shares it at fit 1."""
    if not _skin:
        body = bpy.data.objects["Body_0_1"]
        bm = bmesh.new()
        bm.from_mesh(body.data)
        bm.transform(body.matrix_world)
        _skin.append(BVHTree.FromBMesh(bm))
        bm.free()
    return _skin[0]


def on_skin(x, z, depth, embed=.5):
    """Center for a part at (x, z) whose back sinks `embed` of `depth` into the skin."""
    hit, normal, _, _ = skin_tree().ray_cast(Vector((x, -1, z)), Vector((0, 1, 0)))
    return tuple(hit + normal * depth * (1 - embed))


def near_skin(point, depth, embed=.5):
    hit, normal, _, _ = skin_tree().find_nearest(Vector(point))
    return tuple(hit + normal * depth * (1 - embed))


def seated(points, radius, embed=.4):
    """Project cord points onto the face so brows and lips sit on the skin."""
    return [on_skin(x, z, radius, embed) for x, _, z in points]


def face_points():
    return [obj.matrix_world @ v.co for obj in groups if obj.name.startswith("Face_")
            for v in obj.data.vertices]


def limb(label, profiles, color, upper, lower, joint, broad=1):
    # Continuous skin across the joint, with a measured 12 cm weight transition.
    vertices, weights, faces = [], [], []
    sides=20
    for x,y,z,rx,ry in profiles:
        blend=max(0,min(1,(joint+.06-z)/.12))
        for i in range(sides):
            angle=math.tau*i/sides
            vertices.append((x+rx*broad*math.cos(angle),y+ry*math.sin(angle),z))
            weights.append({upper:1-blend,lower:blend})
    for row in range(len(profiles)-1):
        for i in range(sides):
            j=(i+1)%sides
            faces.append((row*sides+j,row*sides+i,(row+1)*sides+i,(row+1)*sides+j))
    faces.extend((tuple(range(sides)),tuple(reversed([(len(profiles)-1)*sides+i for i in range(sides)]))))
    return refine(surface(label,vertices,faces,color,upper,weights))


def ring(label, z_low, z_high, outer, inner, color, bone="Head", sides=28):
    """One continuous annular band with an explicit inner clearance."""
    verts = []
    for z, radius in ((z_low, outer), (z_high, outer),
                      (z_low, inner), (z_high, inner)):
        for i in range(sides):
            angle = math.tau * i / sides
            verts.append((radius[0] * math.cos(angle),
                          radius[1] * math.sin(angle), z))
    faces = []
    for i in range(sides):
        j = (i + 1) % sides
        faces.extend(((i, j, sides + j, sides + i),
                      (2*sides + j, 2*sides + i, 3*sides + i, 3*sides + j),
                      (sides + i, sides + j, 3*sides + j, 3*sides + i),
                      (j, i, 2*sides + i, 2*sides + j)))
    mesh = bpy.data.meshes.new(label + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(label, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.new()
    for loop in uv.data:
        loop.uv = ((color + .5) / 8, .5)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(mesh.vertices))), 1, "REPLACE")
    parts.append(obj)
    return obj


def tapered_shell(label, z_bottom, z_top, bottom, top, color, bone="Hips", sides=40):
    """A connected A-line skirt with a thin inner wall and seated waistband."""
    inset = .018
    profiles = ((z_bottom, bottom), (z_top, top),
                (z_bottom, (bottom[0] - inset, bottom[1] - inset)),
                (z_top, (top[0] - inset, top[1] - inset)))
    verts = []
    for z, radius in profiles:
        for index in range(sides):
            angle = math.tau * index / sides
            verts.append((radius[0] * math.cos(angle),
                          radius[1] * math.sin(angle), z))
    faces = []
    for index in range(sides):
        j = (index + 1) % sides
        faces.extend(((index, j, sides + j, sides + index),
                      (2*sides + j, 2*sides + index, 3*sides + index, 3*sides + j),
                      (sides + index, sides + j, 3*sides + j, 3*sides + index),
                      (j, index, 2*sides + index, 2*sides + j)))
    mesh = bpy.data.meshes.new(label + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(label, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new()
    for loop in uv.data:
        loop.uv = ((color + .5) / 8, .5)
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(mesh.vertices))), 1, "REPLACE")
    parts.append(obj)
    return obj


def fit_keys(obj):
    """Six authored-width fits share one renderer per wardrobe slot."""
    category = obj.name.split("_")[0]
    if category not in {"Face", "Shirt", "Pants", "Shoes", "Headgear",
                        "Sunglasses", "FacialHair", "Hairstyle", "HairTop", "HairUnderHat",
                        "Accessory", "Role", "EyeGlow", "Equipment"}:
        return
    obj.shape_key_add(name="Basis")
    origin_x = obj.location.x
    for gender in range(2):
        for shape in range(3):
            broad = (1.04 if gender == 0 else .96) * (.92 if shape == 0 else 1.12 if shape == 2 else 1)
            head = (.25 + shape * .008) / .258
            factor = head if category in {"Face", "Headgear", "Sunglasses", "FacialHair", "Hairstyle", "HairTop", "HairUnderHat", "EyeGlow"} else broad
            key = obj.shape_key_add(name=f"Fit_{gender}_{shape}")
            for vertex in obj.data.vertices:
                groups = vertex.groups
                bone_name = obj.vertex_groups[groups[0].group].name if groups else ""
                anchor = (-.38 if bone_name == "ArmL" else .38 if bone_name == "ArmR"
                          else -.20 if bone_name in {"LegL", "ShinL"}
                          else .20 if bone_name in {"LegR", "ShinR"} else 0)
                world_x = vertex.co.x + origin_x
                key.data[vertex.index].co.x = anchor + (world_x - anchor) * factor - origin_x

    if category=="Face":
        blink=obj.shape_key_add(name="Blink",from_mix=False)
        mask=obj.vertex_groups.get("FaceBlink")
        if mask is not None:
            for vertex in obj.data.vertices:
                if any(g.group==mask.index for g in vertex.groups):
                    world_z=vertex.co.z+obj.location.z
                    blink.data[vertex.index].co.z=1.973+(world_z-1.973)*.035-obj.location.z

        intox=obj.shape_key_add(name="WideIntoxicatedEyes",from_mix=False)
        sizes={"IntoxSclera":(1.18,1.46),"IntoxIris":(1.43,1.57),
               "IntoxPupil":(1.90,1.88),"IntoxCatchlight":(1.35,1.35)}
        for name,(sx,sz) in sizes.items():
            mask=obj.vertex_groups.get(name)
            if mask is None:continue
            for sign in (-1,1):
                indices=[v.index for v in obj.data.vertices
                         if (v.co.x+obj.location.x)*sign>0 and any(g.group==mask.index for g in v.groups)]
                if not indices:continue
                cx=(min(obj.data.vertices[i].co.x for i in indices)+max(obj.data.vertices[i].co.x for i in indices))*.5
                cz=(min(obj.data.vertices[i].co.z for i in indices)+max(obj.data.vertices[i].co.z for i in indices))*.5
                for i in indices:
                    base=obj.data.vertices[i].co
                    intox.data[i].co.x=cx+(base.x-cx)*sx
                    intox.data[i].co.z=cz+(base.z-cz)*sz
        for name,dz in (("IntoxUpperLid",.042),("IntoxLowerLid",-.014)):
            mask=obj.vertex_groups.get(name)
            if mask:
                for v in obj.data.vertices:
                    if any(g.group==mask.index for g in v.groups):intox.data[v.index].co.z+=dz


def group(name, build):
    start = len(parts)
    build()
    own = parts[start:]
    if not own:
        raise RuntimeError("Empty character group: " + name)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in own:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = own[0]
    if len(own) > 1:
        bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = name
    joined.data.name = name + "Mesh"
    if name.startswith("Face_") and len(joined.data.materials)==2 and joined.data.materials[0]==eye_mat:
        joined.data.materials[0]=mat
        joined.data.materials[1]=eye_mat
        for polygon in joined.data.polygons:
            polygon.material_index=1-polygon.material_index
    modifier = joined.modifiers.new("FestivalRig", "ARMATURE")
    modifier.object = rig
    joined.parent = rig
    fit_keys(joined)
    joined.select_set(False)
    groups.append(joined)


def base_body(gender, shape):
    broad = (1.04 if gender == 0 else .96) * (.92 if shape == 0 else 1.12 if shape == 2 else 1)
    loft("neck",[(1.43,.14,.12,0),(1.54,.115,.11,0),(1.66,.13,.12,0)],0,"Spine")
    loft("anatomical torso",[(.87,.22*broad,.15,0),(1.0,.26*broad,.17,0),(1.14,.285*broad,.185,0),
         (1.31,.31*broad,.185,0),(1.41,.32*broad,.165,0),(1.49,.23*broad,.13,0)],0,"Spine")
    h=(.25+shape*.008)/.258
    refine(loft("sculpted jaw cheek and cranium",[(1.58,.07*h,.09,-.01),(1.63,.15*h,.14,-.005),
         (1.72,.205*h,.19,0),(1.82,.248*h,.218,-.005),(1.92,.26*h,.223,-.01),
         (2.02,.247*h,.215,0),(2.10,.22*h,.19,.015),(2.17,.145*h,.13,.02),(2.195,.025,.035,.02)],0,"Head",sides=32))
    for side,x in (("L",-1),("R",1)):
        piece("ear helix",(x*.258,0,1.85),(.060,.065,.095),0,"Head")
        piece("ear concha",(x*.286,-.048,1.85),(.025,.014,.049),0,"Head")
        limb("continuous arm "+side,[(x*.35,0,1.43,.095,.105),(x*.40,0,1.33,.10,.10),
             (x*.445,0,1.18,.088,.09),(x*.48,0,1.08,.075,.08),(x*.49,-.005,1.02,.077,.08),
             (x*.505,-.015,.90,.07,.075),(x*.52,-.02,.78,.057,.061)],0,"Arm"+side,"Forearm"+side,1.05,broad)
        piece("palm",(x*.52,-.04,.725),(.084*broad,.064,.10),0,"Hand"+side)
        for finger in range(4):
            px=x*.52+(finger-1.5)*.036
            piece("finger",(px,-.052,.65+abs(finger-1.5)*.012),(.020,.032,.060),0,"Hand"+side,sides=12)
        piece("thumb",(x*.435,-.058,.72),(.035,.039,.065),0,"Hand"+side,rotation=(0,x*.45,0))
        limb("continuous leg "+side,[(x*.19,0,.88,.118,.125),(x*.19,0,.72,.119,.122),
             (x*.195,0,.57,.098,.104),(x*.20,-.01,.47,.087,.095),(x*.20,0,.40,.091,.10),
             (x*.20,.012,.28,.083,.085),(x*.20,0,.085,.064,.065)],0,"Leg"+side,"Shin"+side,.46,broad)


def lashes(x, side, lid, ts):
    # Women's pool: three dark flicks on the outer upper lid. They carry the
    # eyelid label so Blink and the wide-eye morph move them with the lid.
    for t in ts:
        base = (x+side*t, -.247, lid(side*t)+.004)
        cord("sculpted eyelid", [base, (base[0]+side*.011, -.252, base[2]+.018)], .0035, 5, "Head")


def face(gender, variant):
    style = ("scruffy" if variant < 3 else "deadpan") if FACE_STYLE == "both" else FACE_STYLE
    variant = variant % 3
    # Gender 1 (women's pool) gets finer arched brows, lashes and a fuller lip.
    fem = gender == 1
    if style != "current":
        if style == "scruffy":
            # Tired, personable adults: smaller eyes, heavier lids, uneven brows,
            # a crooked nose and relaxed mouth. All detail remains on Head.
            width=(.101,.110,.096)[variant]
            for side in (-1,1):
                x=side*width
                z=1.965+(side*.008 if variant==1 else 0)
                piece("eye",(x,-.218,z),(.048,.026,.046),4,"Head",sides=20)
                piece("iris",(x+side*.004,-.245,z-.004),(.023,.008,.026),6,"Head",sides=16)
                piece("pupil",(x+side*.004,-.252,z-.003),(.012,.005,.016),5,"Head",sides=14)
                lid=lambda t,z=z:z+.017+.010*math.sin((t+.05)/.10*math.pi)
                cord("sculpted eyelid",[(x+t,-.244,lid(t)) for t in (-.051,-.034,-.017,0,.017,.034,.051)],.009,0,"Head")
                if fem:lashes(x,side,lid,(.022,.036,.049))
                cord("lower eyelid",[(x+t,-.238,z-.036+.004*abs(t)/.05) for t in (-.045,-.025,0,.025,.045)],.004,0,"Head")
                cord("tired under-eye crease",[(x+t,-.229,z-.060+.006*(t/.055)**2) for t in (-.055,-.028,0,.028,.055)],.0035,5,"Head")
                brow_z=2.031+(side*.009 if variant!=2 else -side*.005)+(.005 if fem else 0)
                brow_r,arch=(.008,.012) if fem else (.011,.007)
                cord("uneven eyebrow",seated([(x+t,0,brow_z+arch*math.sin((t+.056)/.112*math.pi)+side*t*.09) for t in (-.056,-.037,-.018,0,.018,.037,.056)],brow_r),brow_r,6,"Head")
                for dot in range(3):
                    piece("cheek freckle",(side*(.174+dot*.015),-.160+dot*.011,1.826+dot*.010),(.004,.003,.004),5,"Head",sides=8)
            refine(loft("crooked shaped nose",[(1.80,.022,.025,-.232),(1.82,.050,.052,-.260),
                (1.86,.054,.063,-.253),(1.91,.027,.038,-.232),(1.973,.017,.020,-.207)],0,"Head",center=(.010*(variant-1),0),sides=24))
            cord("uneven relaxed mouth",seated([(t,0,1.742+(.004 if t<0 else -.003)) for t in (-.075,-.050,-.025,0,.025,.050,.075)],.005),.005,5,"Head")
            lip=.008 if fem else .006
            cord("soft lower lip",seated([(t,0,1.724) for t in (-.043,-.020,0,.020,.043)],lip),lip,0,"Head")
            return
        # Deadpan comedy: still recognizably human, but the emotional read comes
        # from a small steady gaze, low straight brows, and an almost level mouth.
        width=(.096,.105,.089)[variant]
        for side in (-1,1):
            x=side*width
            z=1.963
            piece("eye",(x,-.219,z),(.043,.024,.036),4,"Head",sides=20)
            piece("iris",(x,-.244,z-.002),(.019,.008,.022),6,"Head",sides=16)
            piece("pupil",(x,-.251,z-.002),(.011,.005,.014),5,"Head",sides=14)
            lid=lambda t,z=z:z+.020+.003*(1-abs(t)/.047)
            cord("sculpted eyelid",[(x+t,-.242,lid(t)) for t in (-.047,-.032,-.016,0,.016,.032,.047)],.008,0,"Head")
            if fem:lashes(x,side,lid,(.018,.030,.042))
            brow_r=.007 if fem else .009
            cord("straight eyebrow",seated([(x+t,0,2.027+side*.002+(.006*(1-(t/.05)**2)+.004 if fem else 0)) for t in (-.050,-.025,0,.025,.050)],brow_r),brow_r,6,"Head")
        refine(loft("subtle nose",[(1.81,.019,.024,-.230),(1.85,.043,.042,-.247),
            (1.90,.027,.038,-.234),(1.96,.017,.018,-.207)],0,"Head",sides=24))
        cord("flat mouth",seated([(t,0,1.741+(.002 if variant==2 else 0)) for t in (-.064,-.040,-.020,0,.020,.040,.064)],.0045),.0045,5,"Head")
        lip=.0055 if fem else .004
        cord("lower lip",seated([(t,0,1.725) for t in (-.035,0,.035)],lip),lip,0,"Head")
        return
    width=(.095,.108,.088)[variant]
    n=(.92,1.12,.80)[variant]
    refine(loft("shaped nose",[(1.795,.025,.024,-.238),(1.814,.060*n,.053,-.259),
         (1.85,.063*n,.070,-.257),(1.89,.034,.043,-.239),(1.95,.026,.031,-.210),
         (1.978,.016,.018,-.204)],0,"Head",sides=24))
    for x in (-1,1):
        piece("eye",(x*width,-.219,1.973),(.067,.046,.075),4,"Head")
        piece("iris",(x*width+.006,-.262,1.978),(.031,.012,.038),6,"Head",sides=16)
        piece("pupil",(x*width+.006,-.273,1.98),(.017,.008,.026),5,"Head",sides=16)
        piece("catchlight",(x*width-.004,-.280,1.993),(.007,.006,.010),4,"Head",sides=12)
        for upper in (True,False):
            angles=[math.pi*i/12+(0 if upper else math.pi) for i in range(13)]
            points=[(x*width+.070*math.cos(a),-.233-.009*math.sin(a),1.973+.075*math.sin(a)) for a in angles]
            cord("sculpted eyelid",points,.008 if upper else .0055,0,"Head")
        points=[(x*width+t,-.218,2.067+.015*math.sin((t+.07)/.14*math.pi)+x*t*.09) for t in (-.072,-.048,-.024,0,.024,.048,.072)]
        cord("expressive eyebrow",seated(points,.015),.015,6,"Head")
    points=[(t,-.209-.018*(1-(t/.077)**2),1.728+.027*(t/.077)**2+variant*t*.05) for t in (-.077,-.055,-.025,0,.025,.055,.077)]
    cord("smile crease",seated(points,.0055),.0055,5,"Head")
    cord("lower lip",seated([(x,y,z-.014) for x,y,z in points[1:-1]],.008),.008,0,"Head")


for gender in range(2):
    for shape in range(3):
        group(f"Body_{gender}_{shape}", lambda g=gender, s=shape: base_body(g, s))
    for variant in range(6 if FACE_STYLE == "both" else 3):
        group(f"Face_{gender}_{variant}", lambda g=gender, v=variant: face(g, v))
FACE_POINTS = face_points()


def shirt(v):
    bottom=(.91,.87,.94,.83)[v]
    width=(.35,.375,.35,.38)[v]
    refine(loft("tailored garment",[(bottom,width*.94,.235,0),(bottom+.035,width,.249,0),
         (1.08,width*.94,.245,0),(1.25,width,.25,0),(1.39,width*1.05,.239,0),
         (1.46,width*.98,.215,0),(1.51,.25,.177,0),(1.56,.156,.133,0)],1,"Spine"))
    ring("neck binding",1.545,1.575,(.162,.143),(.140,.121),7,"Spine",28)
    ring("garment hem",bottom,bottom+.028,(width,.253),(width-.018,.229),1,"Spine",28)
    for side,x in (("L",-1),("R",1)):
        end=1.17 if v!=2 else 1.29
        refine(loft("tailored sleeve",[(end,.111,.132,0),(end+.025,.12,.143,0),(1.36,.135,.15,0),(1.44,.10,.119,0)],1,"Arm"+side,center=(x*.375,0),sides=24))
        for k in range(2):
            cord("sleeve fold",[(x*.375+t,-.13,1.30+k*.055+abs(t)*.16) for t in (-.085,-.045,0,.045,.085)],.006,1,"Arm"+side)
    if v==1:
        piece("folded hood",(0,.165,1.53),(.24,.12,.12),1,"Spine")
        piece("kangaroo pocket",(0,-.249,1.10),(.195,.021,.092),1,"Spine","cube")
        for x in (-.06,.06):
            cord("drawstring",[(x,-.16,1.54),(x*1.1,-.24,1.43),(x*.9,-.266,1.30)],.009,4,"Spine")
    if v in (2,3):
        cord("zipper",[(0,-.258,1.01),(0,-.259,1.25),(0,-.22,1.45),(0,-.15,1.55)],.008,5,"Spine")
        for x in (-.19,.19):
            piece("jacket pocket",(x,-.241,1.16),(.10,.02,.07),1,"Spine","cube")
            cord("pocket welt",[(x-.08,-.264,1.19),(x+.08,-.264,1.19)],.008,7,"Spine")
    else:
        piece("sewn chest patch",(-.16,-.238,1.37),(.062,.012,.047),7,"Spine","cube")
    for x in (-1,1):
        cord("side seam",[(x*width*.83,-.14,bottom+.06),(x*width*.86,-.14,1.16),(x*width*.9,-.14,1.37)],.005,1,"Spine")


def lower(v):
    loft("fitted waistband",[(.79,.305,.23,0),(.86,.33,.245,0),(.92,.30,.224,0)],2,"Hips")
    if v in (0,1):
        for side,x in (("L",-1),("R",1)):
            if v==0:
                loft("shorts leg",[(.62,.15,.16,0),(.65,.163,.17,0),(.77,.17,.18,0),(.87,.164,.177,0)],2,"Leg"+side,center=(x*.19,0),sides=24)
            else:
                limb("continuous trouser leg",[(x*.19,0,.87,.166,.176),(x*.19,0,.73,.167,.171),
                     (x*.195,0,.57,.14,.147),(x*.20,-.008,.47,.125,.133),(x*.20,0,.38,.13,.14),
                     (x*.20,0,.26,.116,.122),(x*.20,0,.16,.102,.108)],2,"Leg"+side,"Shin"+side,.46)
                loft("trouser cuff",[(.145,.107,.116,0),(.18,.108,.116,0)],2,"Shin"+side,center=(x*.20,0),sides=24)
            piece("stitched side pocket",(x*.30,-.035,.77),(.041,.079,.084),2,"Leg"+side,"cube")
            cord("pocket seam",[(x*.25,-.15,.82),(x*.28,-.15,.77),(x*.30,-.12,.72)],.006,2,"Leg"+side)
    elif v==2:
        tapered_shell("A-line skirt shell",.43,.89,(.45,.51),(.34,.29),2)
        for i in range(12):
            angle=math.tau*i/12
            cord("skirt seam",[(.341*math.cos(angle),.291*math.sin(angle),.87),(.452*math.cos(angle),.512*math.sin(angle),.445)],.005,2,"Hips")
    else:
        tapered_shell("tutu foundation",.51,.89,(.49,.49),(.34,.29),2)
        tapered_shell("tutu middle ruffle",.60,.90,(.475,.46),(.345,.295),2)
        tapered_shell("tutu upper ruffle",.71,.91,(.435,.395),(.35,.30),7)


def shoes(v):
    for side,sign in (("L",-1),("R",1)):
        x=sign*.20
        # Shaped toe, vamp, heel and ankle: the foot is 0.46 m long, not a box.
        profiles=[(-.335,.063,.07,.040),(-.30,.13,.09,.065),(-.22,.145,.115,.084),
                  (-.09,.128,.14,.103),(.035,.105,.155,.116),(.115,.092,.115,.068),(.13,.05,.09,.04)]
        vertices=[];faces=[];sides=24
        for y,width,z,rz in profiles:
            for i in range(sides):
                a=math.tau*i/sides
                vertices.append((x+width*math.cos(a),y,z+rz*math.sin(a)))
        for row in range(len(profiles)-1):
            for i in range(sides):
                j=(i+1)%sides
                faces.append((row*sides+j,row*sides+i,(row+1)*sides+i,(row+1)*sides+j))
        faces.extend((tuple(range(sides)),tuple(reversed([(len(profiles)-1)*sides+i for i in range(sides)]))))
        surface("shaped sneaker upper",vertices,faces,3,"Shin"+side)
        loft("contoured rubber sole",[(.008,.135,.224,-.105),(.024,.148,.238,-.105),
             (.052 if v!=3 else .076,.145,.234,-.105)],5,"Shin"+side,center=(x,0),sides=36,power=.55)
        piece("shoe tongue",(x,-.075,.213),(.056,.075,.013),3,"Shin"+side)
        if v==1:
            loft("boot ankle",[(.14,.092,.108,0),(.27,.095,.105,0),(.29,.091,.10,0)],3,"Shin"+side,center=(x,0),sides=24)
        for i in range(3):
            y=-.18+i*.055
            cord("lace",[(x-.064,y,.198+i*.006),(x,y-.008,.211+i*.006),(x+.064,y,.198+i*.006)],.007,4,"Shin"+side)
        piece("heel tab",(x,.118,.17),(.025,.016,.054),7,"Shin"+side,"cube")
        if v==2:
            cord("sport strap",[(x-.13,-.18,.14),(x-.07,-.18,.21),(x+.07,-.18,.21),(x+.13,-.18,.14)],.018,7,"Shin"+side)


def headgear(v):
    if v == 0:
        ring("bucket crown shell", 2.10, 2.28, (.30, .28), (.265, .245), 7, sides=28)
        piece("bucket top", (0, 0, 2.28), (.30, .28, .025), 7, "Head", "cylinder", 28)
        ring("bucket brim", 2.08, 2.12, (.36, .33), (.265, .245), 7, sides=28)
    elif v == 1:
        piece("beanie", (0, 0, 2.16), (.28, .25, .14), 7, "Head")
        piece("pom", (.12, .02, 2.31), (.085, .085, .085), 6, "Head")
    elif v == 2:
        ring("visor band", 2.055, 2.155, (.32, .30), (.285, .27), 7, sides=28)
        piece("visor bill", (0, -.36, 2.10), (.30, .14, .022), 7, "Head")
    else:
        ring("party crown base", 2.105, 2.215, (.32, .30), (.285, .27), 7, sides=28)
        for i in range(5):
            angle = i * math.tau / 5
            cx,cy=.285*math.sin(angle),.265*math.cos(angle)
            piece("party crown point",(cx,cy,2.235),(.038,.035,.09),7,"Head",rotation=(0,.18*math.sin(angle),0))


LENS_FRONTS = []


def inside(outline, px, pz):
    hit = False
    for (x0, z0), (x1, z1) in zip(outline, outline[1:] + outline[:1]):
        if (z0 > pz) != (z1 > pz) and px < x0 + (pz - z0) * (x1 - x0) / (z1 - z0):
            hit = not hit
    return hit


def sunglasses(v):
    outlines=[]
    for sign in (-1,1):
        points=[]
        for i in range(32):
            a=math.tau*i/32
            if v==2:
                r=1+.18*math.cos(a*5)
                xx=.084*math.cos(a)*r;zz=.074*math.sin(a)*r
            elif v in (1,3):
                xx=.096*math.copysign(abs(math.cos(a))**.5,math.cos(a))
                zz=(.061 if v==1 else .047)*math.copysign(abs(math.sin(a))**.5,math.sin(a))
            else:xx=.085*math.cos(a);zz=.075*math.sin(a)
            points.append((sign*.109+xx,1.98+zz))
        outlines.append(points)
    # Lens back face (y+.012) keeps 6 mm clear of the frontmost eye, lid, brow
    # or nose point of any face behind the outline, grown by the 7 mm rim.
    grown=[[(.109*s+(x-.109*s)*1.1,1.98+(z-1.98)*1.1) for x,z in o] for s,o in zip((-1,1),outlines)]
    front=min(p.y for p in FACE_POINTS if any(inside(o,p.x,p.z) for o in grown))
    y=front-.018
    LENS_FRONTS.append(y-.010)
    # Noses end near z 1.96, so the bridge dips to rest on the frontmost nose at 1.94.
    nose=min(p.y for p in FACE_POINTS if abs(p.x)<.012 and abs(p.z-1.94)<.006)
    for sign,points in zip((-1,1),outlines):
        verts=[(x,y+.012,z) for x,z in points]+[(x,y-.010,z) for x,z in points]
        faces=[tuple(reversed(range(32))),tuple(range(32,64))]
        for i in range(32):faces.append((i,(i+1)%32,(i+1)%32+32,i+32))
        surface("shaped tinted lens",verts,faces,5,"Head")
        cord("eyewear rim",[(x,y-.016,z) for x,z in points+[points[0]]],.007,7,"Head")
        hinge=max(abs(x) for x,_ in points)+.004
        # Straight back from the hinge to rest on the ear top against the skull.
        cord("temple arm",[(sign*hinge,y-.012,1.99),(sign*(hinge+.035),-.16,1.975),(sign*.262,-.01,1.955)],.009,7,"Head")
    inner=min(abs(x) for x,_ in outlines[1])
    cord("nose bridge",[(-inner,y-.016,1.982),(-.014,(y+nose)/2,1.955),(0,nose-.007,1.942),
                        (.014,(y+nose)/2,1.955),(inner,y-.016,1.982)],.008,7,"Head")


def facial_hair(v):
    # Every piece is seated half its depth into the measured skin: moustache
    # on the upper lip above the mouth, beard below the lower lip.
    if v in (0, 2, 3):
        for x in (-1, 1):
            piece("moustache", on_skin(x * .062, 1.772, .035), (.085, .035, .026), 6, "Head")
    if v in (1, 2):
        piece("chin beard", on_skin(0, 1.62, .07), (.15 if v == 2 else .10, .07, .09), 6, "Head")
    if v == 3:
        for x in (-1, 1):
            piece("side whisker", near_skin((x * .20, -.14, 1.80), .045), (.045, .055, .15), 6, "Head")


def hairstyle(v):
    # These lower locks remain visible when an opaque hat compresses the top.
    if v == 0:
        piece("side fringe", (-.23, -.08, 1.88), (.06, .12, .14), 6, "Head")
    elif v == 1:
        for x in (-1, 1):
            piece("bob lock", (x * .245, .08, 1.88), (.047, .11, .16), 6, "Head")
    elif v == 2:
        piece("mohawk side color", (.24, .07, 1.90), (.045, .09, .14), 6, "Head")
    else:
        piece("swept side lock", (.24, -.07, 1.89), (.06, .13, .13), 6, "Head")
        piece("back lock", (-.18, .11, 1.84), (.08, .12, .17), 6, "Head")

    # A connected back section follows the skull; it clears every hat's lower rim.
    floor=1.74 if v==1 else 1.82 if v==3 else 1.94
    vertices=[];faces=[];steps=20
    for row,z in enumerate((floor,floor+.055,2.015,2.045)):
        rx=(.218,.252,.263,.257)[row]
        ry=(.195,.224,.233,.226)[row]
        for i in range(steps+1):
            a=math.pi*i/steps
            vertices.append((rx*math.cos(a),.025+ry*math.sin(a),z))
    for row in range(3):
        for i in range(steps):faces.append((row*(steps+1)+i,row*(steps+1)+i+1,(row+1)*(steps+1)+i+1,(row+1)*(steps+1)+i))
    refine(surface("fitted back hair",vertices,faces,6,"Head"))


def hair_top(v):
    refine(loft("fitted hair mass",[(1.998,.258,.226,.028),(2.09,.261,.231,.025),
         (2.185,.19,.178,.029),(2.237,.055,.07,.025)],6,"Head",sides=28))
    if v==0:
        for i in range(3):
            piece("swept quiff",(-.13+i*.085,-.14,2.155+i*.012),(.10,.09,.09),6,"Head",rotation=(0,-.35,0))
    elif v==2:
        for i in range(5):
            piece("mohawk lock",(0,-.14+i*.07,2.235),(.070,.060,.10),6,"Head")
    elif v==3:
        for i in range(3):
            piece("side sweep",(.02+i*.06,-.145,2.15-i*.015),(.13,.085,.078),6,"Head",rotation=(0,.25,0))


def hair_under_hat():
    # Visors and crowns leave a small top opening. This compressed cap clears
    # the measured inner ring while opaque hats hide the whole top.
    piece("compressed hair cap", (0, .025, 2.075), (.22, .19, .065), 6, "Head")


def accessory(v):
    if v == 0:
        refine(loft("soft backpack",[(.85,.20,.13,.385),(.91,.265,.16,.395),(1.23,.28,.17,.40),
             (1.47,.25,.155,.395),(1.56,.15,.10,.38)],7,"Spine",sides=28))
        piece("backpack front pocket",(0,.58,1.09),(.21,.046,.15),7,"Spine","cube")
        cord("backpack zip",[(-.20,.61,1.19),(0,.633,1.22),(.20,.61,1.19)],.007,5,"Spine")
        for x in (-1,1):
            path=[(x*.21,-.31,1.04),(x*.225,-.32,1.27),(x*.23,-.25,1.45),(x*.23,-.08,1.54),
                  (x*.23,.12,1.53),(x*.23,.33,1.42)]
            verts=[]
            for cx,cy,cz in path:
                verts.extend([(cx-.029,cy-.009,cz),(cx+.029,cy-.009,cz),(cx+.029,cy+.009,cz),(cx-.029,cy+.009,cz)])
            faces=[]
            for row in range(len(path)-1):
                for j in range(4):faces.append((row*4+j,row*4+(j+1)%4,(row+1)*4+(j+1)%4,(row+1)*4+j))
            surface("curved backpack webbing",verts,faces,5,"Spine")
    elif v == 1:
        refine(loft("soft crossbody pouch",[(.84,.105,.075,-.29),(.89,.15,.10,-.29),
             (1.07,.155,.10,-.29),(1.14,.12,.078,-.29)],7,"Spine",center=(.37,0),sides=24))
        cord("crossbody webbing",[(-.20,-.15,1.52),(-.15,-.27,1.43),(0,-.285,1.29),(.18,-.295,1.15),(.31,-.30,1.09)],.022,5,"Spine")
        cord("pouch zipper",[(.27,-.379,1.075),(.38,-.398,1.08),(.48,-.37,1.075)],.006,5,"Spine")
    elif v == 2:
        ring("folded scarf",1.53,1.63,(.205,.19),(.138,.122),7,"Spine",32)
        cord("scarf drape",[(.13,-.15,1.60),(.18,-.22,1.44),(.16,-.27,1.28)],.048,7,"Spine",12)
    else:
        for x in (-1,1):
            piece("headphone cushion",(x*.275,.015,1.93),(.046,.087,.10),5,"Head")
            piece("headphone shell",(x*.313,.015,1.93),(.030,.078,.092),7,"Head")
        points=[(.30*math.cos(math.pi*i/24),.015,1.94+.30*math.sin(math.pi*i/24)) for i in range(25)]
        cord("arched headphone band",points,.023,5,"Head",10)


for category, builder in (("Shirt", shirt), ("Pants", lower), ("Shoes", shoes),
                          ("Headgear", headgear), ("Sunglasses", sunglasses),
                          ("FacialHair", facial_hair), ("Hairstyle", hairstyle),
                          ("HairTop", hair_top), ("Accessory", accessory)):
    for variant in range(4):
        group(f"{category}_{variant}", lambda b=builder, v=variant: b(v))
group("HairUnderHat", hair_under_hat)


def role_piece(role):
    if role in ("Security","Medic","Friend"):
        color=4 if role=="Medic" else 7
        refine(loft("tailored role outerwear",[(.84,.391,.285,0),(.88,.408,.30,0),(1.09,.399,.30,0),
             (1.28,.405,.30,0),(1.41,.421,.287,0),(1.49,.34,.236,0),(1.58,.175,.16,0)],color,"Spine",sides=28))
        cord("role center fastening",[(0,-.305,.89),(0,-.305,1.3),(0,-.269,1.45),(0,-.165,1.58)],.009,5,"Spine")
        for sign in (-1,1):
            piece("outerwear pocket",(sign*.225,-.27,1.14),(.096,.023,.084),color,"Spine","cube")
        if role=="Security":
            for z in (1.03,1.37):
                cord("reflective vest trim",[(-.32,-.208,z),(-.16,-.283,z),(0,-.307,z),(.16,-.283,z),(.32,-.208,z)],.018,4,"Spine")
            piece("radio",(.23,-.292,1.42),(.048,.03,.076),5,"Spine","cube")
            cord("radio aerial",[(.23,-.29,1.48),(.235,-.29,1.59)],.006,5,"Spine")
        elif role=="Medic":
            piece("clinic badge",(-.17,-.296,1.36),(.063,.012,.07),4,"Spine","cube")
            piece("clinic cross horizontal",(-.17,-.313,1.36),(.043,.006,.013),7,"Spine","cube")
            piece("clinic cross vertical",(-.17,-.314,1.36),(.013,.006,.043),7,"Spine","cube")
        else:
            refine(loft("friend shoulder backpack",[(.85,.14,.135,.42),(.92,.19,.18,.43),(1.34,.19,.18,.43),(1.48,.13,.12,.42)],2,"Spine",center=(.55,0),sides=24))
            cord("friend jacket chest stripe",[(-.30,-.23,1.37),(0,-.309,1.37),(.30,-.23,1.37)],.021,4,"Spine")
    else:
        v=int(role[-1]);color=7 if v!=1 else 4
        vertices=[]
        for z,width,depth in ((.78,.30,.265),(1.05,.34,.282),(1.32,.31,.274),(1.47,.19,.258)):
            for i in range(9):
                x=(i/8*2-1)*width
                vertices.append((x,-depth+.10*(x/width)**2,z))
        faces=[]
        for row in range(3):
            for i in range(8):faces.append((row*9+i,row*9+i+1,(row+1)*9+i+1,(row+1)*9+i))
        obj=surface("curved cloth apron",vertices,faces,color,"Spine")
        bpy.context.view_layer.objects.active=obj
        solid=obj.modifiers.new("Apron thickness","SOLIDIFY");solid.thickness=.009;bpy.ops.object.modifier_apply(modifier=solid.name)
        piece("apron pocket",(0,-.284,1.13),(.15,.016,.079),color,"Spine","cube")
        piece("vendor badge",(-.10,-.256,1.40),(.043,.012,.032),5,"Spine","cube")


for role in ("Security", "Medic", "Friend", "Vendor0", "Vendor1", "Vendor2"):
    group("Role_" + role, lambda r=role: role_piece(r))


def little_spoon():
    # Worn over even the thickest staff jacket, with a spoon silhouette that
    # reads from conversational distance. It is a passive equipment mesh.
    for x in (-1, 1):
        piece("necklace cord", (x * .09, -.39, 1.44), (.012, .015, .19), 5,
              "Spine", "cube", rotation=(0, x * .46, 0))
    piece("spoon handle", (0, -.405, 1.245), (.022, .021, .12), 7, "Spine", "cube")
    piece("spoon bowl", (0, -.424, 1.13), (.066, .032, .082), 7, "Spine")


group("Equipment_LittleSpoon", little_spoon)


def glow_eyes():
    # A glowing lozenge from inside the eyeball (y=-.240) to 4 mm past the
    # frontmost lens, so hostile eyes read with or without sunglasses.
    back, front = -.240, min(LENS_FRONTS) - .004
    for x in (-1, 1):
        piece("warning eye", (x * .10, (back + front) / 2, 1.965), (.045, (back - front) / 2, .050), 7, "Head")


group("EyeGlow", glow_eyes)

def neutral_shapes():
    for obj in groups:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name!="Basis":key.value=0
    bpy.context.view_layer.update()


neutral_shapes()
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in groups:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(OUT / "FestivalCharacter.fbx"), use_selection=True,
    object_types={"MESH", "ARMATURE"}, add_leaf_bones=False, bake_anim=False,
    axis_forward="-Z", axis_up="Y", apply_unit_scale=True)
neutral_shapes()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "FestivalCharacter.blend"))

character_groups = {obj.name: sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in groups}
manifest = {
    "source": "Original shaped ring topology, weighted continuous limbs, tailored clothing and facial geometry; no external assets",
    "qualityRevision": 3 if FACE_STYLE == "both" else 2,
    "runtime": "Assets/Festival/Art/Resources/FestivalCharacter.fbx",
    "faceStyle": FACE_STYLE,
    "heightMetres": 2.3,
    "materials": 2,
    "bones": list(bones),
    "groups": character_groups,
    "bodyShapes": ["lanky", "average", "stocky"],
    "genderPools": ["men", "women"],
    "wardrobeCategories": {name: 4 for name in ("Headgear", "Sunglasses", "Shirt", "Pants", "Shoes", "FacialHair", "Hairstyle", "Accessory")},
    "hairColorCount": 10,
    "hairFit": "Full top without headgear, compressed cap under open hats, side locks under every hat",
    "animation": "Unity FestivalCharacter procedural pose library; no baked clips",
    "rootMotion": False,
}
(SOURCE / "character-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
# Preserve the authored master above; derive the distance mesh in memory.
neutral_shapes()
for obj in groups:
    bpy.context.view_layer.objects.active=obj
    obj.shape_key_clear()
    simplify=obj.modifiers.new("Distance silhouette", "DECIMATE")
    simplify.ratio=.32
    simplify.use_collapse_triangulate=True
    bpy.ops.object.modifier_move_up(modifier=simplify.name)
    bpy.ops.object.modifier_apply(modifier=simplify.name)
    fit_keys(obj)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj in groups:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
neutral_shapes()
bpy.ops.export_scene.fbx(filepath=str(OUT / "FestivalCharacterDistant.fbx"),use_selection=True,
    object_types={"MESH","ARMATURE"},add_leaf_bones=False,bake_anim=False,axis_forward="-Z",axis_up="Y",apply_unit_scale=True)
manifest["distantGroups"]={obj.name:sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in groups}
manifest["runtimeSurfaceProfiles"]=["skin","cloth","hair","equipment","eyewear"]
manifest["detailDistancesMetres"]={"switchToDistant":10,"returnToDetailed":8}
(SOURCE / "character-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
print("MODULAR CHARACTER EXPORTED",len(groups),"groups with detailed and distance meshes")
