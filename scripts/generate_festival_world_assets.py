"""Build the original festival kit with rounded silhouettes and detailed stalls."""
import bpy
import json
import math
import os
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / os.environ.get("FESTIVAL_ASSET_STAGE", "artifacts/asset-staging/manual")
SOURCE = STAGE / "ArtSource"
OUT = STAGE / "Resources"
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
    "Bark": (.44, .31, .24, 1),
    "PaintRose": (.65, .21, .34, 1), "PaintMint": (.21, .56, .51, 1),
    "PaintGold": (.72, .48, .22, 1), "AutoGlass": (.18, .30, .38, 1),
    "CanvasRose": (.93, .26, .47, 1), "CanvasGold": (.98, .68, .21, 1),
    "CanvasMint": (.16, .78, .64, 1), "CanvasCream": (.91, .84, .65, 1),
    "CanvasDark": (.13, .20, .24, 1), "Rubber": (.10, .12, .16, 1),
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
        major=1, minor=.25, edge_radius=None, edge_segments=2):
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
        bevel.width = edge_radius if edge_radius is not None else min(scale) * .10
        bevel.segments = edge_segments
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


def box(label, p, s, color, **kwargs):
    return add(label, p, s, color, **kwargs)


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


def formed_mesh(label, vertices, faces, color, uv_coords=None, thickness=0):
    """A shaped, UV-mapped surface with enough contour to catch light."""
    mesh = bpy.data.meshes.new(label)
    mesh.from_pydata(vertices, [], faces)
    mesh.validate()
    obj = bpy.data.objects.new(label + "__" + color, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(MATS[color])
    if uv_coords is not None:
        uv = mesh.uv_layers.new(name="Surface UV")
        for polygon in mesh.polygons:
            for loop in polygon.loop_indices:
                uv.data[loop].uv = uv_coords[mesh.loops[loop].vertex_index]
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    if thickness:
        solid = obj.modifiers.new("Canvas edge thickness", "SOLIDIFY")
        solid.thickness = thickness
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=solid.name)
        obj.select_set(False)
    current.append(obj)
    return obj


def tapered_wood(label, points, radii, segments=12):
    vertices, faces, uvs = [], [], []
    points = [Vector(p) for p in points]
    length = 0
    for ring, point in enumerate(points):
        if ring:
            length += (point-points[ring-1]).length
        tangent = (points[min(ring+1,len(points)-1)]-points[max(0,ring-1)]).normalized()
        side = tangent.cross(Vector((0,1,0))).normalized()
        if side.length < .1:
            side = Vector((1,0,0))
        other = tangent.cross(side).normalized()
        for spoke in range(segments):
            angle = spoke * 2*math.pi/segments
            radius = radii[ring]*(1+.045*math.sin(spoke*3+ring*1.7))
            vertices.append(tuple(point + (side*math.cos(angle)+other*math.sin(angle))*radius))
            uvs.append((spoke/segments*2, length/1.2))
    for ring in range(len(points)-1):
        for spoke in range(segments):
            a=ring*segments+spoke
            b=ring*segments+(spoke+1)%segments
            faces.append((a,b,b+segments,a+segments))
    return formed_mesh(label, vertices, faces, "Bark", uvs)


def leaf_cluster(label, center, size, seed, color):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=center)
    obj = bpy.context.object
    obj.name = label + "__" + color
    for vertex in obj.data.vertices:
        direction = vertex.co.normalized()
        variation = 1 + .095*math.sin(direction.x*5+seed)*math.sin(direction.y*4-seed*.3)
        variation += .055*math.sin(direction.z*6+direction.x*3+seed*.8)
        vertex.co = Vector((direction.x*size[0],direction.y*size[1],direction.z*size[2]))*variation
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(MATS[color])
    current.append(obj)
    obj.select_set(False)
    return obj


def fabric_grid(label, u_steps, v_steps, surface, color, flip=False, uv_scale=(1,1), thickness=.045):
    vertices, faces, uvs = [], [], []
    for i in range(u_steps+1):
        for j in range(v_steps+1):
            u, v = i/u_steps, j/v_steps
            vertices.append(surface(u,v))
            uvs.append((u*uv_scale[0],v*uv_scale[1]))
    stride=v_steps+1
    for i in range(u_steps):
        for j in range(v_steps):
            a=i*stride+j
            quad=(a,a+stride,a+stride+1,a+1)
            faces.append(tuple(reversed(quad)) if flip else quad)
    return formed_mesh(label, vertices, faces, color, uvs, thickness)


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
    def roof_z(x,y):
        # The touring roof peaks at the middle and pulls down to the rails.
        return 7.08+.74*(1-abs(y)/5)**1.45+.08*(1-(x/9.5)**2)
    fabric_grid("Tensioned stage roof",24,14,
                lambda u,v: ((u-.5)*19,(v-.5)*10,roof_z((u-.5)*19,(v-.5)*10)),
                "CanvasDark",uv_scale=(7,3),thickness=.11)
    for x in (-9.35,-4.7,0,4.7,9.35):
        for j in range(10):
            y0=-5+j
            y1=y0+1
            strut("Roof webbing rib",(x,y0,roof_z(x,y0)+.025),
                  (x,y1,roof_z(x,y1)+.025),.038,"CanvasCream",8)
    for y in (-5,5):
        strut("Touring roof edge",(-9.5,y,roof_z(-9.5,y)),
              (9.5,y,roof_z(9.5,y)),.07,"Metal",8)
    for i in range(12):
        x=-8.7+i*1.58
        box("Front canvas drop",(x,4.96,6.91),(.99,.07,.42),
            "CanvasRose" if i%3==0 else "CanvasDark")
    for i in range(18):
        x=-8.35+i*.98
        box("Stage deck front plank",(x,4.53,.65),(.88,.055,1.13),"Wood")
        box("Stage deck plank bracket",(x,4.59,.14),(.12,.04,.14),"Metal")
    for x in (-8.5, 8.5):
        for y in (-4, 4):
            box("Touring truss leg", (x, y, 4), (.3, .3, 8), "Metal")
    for x in (-10, 10):
        # Footprint sits inside Unity's 2 x 2 m speaker proxy at (+/-10, y=1).
        box("Speaker tower", (x, 1.0, 2.1), (1.9, 1.9, 4.2), "Dark")
        for z in (.9, 2.1, 3.3):
            disc("Speaker rim", (x, 1.98, z), .51, .08, "Metal")
            disc("Speaker rubber cone", (x, 2.04, z), .39, .09, "Rubber")
            disc("Speaker dust cap", (x, 2.11, z), .13, .04, "Dark")
        for side in (-.77,.77):
            box("Speaker flight-case rail",(x+side,2.01,2.1),(.08,.09,3.9),"Metal")
    for z in (4.8, 7.1):
        box("Front touring truss", (0, 4, z), (18, .26, .26), "Metal")
    for i in range(8):
        center=-8.5+(i+.5)*2.125
        angle=math.atan2(2.125, 2.3)*(1 if i%2==0 else -1)
        add("Truss diagonal", (center, 4.0, 5.95), (.115, .115, math.hypot(2.125,2.3)),
            "Metal", "cube", (0, angle, 0))
    box("Canopy front fascia", (0, 4.69, 7.04), (18.8, .16, .21), "Blue")
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
    for x, color in ((-7.15,"CanvasRose"),(7.15,"CanvasMint")):
        box("Back wall hanging banner",(x,-4.17,4.0),(1.5,.055,3.8),color)
        box("Banner bottom weight",(x,-4.11,2.10),(1.55,.07,.08),"Metal")
    for x in (-2.4, 0, 2.4):
        box("Front light block", (x, 4.04, 6.95), (.5, .48, .5), "Gold")
    # Interlocking Sun/Moon mark: gold disc on the wall, mint crescent over it.
    disc("Sun brand disc", (-.53, -4.21, 5.78), .90, .06, "Gold", 24)
    crescent("Moon brand crescent", (.52, -4.155, 5.78), .90, .06, "Mint")
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
    box(label + " counter dark backing", (0, 1.73, .65), (4.55, .11, 1.22), "Dark")
    box(label + " counter top", (0, 1.16, 1.36), (4.86, 1.52, .16), "Wood",
        edge_radius=.065,edge_segments=3)
    box(label + " lower shelf", (0, 1.11, .29), (4.5, 1.25, .10), "Wood")
    for x in (-2.13,2.13):
        for y in (.55,1.77):
            box(label + " counter leg",(x,y,.69),(.16,.17,1.35),"Wood")
            box(label + " counter foot",(x,y,.10),(.27,.28,.12),"Metal")
    for i in range(10):
        x=-2.02+i*.45
        box(label + " counter front slat",(x,1.805,.72),(.39,.095,1.10),
            "Wood" if i%3 else "Cream")
    box(label + " counter upper trim",(0,1.87,1.28),(4.64,.13,.09),"Metal")
    def awning_z(x,y):
        return 3.43-.32*(y+1.6)/3.2-.075*(1-(x/2.65)**2)
    fabric_grid(label + " shaped awning",14,10,
                lambda u,v: ((u-.5)*5.3,(v-.5)*3.2,awning_z((u-.5)*5.3,(v-.5)*3.2)),
                "Canvas"+accent,uv_scale=(3,2),thickness=.075)
    for x in (-2.38, 2.38):
        strut(label + " awning edge", (x, -1.62, awning_z(x,-1.6)),
              (x, 1.62, awning_z(x,1.6)), .06, "CanvasCream", 12)
    for i in range(9):
        x=-2.1+i*.525
        box(label + " canvas valance", (x, 1.62, 2.96), (.48, .07, .31),
            "CanvasGold" if i%2 else "Canvas"+accent)
        box(label + " counter plank", (x, 1.22, 1.44), (.47, .13, .09), "Cream" if i%3==0 else "Wood")
    for x in (-2.2,2.2):
        box(label + " post bracket",(x,1.2,2.91),(.15,.72,.08),"Metal")
    for i in range(5):
        x=-1.9+i*.95
        box(label + " back curtain fold",(x,-1.54,2.06),(.85,.055,1.72),
            "CanvasCream" if i%2==0 else "Canvas"+accent)
    box(label + " hanging sign", (0, 1.61, 2.72), (3.7, .12, .52), "Dark")
    for x in (-1.5, 1.5):
        box(label + " sign hanger", (x, 1.61, 3.05), (.04, .04, .18), "Metal")
    if kind == 0:
        for x in (-1.2, 0, 1.2):
            box("Supply canvas bundle", (x, 1.0, 1.58), (.70, .56, .42), "CanvasCream")
            box("Supply folded top",(x,1.0,1.83),(.74,.60,.08),"CanvasRose")
            for side in (-.29,.29):
                box("Bundle tie",(x+side,1.0,1.84),(.04,.61,.035),"Wood")
    elif kind == 1:
        for x in (-1.0, 1.0):
            round_part("Poi display", (x, 1.35, 1.69), (.30, .30, .30), "Rose", "sphere")
            round_part("Poi spool base",(x,1.35,1.53),(.24,.24,.08),"Dark")
        arch("Performance frame", 0, -.9, 0, 2.4, 2.75, "Metal")
    else:
        box("Tilted seller banner", (0, -.7, 3.46), (4.1, .12, .42), "Rose")
        for x in (-1.2, 1.2):
            round_part("Fictional stock jar", (x, 1.3, 1.58), (.28, .28, .47), "Mint")
            round_part("Jar lid",(x,1.3,1.82),(.30,.30,.07),"Metal")
            box("Jar paper label",(x,1.59,1.56),(.38,.035,.18),"Cream")


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
    # The collision walls remain simple Unity proxies; the visible envelope
    # behaves like stretched field canvas, with a raised central ridge.
    fabric_grid("Clinic rear canvas",14,9,
                lambda u,v: ((u-.5)*6,-2.36+.035*math.sin(u*math.tau*3)*math.sin(v*math.pi),
                             .10+v*3.05),"CanvasCream",uv_scale=(3,2),thickness=.09)
    for side in (-1,1):
        fabric_grid("Clinic side canvas",12,9,
                    lambda u,v,s=side: (s*(2.88+.06*math.sin(u*math.tau*3)*math.sin(v*math.pi)),
                                         (u-.5)*4.8,.10+v*3.05),
                    "CanvasCream",uv_scale=(3,2),thickness=.09)
    def clinic_roof_z(x,y):
        return 3.16+.92*(1-abs(x)/3.18)**1.25-.055*(1-(y/2.62)**2)
    fabric_grid("Clinic pitched roof",16,12,
                lambda u,v: ((u-.5)*6.36,(v-.5)*5.24,
                             clinic_roof_z((u-.5)*6.36,(v-.5)*5.24)),
                "CanvasMint",uv_scale=(3,3),thickness=.08)
    strut("Clinic ridge tape",(0,-2.6,4.08),(0,2.6,4.08),.045,"CanvasCream",8)
    for x in (-3.1,3.1):
        strut("Clinic roof hem",(x,-2.6,3.18),(x,2.6,3.18),.055,"CanvasCream",8)
        for y in (-2.36,2.36):
            box("Clinic tubular corner",(x,y,1.58),(.10,.11,3.15),"Metal")
    for y in (-1.55,0,1.55):
        for x in (-2.96,2.96):
            box("Clinic sewn wall seam",(x,y,1.62),(.025,.035,2.97),"CanvasMint")
    for x in (-2.48,2.48):
        box("Clinic tied-back entrance flap",(x,2.46,1.57),(.53,.07,2.79),"CanvasCream")
        round_part("Canvas tie",(x,2.52,1.40),(.07,.07,.10),"Mint")
    box("Reception", (-1.65, 1.25, .84), (2.3, .55, 1.2), "White")
    box("Reception warm worktop",(-1.65,1.24,1.49),(2.47,.67,.12),"Wood")
    box("Clinic first-aid case",(-1.42,1.14,1.71),(.47,.29,.32),"White")
    box("Case handle",(-1.42,1.14,1.90),(.22,.07,.045),"Metal")
    box("Case cross vertical",(-1.42,1.30,1.73),(.055,.025,.18),"Rose")
    box("Case cross horizontal",(-1.42,1.31,1.73),(.18,.025,.055),"Rose")
    box("Recovery cot", (1.2, -.65, .62), (2.7, 1.2, .25), "Blue")
    box("Clinic mattress",(1.2,-.65,.79),(2.55,1.09,.13),"CanvasCream")
    box("Clinic pillow",(2.11,-.65,.88),(.42,.87,.16),"White")
    box("Folded patient blanket",(.44,-.65,.89),(.64,1.04,.08),"CanvasMint")
    for dx in (-1.2, 1.2):
        for dy in (-.5, .5):
            box("Cot leg", (1.2+dx, -.65+dy, .37), (.08, .08, .28), "Metal")
    for y in (-1.29,-.01):
        strut("Cot frame rail",(-.16,y,.70),(2.56,y,.70),.045,"Metal",8)
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
        side=1 if x>0 else -1
        for y in (-1.22,1.22):
            box("Shack inset side window",(side*3.02,y,1.96),(.055,1.23,.91),"AutoGlass")
            for frameY in (y-.67,y+.67):
                box("Window vertical frame",(side*3.07,frameY,1.96),(.075,.055,1.06),"Metal")
            for height in (1.45,2.48):
                box("Window sill frame",(side*3.07,y,height),(.075,1.37,.07),"Metal")
        for y in (-2.24,0,2.24):
            box("Prefab wall seam",(side*3.04,y,1.55),(.04,.045,2.76),"Metal")
    fabric_grid("Folded steel shack roof",14,10,
                lambda u,v: ((u-.5)*6.45,(v-.5)*5.45,
                             3.33+.24*(.5-v)+.035*math.sin(u*math.pi*8)),
                "Metal",uv_scale=(4,3),thickness=.10)
    for x in (-2.62,-1.31,0,1.31,2.62):
        strut("Roof standing seam",(x,-2.7,3.46),(x,2.7,3.22),.035,"Dark",8)
    for y in (-2.72,2.72):
        box("Shack roof gutter",(0,y,3.32),(6.5,.11,.13),"Metal")
    box("Front left", (-2.1, 2.35, 1.65), (1.8, .22, 3), "Blue")
    box("Front right", (2.1, 2.35, 1.65), (1.8, .22, 3), "Blue")
    for x in (-2.08,2.08):
        box("Front counter window",(x,2.475,1.95),(1.2,.055,.82),"AutoGlass")
        box("Front window sill",(x,2.51,1.51),(1.34,.09,.09),"Metal")
        for dx in (-.68,.68):
            box("Front window jamb",(x+dx,2.51,1.95),(.07,.09,.97),"Metal")
    for x in (-2.77,2.77):
        box("Shack structural corner",(x,2.48,1.57),(.15,.13,3.05),"Metal")
    box("Security door tread",(0,2.48,.20),(2.5,.52,.16),"Gold")
    box("Security entrance lintel",(0,2.48,2.99),(2.52,.16,.23),"Metal")
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
    box("Security sign", (0, 2.55, 3.03), (2.5, .08, .37), "Gold")
    round_part("Front warning lamp",(2.56,2.56,2.92),(.13,.13,.13),"Rose","sphere")


def shuttle():
    # Matches Unity's shuttle collision: floor top 0.32 m, door side at -Y
    # (Unity north, toward the festival) with a 2.6 m opening between the
    # doorway proxies at x=+/-1.3, solid far wall at +Y.
    box("Bus undercarriage", (0, 0, .12), (8.0, 2.8, .16), "Dark",
        edge_radius=.07,edge_segments=3)
    box("Bus floor", (0, 0, .26), (7.7, 2.7, .12), "Wood")
    fabric_grid("Curved shuttle roof",18,12,
                lambda u,v: ((u-.5)*8.2,(v-.5)*3.15,
                             2.78+.19*(1-((v-.5)*2)**2)),
                "PaintMint",uv_scale=(4,2),thickness=.11)
    for y in (-1.55,1.55):
        strut("Shuttle rain gutter",(-4.05,y,2.77),(4.05,y,2.77),.035,"Metal",8)
    box("Bus far wall", (0, 1.45, 1.535), (8.0, .23, 2.43), "PaintMint")
    for x in (-3.88, 3.88):
        box("Bus end", (x, 0, 1.535), (.24, 2.9, 2.43), "PaintMint")
    for x in (-2.55, 0, 2.55):
        box("Far side window", (x, 1.575, 1.76), (1.65, .04, .78), "AutoGlass")
        box("Far side window sill",(x,1.615,1.33),(1.75,.06,.08),"Metal")
    # Door side: two panels leave a 2.6 m opening; a header spans above it.
    for x in (-2.65, 2.65):
        box("Bus door-side panel", (x, -1.45, 1.535), (2.7, .23, 2.43), "PaintRose")
        box("Big side window", (x, -1.575, 1.75), (1.72, .04, .79), "AutoGlass")
        box("Side window sill",(x,-1.625,1.31),(1.85,.07,.09),"Metal")
        for windowX in (x-.92,x+.92):
            box("Side glazing pillar",(windowX,-1.63,1.73),(.06,.07,.90),"Metal")
        box("Side body rub strip",(x,-1.58,.92),(2.45,.085,.13),"Cream")
    box("Door header", (0, -1.45, 2.60), (2.7, .23, .30), "PaintRose")
    for x in (-1.36, 1.36):
        box("Open bus door post", (x, -1.49, 1.39), (.12, .16, 2.14), "Gold")
        strut("Boarding grab bar",(x*.87,-1.60,.54),(x*.87,-1.60,2.22),.037,"Metal",8)
    box("Open bus door lintel", (0, -1.49, 2.45), (2.84, .16, .12), "Gold")
    box("Boarding step",(0,-1.52,.20),(2.64,.42,.11),"Metal")
    for x in (-2.7, -1.1, 1.1, 2.7):
        box("Comical bus seat", (x, .55, .62), (1.1, .72, .60), "Blue")
        box("Bus seat back", (x, .90, 1.01), (1.1, .13, .82), "Blue")
    # Rounded front/rear cabs distinguish the vehicle from a cargo crate.
    box("Molded shuttle nose",(3.86,0,1.34),(.39,3.02,2.5),"PaintMint",
        edge_radius=.18,edge_segments=4)
    box("Molded shuttle tail",(-3.86,0,1.34),(.39,3.02,2.5),"PaintRose",
        edge_radius=.18,edge_segments=4)
    box("Windshield", (4.067, 0, 1.80), (.045, 2.3, .93), "AutoGlass")
    box("Windshield center divider",(4.10,0,1.80),(.05,.065,.91),"Metal")
    box("Destination plate", (4.08, 0, 2.49), (.05, 2.0, .30), "Dark")
    box("Destination plate border",(4.113,0,2.49),(.04,2.07,.035),"Gold")
    for y in (-1.0, 1.0):
        box("Bus headlamp", (4.085, y, .70), (.06, .42, .24), "Cream")
        box("Bus taillamp", (-4.085, y, .70), (.06, .36, .24), "Rose")
    box("Bus nose grille",(4.083,0,.77),(.05,1.55,.31),"Dark")
    for y in (-.55,-.18,.18,.55):
        box("Bus grille vane",(4.119,y,.77),(.04,.055,.24),"Metal")
    box("Shuttle front bumper",(4.08,0,.38),(.18,3.10,.16),"Metal")
    box("Shuttle rear bumper",(-4.08,0,.38),(.18,3.10,.16),"Metal")
    box("Rear window", (-4.085, 0, 1.85), (.05, 2.0, .60), "AutoGlass")
    for x in (-2.75, 2.75):
        for y in (-1.52, 1.52):
            disc("Round wheel", (x, y, .42), .42, .30, "Dark", 16)
            disc("Wheel hub", (x, y*1.1, .42), .18, .06, "Metal", 10)
            add("Shuttle wheel arch",(x,y,.43),(1,1,1),"Rubber","torus",
                (math.pi/2,0,0),vertices=20,major=.47,minor=.045)


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
    # The trunk flares into roots and branches grow through overlapping,
    # irregular foliage masses. Two crown profiles break up the forest line.
    bend = -.12 if kind == 0 else .17
    tapered_wood("Rooted woodland trunk", [(0,0,-.08),(.04,0,.18),(.01,0,.65),
        (bend*.45,.04,1.7),(bend,.05,2.8),(bend*1.2,.08,3.85),
        (bend*1.4,.10,4.7)], [.68,.58,.46,.36,.27,.19,.07])
    for side, depth in ((-1,-.28),(1,.21)):
        tapered_wood("Canopy fork", [(bend*.6,0,2.5),
            (side*.43,depth*.4,3.25),(side*.95,depth,3.95),
            (side*1.35,depth*1.3,4.38)], [.24,.18,.11,.035], 9)
    crowns = (
        [(-1.12,-.15,4.04,1.08,.84,.77),(-.38,.18,4.46,1.21,.91,.87),
         (.74,-.33,4.12,1.17,.94,.80),(1.28,.33,4.56,.88,.78,.70),
         (-1.29,.52,4.75,.78,.75,.66),(-.45,-.72,4.98,.89,.85,.78),
         (.53,.68,5.14,1.02,.81,.84),(.05,-.08,5.58,.95,.91,.80),
         (.30,.20,6.0,.70,.68,.58)] if kind == 0 else
        [(-1.31,-.22,3.92,1.17,.86,.81),(-.56,.50,4.29,1.06,.93,.77),
         (.72,-.42,4.00,1.18,.89,.80),(1.37,.36,4.40,.82,.79,.70),
         (-1.08,-.65,4.76,.86,.72,.70),(.05,.53,4.90,1.13,.91,.82),
         (.36,-.10,5.40,.95,.86,.80),(-.30,.01,5.79,.78,.73,.62)])
    for i, (x,y,z,sx,sy,sz) in enumerate(crowns):
        leaf_cluster("Sculpted foliage mass", (x,y,z), (sx,sy,sz), kind*29+i*7,
                     "LeafWarm" if i%4==1 else "Leaf")


def tent():
    box("Ground tarp", (0, 0, .045), (3.9, 3.15, .09), "Dark")
    box("Camp tent floor", (0, 0, .11), (3.6, 2.7, .10), "Wood")
    # A-frame: both panels run from the shared ridge (0, 2.2) to eaves on
    # the floor top (+/-1.75, .16) and cross 8 cm past the ridge to close it.
    ridge, eave_x, eave_z, half = 2.2, 1.75, .16, 1.45
    slope = (ridge-eave_z) / eave_x
    for side, name, color in ((-1, "left", "CanvasRose"), (1, "right", "CanvasGold")):
        def roof(u,v,s=side):
            x=s*eave_x*u
            y=-half+2*half*v
            tension=.055*math.sin(math.pi*u)*math.sin(math.pi*v)
            fold=.025*math.sin(4*math.pi*v+u*1.3)*math.sin(math.pi*u)
            return (x,y,ridge-(ridge-eave_z)*u-tension+fold)
        fabric_grid("Tensioned tent roof "+name,12,12,roof,color,flip=side<0,
                    uv_scale=(1.8,2.8),thickness=.045)
        strut("Tent eave piping", (side*1.72, -half, .22), (side*1.72, half, .22), .04, "Cream")
        for y in (-.74,.74):
            strut("Tent stitched panel seam", (0,y,ridge+.018),
                  (side*eave_x,y,eave_z+.026), .018, "Cream", 8)
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
    for s in (-1,1):
        strut("Door zip track", (s*door,half+.055,eave_z),
              (s*door,half+.055,door_top), .018, "Dark", 8)
        round_part("Tent zipper pull", (s*door,half+.07,1.14), (.055,.04,.08), "Metal")
    box("Tent mesh vent", (0, -half-.015, 1.3), (.6, .03, .3), "Dark")
    # Guy lines run from the gable edge of each roof panel to a ground stake.
    for sx in (-1, 1):
        for sy in (-1, 1):
            anchor = (sx*.9, sy*half, ridge - slope*.9)
            stake = (sx*1.55, sy*2.25, .08)
            strut("Tent guy line", anchor, stake, .02, "Cream")
            round_part("Tent stake", (stake[0], stake[1], .07), (.04, .04, .22), "Metal")


def dome_tent():
    """A low rounded sleeping tent that reads differently from the A-frames."""
    box("Dome footprint tarp", (0, 0, .045), (3.75, 3.35, .09), "Dark")
    for panel in range(8):
        a0, a1 = panel*math.tau/8, (panel+1)*math.tau/8
        def shell(u, v):
            angle = a0+(a1-a0)*u
            elevation = v*math.pi/2
            radius = math.cos(elevation)
            return (1.78*radius*math.cos(angle),
                    1.58*radius*math.sin(angle),
                    .10+1.68*math.sin(elevation))
        # Two front panels stop above the door, leaving a real walk-in gap.
        front_opening = panel in (1, 2)
        fabric_grid("Dome rainfly panel", 4, 10,
                    (lambda u, v: shell(u, .55+.45*v)) if front_opening else shell,
                    "CanvasRose" if panel%2==0 else "CanvasCream",
                    uv_scale=(.75,1.8), thickness=.045)
        for i in range(10):
            a = a0
            v0, v1 = i/10, (i+1)/10
            p0 = Vector((1.79*math.cos(v0*math.pi/2)*math.cos(a),
                         1.59*math.cos(v0*math.pi/2)*math.sin(a),
                         .12+1.68*math.sin(v0*math.pi/2)))
            p1 = Vector((1.79*math.cos(v1*math.pi/2)*math.cos(a),
                         1.59*math.cos(v1*math.pi/2)*math.sin(a),
                         .12+1.68*math.sin(v1*math.pi/2)))
            strut("Dome pole and taped seam", p0, p1, .021, "CanvasDark", 8)
    # Rolled front door and zip lines frame the open centre of the shell.
    strut("Dome door roll",(-.49,1.29,1.17),(.49,1.29,1.17),.075,"CanvasGold")
    for side in (-1,1):
        strut("Dome entry zipper",(side*.53,1.57,.16),(side*.46,1.30,1.13),.018,"Cream",8)
        strut("Dome guy rope",(side*1.29,1.00,.95),(side*2.05,1.83,.08),.018,"Cream",8)
        round_part("Dome stake",(side*2.05,1.83,.08),(.04,.04,.18),"Metal")
    box("Dome floor edging",(0,0,.13),(3.45,3.03,.075),"CanvasDark")


def wheel_wells(body, positions, radius, height):
    """Cut the tyre clearance into the painted shell before adding trim."""
    for side in (-1, 1):
        for y in positions:
            bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=.78,
                location=(side*1.44,y,height), rotation=(0,math.pi/2,0))
            cutter = bpy.context.object
            modifier = body.modifiers.new("Wheel opening", "BOOLEAN")
            modifier.operation = "DIFFERENCE"
            modifier.object = cutter
            modifier.solver = "EXACT"
            bpy.context.view_layer.objects.active = body
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            bpy.data.objects.remove(cutter)


def camp_car():
    # A readable small hatchback: hood, separate passenger cabin, glass,
    # four round wheels, mirrors, lamps and recognizable front/rear ends.
    body = box("Camp car rounded lower shell", (0, 0, .65), (2.85, 4.75, .86), "PaintRose",
        edge_radius=.18,edge_segments=4)
    wheel_wells(body, (-1.51,1.52), .53, .40)
    formed_mesh("Shaped car bonnet",
        [(-1.31,1.20,1.07),(0,1.20,1.17),(1.31,1.20,1.07),
         (-1.31,2.31,.98),(0,2.31,1.09),(1.31,2.31,.98)],
        [(0,3,4,1),(1,4,5,2)],"PaintRose",thickness=.09)
    box("Camp car rear deck", (0, -1.74, 1.08), (2.64, 1.05, .16), "PaintRose",
        edge_radius=.055,edge_segments=3)
    # Cabin silhouette is a sloped trapezoid when seen from either side.
    outline = [(-1.51, 1.05), (-1.05, 1.83), (.77, 1.83), (1.38, 1.05)]
    n = len(outline)
    vertices = [(-1.18, y, z) for y, z in outline] + [(1.18, y, z) for y, z in outline]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2*n)))]
    faces += [(i, (i+1)%n, n+(i+1)%n, n+i) for i in range(n)]
    mesh = bpy.data.meshes.new("Cabin shell")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate()
    cabin = bpy.data.objects.new("Camp car cabin shell__PaintRose", mesh)
    bpy.context.collection.objects.link(cabin)
    mesh.materials.append(MATS["PaintRose"])
    current.append(cabin)
    box("Camp car roof skin", (0, -.15, 1.84), (2.27, 1.80, .11), "PaintRose",
        edge_radius=.045,edge_segments=3)
    add("Camp car front windshield", (0, 1.07, 1.46), (2.08, .035, .83), "AutoGlass", "cube", (.66, 0, 0))
    add("Camp car rear windshield", (0, -1.29, 1.45), (2.08, .035, .78), "AutoGlass", "cube", (-.48, 0, 0))
    strut("Windshield lower gasket",(-1.03,1.36,1.09),(1.03,1.36,1.09),.025,"Dark",8)
    strut("Windshield upper gasket",(-1.03,.80,1.83),(1.03,.80,1.83),.025,"Dark",8)
    for side in (-1, 1):
        x = side*1.19
        formed_mesh("Shaped front side glass",[(x,.12,1.14),(x,1.17,1.14),
            (x,.84,1.72),(x,.12,1.72)],[(0,1,2,3)],"AutoGlass",thickness=.025)
        formed_mesh("Shaped rear side glass",[(x,-1.35,1.14),(x,-.19,1.14),
            (x,-.19,1.72),(x,-1.08,1.72)],[(0,1,2,3)],"AutoGlass",thickness=.025)
        box("Camp car B pillar", (x, -.07, 1.48), (.07, .09, .55), "PaintRose")
        strut("Front window lower seal",(x,.12,1.13),(x,1.17,1.13),.025,"Dark",8)
        strut("Rear window lower seal",(x,-1.35,1.13),(x,-.19,1.13),.025,"Dark",8)
        box("Camp car door seam", (side*1.43, .03, .70), (.028, .035, .61), "Dark")
        box("Camp car door handle", (side*1.46, .38, 1.01), (.07, .28, .07), "Metal")
        box("Camp car rear door seam", (side*1.43, -1.00, .70), (.028, .035, .61), "Dark")
        box("Camp car rear door handle", (side*1.46, -1.27, 1.01), (.07, .23, .07), "Metal")
        strut("Painted beltline crease",(side*1.44,-2.16,1.09),(side*1.44,2.02,1.09),.015,"PaintRose",8)
        box("Side turn signal",(side*1.44,1.86,1.02),(.055,.22,.09),"Gold")
        box("Camp car wing mirror", (side*1.50, .96, 1.18), (.26, .27, .15), "Dark")
        box("Camp car sill", (side*1.43, 0, .28), (.07, 2.8, .10), "Dark")
        for y in (-1.51, 1.52):
            add("Molded wheel arch trim",(side*1.48,y,.40),(1,1,1),"Dark","torus",
                (0,math.pi/2,0),vertices=24,major=.46,minor=.045)
            add("Round rubber tyre", (side*1.43, y, .40), (.43, .43, .16), "Dark", "cylinder",
                (0, math.pi/2, 0), vertices=20)
            add("Wheel hub", (side*1.54, y, .40), (.25, .25, .055), "Metal", "cylinder",
                (0, math.pi/2, 0), vertices=20)
            for spoke in range(5):
                theta=spoke*math.pi*2/5
                box("Pressed wheel spoke",(side*1.605,y+math.sin(theta)*.13,.40+math.cos(theta)*.13),
                    (.025,.055,.18),"Metal",rotation=(theta,0,0))
        strut("Car roof gutter",(side*1.14,-1.12,1.83),(side*1.14,.76,1.83),.025,"Metal",8)
    for x in (-.95, .95):
        box("Camp car headlamp", (x, 2.40, .92), (.39, .06, .22), "Cream")
        box("Camp car taillamp", (x, -2.40, .89), (.35, .06, .24), "Rose")
        box("Camp car amber indicator", (x, -2.42, 1.03), (.28, .065, .09), "Gold")
    box("Camp car front grille", (0, 2.41, .67), (1.28, .07, .23), "Dark")
    box("Camp car lower intake", (0, 2.43, .42), (1.65, .055, .095), "Dark")
    for x in (-.43,-.15,.15,.43):
        box("Grille opening",(x,2.45,.67),(.06,.035,.15),"Metal")
    box("Camp car front bumper", (0, 2.43, .36), (2.88, .15, .16), "Metal")
    for x in (-1.05,1.05):
        box("Inset fog lamp",(x,2.51,.45),(.25,.035,.11),"Cream")
    box("Camp car rear bumper", (0, -2.43, .36), (2.88, .15, .16), "Metal")
    box("Camp car number plate", (0, 2.52, .47), (.66, .02, .18), "Cream")
    for side in (-1,1):
        strut("Windshield wiper",(side*.15,1.30,1.12),(side*.82,1.27,1.22),.018,"Dark",8)


def camp_van():
    """A distinct tall camper van with windows, sliding door and usable road stance."""
    body=box("Camper van lower body",(0,0,.78),(2.95,5.26,1.22),"PaintRose",
        edge_radius=.22,edge_segments=5)
    wheel_wells(body, (-1.68,1.72), .56, .43)
    box("Camper van raised cabin",(0,-.24,1.66),(2.72,3.95,1.38),"PaintRose",
        edge_radius=.18,edge_segments=4)
    box("Camper van roof",(0,-.26,2.39),(2.76,4.04,.14),"PaintRose",
        edge_radius=.065,edge_segments=3)
    # The sloped windscreen, long glazed cabin and back doors carry its identity.
    add("Camper windscreen",(0,1.97,1.74),(2.33,.055,1.04),"AutoGlass","cube",(.22,0,0))
    box("Camper windshield visor",(0,2.03,2.29),(2.61,.17,.12),"PaintRose")
    for side in (-1,1):
        x=side*1.385
        box("Driver side glass",(x,1.05,1.76),(.038,.90,.78),"AutoGlass")
        box("Passenger side glass",(x,-.45,1.75),(.038,1.43,.79),"AutoGlass")
        box("Rear quarter glass",(x,-1.73,1.75),(.038,.66,.79),"AutoGlass")
        for y in (-1.15,.29,1.48):
            box("Camper window pillar",(side*1.41,y,1.75),(.065,.09,.86),"PaintRose")
        box("Camper sliding door track",(side*1.45,-.42,1.27),(.055,2.1,.055),"Metal")
        box("Camper sliding door rear seam",(side*1.46,-1.53,.91),(.055,.035,.77),"Dark")
        box("Camper sliding door handle",(side*1.47,-.10,1.08),(.065,.30,.065),"Metal")
        box("Camper driver door seam",(side*1.46,1.39,.86),(.055,.035,.78),"Dark")
        box("Camper driver door handle",(side*1.47,1.00,1.09),(.065,.26,.065),"Metal")
        box("Camper wing mirror",(side*1.53,2.16,1.43),(.28,.23,.22),"Dark")
        box("Camper sill",(side*1.45,-.06,.36),(.08,4.65,.13),"Dark")
        for y in (-1.68,1.72):
            add("Camper wheel",(side*1.48,y,.43),(.48,.48,.18),"Rubber","cylinder",
                (0,math.pi/2,0),vertices=24)
            add("Camper wheel hub",(side*1.60,y,.43),(.23,.23,.055),"Metal","cylinder",
                (0,math.pi/2,0),vertices=20)
            add("Camper wheel arch",(side*1.48,y,.43),(1,1,1),"Dark","torus",
                (0,math.pi/2,0),vertices=24,major=.48,minor=.04)
        strut("Camper roof rail",(side*1.08,-1.86,2.53),(side*1.08,1.19,2.53),.05,"Metal")
        box("Camper headlamp",(side*.97,2.65,.98),(.42,.07,.27),"Cream")
        box("Camper tail light",(side*1.15,-2.66,.91),(.19,.075,.51),"Rose")
    for y in (-1.38,.75):
        box("Camper roof rack crossbar",(0,y,2.56),(2.38,.065,.065),"Metal")
    box("Camper front grille",(0,2.65,.72),(1.41,.075,.30),"Dark")
    for x in (-.46,-.23,0,.23,.46):
        box("Camper grille slot",(x,2.70,.72),(.075,.036,.22),"Metal")
    box("Camper rear door seam",(0,-2.66,1.52),(.045,.04,1.43),"Dark")
    box("Camper rear window",(0,-2.65,1.98),(2.32,.045,.68),"AutoGlass")
    box("Camper rear door handle",(.45,-2.71,1.08),(.31,.065,.07),"Metal")
    box("Camper front bumper",(0,2.71,.34),(2.97,.20,.20),"Metal")
    box("Camper rear bumper",(0,-2.71,.34),(2.97,.20,.20),"Metal")
    box("Camper front registration",(0,2.82,.46),(.72,.025,.17),"Cream")


def camp_shade():
    def canopy_z(x,y):
        radial=max(abs(x),abs(y))/4
        return 3.30+.25*radial**1.6+.11*abs(x*y)/16
    fabric_grid("Draped shade canvas",16,16,
                lambda u,v: ((u-.5)*8,(v-.5)*8,canopy_z((u-.5)*8,(v-.5)*8)),
                "CanvasMint",uv_scale=(4,4),thickness=.065)
    for edge in (-4,4):
        for i in range(16):
            lo=-4+i*.5
            hi=lo+.5
            strut("Shade sewn edge",(lo,edge,canopy_z(lo,edge)),
                  (hi,edge,canopy_z(hi,edge)),.035,"CanvasCream",8)
            strut("Shade sewn edge",(edge,lo,canopy_z(edge,lo)),
                  (edge,hi,canopy_z(edge,hi)),.035,"CanvasCream",8)
    for x in (-3.62, 3.62):
        for y in (-3.62, 3.62):
            box("Canvas support pole", (x, y, 1.75), (.16, .16, 3.5), "Wood")
            box("Canvas pole foot", (x, y, .06), (.32, .32, .12), "Metal")
            round_part("Shade corner eyelet",(x,y,3.53),(.16,.16,.08),"Metal", "cylinder")
    for y in (-1.7, 0, 1.7):
        for i in range(16):
            lo=-3.85+i*.48
            hi=lo+.48
            strut("Shade stitched span",(lo,y,canopy_z(lo,y)+.025),
                  (hi,y,canopy_z(hi,y)+.025),.018,"CanvasRose" if y==0 else "CanvasGold",6)
    for x in (-2.4, 0, 2.4):
        box("Shade hanging trim", (x, 3.95, 3.43), (1.3, .08, .19), "CanvasRose")


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
    ("FestivalTent", tent), ("FestivalDomeTent", dome_tent), ("FestivalPoi", poi),
    ("FestivalWristband", wristband),
    ("FestivalCampCar", camp_car), ("FestivalCampVan", camp_van), ("FestivalCampShade", camp_shade),
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
