"""Build the original festival kit with rounded silhouettes and detailed stalls."""
import bpy
import json
import math
import os
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
GEAR_ONLY = os.environ.get("FESTIVAL_ASSET_KIND") == "gear"
GEAR_NAMES = ("Confetti", "MerchBag", "Map", "StagePass", "Stock", "Voucher", "Stash")
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
    "PaintGold": (.72, .48, .22, 1), "PaintCream": (.82, .77, .62, 1),
    "AutoGlass": (.18, .30, .38, 1),
    "CanvasRose": (.93, .26, .47, 1), "CanvasGold": (.98, .68, .21, 1),
    "CanvasMint": (.16, .78, .64, 1), "CanvasCream": (.91, .84, .65, 1),
    "CanvasDark": (.13, .20, .24, 1), "Rubber": (.10, .12, .16, 1),
    "Needle": (.13, .28, .24, 1), "Stone": (.38, .42, .39, 1),
    "StageGlowGold": (1.0, .64, .28, 1),
    "StageGlowMint": (.28, .92, .75, 1),
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


def longitudinal_prism(label, outline, half_width, color):
    """Extrude a YZ silhouette across a vehicle without disconnected cab boxes."""
    n = len(outline)
    verts = [(-half_width, y, z) for y, z in outline]
    verts += [(half_width, y, z) for y, z in outline]
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
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
    mesh.materials.append(MATS[color])
    current.append(obj)
    return obj


def xz_prism(label, outline, center_y, depth, color, bevel=.045):
    """A closed, lightly rounded coach body section with an X/Z profile."""
    n = len(outline)
    verts = [(x, center_y-depth/2, z) for x, z in outline]
    verts += [(x, center_y+depth/2, z) for x, z in outline]
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, n+(i+1) % n, n+i) for i in range(n)]
    obj = formed_mesh(label, verts, faces, color)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    if bevel:
        modifier = obj.modifiers.new("Soft coach panel edges", "BEVEL")
        modifier.width, modifier.segments = bevel, 3
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
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
    # The rear elevation needs to read as architecture from the crowd, not
    # isolated colored pixels on a black rectangle. These folded touring
    # panels frame a central lit opening without narrowing the playable deck.
    for side, fabric in ((-1,"CanvasRose"),(1,"CanvasMint")):
        outline=[(5.18,1.48),(8.10,1.48),(8.10,6.12),(7.42,6.12),
                 (6.23,5.48),(5.18,3.75)]
        verts=[(side*x,-4.07+(.17 if x<6.3 else .04),z) for x,z in outline]
        formed_mesh("Folded rear scenic wing",verts,[(0,1,2,3,4,5)],fabric,
                    [(x/8.5,z/7.0) for x,z in outline],thickness=.075)
        for a,b in ((0,1),(1,2),(2,3),(3,4),(4,5),(5,0)):
            strut("Scenic wing bound edge",verts[a],verts[b],.035,"CanvasCream",8)
        for height in (2.02,2.72,3.42,4.12,4.82):
            x=7.59-(height-2.02)*.23
            box("Wing recessed light cassette",(side*x,-3.93,height),(.68,.12,.39),"Dark")
            box("Wing luminous window",(side*x,-3.86,height),(.49,.025,.18),
                "StageGlowGold" if height<3.5 else "StageGlowMint")
        strut("Wing diagonal stiffener",(side*5.35,-3.88,1.60),
              (side*7.40,-3.88,5.95),.070,"Metal",10)
    # Curved pipework and an inset luminous ring create a deliberate focal
    # target behind the DJ. The segmented tubing has physical highlight and
    # shadow; none of it is a screen-space decal or a borrowed logo.
    for radius,color,depth in ((2.52,"Metal",-3.93),(2.34,"StageGlowGold",-3.84),
                                (1.82,"StageGlowMint",-3.79)):
        for i in range(28):
            a=math.pi*(.06+i*.88/28)
            b=math.pi*(.06+(i+1)*.88/28)
            p0=(math.cos(a)*radius,depth,3.40+math.sin(a)*radius)
            p1=(math.cos(b)*radius,depth,3.40+math.sin(b)*radius)
            strut("Backline halo tubing",p0,p1,.052 if color=="Metal" else .036,
                  color,8)
    for x in (-2.28,2.28):
        strut("Halo grounded support",(x,-3.95,1.42),(x,-3.95,3.30),.080,"Metal",10)
        box("Halo support foot",(x,-3.94,1.43),(.36,.32,.14),"Dark")
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
        round_part("Turned checkout post", (x, 0, 1.61), (.12, .12, 3.22), "Wood")
        round_part("Checkout post foot", (x, 0, .12), (.23, .23, .18), "Metal")
        strut("Checkout timber knee brace",(x,0,2.55),(x*.83,-.56,3.14),.065,"Wood",10)
    for i in range(5):
        box("Horizontal checkout timber", (0, 1.0, .24+i*.19), (3.22, .12, .13), "Wood" if i%2 else "Cream")
    disc("Painted counter emblem", (0, 1.09, .68), .32, .08, "Gold", 24)
    disc("Counter emblem inset", (0, 1.15, .68), .19, .09, "Mint", 24)
    counter_z, counter_thickness = 1.25, .10
    for i in range(7):
        x=-1.43+i*.475
        box("Countertop plank", (x, .57, counter_z), (.44, 1.05, counter_thickness), "Wood")
    def awning_z(x,y):
        front=(y+1.65)/2.8
        return 3.33-.38*front+.09*(1-(x/2.05)**2)
    fabric_grid("Checkout shaped canvas awning",20,12,
                lambda u,v: ((u-.5)*4.10,-1.65+v*2.8,awning_z((u-.5)*4.10,-1.65+v*2.8)),
                "CanvasMint",uv_scale=(2.4,1.8),thickness=.075)
    for side in (-1,1):
        for i in range(12):
            y0=-1.65+i*2.8/12
            y1=-1.65+(i+1)*2.8/12
            strut("Awning canvas hem",(side*2.05,y0,awning_z(side*2.05,y0)),
                  (side*2.05,y1,awning_z(side*2.05,y1)),.035,"CanvasCream",8)
    for i in range(6):
        x0=-2.05+i*4.10/6
        fabric_grid("Checkout scalloped valance",6,1,
                    lambda u,v,a=x0: (a+u*4.10/6,1.15,
                        awning_z(a+u*4.10/6,1.15)-(.14+.16*math.sin(math.pi*u))*v),
                    "CanvasRose" if i%2==0 else "CanvasGold",uv_scale=(.55,.55),thickness=.035)
    for y in (-1.3,.0,1.03):
        for i in range(12):
            x0=-1.99+i*3.98/12
            x1=-1.99+(i+1)*3.98/12
            strut("Checkout underside seam",(x0,y,awning_z(x0,y)-.07),
                  (x1,y,awning_z(x1,y)-.07),.019,"CanvasCream",6)
    for x in (-1.45, 1.45):
        strut("Warm string light", (x, 1.12, 2.91), (x, -1.42, 2.91), .035, "Gold", 12)
    # Cylinder z-scale is the full height (depth 1), so stack from the counter top:
    # each jar stands on the planks and its wider lid sinks 1 cm over the rim.
    counter_top = counter_z + counter_thickness / 2
    jar_height, lid_height = .29, .05
    for x, color in ((-.68,"Rose"),(.22,"Glass"),(.83,"Gold")):
        round_part("Checkout jar", (x, .22, counter_top + jar_height / 2), (.19, .19, jar_height), color, "cylinder")
        round_part("Checkout jar lid", (x, .22, counter_top + jar_height + lid_height / 2 - .01),
                   (.21, .21, lid_height), "Metal", "cylinder")


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
    for side in (-1,1):
        x=side*2.99
        box("Clinic weather skirt",(x,0,.44),(.045,4.65,.58),"CanvasMint")
        for y in (-1.32,1.32):
            box("Clinic screened side window",(x+side*.018,y,2.14),
                (.035,.91,.67),"CanvasDark")
            for edge in (y-.50,y+.50):
                box("Clinic window tape",(x+side*.048,edge,2.14),
                    (.025,.045,.78),"CanvasMint")
            for height in (1.76,2.52):
                box("Clinic window hem",(x+side*.048,y,height),
                    (.025,1.03,.04),"CanvasMint")
            box("Clinic rolled mesh flap",(x+side*.075,y,2.62),
                (.105,.98,.13),"CanvasCream")
        # A cross at eye height identifies the treatment space from either
        # side approach, even when the front sign is outside the camera.
        box("Clinic side cross vertical",(x+side*.09,0,2.12),
            (.045,.16,.61),"Rose")
        box("Clinic side cross horizontal",(x+side*.095,0,2.12),
            (.045,.53,.16),"Rose")
        strut("Clinic roof guy line",(side*3.06,1.93,3.15),
              (side*3.75,3.45,.13),.018,"CanvasCream",8)
        box("Clinic guy line stake",(side*3.75,3.45,.12),
            (.09,.09,.24),"Wood")
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
        box("Security kick plate",(side*3.025,0,.43),(.06,4.65,.55),"Dark")
        box("Security warning band",(side*3.055,0,1.30),(.042,4.60,.105),"Gold")
        for y in (-1.22,1.22):
            box("Shack inset side window",(side*3.02,y,1.96),(.055,1.23,.91),"AutoGlass")
            for frameY in (y-.67,y+.67):
                box("Window vertical frame",(side*3.07,frameY,1.96),(.075,.055,1.06),"Metal")
            for height in (1.45,2.48):
                box("Window sill frame",(side*3.07,y,height),(.075,1.37,.07),"Metal")
        for y in (-2.24,0,2.24):
            box("Prefab wall seam",(side*3.04,y,1.55),(.04,.045,2.76),"Metal")
        for y in (-1.90,-1.54,-.76,-.40,.40,.76,1.54,1.90):
            box("Security pressed-metal rib",(side*3.07,y,.86),
                (.045,.04,.66),"Metal")
        add("Security shield backing",(side*3.12,0,2.12),
            (.36,.36,.075),"Gold","cylinder",(0,math.pi/2,0),vertices=20)
        add("Security shield center",(side*3.17,0,2.12),
            (.27,.27,.04),"Dark","cylinder",(0,math.pi/2,0),vertices=20)
        box("Security shield bar",(side*3.195,0,2.12),
            (.035,.34,.075),"Gold")
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
    round_part("Security rooftop beacon base",(2.07,1.80,3.52),
               (.21,.21,.10),"Dark")
    round_part("Security rooftop beacon lens",(2.07,1.80,3.68),
               (.15,.15,.24),"Rose")
    round_part("Security rooftop beacon cap",(2.07,1.80,3.83),
               (.16,.16,.045),"Metal")


def shuttle():
    # Matches Unity's shuttle collision: floor top 0.32 m, door side at -Y
    # (Unity north, toward the festival) with a 2.6 m opening between the
    # doorway proxies at x=+/-1.3, solid far wall at +Y.
    box("Bus undercarriage", (0, 0, .12), (8.0, 2.8, .16), "Dark",
        edge_radius=.07,edge_segments=3)
    box("Bus floor", (0, 0, .26), (7.7, 2.7, .12), "Wood")
    fabric_grid("Curved shuttle roof",18,12,
                lambda u,v: ((u-.5)*7.50,(v-.5)*3.15,
                             2.78+.19*(1-((v-.5)*2)**2)),
                "PaintMint",uv_scale=(4,2),thickness=.11)
    for y in (-1.55,1.55):
        strut("Shuttle rain gutter",(-3.72,y,2.77),(3.72,y,2.77),.035,"Metal",8)
    # Lower corners remain within the old 8 m collision envelope. The upper
    # corners sweep inward to form a believable cab and rear hatch profile.
    for side in (-1,1):
        inner, outer, crown = side*1.31, side*4.00, side*3.53
        profile=[(inner,.28),(outer,.28),(outer,1.30),
                 (side*3.74,2.45),(crown,2.75),(inner,2.75)]
        xz_prism("Passenger-side coach panel",profile,-1.45,.23,
                 "PaintRose" if side<0 else "PaintMint")
        xz_prism("Far-side coach panel",profile,1.45,.23,"PaintMint")
        end=[(side*3.44,.25),(side*4.04,.25),(side*4.07,.43),
             (side*4.07,1.30),(side*3.74,2.45),
             (side*3.54,2.76),(side*3.44,2.76)]
        xz_prism("Molded shuttle end",end,0,2.94,
                 "PaintRose" if side<0 else "PaintMint",.075)
    box("Far-side center panel",(0,1.45,1.535),(2.62,.23,2.43),"PaintMint")
    for x in (-2.55, 0, 2.55):
        box("Far side window", (x, 1.575, 1.76), (1.65, .04, .78), "AutoGlass")
        box("Far side window sill",(x,1.615,1.33),(1.75,.06,.08),"Metal")
    # Door side: the shaped panels leave the original 2.6 m opening.
    for x in (-2.65, 2.65):
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
    # Windscreen follows the sloped shell at a constant small offset, rather
    # than floating as an upright rectangle ahead of it.
    front_glass=[(4.057,-1.19,1.42),(4.057,1.19,1.42),
                 (3.762,1.14,2.43),(3.762,-1.14,2.43)]
    formed_mesh("Integrated sloped windscreen",front_glass,[(0,1,2,3)],
                "AutoGlass",thickness=.018)
    for a,b in ((0,1),(1,2),(2,3),(3,0)):
        strut("Windscreen rubber seal",front_glass[a],front_glass[b],.025,"Rubber",8)
    strut("Windscreen center divider",(4.069,0,1.42),(3.774,0,2.43),.027,"Metal",8)
    rear_glass=[(-4.055,-1.04,1.51),(-4.055,1.04,1.51),
                (-3.782,1.04,2.36),(-3.782,-1.04,2.36)]
    formed_mesh("Inset rear hatch glass",rear_glass,[(3,2,1,0)],
                "AutoGlass",thickness=.018)
    for a,b in ((0,1),(1,2),(2,3),(3,0)):
        strut("Rear hatch glass seal",rear_glass[a],rear_glass[b],.024,"Rubber",8)
    box("Destination plate", (3.765, 0, 2.51), (.055, 1.78, .20), "Dark")
    box("Destination plate border",(3.794,0,2.51),(.025,1.85,.035),"Gold")
    for y in (-1.0, 1.0):
        box("Bus headlamp", (4.085, y, .70), (.06, .42, .24), "Cream")
        box("Bus taillamp", (-4.085, y, .70), (.06, .36, .24), "Rose")
    box("Bus nose grille",(4.083,0,.77),(.05,1.55,.31),"Dark")
    for y in (-.55,-.18,.18,.55):
        box("Bus grille vane",(4.119,y,.77),(.04,.055,.24),"Metal")
    box("Shuttle front bumper",(4.08,0,.38),(.18,3.10,.16),"Metal")
    box("Shuttle rear bumper",(-4.08,0,.38),(.18,3.10,.16),"Metal")
    box("Rear hatch seam", (-4.082, 0, .89), (.025, 2.34, .035), "Dark")
    box("Rear registration plate",(-4.097,0,.55),(.028,.73,.21),"Cream")
    box("Rear hatch pull",(-4.115,0,1.22),(.045,.45,.05),"Metal")
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


def fir_tree():
    """Tall narrow firs interrupt the broadleaf canopy and frame long views."""
    tapered_wood("Fir trunk", [(0,0,-.06),(.04,0,.28),(.08,.02,1.3),
        (.13,.03,3.0),(.10,.05,5.3),(.04,.07,7.55)],
        [.50,.41,.33,.25,.16,.045], 10)
    tiers = ((1.55,1.34,1.92),(2.65,1.62,2.05),(3.85,1.48,2.05),
             (5.05,1.17,1.89),(6.11,.78,1.51))
    for level,(base,radius,height) in enumerate(tiers):
        verts,faces,uvs=[],[],[]
        spokes=20
        rings=((0,.70),(.18,1.0),(.49,.83),(.75,.52),(1.0,.025))
        for ring,(t,relative_radius) in enumerate(rings):
            for spoke in range(spokes):
                angle=spoke*math.tau/spokes
                scallop=1+.075*math.sin(angle*7+level*1.7)
                spread=radius*relative_radius*scallop
                verts.append((.10*base/7.5+math.cos(angle)*spread,
                              math.sin(angle)*spread,
                              base+height*t+.085*math.sin(angle*7+level)*relative_radius))
                uvs.append((spoke/spokes,t))
        for ring in range(len(rings)-1):
            for spoke in range(spokes):
                a=ring*spokes+spoke;b=ring*spokes+(spoke+1)%spokes
                faces.append((a,b,b+spokes,a+spokes))
        faces.append(tuple(range(spokes-1,-1,-1)))
        formed_mesh("Layered fir boughs",verts,faces,
                    "Needle" if level%3 else "LeafWarm",uvs)
        for spoke in range(0,spokes,4):
            angle=spoke*math.tau/spokes
            strut("Visible fir branch",(.1,0,base+.27),
                  (math.cos(angle)*radius*.82,math.sin(angle)*radius*.82,base+.12),
                  .035,"Bark",6)


def grove_detail():
    """Small rock and fern assembly for the playable meadow's quiet margins."""
    for index,(x,y,r) in enumerate(((-.60,-.13,.41),(.02,.15,.29),(.58,-.13,.36))):
        rock=add("Weathered fieldstone",(x,y,.18*r/.4),
                 (r*1.10,r*.78,r*.61),"Stone","sphere",vertices=12)
        for vertex in rock.data.vertices:
            direction=vertex.co.normalized()
            vertex.co *= 1+.065*math.sin(direction.x*7+index*2)*math.cos(direction.y*5)
    for plant,(px,py,scale) in enumerate(((-.82,.41,1.0),(.78,.43,.76),(.25,-.62,.64))):
        verts,faces,uvs=[],[],[]
        for blade in range(7):
            angle=blade*math.tau/7+plant*.41
            tangent=Vector((math.cos(angle),math.sin(angle),0))
            side=Vector((-math.sin(angle),math.cos(angle),0))
            start=len(verts)
            for row in range(6):
                t=row/5
                center=Vector((px,py,.11))+tangent*(.68*scale*t)
                center.z+=scale*(.12+.60*math.sin(t*math.pi*.80)-.12*t)
                width=.16*scale*math.sin(math.pi*t)**.75
                for edge in (-1,1):
                    p=center+side*(edge*width)
                    verts.append(tuple(p));uvs.append(((edge+1)*.5,t))
            for row in range(5):
                a=start+row*2
                faces.append((a,a+1,a+3,a+2))
                faces.append((a+2,a+3,a+1,a))
        formed_mesh("Two-sided fern fronds",verts,faces,
                    "LeafWarm" if plant==1 else "Leaf",uvs)
    for index,(x,y) in enumerate(((-.28,.72),(.93,-.39),(-1.03,-.42))):
        strut("Dry meadow stem",(x,y,.07),(x+.07,y,.42),.018,"Bark",6)
        add("Seed head",(x+.07,y,.43),(.065,.065,.11),"Gold","sphere",vertices=10)


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


def wagon_body():
    """Crowned, tapered panel shell instead of a bevelled rectangular block."""
    sections = [
        (-2.39, 1.16, .92, .96), (-2.32, 1.27, 1.00, 1.04),
        (-2.12, 1.38, 1.10, 1.14), (-1.65, 1.43, 1.13, 1.17),
        (-.75, 1.43, 1.13, 1.17), (.30, 1.43, 1.13, 1.17),
        (1.10, 1.40, 1.13, 1.17), (1.55, 1.38, 1.10, 1.14),
        (2.08, 1.34, 1.02, 1.07), (2.32, 1.25, .93, .97),
        (2.40, 1.12, .86, .89),
    ]
    vertices, faces = [], []
    for y, width, edge, crown in sections:
        lower = width*.91
        profile = [(-lower*.72,.24),(-lower,.33),(-width,.51),
                   (-width,.82),(-width*.965,edge-.065),
                   (-width*.83,edge),(0,crown),
                   (width*.83,edge),(width*.965,edge-.065),
                   (width,.82),(width,.51),(lower,.33),(lower*.72,.24),(0,.23)]
        vertices.extend((x,y,z) for x,z in profile)
    ring = 14
    faces.append(tuple(reversed(range(ring))))
    for station in range(len(sections)-1):
        for spoke in range(ring):
            a=station*ring+spoke
            b=station*ring+(spoke+1)%ring
            faces.append((a,b,b+ring,a+ring))
    last=(len(sections)-1)*ring
    faces.append(tuple(last+i for i in range(ring)))
    body=formed_mesh("Camp wagon formed painted body",vertices,faces,"PaintRose")
    # Correct the winding after the longitudinal loft, before cutting the arches.
    bpy.context.view_layer.objects.active=body
    body.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    body.select_set(False)
    return body


def wagon_cabin():
    """Wagon glasshouse with a raked windshield, sloped hatch and crowned roof."""
    # Longitudinal stations are shared by the side panel and upper roof.
    outline = [(-2.20,1.08,1.19),(-2.08,1.73,1.08),
               (-1.97,1.83,1.03),(.70,1.83,1.03),
               (.83,1.76,1.07),(1.38,1.09,1.20)]
    vertices=[(-half,y,z) for y,z,half in outline]
    vertices += [(half,y,z) for y,z,half in outline]
    n=len(outline)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,n+(i+1)%n,n+i) for i in range(n)]
    cabin=formed_mesh("Camp wagon tapered cabin shell",vertices,faces,"PaintRose")
    roof=[(-1.97,1.84),(-1.66,1.85),(.42,1.85),(.70,1.84)]
    fabric_grid("Camp wagon crowned roof",8,3,
        lambda u,v:(-1.035+2.07*u,roof[0][0]+(roof[-1][0]-roof[0][0])*v,
                    1.83+.055*(1-(2*u-1)**2)),"PaintRose",thickness=.035)
    return cabin


def camp_car():
    # A compact camp wagon with formed panels, a long cabin and useful cargo hatch.
    # It shares its gameplay footprint with the previous hatchback.
    body = wagon_body()
    wheel_wells(body, (-1.51,1.52), .53, .40)
    wagon_cabin()
    strut("Camp wagon bonnet crease",(-1.15,1.39,1.12),(-1.08,2.27,.99),.012,"PaintRose",8)
    strut("Camp wagon bonnet crease",(1.15,1.39,1.12),(1.08,2.27,.99),.012,"PaintRose",8)
    windshield=[(-1.08,1.415,1.13),(1.08,1.415,1.13),
                (1.00,.855,1.75),(-1.00,.855,1.75)]
    formed_mesh("Camp wagon raked windshield",windshield,[(0,1,2,3)],"AutoGlass",thickness=.018)
    for a,b in ((0,1),(1,2),(2,3),(3,0)):
        strut("Camp wagon windshield gasket",windshield[a],windshield[b],.027,"Rubber",8)
    hatch=[(-1.08,-2.25,1.12),(1.08,-2.25,1.12),
           (1.00,-2.13,1.71),(-1.00,-2.13,1.71)]
    formed_mesh("Camp wagon rear hatch glass",hatch,[(3,2,1,0)],"AutoGlass",thickness=.018)
    for a,b in ((0,1),(1,2),(2,3),(3,0)):
        strut("Camp wagon hatch seal",hatch[a],hatch[b],.025,"Rubber",8)
    box("Camp wagon rear hatch lip",(0,-2.30,1.04),(2.57,.10,.12),"PaintRose",
        edge_radius=.025,edge_segments=2)
    for side in (-1, 1):
        x = side*1.225
        front=[(x,.03,1.17),(x,1.15,1.17),(side*1.12,.76,1.68),(side*1.12,.03,1.68)]
        rear=[(x,-1.88,1.17),(x,-.13,1.17),(side*1.12,-.13,1.68),
              (side*1.12,-1.82,1.68)]
        formed_mesh("Camp wagon front door glass",front,[(0,1,2,3)],"AutoGlass",thickness=.018)
        formed_mesh("Camp wagon cargo glass",rear,[(0,1,2,3)],"AutoGlass",thickness=.018)
        for pane in (front,rear):
            for a,b in ((0,1),(1,2),(2,3),(3,0)):
                strut("Camp wagon side glass gasket",pane[a],pane[b],.020,"Rubber",8)
        strut("Camp wagon B pillar",(side*1.245,-.06,1.13),
              (side*1.13,-.06,1.74),.055,"PaintRose",8)
        strut("Camp wagon cargo pillar",(side*1.245,-1.13,1.13),
              (side*1.13,-1.13,1.74),.055,"PaintRose",8)
        strut("Camp wagon rear D pillar",(side*1.23,-2.09,1.13),
              (side*1.12,-1.95,1.76),.065,"PaintRose",8)
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
        strut("Car roof gutter",(side*1.14,-2.06,1.83),(side*1.14,.76,1.83),.025,"Metal",8)
        strut("Camp wagon roof rail",(side*1.00,-1.92,1.94),
              (side*1.00,.55,1.94),.035,"Metal",8)
    box("Camp wagon rear hatch handle",(0,-2.45,.96),(.54,.055,.08),"Metal")
    strut("Camp wagon rear wiper",(-.70,-2.255,1.17),
          (.48,-2.245,1.25),.018,"Rubber",8)
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
    """An original two-tone camper with a continuous cab and seated glazing."""
    body=box("Camper van lower body",(0,0,.76),(2.95,5.26,1.18),"PaintRose",
        edge_radius=.16,edge_segments=4)
    wheel_wells(body, (-1.68,1.72), .56, .43)
    # Side profile and roof are joined along z=2.27. The rear face sits over
    # the lower body rather than ending 40 cm before its rear glazing.
    profile=[(-2.52,1.22),(2.43,1.22),(2.20,1.38),
             (1.69,2.27),(-2.38,2.27),(-2.52,2.13)]
    longitudinal_prism("Camper continuous cab",profile,1.31,"PaintCream")
    box("Camper gently crowned roof",(0,-.40,2.34),(2.71,4.38,.16),"PaintCream",
        edge_radius=.065,edge_segments=3)
    # The windshield corners are positioned 2-3 cm outside the sloped cab
    # face. The checks make a floating glass panel a generation error.
    def face_y(z):
        return 2.20+(1.69-2.20)*(z-1.38)/(2.27-1.38)
    glass_low=(2.18,1.45)
    glass_high=(1.762,2.18)
    assert .015 < glass_low[0]-face_y(glass_low[1]) < .04
    assert .015 < glass_high[0]-face_y(glass_high[1]) < .04
    corners=[(-1.16,glass_low[0],glass_low[1]),
             (1.16,glass_low[0],glass_low[1]),
             (1.12,glass_high[0],glass_high[1]),
             (-1.12,glass_high[0],glass_high[1])]
    formed_mesh("Camper integrated sloped windscreen",corners,[(0,3,2,1)],
                "AutoGlass",thickness=.018)
    for a,b in ((0,1),(1,2),(2,3),(3,0)):
        strut("Camper windscreen rubber seal",corners[a],corners[b],.025,"Rubber",8)
    strut("Camper visor lip",(-1.27,1.68,2.29),(1.27,1.68,2.29),.055,"PaintCream",8)
    for side in (-1,1):
        x=side*1.335
        box("Camper cab side glass",(x,1.12,1.78),(.04,.81,.72),"AutoGlass")
        box("Camper sliding side glass",(x,-.33,1.78),(.04,1.51,.72),"AutoGlass")
        box("Camper rear quarter glass",(x,-1.76,1.78),(.04,.73,.72),"AutoGlass")
        for y in (-1.34,.47,1.55):
            box("Camper structural window pillar",(side*1.34,y,1.78),(.09,.11,.80),"PaintCream")
        strut("Camper black window sill",(side*1.36,-2.15,1.40),
              (side*1.36,1.58,1.40),.027,"Rubber",8)
        box("Camper sliding door track",(side*1.45,-.42,1.27),(.055,2.1,.055),"Metal")
        box("Camper sliding door rear seam",(side*1.46,-1.55,.87),(.055,.035,.67),"Dark")
        box("Camper sliding door handle",(side*1.47,-.10,1.08),(.065,.30,.065),"Metal")
        box("Camper driver door seam",(side*1.46,1.42,.87),(.055,.035,.67),"Dark")
        box("Camper driver door handle",(side*1.47,1.00,1.09),(.065,.26,.065),"Metal")
        strut("Camper mirror stalk",(side*1.31,1.82,1.48),
              (side*1.65,1.99,1.47),.038,"Metal",8)
        box("Camper wing mirror",(side*1.68,2.01,1.49),(.19,.22,.19),"Dark")
        box("Camper sill",(side*1.45,-.06,.36),(.08,4.65,.13),"Dark")
        for y in (-1.68,1.72):
            add("Camper wheel",(side*1.48,y,.43),(.48,.48,.18),"Rubber","cylinder",
                (0,math.pi/2,0),vertices=24)
            add("Camper wheel hub",(side*1.60,y,.43),(.25,.25,.055),"Metal","cylinder",
                (0,math.pi/2,0),vertices=20)
            add("Camper wheel arch",(side*1.48,y,.43),(1,1,1),"Dark","torus",
                (0,math.pi/2,0),vertices=24,major=.48,minor=.04)
        strut("Camper roof rail",(side*1.08,-2.13,2.52),(side*1.08,1.36,2.52),.05,"Metal")
        disc("Camper round inset headlamp",(side*1.00,2.69,.91),.20,.07,"Cream",20)
        disc("Camper headlamp bezel",(side*1.00,2.66,.91),.245,.035,"Metal",24)
        box("Camper amber signal",(side*1.31,2.65,1.10),(.24,.065,.11),"Gold")
        box("Camper tail light",(side*1.15,-2.66,.91),(.19,.075,.51),"Rose")
    for y in (-1.64,.64):
        box("Camper roof rack crossbar",(0,y,2.57),(2.38,.065,.065),"Metal")
    box("Camper ventilation grille inset",(0,2.65,1.06),(1.36,.042,.21),"Dark")
    for x in (-.54,-.39,-.24,-.09,.06,.21,.36,.51):
        box("Camper ventilation slot",(x,2.68,1.06),(.035,.026,.13),"Metal")
    box("Camper lower front intake",(0,2.67,.64),(1.25,.07,.18),"Dark")
    box("Camper rear door seam",(0,-2.66,1.52),(.045,.04,1.43),"Dark")
    # Rear glass is 2.5 cm outside the cab's rear face, and inside the roof.
    assert abs(-2.545-profile[0][0]) < .04
    box("Camper rear window",(0,-2.545,1.75),(2.27,.045,.73),"AutoGlass")
    box("Camper rear door handle",(.45,-2.71,1.08),(.31,.065,.07),"Metal")
    box("Camper front bumper",(0,2.71,.34),(2.97,.20,.20),"Metal")
    box("Camper rear bumper",(0,-2.71,.34),(2.97,.20,.20),"Metal")
    box("Camper front registration",(0,2.82,.46),(.72,.025,.17),"Cream")
    for side in (-1,1):
        strut("Camper windscreen wiper",(side*.12,2.17,1.47),
              (side*1.02,2.10,1.57),.018,"Rubber",8)


def camp_shade():
    def canopy_z(x,y):
        radial=math.sqrt((x/4)**2+(y/4)**2)
        lift=max(0,1-radial)
        return 3.48+.72*lift**1.45-.045*(x*y/16)**2
    fabric_grid("Raised tension canopy",24,24,
                lambda u,v: ((u-.5)*8,(v-.5)*8,canopy_z((u-.5)*8,(v-.5)*8)),
                "CanvasMint",uv_scale=(4,4),thickness=.075)
    round_part("Pavilion center mast",(0,0,2.08),(.13,.13,4.16),"Wood")
    round_part("Pavilion mast foot",(0,0,.11),(.26,.26,.20),"Metal")
    round_part("Pavilion canvas crown",(0,0,4.21),(.25,.25,.13),"Gold")
    for edge in (-4,4):
        for i in range(16):
            lo=-4+i*.5
            hi=lo+.5
            strut("Shade sewn edge",(lo,edge,canopy_z(lo,edge)),
                  (hi,edge,canopy_z(hi,edge)),.035,"CanvasCream",8)
            strut("Shade sewn edge",(edge,lo,canopy_z(edge,lo)),
                  (edge,hi,canopy_z(edge,hi)),.035,"CanvasCream",8)
    # Scalloped valances make the thin canvas edge legible at walking height.
    for side in (-1,1):
        for panel in range(8):
            start=-4+panel
            fabric_grid("Pavilion front valance",8,1,
                lambda u,v,s=side,a=start: (a+u,s*4,canopy_z(a+u,s*4)-(.15+.17*math.sin(math.pi*u))*v),
                "CanvasRose" if side<0 else "CanvasCream",uv_scale=(.55,.55),thickness=.035)
            fabric_grid("Pavilion side valance",8,1,
                lambda u,v,s=side,a=start: (s*4,a+u,canopy_z(s*4,a+u)-(.15+.17*math.sin(math.pi*u))*v),
                "CanvasCream",uv_scale=(.55,.55),thickness=.035)
    for x in (-3.62, 3.62):
        for y in (-3.62, 3.62):
            round_part("Turned pavilion corner post", (x, y, 1.75), (.11, .11, 3.5), "Wood")
            round_part("Pavilion post foot", (x, y, .08), (.20, .20, .16), "Metal")
            round_part("Pavilion corner eyelet",(x,y,3.53),(.16,.16,.08),"Metal", "cylinder")
    # Seams follow the lifted canvas rather than floating across its top.
    for sx in (-1,1):
        for sy in (-1,1):
            for step in range(12):
                t0,t1=step/12,(step+1)/12
                p0=(sx*3.85*t0,sy*3.85*t0,canopy_z(sx*3.85*t0,sy*3.85*t0)-.055)
                p1=(sx*3.85*t1,sy*3.85*t1,canopy_z(sx*3.85*t1,sy*3.85*t1)-.055)
                strut("Radial pavilion seam",p0,p1,.018,"CanvasCream",6)


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


def print_text(label, text, position, size, color="Dark", rotation=(0,0,0), surface=None):
    bpy.ops.object.text_add(location=position, rotation=rotation)
    obj=bpy.context.object
    obj.name=label+"__"+color
    obj.data.body=text
    obj.data.align_x="CENTER"
    obj.data.align_y="CENTER"
    obj.data.size=size
    obj.data.extrude=0
    obj.data.resolution_u=3
    obj.data.materials.append(MATS[color])
    bpy.ops.object.convert(target="MESH")
    obj=bpy.context.object
    if surface is not None:
        inverse=obj.matrix_world.inverted()
        for vertex in obj.data.vertices:
            vertex.co=inverse@Vector(surface(obj.matrix_world@vertex.co))
        obj.data.update()
    current.append(obj)
    obj.select_set(False)


def merch_bag():
    # 55 cm body / 19 cm handle rise in source space; runtime scales to .65.
    # Both straps converge at the authored palm anchor (0,0,.79).
    for side in (-1,1):
        def panel(u,v,side=side):
            x=(u-.5)*(.49+.065*math.sin(v*math.pi))
            y=side*(.095+.025*math.sin(u*math.pi)*math.sin(v*math.pi))
            y+=side*.006*math.sin(u*math.pi*6)*math.sin(v*math.pi)
            return (x,y,.045+.58*v)
        fabric_grid("Tote bowed canvas panel",16,16,panel,"CanvasCream",flip=side>0,thickness=.006)
        fabric_grid("Reinforced sewn rim",16,1,lambda u,v,side=side: ((u-.5)*.49,side*.097,.603+.023*v),"CanvasGold",flip=side>0,thickness=.008)
        for x in (-.23,.23):
            for z in (.12,.20,.28,.36,.44,.52):
                strut("Tote small seam stitch",(x,side*.104,z),(x,side*.104,z+.026),.0018,"Cream",6)
        vertices=[]
        for i in range(25):
            t=math.pi*i/24
            for offset in (-.015,.015):
                vertices.append(((.17+offset)*math.cos(t),side*.095*(1-math.sin(t)),.60+(.19+offset)*math.sin(t)))
        faces=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(24)]
        formed_mesh("Continuous woven tote strap",vertices,faces,"Gold",thickness=.006)
        for x in (-.17,.17):
            box("Strap sewn reinforcement",(x,side*.103,.58),(.042,.009,.070),"CanvasGold")
    for side in (-1,1):
        fabric_grid("Tote folded gusset",6,12,lambda u,v,side=side:(side*(.245+.0325*math.sin(v*math.pi)-.018*math.sin(u*math.pi)),(u-.5)*.19,.045+.58*v),"CanvasGold",flip=side<0,thickness=.006)
    box("Tote bottom seam",(0,0,.049),(.49,.19,.008),"CanvasGold")
    box("Dark open tote interior",(0,0,.611),(.46,.17,.005),"CanvasDark")
    disc("Printed festival sun",(0,-.123,.37),.069,.002,"Rose",24)
    for i in range(8):
        a=i*math.tau/8
        strut("Sun print ray",(.082*math.cos(a),-.124,.37+.082*math.sin(a)),(.103*math.cos(a),-.124,.37+.103*math.sin(a)),.003,"Rose",6)
    print_text("Tote festival print","AFTER HOURS",(0,-.126,.22),.045,"Dark",(math.pi/2,0,0))


def stock_tin():
    # Connected lathed wall, rolled base, lid seam and raised cap.
    rings=[(.010,.211),(.018,.236),(.032,.241),(.045,.229),(.495,.229),(.508,.240),(.525,.240),(.532,.232)]
    vertices=[]
    for z,r in rings:
        for i in range(40):
            a=i*math.tau/40
            vertices.append((r*math.cos(a),r*math.sin(a),z))
    faces=[]
    for ring in range(len(rings)-1):
        for i in range(40):
            a=ring*40+i;b=ring*40+(i+1)%40
            faces.append((a,b,b+40,a+40))
    faces.extend([tuple(reversed(range(40))),tuple((len(rings)-1)*40+i for i in range(40))])
    formed_mesh("Tin formed metal wall",vertices,faces,"Metal")
    add("Rolled lid",(0,0,.553),(.245,.245,.042),"PaintMint","cylinder",vertices=40)
    add("Lid seal",(0,0,.528),(.241,.241,.009),"Dark","cylinder",vertices=40)
    add("Inset lid face",(0,0,.577),(.214,.214,.004),"Cream","cylinder",vertices=40)
    fabric_grid("Wrapped printed paper label",40,1,lambda u,v:(.2305*math.cos(u*math.tau),.2305*math.sin(u*math.tau),.12+.31*v),"Cream",uv_scale=(3,1),thickness=.001)
    for z in (.145,.409):
        fabric_grid("Label color band",40,1,lambda u,v,z=z:(.232*math.cos(u*math.tau),.232*math.sin(u*math.tau),z+.012*v),"Rose",thickness=.001)
    for side in (-1,1):
        # Two-sided printing remains visible in either hand orientation.
        rotation=(math.pi/2,0,0 if side<0 else math.pi)
        print_text("Tin label name","PRISM",(0,side*.233,.32),.062,"Dark",rotation,surface=lambda p,side=side:(p.x,side*math.sqrt(.233**2-p.x**2),p.z))
        print_text("Tin label subtitle","FESTIVAL SUPPLY",(0,side*.234,.245),.027,"Rose",rotation,surface=lambda p,side=side:(p.x,side*math.sqrt(.234**2-p.x**2),p.z))
    for i in range(5):
        box("Lid embossed ribs",(-.10+i*.05,0,.581),(.015,.14,.004),"Metal")


def map_height(x):
    points=[(-.25,.028),(-.083,.038),(.083,.025),(.25,.034)]
    for (a,za),(b,zb) in zip(points,points[1:]):
        if x<=b:return za+(zb-za)*(x-a)/(b-a)
    return points[-1][1]


def paper_map():
    fabric_grid("Three folded paper panels",3,1,lambda u,v:((u-.5)*.5,(v-.5)*.68,map_height((u-.5)*.5)),"Cream",thickness=.003)
    print_text("Map title","SITE GUIDE",(0,.265,.043),.052,surface=lambda p:(p.x,p.y,map_height(p.x)+.002))
    route=[(-.14,-.26),(-.14,-.11),(-.02,-.07),(.10,.03),(.10,.18)]
    for (x,y),(xx,yy) in zip(route,route[1:]):
        segments=max(2,math.ceil(math.hypot(xx-x,yy-y)/.012))
        for i in range(segments):
            ax=x+(xx-x)*i/segments;ay=y+(yy-y)*i/segments
            bx=x+(xx-x)*(i+1)/segments;by=y+(yy-y)*(i+1)/segments
            strut("Printed walking route",(ax,ay,map_height(ax)+.003),(bx,by,map_height(bx)+.003),.003,"Mint",6)
    for x,y,label,color in [(-.14,-.25,"CAMP","Rose"),(.10,.16,"SUN","Gold"),(-.14,.12,"MOON","Blue"),(.15,-.17,"HELP","Mint")]:
        add("Printed map landmark",(x,y,map_height(x)+.004),(.026,.026,.001),color,"cylinder",vertices=16)
        print_text("Map landmark label",label,(x,y-.044,map_height(x)+.007),.028,surface=lambda p:(p.x,p.y,map_height(p.x)+.002))
    for x in (-.083,.083):
        strut("Paper fold highlight",(x,-.335,map_height(x)+.003),(x,.335,map_height(x)+.003),.0015,"White",6)


def printed_card(voucher=False):
    width,height=(.60,.37) if voucher else (.45,.67)
    box("Printed paper stock",(0,0,.02),(width,height,.004),"White" if voucher else "Gold")
    box("Card header band",(0,height*.32,.023),(width*.90,height*.19,.001),"Mint" if voucher else "Dark")
    print_text("Card heading","FIRST AID" if voucher else "STAGE CREW",(0,height*.32,.025),.047,"Dark" if voucher else "Cream")
    if voucher:
        box("Aid cross upright",(-.19,-.01,.025),(.028,.10,.001),"Mint")
        box("Aid cross horizontal",(-.19,-.01,.026),(.10,.028,.001),"Mint")
        print_text("Voucher instruction","ONE GOOD DEED",(.04,-.015,.025),.028)
        print_text("Voucher footer","AFTER HOURS",(0,-.13,.025),.03)
    else:
        print_text("Pass access","ALL NIGHT",(0,.02,.025),.065)
        print_text("Pass festival","AFTER HOURS",(0,-.10,.025),.038)
        for i in range(23):
            box("Printed pass barcode",(-.16+i*.014,-.23,.025),(.004+(i%3)*.001,.085,.001),"Dark")
        box("Pass punched slot",(0,.295,.026),(.095,.014,.001),"Dark")


def small_gear(kind):
    if kind == "Confetti":
        round_part("Goofy confetti barrel", (0, 0, .36), (.17, .17, .65), "Rose")
        box("Handle", (0, -.19, .18), (.20, .17, .33), "Dark")
    elif kind == "MerchBag":
        merch_bag()
    elif kind == "Map":
        paper_map()
    elif kind == "StagePass":
        printed_card()
    elif kind == "Stock":
        stock_tin()
    elif kind == "Voucher":
        printed_card(True)
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
    if GEAR_ONLY and name not in {"Festival"+n for n in GEAR_NAMES}:
        return
    current.clear()
    build()
    # Paper faces the reader in the authored handheld orientation.
    if name in ("FestivalMap", "FestivalStagePass", "FestivalVoucher"):
        for obj in current:
            obj.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ obj.matrix_world
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
    ("FestivalTreeFir", fir_tree), ("FestivalGroveDetail", grove_detail),
    ("FestivalTent", tent), ("FestivalDomeTent", dome_tent), ("FestivalPoi", poi),
    ("FestivalWristband", wristband),
    ("FestivalCampCar", camp_car), ("FestivalCampVan", camp_van), ("FestivalCampShade", camp_shade),
    ("FestivalCampShop", camp_shop),
    ("FestivalPortaPotty", porta_potty), ("FestivalLittleSpoon", little_spoon),
):
    export(name, build)

for name in GEAR_NAMES:
    export("Festival" + name, lambda n=name: small_gear(n))

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / ("FestivalGear.blend" if GEAR_ONLY else "FestivalWorld.blend")))
(SOURCE / ("gear-manifest.json" if GEAR_ONLY else "world-manifest.json")).write_text(json.dumps({
    "source": "Original scripted Blender geometry; no external assets",
    "runtimeDirectory": "Assets/Festival/Art/Resources",
    "models": manifest,
}, indent=2) + "\n")
print("FESTIVAL WORLD KIT EXPORTED", len(manifest), "models")
