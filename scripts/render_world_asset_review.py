"""Disposable neutral-light geometry views of staged Blender world assets."""
import json
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
if len(args) != 2:
    raise SystemExit("Usage: blender --background --python scripts/render_world_asset_review.py -- <stage-directory> <output-directory>")
stage, out = map(Path, args)
stage, out = stage.resolve(), out.resolve()
out.mkdir(parents=True, exist_ok=True)
manifest = json.loads((stage / "ArtSource/world-manifest.json").read_text())
names = list(manifest["models"])
bpy.ops.wm.open_mainfile(filepath=str(stage / "ArtSource/FestivalWorld.blend"))
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 1100
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.show_shadows = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (.14, .17, .20)
camera_data = bpy.data.cameras.new("Disposable review lens")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("Disposable review camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, position, target, scale in (
    ("FestivalTent", (6,-8,4.2), (0,0,1.15), 5.9),
    ("FestivalDomeTent", (6,8,4.2), (0,0,1.0), 5.9),
    ("FestivalCampShade", (9,-11,7), (0,0,2), 11.5),
    ("FestivalTreeA", (8,-10,7), (0,0,3.1), 8.2),
    ("FestivalTreeB", (8,-10,7), (0,0,3.1), 8.2),
    ("FestivalTreeFir", (8,-10,8), (0,0,3.8), 9.8),
    ("FestivalGroveDetail", (4,-5,3.0), (0,0,.4), 4.2),
    ("FestivalCampCar", (7,8,3.5), (0,0,.85), 7.5),
    ("FestivalCampVan", (7,8,4), (0,0,1.2), 8.0),
    ("FestivalStage", (24,34,16), (0,0,3.8), 27),
    ("FestivalStallSupplies", (5,8,5), (0,0,1.6), 8.5),
    ("FestivalStallPerformance", (5,8,5), (0,0,1.6), 8.5),
    ("FestivalStallStock", (5,8,5), (0,0,1.6), 8.5),
    ("FestivalMedical", (7,9,6), (0,0,1.5), 8.6),
    ("FestivalSecurity", (7,9,6), (0,0,1.5), 8.6),
    ("FestivalShuttle", (11,-9,6), (0,0,1.5), 11),
):
    if os.environ.get("FESTIVAL_REVIEW_ASSET") and name != os.environ["FESTIVAL_REVIEW_ASSET"]:
        continue
    index = names.index(name)
    shift_x, shift_y = (index % 4)*25, -(index // 4)*25
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        selected = obj.get("kit_asset") == name
        obj.hide_render = not selected
        if selected:
            obj.location.x -= shift_x
            obj.location.y -= shift_y
    camera.location = position
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera_data.ortho_scale = scale
    scene.render.filepath = str(out / (name + ".png"))
    bpy.ops.render.render(write_still=True)
    for obj in scene.objects:
        if obj.type == "MESH" and obj.get("kit_asset") == name:
            obj.location.x += shift_x
            obj.location.y += shift_y
