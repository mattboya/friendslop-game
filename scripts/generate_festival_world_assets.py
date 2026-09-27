"""Build the original festival kit with rounded silhouettes and detailed stalls."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource"
OUT = ROOT / "Assets/Festival/Art/Resources"
SOURCE.mkdir(exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

COLORS = {
    "Dark": (.09, .10, .17, 1), "Wood": (.39, .25, .18, 1),
    "Mint": (.16, .78, .64, 1), "Rose": (.93, .26, .47, 1),
    "Gold": (.98, .68, .21, 1), "Cream": (.91, .84, .65, 1),
    "Blue": (.23, .42, .73, 1), "Metal": (.37, .41, .47, 1),
    "Leaf": (.15, .36, .25, 1), "LeafWarm": (.33, .43, .24, 1),
    "Glass": (.30, .73, .79, 1), "White": (.86, .90, .82, 1),
}
MATS = {}
for name, color in COLORS.items():
    mat = bpy.data.materials.new("Festival" + name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = .82
    MATS[name] = mat

current = []
manifest = {}


def add(label, position, scale, color, shape="cube", rotation=(0, 0, 0), vertices=18,
        major=1, minor=.25):
    # `scale` is applied in the primitive's own axes before `rotation`.
    if shape == "cube":
        bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    elif shape == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=1, depth=1, location=position)
    elif shape == "cone":
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=1, radius2=0, depth=1, location=position)
    elif shape == "torus":
        bpy.ops.mesh.primitive_torus_add(major_segments=max(16, vertices), minor_segments=6 if major != 1 else 5,
                                         major_radius=major, minor_radius=minor, location=position)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=vertices, ring_count=12, radius=1, location=position)
    obj = bpy.context.object
    obj.name = label + "__" + color
    obj.scale = scale
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if shape == "cube" and min(scale) >= .12:
        bevel = obj.modifiers.new("Soft manufactured edges", "BEVEL")
        bevel.width = min(scale) * .10
        bevel.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        for poly in obj.data.polygons:
            poly.use_smooth = True
    elif shape in ("sphere", "cylinder", "cone", "torus"):
        for poly in obj.data.polygons:
            poly.use_smooth = True
    obj.data.materials.append(MATS[color])
    current.append(obj)
    obj.select_set(False)
    return obj


def box(label, p, s, color):
    return add(label, p, s, color)


def round_part(label, p, s, color, kind="cylinder"):
    return add(label, p, s, color, kind)


def disc(label, p, radius, depth, color, vertices=16):
    """A round plate whose faces point along +/-Y, toward the audience."""
    return add(label, p, (radius, radius, depth), color, "cylinder", (math.pi/2, 0, 0), vertices)


def crescent(label, p, radius, depth, color, cut=(.34, .17, .08)):
    """A Y-facing disc minus an offset disc: a real crescent, readable from both sides."""
    moon = disc(label, p, radius, depth, color, 24)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius*cut[0]/.40, depth=depth*4,
                                        location=(p[0]+radius*cut[1]/.40, p[1], p[2]+radius*cut[2]/.40),
                                        rotation=(math.pi/2, 0, 0))
    cutter = bpy.context.object
    boolean = moon.modifiers.new("Crescent cut", "BOOLEAN")
    boolean.operation, boolean.object, boolean.solver = "DIFFERENCE", cutter, "EXACT"
    bpy.context.view_layer.objects.active = moon
    bpy.ops.object.modifier_apply(modifier=boolean.name)
    bpy.data.objects.remove(cutter)
    return moon


def strut(label, a, b, radius, color, vertices=6):
    """A thin cylinder running exactly from point a to point b."""
    a, b = Vector(a), Vector(b)
    rotation = (b - a).to_track_quat("Z", "Y").to_euler()
    return add(label, (a + b) / 2, (radius, radius, (b - a).length), color, "cylinder", rotation, vertices)


def prism(label, outline, y0, y1, color):
    """Extrude a convex XZ outline between two Y planes (gables, wedges)."""
    n = len(outline)
    verts = [(x, y0, z) for x, z in outline] + [(x, y1, z) for x, z in outline]
    faces = [tuple(range(n)), tuple(range(2*n-1, n-1, -1))]
    faces += [(i, (i+1) % n, n+(i+1) % n, n+i) for i in range(n)]
    mesh = bpy.data.meshes.new(label)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    obj = bpy.data.objects.new(label + "__" + color, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)
    mesh.uv_layers.new()
    mesh.materials.append(MATS[color])
    current.append(obj)
    return obj


def arch(label, x, y, z, width, height, color):
    box(label + " left", (x-width/2, y, z+height/2), (.15, .16, height), color)
    box(label + " right", (x+width/2, y, z+height/2), (.15, .16, height), color)
    box(label + " lintel", (x, y, z+height), (width+.2, .16, .16), color)


def stage():
    # Deck top 1.4 m matches Unity's stage collision, so players and the DJ
    # deck (placed at y=1.4) stand on the visible surface.
    box("Chunky stage deck", (0, 0, .70), (18, 9, 1.40), "Dark")
    box("Back LED wall", (0, -4.35, 4.1), (17, .23, 5.5), "Dark")
    box("Canopy", (0, -.2, 7.2), (19, 10, .35), "Dark")
    for x in (-8.5, 8.5):
        for y in (-4, 4):
            box("Touring truss leg", (x, y, 4), (.3, .3, 8), "Metal")
    for x in (-10, 10):
        # Footprint sits inside Unity's 2 x 2 m speaker proxy at (+/-10, y=1).
        box("Speaker tower", (x, 1.0, 2.1), (1.9, 1.9, 4.2), "Dark")
        for z in (.9, 2.1, 3.3):
            disc("Speaker cone", (x, 1.98, z), .45, .08, "Gold")
    for z in (4.8, 7.1):
        box("Front touring truss", (0, 4, z), (18, .26, .26), "Metal")
    for i in range(8):
        center=-8.5+(i+.5)*2.125
        angle=math.atan2(2.125, 2.3)*(1 if i%2==0 else -1)
        add("Truss diagonal", (center, 4.0, 5.95), (.115, .115, math.hypot(2.125,2.3)),
            "Metal", "cube", (0, angle, 0))
    box("Canopy front fascia", (0, 4.69, 7.05), (18.8, .16, .37), "Blue")
    box("Low stage light rail", (0, 4.20, 4.75), (17.5, .17, .13), "Metal")
    for i in range(9):
        x = -7.5 + i*1.875
        # LED wall front face is y=-4.235; pixels and spines seat into it.
        box("Oversized LED pixel", (x, -4.21, 3.2 + (i%3)*.55), (1.30, .06, 1.1), "Mint" if i%2 else "Rose")
        round_part("Touring wash light", (x, 4.20, 4.585), (.22, .30, .22),
                   "Gold" if i%2 else "Mint")
    for x in (-8.25, 8.25):
        box("Side LED spine", (x, -4.195, 3.63), (.32, .08, 4.8), "Blue")
        for z in (1.8, 2.7, 3.6, 4.5, 5.4):
            box("Side LED cell", (x, -4.13, z), (.23, .08, .47), "Rose" if z<3.7 else "Mint")
    for x in (-2.4, 0, 2.4):
        box("Front light block", (x, 4.04, 6.95), (.5, .48, .5), "Gold")
    # Interlocking Sun/Moon mark: gold disc on the wall, mint crescent over it.
    disc("Sun brand disc", (-.35, -4.21, 5.7), .66, .06, "Gold", 24)
    crescent("Moon brand crescent", (.35, -4.155, 5.7), .66, .06, "Mint")
    for x in (-6.4, 6.4):
        box("Stage flight case", (x, 2.65, 1.73), (1.15, .72, .66), "Metal")
        for y in (2.30, 2.98):
            box("Flight case latch", (x, y, 1.73), (.18, .08, .13), "Gold")


def dj_deck():
    # Console base sits at z=0 so the deck rests on the 1.4 m stage top.
    box("Tactile DJ console", (0, 0, .375), (3.7, 1.25, .75), "Dark")
    box("Goofy beveled face", (0, .02, .765), (3.45, 1.05, .07), "Metal")
    for x in (-1.12, 1.12):
        round_part("Big platter", (x, 0, .815), (.56, .56, .055), "Dark")
        round_part("Platter center", (x, 0, .855), (.19, .19, .03), "Mint" if x < 0 else "Rose")
    for i in range(4):
        x = -.42 + i*.28
        box("Fader rail", (x, .02, .81), (.06, .60, .02), "Dark")
        # Separate objects: faders keep their own pivots for later animation.
        box("Fader moving " + str(i), (x, -.12+i*.08, .86), (.16, .12, .09), "Gold")
    for x in (-.28, .28):
        round_part("Knob", (x, -.40, .87), (.08, .08, .08), "Rose", "sphere")


def stall(kind):
    accents = (("Rose", "Supplies"), ("Mint", "Performance"), ("Gold", "Fictional stock"))
    accent, label = accents[kind]
    for x in (-2.25, 2.25):
        box(label + " pole", (x, 0, 1.7), (.15, .15, 3.4), "Wood")
    box(label + " counter", (0, 1.15, .70), (4.6, 1.35, 1.40), "Wood")
    box(label + " canopy", (0, 0, 3.25), (5.3, 3.2, .26), accent)
    for x in (-2.38, 2.38):
        strut(label + " arched canopy edge", (x, -1.62, 3.1), (x, 1.62, 3.1), .07, "Cream", 12)
    for i in range(9):
        x=-2.1+i*.525
        box(label + " canvas valance", (x, 1.62, 3.02), (.48, .07, .34), "Gold" if i%2 else accent)
        box(label + " counter plank", (x, 1.22, 1.44), (.47, .13, .09), "Cream" if i%3==0 else "Wood")
    box(label + " hanging sign", (0, 1.61, 2.72), (3.7, .12, .52), "Dark")
    for x in (-1.5, 1.5):
        box(label + " sign hanger", (x, 1.61, 3.05), (.04, .04, .18), "Metal")
    if kind == 0:
        for x in (-1.2, 0, 1.2):
            box("Supply crate", (x, 1.0, 1.68), (.7, .65, .65), "Cream")
    elif kind == 1:
        for x in (-1.0, 1.0):
            round_part("Poi display", (x, 1.35, 1.69), (.30, .30, .30), "Rose", "sphere")
        arch("Performance frame", 0, -.9, 0, 2.4, 2.75, "Metal")
    else:
        box("Tilted seller banner", (0, -.7, 3.46), (4.1, .12, .42), "Rose")
        for x in (-1.2, 1.2):
            round_part("Fictional stock jar", (x, 1.3, 1.58), (.28, .28, .47), "Mint")


def camp_shop():
    """A small timber-and-canvas checkout kiosk; collision stays in Unity."""
    for x in (-1.65, 1.65):
        round_part("Turned checkout post", (x, 0, 1.48), (.12, .12, 2.96), "Wood")
        round_part("Checkout post foot", (x, 0, .12), (.23, .23, .18), "Metal")
    for i in range(5):
        box("Horizontal checkout timber", (0, 1.0, .24+i*.19), (3.22, .12, .13), "Wood" if i%2 else "Cream")
    disc("Painted counter emblem", (0, 1.09, .68), .32, .08, "Gold", 24)
    disc("Counter emblem inset", (0, 1.15, .68), .19, .09, "Mint", 24)
    for i in range(7):
        x=-1.43+i*.475
        box("Countertop plank", (x, .57, 1.25), (.44, 1.05, .10), "Wood")
    box("Checkout canvas awning", (0, -.25, 3.03), (4.10, 2.8, .22), "Mint")
    for x in (-1.7, -1.0, -.3, .4, 1.1, 1.8):
        box("Awning scallop", (x, 1.17, 2.89), (.62, .07, .33), "Rose" if x < 0 else "Gold")
    for x in (-1.45, 1.45):
        strut("Warm string light", (x, 1.12, 2.91), (x, -1.42, 2.91), .035, "Gold", 12)
    for x, color in ((-.68,"Rose"),(.22,"Glass"),(.83,"Gold")):
        round_part("Checkout jar", (x, .22, 1.46), (.19, .19, .29), color, "cylinder")
        round_part("Checkout jar lid", (x, .22, 1.76), (.21, .21, .05), "Metal", "cylinder")


def medical():
    box("Clean tent floor", (0, 0, .12), (6, 5, .24), "Cream")
    # Walls run 0.1-3.15 so they seat into the roof underside at 3.13.
    box("Back wall", (0, -2.35, 1.625), (6, .25, 3.05), "White")
    for x in (-2.85, 2.85):
        box("Clinic side wall", (x, 0, 1.625), (.25, 4.8, 3.05), "White")
    box("Clinic roof", (0, 0, 3.25), (6.3, 5.2, .24), "Mint")
    box("Reception", (-1.65, 1.25, .84), (2.3, .55, 1.2), "White")
    box("Recovery cot", (1.2, -.65, .62), (2.7, 1.2, .25), "Blue")
    for dx in (-1.2, 1.2):
        for dy in (-.5, .5):
            box("Cot leg", (1.2+dx, -.65+dy, .37), (.08, .08, .28), "Metal")
    box("Cot head", (1.2, -1.25, .9), (2.7, .13, .65), "White")
    arch("Clinic entry", 0, 2.36, .23, 2.3, 2.57, "Mint")
    # Cross is mounted on the entry lintel's front face (y=2.44).
    box("Cross vertical", (0, 2.47, 2.65), (.22, .06, .75), "Rose")
    box("Cross horizontal", (0, 2.48, 2.65), (.67, .06, .22), "Rose")


def security():
    box("Cabin floor", (0, 0, .13), (6, 5, .26), "Metal")
    box("Cabin back", (0, -2.38, 1.65), (6, .25, 3), "Blue")
    for x in (-2.88, 2.88):
        box("Cabin side", (x, 0, 1.65), (.25, 5, 3), "Blue")
    box("Cabin roof", (0, 0, 3.25), (6.4, 5.25, .26), "Dark")
    box("Front left", (-2.1, 2.35, 1.65), (1.8, .22, 3), "Blue")
    box("Front right", (2.1, 2.35, 1.65), (1.8, .22, 3), "Blue")
    box("Holding bench", (0, -1.55, .7), (3.3, .60, .35), "Wood")
    for x in (-1.4, 1.4):
        box("Holding bench leg", (x, -1.55, .40), (.12, .5, .30), "Metal")
    for x in (-1.5, -.75, 0, .75, 1.5):
        box("Holding bar", (x, -.35, 1.55), (.075, .075, 2.6), "Metal")
    # Side bars close the cell against the back wall (inner face y=-2.255).
    for x in (-1.72, 1.72):
        for y in (-.73, -1.11, -1.49, -1.87):
            box("Holding side bar", (x, y, 1.55), (.075, .075, 2.6), "Metal")
        box("Holding side rail", (x, -1.30, 2.8), (.09, 1.95, .09), "Metal")
    box("Holding crossbar", (0, -.35, 2.8), (3.54, .09, .09), "Metal")
    box("Officer counter", (2.1, .75, .755), (1.3, .9, 1), "Wood")
    box("Security sign", (0, 2.49, 2.95), (2.5, .06, .37), "Gold")


def shuttle():
    # Matches Unity's shuttle collision: floor top 0.32 m, door side at -Y
    # (Unity north, toward the festival) with a 2.6 m opening between the
    # doorway proxies at x=+/-1.3, solid far wall at +Y.
    box("Bus undercarriage", (0, 0, .12), (8.0, 2.8, .16), "Dark")
    box("Bus floor", (0, 0, .26), (7.7, 2.7, .12), "Wood")
    box("Bus roof", (0, 0, 2.85), (8.1, 3.2, .24), "Mint")
    box("Bus far wall", (0, 1.45, 1.535), (8.0, .23, 2.43), "Mint")
    for x in (-3.88, 3.88):
        box("Bus end", (x, 0, 1.535), (.24, 2.9, 2.43), "Mint")
    for x in (-2.55, 0, 2.55):
        box("Far side window", (x, 1.575, 1.75), (1.7, .04, .72), "Glass")
    # Door side: two panels leave a 2.6 m opening; a header spans above it.
    for x in (-2.65, 2.65):
        box("Bus door-side panel", (x, -1.45, 1.535), (2.7, .23, 2.43), "Rose")
        box("Big side window", (x, -1.575, 1.75), (1.7, .04, .72), "Glass")
    box("Door header", (0, -1.45, 2.60), (2.7, .23, .30), "Rose")
    for x in (-1.36, 1.36):
        box("Open bus door post", (x, -1.49, 1.39), (.12, .16, 2.14), "Gold")
    box("Open bus door lintel", (0, -1.49, 2.45), (2.84, .16, .12), "Gold")
    for x in (-2.7, -1.1, 1.1, 2.7):
        box("Comical bus seat", (x, .55, .62), (1.1, .72, .60), "Blue")
        box("Bus seat back", (x, .90, 1.01), (1.1, .13, .82), "Blue")
    # Front (+X) has a windshield and headlamps so the box reads as a bus.
    box("Windshield", (4.015, 0, 1.75), (.04, 2.3, .95), "Glass")
    box("Destination plate", (4.015, 0, 2.48), (.05, 2.0, .28), "Gold")
    for y in (-1.0, 1.0):
        box("Bus headlamp", (4.02, y, .70), (.05, .42, .24), "Gold")
        box("Bus taillamp", (-4.02, y, .70), (.05, .36, .24), "Rose")
    box("Rear window", (-4.015, 0, 1.85), (.04, 2.0, .60), "Glass")
    for x in (-2.75, 2.75):
        for y in (-1.52, 1.52):
            disc("Round wheel", (x, y, .42), .42, .30, "Dark", 16)
            disc("Wheel hub", (x, y*1.1, .42), .18, .06, "Metal", 10)


def totem(sun):
    accent = "Gold" if sun else "Mint"
    box("Touchable plinth", (0, 0, .29), (1.4, 1.25, .58), "Wood")
    round_part("Sculpture column", (0, 0, 1.0), (.38, .38, 1.0), "Dark")
    # Upright ring (outer radius .545) facing the audience, its lowest arc
    # seated 4 cm into the column top at 1.5 m. Everything stays below
    # Unity's name plaque at 2.8 m.
    cz = 1.5 + .545 - .04
    add("Illuminated crest", (0, 0, cz), (1, 1, 1), accent, "torus", (math.pi/2, 0, 0),
        vertices=24, major=.47, minor=.075)
    if sun:
        disc("Sun center", (0, 0, cz), .40, .14, "Gold", 20)
        for i in range(8):
            if i == 4:
                continue  # straight down would be buried in the column
            a = i*math.tau/8
            add("Sun ray", (.62*math.sin(a), 0, cz+.62*math.cos(a)), (.10, .10, .24), "Gold", "cube", (0, a, 0))
    else:
        crescent("Moon crescent", (0, 0, cz), .40, .14, "Mint")
    box("Clue icon plate", (0, .66, .29), (.65, .08, .22), accent)


def tree(kind):
    # Trunk, branches and clustered canopies share explicit overlap; the
    # scattered crowns read as trees rather than stacked green boulders.
    round_part("Tapered woodland trunk", (0, 0, 2.0), (.48, .48, 4.0), "Wood")
    for side in (-1, 1):
        add("Forking branch", (side*.53, 0, 3.34), (.15, .17, 1.65), "Wood", "cylinder",
            (0, side*.56, 0), vertices=8)
    crowns = ((-.80, 0, 4.17, 1.10), (.73, .12, 4.22, 1.15),
              (-.25, -.47, 4.85, 1.12), (.30, .40, 5.05, 1.06),
              (0, 0, 5.65, .81)) if kind == 0 else (
              (-.96, 0, 3.95, 1.12), (.83, -.12, 4.12, 1.18),
              (0, .55, 4.78, 1.16), (-.13, -.32, 5.31, .96))
    for i, (x, y, z, r) in enumerate(crowns):
        add("Asymmetric leaf crown", (x, y, z), (r*1.04, r*.92, r*.83),
            "LeafWarm" if i%3==1 else "Leaf", "sphere", vertices=12)


def tent():
    box("Ground tarp", (0, 0, .045), (3.9, 3.15, .09), "Dark")
    box("Camp tent floor", (0, 0, .11), (3.6, 2.7, .10), "Wood")
    # A-frame: both panels run from the shared ridge (0, 2.2) to eaves on
    # the floor top (+/-1.75, .16) and cross 8 cm past the ridge to close it.
    ridge, eave_x, eave_z, half = 2.2, 1.75, .16, 1.45
    run, tilt = math.hypot(eave_x, ridge-eave_z), math.atan2(ridge-eave_z, eave_x)
    slope = (ridge-eave_z) / eave_x
    for side, name, color in ((-1, "left", "Rose"), (1, "right", "Gold")):
        down = Vector((side*math.cos(tilt), 0, -math.sin(tilt)))
        add("Ridged tent roof " + name, Vector((0, 0, ridge)) + down*((run-.08)/2),
            (run+.08, 2*half, .08), color, "cube", (0, side*tilt, 0))
        strut("Tent eave piping", (side*1.72, -half, .22), (side*1.72, half, .22), .04, "Cream")
    # Closed back gable; front gable framed around a 1.1 x 1.29 m doorway.
    prism("Tent back gable", [(-eave_x, eave_z), (eave_x, eave_z), (0, ridge)], -half, -half+.05, "Cream")
    door, door_top = .55, 1.45
    at_door = ridge - slope*door
    for s in (-1, 1):
        prism("Tent front gable", [(s*eave_x, eave_z), (s*door, eave_z), (s*door, at_door)], half-.05, half, "Cream")
    prism("Tent door header", [(-door, door_top), (door, door_top), (door, at_door), (0, ridge), (-door, at_door)],
          half-.05, half, "Cream")
    strut("Tent rolled door", (-.6, half+.06, door_top+.05), (.6, half+.06, door_top+.05), .06, "Cream", 8)
    strut("Tent ridge piping", (0, -half-.05, ridge+.06), (0, half+.05, ridge+.06), .05, "Cream", 8)
    box("Tent mesh vent", (0, -half-.015, 1.3), (.6, .03, .3), "Dark")
    # Guy lines run from the gable edge of each roof panel to a ground stake.
    for sx in (-1, 1):
        for sy in (-1, 1):
            anchor = (sx*.9, sy*half, ridge - slope*.9)
            stake = (sx*1.55, sy*2.25, .08)
            strut("Tent guy line", anchor, stake, .02, "Cream")
            round_part("Tent stake", (stake[0], stake[1], .07), (.04, .04, .22), "Metal")


def camp_car():
    box("Boxy camp car body", (0, 0, .60), (3.4, 5.2, .85), "Rose")
    box("Slightly crooked cabin", (0, .20, 1.25), (2.8, 2.8, .82), "Blue")
    box("Front windshield", (0, 1.63, 1.31), (2.35, .08, .56), "Glass")
    box("Rear windshield", (0, -1.24, 1.31), (2.35, .08, .56), "Glass")
    for side in (-1, 1):
        box("Side glass", (side*1.415, .24, 1.31), (.045, 2.07, .49), "Glass")
        box("Door seam", (side*1.70, .12, .71), (.025, .035, .54), "Dark")
        box("Door handle", (side*1.73, .37, .87), (.07, .33, .08), "Metal")
        box("Wheel arch", (side*1.69, 1.58, .58), (.08, 1.02, .74), "Dark")
        box("Roof rack rail", (side*1.02, .20, 1.77), (.10, 3.18, .09), "Metal")
        for y in (-1.1, 1.5):
            box("Roof rack foot", (side*1.02, y, 1.695), (.12, .12, .09), "Metal")
    for y in (-1.25, 1.30):
        box("Roof rack crossbar", (0, y, 1.80), (2.18, .12, .09), "Metal")
    for x in (-1.58, 1.58):
        for y in (-1.55, 1.55):
            add("Chunky wheel", (x, y, .37), (.42, .75, .75), "Dark", "sphere", vertices=8)
            add("Wheel hub", (x*1.18, y, .37), (.08, .31, .31), "Metal", "cylinder",
                (0, math.pi/2, 0), vertices=10)
    for x in (-1.04, 1.04):
        box("Camp car headlamp", (x, 2.63, .72), (.42, .06, .23), "Gold")
        box("Camp car taillamp", (x, -2.62, .74), (.40, .06, .23), "Rose")
    box("Front grille", (0, 2.63, .63), (1.18, .07, .30), "Dark")
    for i in range(4):
        box("Grille tooth", (-.39+i*.26, 2.67, .63), (.10, .03, .22), "Metal")
    box("Front bumper", (0, 2.67, .30), (2.98, .17, .17), "Cream")


def camp_shade():
    box("Lopsided sun shade", (0, 0, 3.55), (8.0, 8.0, .16), "Mint")
    for y in (-3.76, 3.76):
        box("Sun shade edge binding", (0, y, 3.56), (8.04, .16, .08), "Cream")
    for x in (-3.76, 3.76):
        box("Sun shade edge binding", (x, 0, 3.56), (.16, 8.04, .08), "Cream")
    for x in (-3.62, 3.62):
        for y in (-3.62, 3.62):
            box("Canvas support pole", (x, y, 1.75), (.16, .16, 3.5), "Wood")
            box("Canvas pole foot", (x, y, .06), (.32, .32, .12), "Metal")
    for y in (-1.7, 0, 1.7):
        box("Shade woven color band", (0, y, 3.64), (7.5, .14, .03), "Rose" if y==0 else "Gold")
    for x in (-2.4, 0, 2.4):
        box("Shade trim", (x, 3.95, 3.44), (1.3, .1, .16), "Rose")


def porta_potty():
    box("Portable toilet shell", (0, 0, 1.34), (2.2, 2.2, 2.68), "Blue")
    box("Molded roof", (0, 0, 2.78), (2.42, 2.42, .25), "Dark")
    box("Obvious door", (0, 1.12, 1.3), (1.72, .08, 2.37), "Mint")
    box("Door handle", (.63, 1.18, 1.29), (.14, .09, .13), "Gold")
    box("Air vent", (0, 1.18, 2.26), (.78, .04, .17), "Dark")


def little_spoon():
    # Upright cord loop whose lowest point sits on the handle top (z=.56).
    add("Necklace loop", (0, -.10, .745), (1, 1, 1), "Dark", "torus", (math.pi/2, 0, 0),
        vertices=20, major=.20, minor=.025)
    box("Spoon handle", (0, -.10, .36), (.07, .06, .40), "Gold")
    add("Tiny spoon bowl", (0, -.13, .12), (.18, .08, .20), "Gold", "sphere", vertices=10)


def poi():
    round_part("Poi handle", (0, 0, .92), (.08, .08, .25), "Dark")
    round_part("Visible tether", (0, 0, .55), (.025, .025, .58), "Cream")
    round_part("LED orb", (0, 0, .13), (.24, .24, .24), "Mint", "sphere")
    round_part("LED central jewel", (0, -.21, .13), (.09, .06, .09), "Rose", "sphere")


def wristband():
    add("Oversized band", (0, 0, .18), (.33, .33, .16), "Rose", "torus")
    box("Readable rescue icon plate", (0, -.33, .19), (.28, .07, .27), "Gold")
    box("Rescue icon bar", (0, -.38, .19), (.15, .03, .045), "White")


def small_gear(kind):
    if kind == "Confetti":
        round_part("Goofy confetti barrel", (0, 0, .36), (.17, .17, .65), "Rose")
        box("Handle", (0, -.19, .18), (.20, .17, .33), "Dark")
    elif kind == "MerchBag":
        box("Merch bag body", (0, 0, .34), (.55, .25, .60), "Cream")
        add("Bag handle", (0, 0, .67), (.22, .12, .08), "Gold", "torus", (math.pi/2, 0, 0))
    elif kind == "Map":
        box("Folded map", (0, 0, .03), (.50, .68, .06), "Cream")
        box("Map route", (0, 0, .07), (.09, .45, .02), "Mint")
    elif kind == "StagePass":
        box("Stage pass", (0, 0, .02), (.45, .67, .04), "Gold")
        box("Pass icon", (0, 0, .045), (.18, .20, .02), "Dark")
    elif kind == "Stock":
        round_part("Fictional stock tin", (0, 0, .28), (.24, .24, .55), "Mint")
        box("Fictional label", (0, -.23, .31), (.32, .03, .20), "Rose")
    elif kind == "Voucher":
        box("Clinic voucher", (0, 0, .02), (.60, .37, .04), "White")
        box("Clinic voucher cross", (0, 0, .045), (.21, .06, .02), "Mint")
    else:
        box("Shared stash crate", (0, 0, .50), (1.5, 1.15, 1), "Wood")
        box("Stash lock", (0, .59, .62), (.25, .08, .27), "Gold")


def merge_by_material(name):
    """One renderer per palette color keeps draw calls low. Unity maps the
    material from the "__Color" name suffix; "moving" parts stay separate."""
    groups = {}
    for obj in current:
        color = obj.name.split("__")[-1].split(".")[0]
        groups.setdefault(obj.name if "moving" in obj.name else color, []).append(obj)
    merged = []
    for key, objs in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objs:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        if key in COLORS:
            obj.name = f"{name} {key}__{key}"
        obj.data.name = obj.name
        merged.append(obj)
    current[:] = merged


def export(name, build):
    current.clear()
    build()
    primitives = len(current)
    merge_by_material(name)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in current:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = current[0]
    bpy.ops.export_scene.fbx(
        filepath=str(OUT / (name + ".fbx")), use_selection=True,
        object_types={"MESH"}, bake_anim=False,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True)
    triangles = sum(sum(len(poly.vertices)-2 for poly in obj.data.polygons) for obj in current)
    manifest[name] = {"parts": primitives, "renderers": len(current), "triangles": triangles}
    # Spread the retained source objects into an inspectable kit layout.
    index = len(manifest)-1
    shift_x, shift_y = (index % 4)*25, -(index // 4)*25
    for obj in current:
        obj.location.x += shift_x
        obj.location.y += shift_y
        obj["kit_asset"] = name


for name, build in (
    ("FestivalStage", stage), ("FestivalDJDeck", dj_deck),
    ("FestivalStallSupplies", lambda: stall(0)),
    ("FestivalStallPerformance", lambda: stall(1)),
    ("FestivalStallStock", lambda: stall(2)),
    ("FestivalMedical", medical), ("FestivalSecurity", security),
    ("FestivalShuttle", shuttle), ("FestivalSun", lambda: totem(True)),
    ("FestivalMoon", lambda: totem(False)),
    ("FestivalTreeA", lambda: tree(0)), ("FestivalTreeB", lambda: tree(1)),
    ("FestivalTent", tent), ("FestivalPoi", poi),
    ("FestivalWristband", wristband),
    ("FestivalCampCar", camp_car), ("FestivalCampShade", camp_shade),
    ("FestivalCampShop", camp_shop),
    ("FestivalPortaPotty", porta_potty), ("FestivalLittleSpoon", little_spoon),
):
    export(name, build)

for name in ("Confetti", "MerchBag", "Map", "StagePass", "Stock", "Voucher", "Stash"):
    export("Festival" + name, lambda n=name: small_gear(n))

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "FestivalWorld.blend"))
(SOURCE / "world-manifest.json").write_text(json.dumps({
    "source": "Original scripted Blender geometry; no external assets",
    "runtimeDirectory": "Assets/Festival/Art/Resources",
    "models": manifest,
}, indent=2) + "\n")
print("FESTIVAL WORLD KIT EXPORTED", len(manifest), "models")
