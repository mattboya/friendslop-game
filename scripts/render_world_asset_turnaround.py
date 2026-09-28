"""Render six neutral inspection views of one staged world-kit mesh."""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
if len(args) != 3:
    raise SystemExit("Usage: blender --background --python scripts/render_world_asset_turnaround.py -- <stage-directory> <asset-name> <output-directory>")
stage, asset_name, output = Path(args[0]).resolve(), args[1], Path(args[2]).resolve()
manifest = json.loads((stage / "ArtSource/world-manifest.json").read_text())
if asset_name not in manifest["models"]:
    raise SystemExit(f"Unknown staged asset: {asset_name}")
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(stage / "ArtSource/FestivalWorld.blend"))
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 900
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.show_shadows = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (.14, .17, .20)
camera_data = bpy.data.cameras.new("Disposable turnaround lens")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 12.0 if asset_name == "FestivalCampShade" else 7.0
camera = bpy.data.objects.new("Disposable turnaround camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
index = list(manifest["models"]).index(asset_name)
shift_x, shift_y = (index % 4)*25, -(index // 4)*25
for obj in scene.objects:
    if obj.type == "MESH":
        selected = obj.get("kit_asset") == asset_name
        obj.hide_render = not selected
        if selected:
            obj.location.x -= shift_x
            obj.location.y -= shift_y
for label, position in (
    ("front", (0, 8, 2.3)), ("rear", (0, -8, 2.3)),
    ("left", (-8, 0, 2.3)), ("right", (8, 0, 2.3)),
    ("top", (0, 0, 9)), ("bottom", (0, 0, -8)),
):
    camera.location = position
    camera.rotation_euler = (Vector((0, 0, 1.2))-camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(output / f"{asset_name}-{label}.png")
    bpy.ops.render.render(write_still=True)
