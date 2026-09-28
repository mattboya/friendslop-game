"""Temporary orthographic Solid-style review captures, never saved as art cameras."""
from pathlib import Path
import os

import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(Path(os.environ.get(
    "FESTIVAL_CHARACTER_REVIEW", root / "artifacts/character-fit-review.blend"))))
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 480
scene.render.resolution_y = 480
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.show_shadows = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (.12, .14, .18)
camera_data = bpy.data.cameras.new("Temporary fit review orthographic lens")
camera_data.type = "ORTHO"
camera = bpy.data.objects.new("Temporary fit review camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
output = Path(os.environ.get("FESTIVAL_CHARACTER_REVIEW_OUTPUT", root / "artifacts/fit-review"))
output.mkdir(parents=True, exist_ok=True)
shots = {
    "front": ((0, -6, 1.2), (0, 0, 1.2), 3.3),
    "back": ((0, 6, 1.2), (0, 0, 1.2), 3.3),
    "left": ((-6, 0, 1.2), (0, 0, 1.2), 3.3),
    "right": ((6, 0, 1.2), (0, 0, 1.2), 3.3),
    "top": ((0, 0, 6), (0, 0, 1.2), 3.3),
    "bottom": ((0, 0, -4), (0, 0, 1.2), 3.3),
    "head-front": ((0, -4, 1.92), (0, 0, 1.92), 1.15),
    "head-side": ((4, 0, 1.92), (0, 0, 1.92), 1.15),
    "backpack": ((0, 4, 1.25), (0, 0, 1.25), 1.6),
}
for name, (position, target, scale) in shots.items():
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.ortho_scale = scale
    scene.render.filepath = str(output / (name + ".png"))
    bpy.ops.render.render(write_still=True)
    print("FESTIVAL FIT VIEW", name, scene.render.filepath)
