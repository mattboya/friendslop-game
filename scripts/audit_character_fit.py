"""Measure the current modular character's contact and clearance in Blender.

Run after generate_modular_character.py:
  Blender --background --factory-startup --python scripts/audit_character_fit.py

The checks are deliberately geometric. They cover the authored wardrobe and
stress poses; native visual review remains a separate acceptance gate.
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource/FestivalCharacter.blend"
REPORT = ROOT / "artifacts/character-fit-audit.json"
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rig = bpy.data.objects["FestivalRig"]
meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
depsgraph = bpy.context.evaluated_depsgraph_get()
failures = []
checks = {"fitShapes": 0, "torsoCoverage": 0, "legCoverage": 0,
          "shoeCoverage": 0, "roleCoverage": 0, "hairHatPairs": 0,
          "backpackInterfaces": 0, "skirtPoseVertices": 0,
          "faceSeating": 0, "eyewearSeating": 0}
pose_names = []


def require(condition, message):
    if not condition:
        failures.append(message)


def choose_fit(gender, shape):
    selected = f"Fit_{gender}_{shape}"
    for obj in meshes:
        if obj.name.startswith("Body_"):
            continue
        keys = obj.data.shape_keys
        require(keys is not None and selected in keys.key_blocks,
                f"{obj.name} missing {selected}")
        if keys is None:
            continue
        for key in keys.key_blocks:
            if key.name.startswith("Fit_"):
                key.value = 1 if key.name == selected else 0
        checks["fitShapes"] += 1
    bpy.context.view_layer.update()
    depsgraph.update()


def reset_pose(angles=None):
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0, 0, 0)
    for name, degrees in (angles or {}).items():
        rig.pose.bones[name].rotation_euler = tuple(math.radians(x) for x in degrees)
    bpy.context.view_layer.update()
    depsgraph.update()


def evaluated(name):
    return bpy.data.objects[name].evaluated_get(depsgraph)


def front_y(name, x, z):
    obj = evaluated(name)
    matrix = obj.matrix_world
    origin = matrix.inverted() @ Vector((x, -3, z))
    direction = matrix.inverted().to_3x3() @ Vector((0, 1, 0))
    hit, location, _, _ = obj.ray_cast(origin, direction, distance=6)
    return (matrix @ location).y if hit else None


def rear_y(name, x, z):
    obj = evaluated(name)
    matrix = obj.matrix_world
    origin = matrix.inverted() @ Vector((x, 3, z))
    direction = matrix.inverted().to_3x3() @ Vector((0, -1, 0))
    hit, location, _, _ = obj.ray_cast(origin, direction, distance=6)
    return (matrix @ location).y if hit else None


def tree(name):
    obj = evaluated(name)
    mesh = obj.to_mesh()
    result = BVHTree.FromPolygons(
        [obj.matrix_world @ vertex.co for vertex in mesh.vertices],
        [list(poly.vertices) for poly in mesh.polygons])
    obj.to_mesh_clear()
    return result


def tree_with_centers(name):
    obj = evaluated(name)
    mesh = obj.to_mesh()
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    polygons = [list(poly.vertices) for poly in mesh.polygons]
    centers = [sum((points[i] for i in poly), Vector()) / len(poly) for poly in polygons]
    result = BVHTree.FromPolygons(points, polygons)
    obj.to_mesh_clear()
    return result, centers


def components(obj, transform=None):
    """Connected mesh shells with measured bounds after deformation."""
    evaluated_obj = evaluated(obj.name)
    mesh = evaluated_obj.to_mesh()
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    seen = set()
    shells = []
    for start in range(len(mesh.vertices)):
        if start in seen:
            continue
        pending = [start]
        seen.add(start)
        indices = []
        while pending:
            index = pending.pop()
            indices.append(index)
            for neighbor in adjacency[index]:
                if neighbor not in seen:
                    seen.add(neighbor)
                    pending.append(neighbor)
        points = [evaluated_obj.matrix_world @ mesh.vertices[index].co
                  for index in indices]
        if transform is not None:
            points = [transform @ point for point in points]
        shells.append({"points": points,
                       "min": Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
                       "max": Vector(tuple(max(point[axis] for point in points) for axis in range(3)))})
    evaluated_obj.to_mesh_clear()
    return shells


def skirt_clearance(body, pants, context):
    hips = (rig.matrix_world @ rig.pose.bones["Hips"].matrix
            @ rig.data.bones["Hips"].matrix_local.inverted()).inverted()
    shells = components(bpy.data.objects[pants], hips)
    shell = next((part for part in shells
                  if part["min"].z < .54 and part["max"].z > .85), None)
    require(shell is not None, f"{pants} skirt shell absent")
    if shell is None:
        return
    # The skirt shell has planar polygon sides. Its projected convex hull is
    # a tighter clearance test than a bounding box or ellipse approximation.
    def hull(points):
        unique = sorted({(round(point.x, 5), round(point.y, 5)) for point in points})
        def cross(a, b, c):
            return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        lower, upper = [], []
        for point in unique:
            while len(lower) >= 2 and cross(lower[-2], lower[-1], point) <= 0:
                lower.pop()
            lower.append(point)
        for point in reversed(unique):
            while len(upper) >= 2 and cross(upper[-2], upper[-1], point) <= 0:
                upper.pop()
            upper.append(point)
        return sorted(lower[:-1] + upper[:-1], key=lambda xy: math.atan2(xy[1], xy[0]))
    bottom = hull([point for point in shell["points"] if abs(point.z - shell["min"].z) < .001])
    top = hull([point for point in shell["points"] if abs(point.z - shell["max"].z) < .001])
    require(len(bottom) == len(top) and len(bottom) >= 8,
            f"{pants} skirt rings do not correspond")
    if len(bottom) != len(top):
        return
    source = bpy.data.objects[body]
    obj = evaluated(body)
    mesh = obj.to_mesh()
    for vertex in mesh.vertices:
        groups = {source.vertex_groups[group.group].name
                  for group in source.data.vertices[vertex.index].groups}
        if not groups.intersection({"LegL", "LegR", "ShinL", "ShinR"}):
            continue
        point = hips @ (obj.matrix_world @ vertex.co)
        if not shell["min"].z + .02 <= point.z <= shell["max"].z - .02:
            continue
        checks["skirtPoseVertices"] += 1
        blend = (point.z - shell["min"].z) / (shell["max"].z - shell["min"].z)
        perimeter = [((1 - blend) * a[0] + blend * b[0],
                      (1 - blend) * a[1] + blend * b[1])
                     for a, b in zip(bottom, top)]
        for index, a in enumerate(perimeter):
            b = perimeter[(index + 1) % len(perimeter)]
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            signed = ((b[0] - a[0]) * (point.y - a[1])
                      - (b[1] - a[1]) * (point.x - a[0])) / length
            if signed < -.01:
                failures.append(f"{context} {body} leg pierces {pants} at {point.x:.3f},{point.y:.3f},{point.z:.3f} by {-signed:.3f}m")
                break
    obj.to_mesh_clear()


POSES = {
    "Idle": {},
    "WalkLeft": {"LegL": (30, 0, 0), "LegR": (-30, 0, 0), "ShinL": (24, 0, 0)},
    "WalkRight": {"LegL": (-30, 0, 0), "LegR": (30, 0, 0), "ShinR": (24, 0, 0)},
    "DanceA": {"Hips": (11, 18, 12), "LegL": (30, 0, 0), "LegR": (5, 0, 0)},
    "DanceB": {"Hips": (11, -18, -12), "LegL": (5, 0, 8), "LegR": (30, 0, -8)},
    "Poi": {"Hips": (11, 18, 12), "LegL": (30, 0, 0), "LegR": (5, 0, 0)},
    "Dj": {"Hips": (11, -18, -12), "LegL": (5, 0, 0), "LegR": (30, 0, 0)},
    "Downed": {"Hips": (75, 0, 4), "LegL": (12, 0, 10), "LegR": (-12, 0, -10)},
    "Detained": {"Spine": (20, 0, 0)},
    "Rescue": {"Spine": (15, 0, 0)},
    "Drag": {"Spine": (15, 0, 0)},
    "FindFriend": {"Spine": (15, 0, 0)},
    "ReadClue": {"Head": (16, 0, -18)},
    "Extract": {"Spine": (-15, 0, 0)},
    "Accusing": {"Spine": (20, 0, 0)},
    "Swarming": {"Spine": (20, 0, 0)},
    "Questioning": {"Head": (-8, 15, 20)},
    "Watching": {"Head": (-8, 15, 20)},
    "Spirit": {"Spine": (-10, 0, 8)},
}

for gender in range(2):
    for shape in range(3):
        label = f"{gender}/{shape}"
        choose_fit(gender, shape)
        reset_pose()
        body = f"Body_{gender}_{shape}"
        for shirt in range(4):
            clothing = f"Shirt_{shirt}"
            for x in (-.20, -.10, 0, .10, .20):
                for z in (1.13, 1.23, 1.33, 1.43):
                    skin, cloth = front_y(body, x, z), front_y(clothing, x, z)
                    if skin is None:
                        continue
                    checks["torsoCoverage"] += 1
                    require(cloth is not None and cloth < skin - .01,
                            f"{label} {clothing} leaves torso visible at {x:.2f},{z:.2f}")
            for role in ("Security", "Medic", "Friend", "Vendor0", "Vendor1", "Vendor2"):
                overlay = "Role_" + role
                for z in (1.18, 1.28, 1.38):
                    shirt_front = front_y(clothing, 0, z)
                    role_front = front_y(overlay, 0, z)
                    checks["roleCoverage"] += 1
                    require(shirt_front is not None and role_front is not None
                            and role_front < shirt_front - .005,
                            f"{label} {overlay} is buried under {clothing} at z={z:.2f}")
        for lower in range(4):
            for x in (-.19, .19):
                skin, cloth = front_y(body, x, .72), front_y(f"Pants_{lower}", x, .72)
                checks["legCoverage"] += 1
                require(skin is not None and cloth is not None and cloth < skin - .005,
                        f"{label} Pants_{lower} leaves upper leg visible at x={x:.2f}")
        for shoes in range(4):
            for x in (-.20, .20):
                skin, shoe = front_y(body, x, .12), front_y(f"Shoes_{shoes}", x, .12)
                checks["shoeCoverage"] += 1
                require(skin is not None and shoe is not None and shoe < skin - .005,
                        f"{label} Shoes_{shoes} leaves ankle visible at x={x:.2f}")
        for hat in range(4):
            hat_tree = tree(f"Headgear_{hat}")
            for hair in range(4):
                checks["hairHatPairs"] += 1
                require(not tree(f"Hairstyle_{hair}").overlap(hat_tree),
                        f"{label} Hairstyle_{hair} pierces Headgear_{hat}")
            if hat in (2, 3):
                checks["hairHatPairs"] += 1
                require(not tree("HairUnderHat").overlap(hat_tree),
                        f"{label} compressed hair pierces Headgear_{hat}")
        # The ordinary backpack seats against the shirt and remains separate
        # from the friend's distinctive shoulder backpack if both are drawn.
        bag = next(part for part in components(bpy.data.objects["Accessory_0"])
                   if part["max"].y > .4 and part["max"].z > 1.5)
        friend = next(part for part in components(bpy.data.objects["Role_Friend"])
                      if part["max"].x > .6 and part["max"].y > .4)
        back = rear_y("Shirt_0", 0, 1.20)
        checks["backpackInterfaces"] += 2
        require(back is not None and abs(bag["min"].y - back) <= .04,
                f"{label} backpack-to-shirt plane gap exceeds 4cm")
        require(friend["min"].x - bag["max"].x >= .015,
                f"{label} friend and selected backpacks intersect")
        # Face details, facial hair and eyewear must rest on the head, not
        # hover in front of it. Eyes, lids and lashes (z 1.90-2.02) sit on
        # the eyeball instead of the skin and are covered by the glow check.
        skin = tree(body)
        for part in [f"Face_{gender}_{face}" for face in range(6)] + [f"FacialHair_{v}" for v in range(4)]:
            for shell in components(bpy.data.objects[part]):
                z = sum(point.z for point in shell["points"]) / len(shell["points"])
                if part.startswith("Face_") and 1.90 <= z <= 2.02:
                    continue
                gap = min(skin.find_nearest(point)[3] for point in shell["points"])
                checks["faceSeating"] += 1
                require(gap <= .006, f"{label} {part} detail at z={z:.3f} floats {gap:.3f}m off the skin")
        faces = [tree(f"Face_{gender}_{face}") for face in range(6)]
        glow = tree("EyeGlow")
        checks["eyewearSeating"] += 1
        require(all(glow.overlap(face) for face in faces), f"{label} EyeGlow is not seated in every face's eyes")
        for glasses in range(4):
            name = f"Sunglasses_{glasses}"
            shells = components(bpy.data.objects[name])
            bridge = [s for s in shells if abs(s["min"].x + s["max"].x) < .01 and s["max"].x < .05]
            temples = [s for s in shells if s["max"].y > -.1]
            checks["eyewearSeating"] += 1
            require(len(bridge) == 1 and min(min(face.find_nearest(p)[3] for p in bridge[0]["points"])
                                             for face in faces) <= .003,
                    f"{label} {name} bridge does not rest on a nose")
            for temple in temples:
                tip = sorted(temple["points"], key=lambda point: -point.y)[:12]
                require(min(skin.find_nearest(point)[3] for point in tip) <= .006,
                        f"{label} {name} temple arm does not reach the head")
            require(glow.overlap(tree(name)), f"{label} EyeGlow is hidden behind {name}")
            glasses_tree, centers = tree_with_centers(name)
            cutting = {index for face in faces for index, _ in glasses_tree.overlap(face)}
            require(all(abs(centers[index].x) < .03 for index in cutting),
                    f"{label} {name} lens or arm cuts into a face")
        for pose, angles in POSES.items():
            reset_pose(angles)
            if pose not in pose_names:
                pose_names.append(pose)
            for variant in (2, 3):
                skirt_clearance(body, f"Pants_{variant}", f"{label}/{pose}")

REPORT.parent.mkdir(parents=True, exist_ok=True)
result = {"source": str(SOURCE.relative_to(ROOT)), "profiles": 6,
          "wardrobeVariantsPerCategory": 4, "poses": pose_names,
          "checks": checks, "failureCount": len(failures), "failures": failures}
REPORT.write_text(json.dumps(result, indent=2) + "\n")
print("FESTIVAL CHARACTER FIT AUDIT", "PASSED" if not failures else "FAILED",
      json.dumps(checks, sort_keys=True), "failures=", len(failures))
for failure in failures[:12]:
    print("FIT FAILURE", failure)
if failures:
    raise SystemExit(1)
