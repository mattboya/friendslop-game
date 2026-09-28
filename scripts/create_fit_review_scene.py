"""Save a single focused Solid-viewport assembly for six-side fit critique."""
from pathlib import Path
import math
import os

import bpy
from mathutils import Euler, Vector

root = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(Path(os.environ.get(
    "FESTIVAL_CHARACTER_SOURCE", root / "ArtSource/Generated/FestivalCharacter.blend"))))
visible = {
    "Body_1_2", "Face_1_1", "Shirt_3", "Pants_2", "Shoes_3",
    "Headgear_2", "Sunglasses_2", "Hairstyle_3", "HairUnderHat",
    "Accessory_0", "Role_Friend", "Equipment_LittleSpoon",
}
for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    show = obj.name in visible
    obj.hide_set(not show)
    obj.hide_render = not show
    if obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks:
            if key.name.startswith("Fit_"):
                key.value = 1 if key.name == "Fit_1_2" else 0
if os.environ.get("FESTIVAL_REVIEW_POSE") == "walk-left":
    pose = bpy.data.objects["FestivalRig"].pose.bones
    for name, degrees in {"LegL": 30, "ShinL": 24, "FootL": -54,
                          "LegR": -30, "FootR": 30}.items():
        pose[name].rotation_mode = "XYZ"
        pose[name].rotation_euler.x = math.radians(degrees)
bpy.ops.object.select_all(action="DESELECT")
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type != "VIEW_3D":
            continue
        space = area.spaces.active
        space.shading.type = "SOLID"
        space.shading.color_type = "MATERIAL"
        space.shading.light = "STUDIO"
        space.shading.show_cavity = True
        space.overlay.show_floor = False
        space.overlay.show_axis_x = False
        space.overlay.show_axis_y = False
        space.overlay.show_extras = False
        space.region_3d.view_location = Vector((0, 0, 1.2))
        space.region_3d.view_distance = 4.8
        space.region_3d.view_rotation = Euler((math.radians(90), 0, 0)).to_quaternion()
out = Path(os.environ.get("FESTIVAL_CHARACTER_REVIEW", root / "artifacts/character-fit-review.blend"))
out.parent.mkdir(exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(out))
print("FESTIVAL FIT REVIEW SCENE", out)
