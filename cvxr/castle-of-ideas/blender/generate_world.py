"""Generate the editable Castle of Ideas ChilloutVR world blockout.

Run with Blender, not regular Python. Geometry is intentionally primitive and
material-slot driven so dimensions and textures can be replaced without
reverse-engineering a monolithic mesh.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_DIR = SCRIPT_DIR.parent
DEFAULT_CONFIG = PROJECT_DIR / "design" / "layout.json"

COLLECTIONS: dict[str, bpy.types.Collection] = {}
MATERIALS: dict[str, bpy.types.Material] = {}
PHOTO_TEXTURES = {
    "MAT_MAIN_WALL_PLASTER": ("main-stone", (4.0, 1.2)),
    "MAT_MAIN_FLOOR_STONE": ("floor-stone", (2.0, 2.0)),
    "MAT_MAIN_CEILING_STONE": ("ceiling-stone", (2.0, 2.0)),
    "MAT_CORRIDOR_WALL": ("corridor-blue-stone", (4.0, 1.2)),
    "MAT_MAIN_WOOD": ("desk-wood", None),
    "MAT_CHAIR_OAK": ("chair-oak", None),
    "MAT_MAIN_UPHOLSTERY": ("black-leather", None),
    "MAT_DESK_CARVING": ("desk-carving", None),
    "MAT_FIREPLACE_STONE": ("fireplace-stone", None),
    "MAT_FIREPLACE_RELIEF": ("fireplace-relief", None),
    "MAT_SUB_FLOOR_MARBLE": ("alice-white-planks", None),
    "MAT_SUB_TRIM_WHITE": ("alice-painted-wood", None),
    "MAT_SUB_CLOTH": ("alice-red-cloth", (0.18, 0.18)),
    "MAT_CHAIR_BACK_OAK": ("chair-back-oak", None),
}
LITERATURE_TEXTURES = {
    "MAT_ALICE_WALL_N": "wallpaper-north",
    "MAT_ALICE_WALL_E": "wallpaper-east",
    "MAT_ALICE_WALL_S": "wallpaper-south",
    "MAT_ALICE_WALL_W": "wallpaper-west",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    parser.add_argument(
        "--output", type=Path, default=SCRIPT_DIR / "castle-of-ideas.blend"
    )
    parser.add_argument("--export-fbx", type=Path)
    parser.add_argument("--render", type=Path)
    parser.add_argument("--render-elevator", type=Path)
    parser.add_argument("--render-main", type=Path)
    parser.add_argument("--render-fireplace", type=Path)
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)


def collection(name: str) -> bpy.types.Collection:
    if name not in COLLECTIONS:
        value = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(value)
        COLLECTIONS[name] = value
    return COLLECTIONS[name]


def move_to_collection(obj: bpy.types.Object, target_name: str) -> None:
    target = collection(target_name)
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    target.objects.link(obj)


def material(
    name: str,
    color: tuple[float, float, float, float],
    *,
    roughness: float = 0.55,
    metallic: float = 0.0,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    principled = result.node_tree.nodes.get("Principled BSDF")
    if principled:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = roughness
        principled.inputs["Metallic"].default_value = metallic
        if emission:
            emission_input = principled.inputs.get("Emission Color") or principled.inputs.get(
                "Emission"
            )
            if emission_input:
                emission_input.default_value = emission
            strength_input = principled.inputs.get("Emission Strength")
            if strength_input:
                strength_input.default_value = emission_strength
    MATERIALS[name] = result
    return result


def create_materials() -> None:
    material("MAT_MAIN_WALL_PLASTER", (0.105, 0.085, 0.11, 1), roughness=0.82)
    material("MAT_MAIN_FLOOR_STONE", (0.055, 0.06, 0.07, 1), roughness=0.75)
    material("MAT_MAIN_WOOD", (0.105, 0.035, 0.022, 1), roughness=0.5)
    material("MAT_MAIN_METAL", (0.42, 0.22, 0.055, 1), roughness=0.28, metallic=0.72)
    material("MAT_MAIN_UPHOLSTERY", (0.26, 0.018, 0.038, 1), roughness=0.72)
    material("MAT_SUB_WALL_IVORY", (0.89, 0.86, 0.82, 1), roughness=0.72)
    material("MAT_SUB_FLOOR_MARBLE", (0.72, 0.79, 0.82, 1), roughness=0.38)
    material("MAT_SUB_TRIM_WHITE", (0.94, 0.92, 0.91, 1), roughness=0.48)
    material("MAT_SUB_ACCENT_BLUE", (0.43, 0.65, 0.72, 1), roughness=0.62)
    material("MAT_SUB_UPHOLSTERY_ROSE", (0.74, 0.43, 0.49, 1), roughness=0.78)
    material("MAT_CORRIDOR_WALL", (0.28, 0.24, 0.27, 1), roughness=0.72)
    material("MAT_ELEVATOR_SILVER", (0.62, 0.65, 0.68, 1), roughness=0.28, metallic=0.85)
    material("MAT_ELEVATOR_CARPET", (0.018, 0.055, 0.11, 1), roughness=0.88)
    material("MAT_ELEVATOR_MIRROR", (0.2, 0.28, 0.34, 1), roughness=0.08, metallic=0.9)
    material("MAT_BLACKOUT", (0.008, 0.009, 0.012, 1), roughness=0.9)
    material("MAT_MAIN_CEILING_STONE", (0.3, 0.28, 0.24, 1), roughness=0.9)
    material("MAT_CHAIR_OAK", (0.3, 0.13, 0.04, 1), roughness=0.4)
    material("MAT_DESK_CARVING", (0.15, 0.12, 0.1, 1), roughness=0.45)
    material("MAT_FIREPLACE_STONE", (0.4, 0.4, 0.36, 1), roughness=0.9)
    material("MAT_FIREPLACE_RELIEF", (0.4, 0.4, 0.36, 1), roughness=0.9)
    material("MAT_SUB_CLOTH", (0.45, 0.06, 0.13, 1), roughness=0.9)
    material("MAT_CHAIR_BACK_OAK", (0.3, 0.13, 0.04, 1), roughness=0.45)
    material("MAT_FIRE_PREVIEW", (1, 0.16, 0.01, 1), emission=(1, 0.14, 0.005, 1), emission_strength=5)
    material(
        "MAT_SCREEN_PLACEHOLDER",
        (0.035, 0.095, 0.14, 1),
        roughness=0.2,
        emission=(0.08, 0.36, 0.58, 1),
        emission_strength=1.8,
    )
    material(
        "MAT_PORTAL_PLACEHOLDER",
        (0.04, 0.025, 0.11, 1),
        roughness=0.12,
        emission=(0.24, 0.14, 0.72, 1),
        emission_strength=2.5,
    )
    material(
        "MAT_LIGHT_WARM",
        (0.85, 0.51, 0.18, 1),
        roughness=0.3,
        emission=(1.0, 0.45, 0.12, 1),
        emission_strength=4.0,
    )
    material(
        "MAT_LIGHT_COOL",
        (0.78, 0.9, 1.0, 1),
        roughness=0.25,
        emission=(0.68, 0.86, 1.0, 1),
        emission_strength=3.0,
    )
    material("MAT_LIGHT_SUB", (0.96,0.93,0.85,1), roughness=0.25,
             emission=(1.0,0.94,0.84,1), emission_strength=3.0)
    texture_dir = PROJECT_DIR / "unity/Assets/CastleOfIdeas/Textures/PhotoDerived"
    for material_name, (texture_name, _) in PHOTO_TEXTURES.items():
        path = texture_dir / f"{texture_name}.png"
        if not path.exists():
            raise FileNotFoundError(f"Run extract_photo_textures.py first: {path}")
        target = MATERIALS[material_name]
        target.diffuse_color = (1, 1, 1, 1)
        nodes = target.node_tree.nodes
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = bpy.data.images.load(str(path), check_existing=True)
        target.node_tree.links.new(texture.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
        normal = nodes.new("ShaderNodeTexImage")
        normal.image = bpy.data.images.load(str(path.with_name(f"{texture_name}-normal.png")), check_existing=True)
        normal.image.colorspace_settings.name = "Non-Color"
        normal_node = nodes.new("ShaderNodeNormalMap")
        normal_node.inputs["Strength"].default_value = 0.35
        target.node_tree.links.new(normal.outputs["Color"], normal_node.inputs["Color"])
        target.node_tree.links.new(normal_node.outputs["Normal"], nodes.get("Principled BSDF").inputs["Normal"])
    for material_name, texture_name in LITERATURE_TEXTURES.items():
        target = material(material_name, (1, 1, 1, 1), roughness=0.95)
        path = texture_dir.parent / "AliceLiterature" / f"{texture_name}.png"
        texture = target.node_tree.nodes.new("ShaderNodeTexImage")
        texture.image = bpy.data.images.load(str(path), check_existing=True)
        target.node_tree.links.new(texture.outputs["Color"], target.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])


def empty(
    name: str,
    location=(0.0, 0.0, 0.0),
    rotation_z: float = 0.0,
    *,
    target_collection: str = "Markers",
    parent: bpy.types.Object | None = None,
    display: str = "PLAIN_AXES",
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = display
    obj.empty_display_size = 0.35
    obj.location = location
    obj.rotation_euler[2] = rotation_z
    collection(target_collection).objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def box(
    name: str,
    location,
    size,
    material_name: str,
    *,
    target_collection: str = "Architecture",
    parent: bpy.types.Object | None = None,
    rotation_z: float = 0.0,
    bevel: float = 0.025,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=(0.0, 0.0, rotation_z))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material_name in PHOTO_TEXTURES:
        # Dedicated per-face UVs: decorative photographs fill a face; masonry
        # repeats in metres rather than stretching one crop across the room.
        repeat = PHOTO_TEXTURES[material_name][1]
        uv_layer = obj.data.uv_layers.active or obj.data.uv_layers.new(name="UVMap")
        for polygon in obj.data.polygons:
            normal_axis = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
            axes = [axis for axis in range(3) if axis != normal_axis]
            if normal_axis == 1:
                axes = [0, 2]
            for loop_index in polygon.loop_indices:
                vertex = obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
                uv_layer.data[loop_index].uv = tuple(
                    (vertex[axis] + size[axis] / 2) / (repeat[i] if repeat else size[axis])
                    for i, axis in enumerate(axes)
                )
    if MATERIALS.get(material_name):
        obj.data.materials.append(MATERIALS[material_name])
    if bevel:
        modifier = obj.modifiers.new("Edge Softening", "BEVEL")
        modifier.width = min(bevel, min(size) * 0.2)
        modifier.segments = 2
    move_to_collection(obj, target_collection)
    if parent:
        obj.parent = parent
    return obj


def video_surface(
    name: str,
    location,
    width: float,
    height: float,
    faces: str,
    material_name: str,
    *,
    target_collection: str = "Screens",
) -> bpy.types.Object:
    """Create a single display quad with a full-range 0..1 UV map.

    Blender cube UVs allocate only part of the texture to each face, which crops
    a RenderTexture when a thin cube is used as a screen.  Keeping the visible
    surface as a dedicated quad also makes later material replacement predictable.
    """

    if faces in ("north", "south"):
        vertices = [
            (-width / 2, 0.0, -height / 2),
            (width / 2, 0.0, -height / 2),
            (width / 2, 0.0, height / 2),
            (-width / 2, 0.0, height / 2),
        ]
        if faces == "north":
            vertices = [vertices[index] for index in (1, 0, 3, 2)]
    elif faces in ("east", "west"):
        vertices = [
            (0.0, -width / 2, -height / 2),
            (0.0, width / 2, -height / 2),
            (0.0, width / 2, height / 2),
            (0.0, -width / 2, height / 2),
        ]
        if faces == "west":
            vertices = [vertices[index] for index in (1, 0, 3, 2)]
    else:
        raise ValueError(f"Unsupported display direction: {faces}")

    mesh = bpy.data.meshes.new(f"{name}_MESH")
    mesh.from_pydata(vertices, [], [(0, 1, 2, 3)])
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for loop, uv in zip(mesh.polygons[0].loop_indices, ((0, 0), (1, 0), (1, 1), (0, 1))):
        uv_layer.data[loop].uv = uv

    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    if MATERIALS.get(material_name):
        mesh.materials.append(MATERIALS[material_name])
    collection(target_collection).objects.link(obj)
    return obj


def cylinder(
    name: str,
    location,
    radius: float,
    depth: float,
    material_name: str,
    *,
    vertices: int = 16,
    target_collection: str = "Architecture",
    parent: bpy.types.Object | None = None,
    rotation=(0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(MATERIALS[material_name])
    move_to_collection(obj, target_collection)
    if parent:
        obj.parent = parent
    bevel_modifier = obj.modifiers.new("Edge Softening", "BEVEL")
    bevel_modifier.width = 0.025
    bevel_modifier.segments = 2
    return obj


def wall_x(name: str, x: float, y1: float, y2: float, height: float, thickness: float, mat: str):
    return box(name, (x, (y1 + y2) / 2, height / 2), (thickness, y2 - y1, height), mat)


def wall_y(name: str, y: float, x1: float, x2: float, height: float, thickness: float, mat: str):
    return box(name, ((x1 + x2) / 2, y, height / 2), (x2 - x1, thickness, height), mat)


def create_arch_frame(
    name: str,
    center,
    width: float,
    height: float,
    axis: str,
    material_name: str,
) -> None:
    thickness = 0.18
    depth = 0.34
    if axis == "x":
        box(f"{name}_Post_A", (center[0], center[1] - width / 2, height / 2), (depth, thickness, height), material_name)
        box(f"{name}_Post_B", (center[0], center[1] + width / 2, height / 2), (depth, thickness, height), material_name)
        box(f"{name}_Lintel", (center[0], center[1], height), (depth, width + thickness, 0.24), material_name)
    else:
        box(f"{name}_Post_A", (center[0] - width / 2, center[1], height / 2), (thickness, depth, height), material_name)
        box(f"{name}_Post_B", (center[0] + width / 2, center[1], height / 2), (thickness, depth, height), material_name)
        box(f"{name}_Lintel", (center[0], center[1], height), (width + thickness, depth, 0.24), material_name)


def create_architecture(config: dict) -> None:
    dims = config["dimensions"]
    height = dims["wallHeight"]
    thickness = dims["wallThickness"]
    floor_t = dims["floorThickness"]
    main = dims["main"]
    sub = dims["sub"]
    corridor = dims["corridor"]
    lift = dims["elevatorLobby"]
    openings = config["openings"]

    def floor_and_ceiling(prefix: str, bounds: dict, floor_mat: str):
        width = bounds["maxX"] - bounds["minX"]
        depth = bounds["maxY"] - bounds["minY"]
        center = ((bounds["minX"] + bounds["maxX"]) / 2, (bounds["minY"] + bounds["maxY"]) / 2)
        box(f"{prefix}_FLOOR", (center[0], center[1], -floor_t / 2), (width, depth, floor_t), floor_mat, bevel=0)
        ceiling = box(
            f"{prefix}_CEILING",
            (center[0], center[1], height + floor_t / 2),
            (width, depth, floor_t),
            "MAT_MAIN_CEILING_STONE" if prefix in ("MAIN", "CORRIDOR") else "MAT_SUB_WALL_IVORY",
            target_collection="Ceilings",
            bevel=0,
        )
        ceiling.hide_render = False

    floor_and_ceiling("MAIN", main, "MAT_MAIN_FLOOR_STONE")
    floor_and_ceiling("SUB", sub, "MAT_SUB_WALL_IVORY")
    create_alice_floor(config)
    floor_and_ceiling("CORRIDOR", corridor, "MAT_MAIN_FLOOR_STONE")
    lift_width = lift["maxX"] - lift["minX"]
    lift_depth = lift["maxY"] - lift["minY"]
    lift_center = (
        (lift["minX"] + lift["maxX"]) / 2,
        (lift["minY"] + lift["maxY"]) / 2,
    )
    lift_height = lift["height"]
    lift_y_offset = lift["maxY"] - 1.5
    box(
        "ELEVATOR_FLOOR",
        (lift_center[0], lift_center[1], -floor_t / 2),
        (lift_width, lift_depth, floor_t),
        "MAT_ELEVATOR_CARPET",
    )
    lift_ceiling = box(
        "ELEVATOR_CEILING",
        (lift_center[0], lift_center[1], lift_height + floor_t / 2),
        (lift_width, lift_depth, floor_t),
        "MAT_ELEVATOR_SILVER",
        target_collection="Ceilings",
    )
    lift_ceiling.hide_render = False

    # The virtual plan mirrors the venue: the main room is east/right of the
    # corridor, with its entrance in the west wall and the portal on the far
    # east wall.
    main_open = openings["mainToCorridor"]
    wall_x("MAIN_WALL_WEST_SOUTH", main["minX"], main["minY"], main_open["min"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_x("MAIN_WALL_WEST_NORTH", main["minX"], main_open["max"], main["maxY"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_x("MAIN_WALL_EAST", main["maxX"], main["minY"], main["maxY"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_y("MAIN_WALL_SOUTH", main["minY"], main["minX"], main["maxX"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_y("MAIN_WALL_NORTH", main["maxY"], main["minX"], main["maxX"], height, thickness, "MAT_MAIN_WALL_PLASTER")

    wall_y("SUB_WALL_NORTH", sub["maxY"], sub["minX"], sub["maxX"], height, thickness, "MAT_SUB_WALL_IVORY")
    wall_x("SUB_WALL_WEST", sub["minX"], sub["minY"], sub["maxY"], height, thickness, "MAT_SUB_WALL_IVORY")
    sub_open = openings["corridorToSub"]
    front_open = openings["corridorToSubFront"]
    wall_y("SUB_WALL_SOUTH_FRONT", sub["minY"], sub["minX"], front_open["min"], height, thickness, "MAT_SUB_WALL_IVORY")
    wall_y("SUB_WALL_SOUTH_WEST", sub["minY"], front_open["max"], sub_open["min"], height, thickness, "MAT_SUB_WALL_IVORY")
    wall_y("SUB_WALL_SOUTH_EAST", sub["minY"], sub_open["max"], sub["maxX"], height, thickness, "MAT_SUB_WALL_IVORY")

    # Full-height door heads close the gaps above the visible arch frames.
    header_height = height - dims["doorHeight"]
    header_z = dims["doorHeight"] + header_height / 2
    box("MAIN_WALL_DOOR_HEADER", (main_open["wall"], (main_open["min"] + main_open["max"]) / 2, header_z), (thickness, main_open["max"] - main_open["min"], header_height), "MAT_MAIN_WALL_PLASTER", bevel=0)
    box("SUB_WALL_DOOR_HEADER", ((sub_open["min"] + sub_open["max"]) / 2, sub_open["wall"], header_z), (sub_open["max"] - sub_open["min"], thickness, header_height), "MAT_SUB_WALL_IVORY", bevel=0)
    box("SUB_WALL_FRONT_DOOR_HEADER", ((front_open["min"] + front_open["max"]) / 2, front_open["wall"], header_z), (front_open["max"] - front_open["min"], thickness, header_height), "MAT_SUB_WALL_IVORY", bevel=0)
    # Independent surface layers let the hallway face stay blue stone while
    # the room face carries the white literary salon finish.
    for suffix, x1, x2 in (("FRONT", sub["minX"], front_open["min"]), ("WEST", front_open["max"], sub_open["min"]), ("EAST", sub_open["max"], sub["maxX"])):
        wall_y(f"CORRIDOR_BLUE_SUB_FACE_{suffix}", sub["minY"] - thickness / 2 - 0.006, x1, x2, height, 0.012, "MAT_CORRIDOR_WALL")
    box("CORRIDOR_BLUE_SUB_HEADER", ((sub_open["min"] + sub_open["max"]) / 2, sub_open["wall"] - thickness / 2 - 0.006, header_z), (sub_open["max"] - sub_open["min"], 0.012, header_height), "MAT_CORRIDOR_WALL", bevel=0)
    box("CORRIDOR_BLUE_SUB_FRONT_HEADER", ((front_open["min"] + front_open["max"]) / 2, front_open["wall"] - thickness / 2 - 0.006, header_z), (front_open["max"] - front_open["min"], 0.012, header_height), "MAT_CORRIDOR_WALL", bevel=0)
    empty("DOOR_MAIN_CENTER", (main_open["wall"], (main_open["min"] + main_open["max"]) / 2, 1.6))
    box("CORRIDOR_BLUE_MAIN_HEADER", (main_open["wall"] - thickness / 2 - 0.006, (main_open["min"] + main_open["max"]) / 2, header_z), (0.012, main_open["max"] - main_open["min"], header_height), "MAT_CORRIDOR_WALL", bevel=0)
    wall_x("SUB_WALL_EAST_IVORY_FACE", sub["maxX"] - thickness / 2 - 0.006, sub["minY"], sub["maxY"], height, 0.012, "MAT_SUB_WALL_IVORY")

    lift_open = openings["elevator"]
    wall_y("CORRIDOR_WALL_SOUTH_WEST", corridor["minY"], corridor["minX"], lift_open["min"], height, thickness, "MAT_CORRIDOR_WALL")
    wall_y("CORRIDOR_WALL_SOUTH_EAST", corridor["minY"], lift_open["max"], corridor["maxX"], height, thickness, "MAT_CORRIDOR_WALL")
    wall_x("CORRIDOR_WALL_WEST", corridor["minX"], corridor["minY"], corridor["maxY"], height, thickness, "MAT_CORRIDOR_WALL")
    box("CORRIDOR_WALL_ELEVATOR_HEADER", ((lift_open["min"] + lift_open["max"]) / 2, lift_open["wall"], header_z), (lift_open["max"] - lift_open["min"], thickness, header_height), "MAT_CORRIDOR_WALL", bevel=0)

    wall_x(
        "ELEVATOR_WALL_WEST",
        lift["minX"],
        lift["minY"],
        lift["maxY"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_SILVER",
    )
    wall_x(
        "ELEVATOR_WALL_EAST",
        lift["maxX"],
        lift["minY"],
        lift["maxY"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_SILVER",
    )
    wall_y(
        "ELEVATOR_WALL_BACK",
        lift["minY"],
        lift["minX"],
        lift["maxX"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_SILVER",
    )

    # Keep the entrance visibly open for the arrival shot. Closed targets are
    # exported as markers for a later CVR-synced sliding-door animation.
    box(
        "ELEVATOR_DOOR_LEFT_OPEN",
        (16.52, 1.42 + lift_y_offset, 1.15),
        (0.48, 0.12, 2.3),
        "MAT_ELEVATOR_SILVER",
    )
    box(
        "ELEVATOR_DOOR_RIGHT_OPEN",
        (18.48, 1.42 + lift_y_offset, 1.15),
        (0.48, 0.12, 2.3),
        "MAT_ELEVATOR_SILVER",
    )
    empty(
        "ELEVATOR_DOOR_LEFT_CLOSED",
        (16.95, 1.42 + lift_y_offset, 1.15),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-closed-target"
    empty(
        "ELEVATOR_DOOR_RIGHT_CLOSED",
        (18.05, 1.42 + lift_y_offset, 1.15),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-closed-target"
    empty(
        "ELEVATOR_DOOR_TRIGGER",
        (17.5, 1.05 + lift_y_offset, 1.0),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-trigger"

    # Plain silver cabin with handrails; no decorative paneling or controls.
    cylinder(
        "ELEVATOR_RAIL_BACK",
        (17.5, lift["minY"] + 0.24, 1.0),
        0.035,
        2.0,
        "MAT_ELEVATOR_SILVER",
        rotation=(0.0, math.pi / 2, 0.0),
    )
    cylinder(
        "ELEVATOR_RAIL_WEST",
        (lift["minX"] + 0.24, lift_y_offset, 1.0),
        0.035,
        2.2,
        "MAT_ELEVATOR_SILVER",
        rotation=(math.pi / 2, 0.0, 0.0),
    )
    cylinder(
        "ELEVATOR_RAIL_EAST",
        (lift["maxX"] - 0.24, lift_y_offset, 1.0),
        0.035,
        2.2,
        "MAT_ELEVATOR_SILVER",
        rotation=(math.pi / 2, 0.0, 0.0),
    )
    box(
        "ELEVATOR_CEILING_LIGHT",
        (17.5, lift_y_offset, lift_height - 0.08),
        (1.45, 1.15, 0.07),
        "MAT_LIGHT_COOL",
        target_collection="Lighting",
        bevel=0.04,
    )
    empty(
        "ELEVATOR_DING_AUDIO",
        (17.5, 1.2 + lift_y_offset, 2.4),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "audio-source-placeholder"


    # Dark-academia structural rhythm: timber pilasters and beams.
    main_center_x = (main["minX"] + main["maxX"]) / 2
    main_depth = main["maxY"] - main["minY"]
    px0, py0, px1, py1 = config["mainPiers"]["planRoomPixels"]
    for index, (left, top, right, bottom) in enumerate(config["mainPiers"]["rectanglesPixels"]):
        pier_width = (right - left) / (px1 - px0) * (main["maxX"] - main["minX"])
        pier_depth = (bottom - top) / (py1 - py0) * main_depth
        y = main["maxY"] - ((top + bottom) / 2 - py0) / (py1 - py0) * main_depth
        for side, x in (("W", main["minX"] + pier_width / 2), ("E", main["maxX"] - pier_width / 2)):
            if side == "W" and index == 1:
                continue  # Clear the entrance-side pillar at the user's request.
            fireplace = config["fireplace"]
            if side == "W" and abs(y - fireplace["center"][1]) < (pier_depth + fireplace["width"]) / 2:
                continue
            portal = config["screens"]["portal"]
            if side == "E" and abs(y - portal["center"][1]) < (pier_depth + portal["width"]) / 2 + 0.12:
                continue
            box(f"MAIN_PILASTER_{side}_{index:02d}", (x, y, height / 2), (pier_width, pier_depth, height), "MAT_MAIN_WALL_PLASTER", bevel=0)
    for index in range(4):
        y = main["minY"] + 1.2 + index * (main_depth - 2.4) / 3
        box(f"MAIN_CEILING_BEAM_{index:02d}", (main_center_x, y, height - 0.35), (main["maxX"] - main["minX"] - 0.5, 0.25, 0.7), "MAT_MAIN_CEILING_STONE", target_collection="Ceilings", bevel=0)



def create_table(name: str, center, size, style: str) -> bpy.types.Object:
    mat = "MAT_MAIN_WOOD" if style == "main" else "MAT_SUB_TRIM_WHITE"
    accent = "MAT_MAIN_METAL" if style == "main" else "MAT_SUB_ACCENT_BLUE"
    root = empty(name, center, target_collection="Furniture")
    root["castle_of_ideas_role"] = "table"
    width, depth, height = size
    box(f"{name}_Top", (0.0, 0.0, height), (width, depth, 0.12), mat, target_collection="Furniture", parent=root, bevel=0.05)
    if style == "main":
        # Front and side panels, stepped cornice and bun feet observed in the
        # supplied desk photographs. Omit the photographed letter medallion.
        box(f"{name}_Cornice", (0, 0, height - 0.08), (width - 0.04, depth + 0.04, 0.07), mat, target_collection="Furniture", parent=root, bevel=0.015)
        box(f"{name}_FrontPanel", (0, depth / 2 - 0.05, height / 2 + 0.04), (width - 0.12, 0.09, height - 0.2), mat, target_collection="Furniture", parent=root)
        box(f"{name}_FloralFrieze", (0, depth / 2 + 0.005, height - 0.22), (width - 0.18, 0.035, 0.19), "MAT_DESK_CARVING", target_collection="Furniture", parent=root, bevel=0.005)
        for side in (-1, 1):
            box(f"{name}_Side_{side}", (side * (width / 2 - 0.07), 0, height / 2 + 0.04), (0.1, depth - 0.1, height - 0.2), mat, target_collection="Furniture", parent=root)
            for end in (-1, 1):
                turned_post(f"{name}_Column_{side}_{end}", (side * (width / 2 - 0.09), end * (depth / 2 - 0.08), 0.03), 0.67, 0.065, mat, root)
        return root
    inset_x = width / 2 - 0.22
    inset_y = depth / 2 - 0.16
    for suffix, x, y in (
        ("NW", -inset_x, inset_y),
        ("NE", inset_x, inset_y),
        ("SW", -inset_x, -inset_y),
        ("SE", inset_x, -inset_y),
    ):
        box(f"{name}_Leg_{suffix}", (x, y, height / 2), (0.11, 0.11, height), accent, target_collection="Furniture", parent=root, bevel=0.015)
    if style == "sub":
        box(f"{name}_RedClothTop", (0, 0, height + 0.065), (width + 0.05, depth + 0.05, 0.015), "MAT_SUB_CLOTH", target_collection="Furniture", parent=root, bevel=0.005)
        for side in (-1, 1):
            box(f"{name}_ClothDrop_{side}", (0, side * (depth / 2 + 0.015), height - 0.06), (width + 0.05, 0.02, 0.23), "MAT_SUB_CLOTH", target_collection="Furniture", parent=root, bevel=0.005)
        for side in (-1, 1):
            box(f"{name}_ClothEnd_{side}", (side * (width / 2 + 0.015), 0, height - 0.06), (0.02, depth + 0.05, 0.23), "MAT_SUB_CLOTH", target_collection="Furniture", parent=root, bevel=0.005)
    return root


def turned_post(name: str, position, height: float, radius: float, mat: str, parent, twist: bool = False):
    vertices = []
    faces = []
    rings, sides = 13, 8
    for ring in range(rings):
        t = ring / (rings - 1)
        profile = 0.7 + 0.3 * math.cos(t * math.pi * 6) ** 2
        if t < 0.1 or t > 0.9:
            profile = 1.1
        for side in range(sides):
            angle = side * math.tau / sides + (t * math.tau * 1.5 if twist else 0)
            r = radius * profile * (1.0 if not twist or side % 2 == 0 else 0.65)
            vertices.append((position[0] + r * math.cos(angle), position[1] + r * math.sin(angle), position[2] + t * height))
    for ring in range(rings - 1):
        for side in range(sides):
            a = ring * sides + side
            b = ring * sides + (side + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.extend((tuple(reversed(range(sides))), tuple((rings - 1) * sides + s for s in range(sides))))
    mesh = bpy.data.meshes.new(name + "_MESH")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection("Furniture").objects.link(obj)
    obj.parent = parent
    mesh.materials.append(MATERIALS[mat])
    # Cylindrical UVs are local and deterministic.
    uv = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        for loop in polygon.loop_indices:
            index = mesh.loops[loop].vertex_index
            uv.data[loop].uv = ((index % sides) / sides, (index // sides) / (rings - 1))
    return obj


def create_chair(name: str, location, rotation_z: float, style: str) -> bpy.types.Object:
    frame = "MAT_CHAIR_OAK" if style == "main" else "MAT_SUB_TRIM_WHITE"
    upholstery = "MAT_MAIN_UPHOLSTERY" if style == "main" else "MAT_SUB_UPHOLSTERY_ROSE"
    root = empty(name, location, rotation_z, target_collection="Furniture")
    root["castle_of_ideas_role"] = "seat"
    root["castle_of_ideas_gimmick"] = "CVRInteractable.SitAtPosition"
    box(f"{name}_Seat", (0.0, 0.0, 0.5), (0.52, 0.52, 0.12), upholstery, target_collection="Furniture", parent=root, bevel=0.055)
    if style == "main":
        box(f"{name}_SeatFrame", (0, 0, 0.44), (0.57, 0.56, 0.08), frame, target_collection="Furniture", parent=root)
        for x in (-0.235, 0.235):
            for y in (-0.22, 0.22):
                turned_post(f"{name}_Leg_{x}_{y}", (x, y, 0.03), 0.4, 0.035, frame, root, twist=True)
            turned_post(f"{name}_BackPost_{x}", (x, -0.235, 0.48), 0.92, 0.031, frame, root, twist=True)
            turned_post(f"{name}_Finial_{x}", (x, -0.235, 1.4), 0.08, 0.041, frame, root)
            box(f"{name}_SideBrace_{x}", (x, 0, 0.19), (0.035, 0.45, 0.035), frame, target_collection="Furniture", parent=root)
        for index, x in enumerate((-0.13, 0, 0.13)):
            box(f"{name}_BackSlat_{index}", (x, -0.235, 0.96), (0.075, 0.035, 0.69), frame, target_collection="Furniture", parent=root, bevel=0.008)
        box(f"{name}_TopRail", (0, -0.235, 1.36), (0.49, 0.065, 0.14), "MAT_CHAIR_BACK_OAK", target_collection="Furniture", parent=root)
        box(f"{name}_LowerRail", (0, -0.235, 0.63), (0.5, 0.045, 0.05), frame, target_collection="Furniture", parent=root)
        box(f"{name}_FootBrace", (0, 0.2, 0.19), (0.48, 0.035, 0.035), frame, target_collection="Furniture", parent=root)
    else:
        # The salon photograph shows simple weathered plank chairs rather than
        # upholstered lounge chairs: white paint over timber, with open backs.
        bpy.data.objects[f"{name}_Seat"].data.materials[0] = MATERIALS[frame]
        for x in (-0.22, 0.22):
            for y in (-0.21, 0.21):
                box(f"{name}_TimberLeg_{x}_{y}", (x, y, 0.25), (0.055, 0.055, 0.5), "MAT_CHAIR_OAK", target_collection="Furniture", parent=root, bevel=0.008)
            box(f"{name}_BackUpright_{x}", (x, -0.23, 0.80), (0.065, 0.055, 0.64), frame, target_collection="Furniture", parent=root)
        box(f"{name}_PlankBack", (0, -0.23, 1.05), (0.52, 0.065, 0.24), frame, target_collection="Furniture", parent=root)
        box(f"{name}_FootBrace", (0, 0.18, 0.17), (0.48, 0.035, 0.04), "MAT_CHAIR_OAK", target_collection="Furniture", parent=root)
    seat_point = empty(f"{name}_SeatPoint", (0.0, 0.0, 0.64), target_collection="Markers", parent=root)
    seat_point["castle_of_ideas_role"] = "sit-position"
    exit_point = empty(f"{name}_ExitPoint", (0.0, -0.92, 0.0), target_collection="Markers", parent=root)
    exit_point["castle_of_ideas_role"] = "sit-exit"
    return root


def create_furniture(config: dict) -> None:
    furniture = config["furniture"]
    main = furniture["main"]
    dimensions = config["dimensions"]
    main_bounds = dimensions["main"]
    sub_bounds = dimensions["sub"]
    sub_screen = config["screens"]["sub"]
    chair_index = 0
    for row_index, y in enumerate(main["tableRows"]):
        for column_index, x in enumerate(main["tableColumns"]):
            table_name = f"MAIN_TABLE_R{row_index + 1:02d}_C{column_index + 1:02d}"
            create_table(table_name, (x, y, 0.0), main["tableSize"], "main")
            for seat in range(main["chairsPerTable"]):
                chair_index += 1
                chair_x = x + (-1 if seat == 0 else 1) * (main["tableSize"][0] / 2 - main["chairEdgeInset"] - main["chairFrameWidth"] / 2)
                create_chair(f"MAIN_CHAIR_{chair_index:03d}", (chair_x, y - 0.72, 0.0), 0.0, "main")

    sub = furniture["sub"]
    chair_index = 0
    for table_index, x in enumerate(sub["tableCentersX"]):
        y = sub["tableCenterY"]
        table_name = f"SUB_TABLE_{table_index + 1:02d}"
        create_table(table_name, (x, y, 0.0), sub["tableSize"], "sub")
        for side, chair_y, rotation in (
            ("S", y - 1.05, 0.0),
            ("N", y + 1.05, math.pi),
        ):
            for seat in range(sub["chairsPerLongSide"]):
                chair_index += 1
                offset = -0.55 if seat == 0 else 0.55
                create_chair(
                    f"SUB_CHAIR_{chair_index:03d}_{side}",
                    (x + offset, chair_y, 0.0),
                    rotation,
                    "sub",
                )



def create_fireplace(config: dict) -> None:
    data = config["fireplace"]
    x, y, _ = data["center"]
    # The declared width is the complete footprint, including the hearth.
    width, height, depth = data["width"] - 0.25, 1.42, data["depth"]
    # Local front is -Y; rotate it toward +X on the mirrored corridor-side wall.
    root = empty("MAIN_FIREPLACE", (x, y, 0), math.pi / 2, target_collection="Furniture")
    root.scale.z = data["height"] / height
    root["dimension_status"] = data["dimensionStatus"]
    def piece(name, location, size, mat="MAT_FIREPLACE_STONE"):
        return box(f"FIREPLACE_{name}", location, size, mat, parent=root, target_collection="Furniture", bevel=0.018)
    piece("HEARTH", (0, -0.05, 0.10), (width + 0.25, depth + 0.25, 0.20))
    piece("BACK", (0, depth / 2 - 0.04, 0.63), (width - 0.25, 0.08, 0.95), "MAT_BLACKOUT")
    opening_width = width * 0.52
    leg_width = (width - opening_width) / 2
    for side in (-1, 1):
        px = side * (opening_width + leg_width) / 2
        piece(f"JAMB_{side}", (px, 0, 0.64), (leg_width, depth, 0.90))
        piece(f"RELIEF_{side}", (px, -depth / 2 - 0.006, 0.65), (leg_width * 0.84, 0.018, 0.86), "MAT_FIREPLACE_RELIEF")
    piece("LINTEL", (0, -0.01, 1.08), (width, depth + 0.03, 0.25))
    piece("MANTEL_LOWER", (0, -0.02, 1.24), (width + 0.08, depth + 0.10, 0.13))
    piece("MANTEL_TOP", (0, -0.04, height - 0.06), (width + 0.20, depth + 0.18, 0.12))
    for index in range(13):
        piece(f"DENTIL_{index:02d}", (-width * 0.44 + index * width * 0.88 / 12, -depth / 2 - 0.035, 1.13), (0.075, 0.06, 0.09))
    # Logs and a small static flame preview; Unity replaces preview flames with
    # native particles using an analytic shader, with no generated bitmap.
    for index in range(3):
        piece(f"LOG_{index}", ((index - 1) * 0.25, -0.06, 0.25), (0.34, 0.15, 0.10), "MAT_MAIN_WOOD")
        bpy.ops.mesh.primitive_cone_add(vertices=7, radius1=0.10, radius2=0.0, depth=0.40)
        flame = bpy.context.object
        flame.name = f"FIRE_PREVIEW_{index}"
        flame.parent = root
        flame.location = ((index - 1) * 0.23, -0.08, 0.48)
        flame.data.materials.append(MATERIALS["MAT_FIRE_PREVIEW"])
        move_to_collection(flame, "FirePreview")
    empty("FIREPLACE_FLAME_ANCHOR", (0, -0.06, 0.32), parent=root, target_collection="Gimmicks")


def create_alice_floor(config: dict) -> None:
    """A single mesh of measured, half-staggered plank faces along the long axis."""
    bounds = config["dimensions"]["sub"]
    spec = config["aliceFloor"]
    width, length = spec["plankWidth"], spec["plankLength"]
    vertices, faces, uvs = [], [], []
    row = 0
    y = bounds["minY"]
    while y < bounds["maxY"] - 1e-6:
        top = min(y + width, bounds["maxY"])
        start = bounds["minX"] - (length * spec["stagger"] if row % 2 else 0)
        while start < bounds["maxX"] - 1e-6:
            left, right = max(start, bounds["minX"]), min(start + length, bounds["maxX"])
            gap = 0.001
            index = len(vertices)
            vertices.extend([(left + gap, y + gap, .002), (right - gap, y + gap, .002),
                             (right - gap, top - gap, .002), (left + gap, top - gap, .002)])
            faces.append(tuple(range(index, index + 4)))
            u0, u1 = (left - start) / length, (right - start) / length
            uvs.extend([(u0, 0), (u1, 0), (u1, (top-y)/width), (u0, (top-y)/width)])
            start += length
        y += width
        row += 1
    mesh = bpy.data.meshes.new("SUB_FLOOR_PLANKS_MESH")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        for loop in polygon.loop_indices:
            uv.data[loop].uv = uvs[mesh.loops[loop].vertex_index]
    mesh.materials.append(MATERIALS["MAT_SUB_FLOOR_MARBLE"])
    obj = bpy.data.objects.new("SUB_FLOOR_PLANKS", mesh)
    collection("Architecture").objects.link(obj)
    obj["plank_width_m"] = width
    obj["plank_length_m"] = length
    obj["stagger"] = spec["stagger"]


def create_alice_wall_art(config: dict) -> None:
    """One continuous literary panorama, cut only at corners and the doorway."""
    b = config["dimensions"]["sub"]
    long, short = b["maxX"] - b["minX"], b["maxY"] - b["minY"]
    height = config["dimensions"]["wallHeight"]
    # The shared east wall has a separate 12-mm ivory facing. Stay in front
    # of its innermost surface as well as the ordinary plaster walls.
    inset = config["dimensions"]["wallThickness"] / 2 + .026
    opening = config["openings"]["corridorToSub"]
    walls = [
        ("N", ((b["minX"]+b["maxX"])/2, b["maxY"]-inset, height/2), long, height, "south", (0,1,0,1)),
        ("E", (b["maxX"]-inset, (b["minY"]+b["maxY"])/2, height/2), short, height, "west", (0,1,0,1)),
        ("W", (b["minX"]+inset, (b["minY"]+b["maxY"])/2, height/2), short, height, "east", (0,1,0,1)),
    ]
    # South wall reads right-to-left in world X when seen from inside.
    for suffix, left, right, bottom, top in (
        ("FRONT", b["minX"], config["openings"]["corridorToSubFront"]["min"], 0, height),
        ("LEFT", config["openings"]["corridorToSubFront"]["max"], opening["min"], 0, height),
        ("FRONT_HEADER", config["openings"]["corridorToSubFront"]["min"], config["openings"]["corridorToSubFront"]["max"], config["dimensions"]["doorHeight"], height),
        ("RIGHT", opening["max"], b["maxX"], 0, height),
        ("HEADER", opening["min"], opening["max"], config["dimensions"]["doorHeight"], height),
    ):
        walls.append(("S_"+suffix, ((left+right)/2, b["minY"]+inset, (bottom+top)/2),
                      right-left, top-bottom, "north",
                      ((b["maxX"]-right)/long, (b["maxX"]-left)/long, bottom/height, top/height)))
    for name, center, width, h, direction, window in walls:
        obj = video_surface("ALICE_WALLPAPER_"+name, center, width, h, direction,
                            "MAT_ALICE_WALL_"+name[0], target_collection="WallArt")
        u0, u1, v0, v1 = window
        for loop, uv in zip(obj.data.polygons[0].loop_indices, ((u0,v0),(u1,v0),(u1,v1),(u0,v1))):
            obj.data.uv_layers.active.data[loop].uv = uv
        obj["source_kind"] = "seeded-public-domain-illustration-and-Hoefler-text-panorama"


def create_screen(name: str, screen: dict) -> None:
    width = screen["width"]
    height = screen["height"]
    x, y, z = screen["center"]
    faces = screen["faces"]
    frame_mat = "MAT_MAIN_METAL" if name == "MAIN" else "MAT_SUB_ACCENT_BLUE"
    if name == "MAIN":
        # A shallow stone-faced pier projects from the north wall; the display
        # is a 110-inch 16:9 surface rather than a room-width cinema screen.
        wall_y_position = y + screen.get("wallProjection", 0.55) / 2
        box("MAIN_MONITOR_PROJECTING_PIER", (x, wall_y_position, 1.5), (width + 0.6, screen.get("wallProjection", 0.55), 3.0), "MAT_MAIN_WALL_PLASTER")
    if faces in ("north", "south"):
        box(f"SCREEN_{name}_BACKING", (x, y, z), (width, 0.08, height), "MAT_BLACKOUT", target_collection="Screens", bevel=0.01)
        surface_offset = -0.041 if faces == "south" else 0.041
        video_surface(f"SCREEN_{name}_SURFACE", (x, y + surface_offset, z), width, height, faces, "MAT_SCREEN_PLACEHOLDER")
        for suffix, sx, sz, size in (
            ("LEFT", x - width / 2 - 0.08, z, (0.16, 0.14, height + 0.32)),
            ("RIGHT", x + width / 2 + 0.08, z, (0.16, 0.14, height + 0.32)),
            ("TOP", x, z + height / 2 + 0.08, (width + 0.32, 0.14, 0.16)),
            ("BOTTOM", x, z - height / 2 - 0.08, (width + 0.32, 0.14, 0.16)),
        ):
            box(f"SCREEN_{name}_FRAME_{suffix}", (sx, y - 0.02, sz), size, frame_mat, target_collection="Screens", bevel=0.02)
    else:
        box(f"SCREEN_{name}_BACKING", (x, y, z), (0.02, width, height), "MAT_BLACKOUT", target_collection="Screens", bevel=0)
        surface_offset = -0.011 if faces == "west" else 0.011
        video_surface(f"SCREEN_{name}_SURFACE", (x + surface_offset, y, z), width, height, faces, "MAT_SCREEN_PLACEHOLDER")
        for suffix, sy, sz, size in (() if name == "SUB" else (
            ("LEFT", y - width / 2 - 0.08, z, (0.14, 0.16, height + 0.32)),
            ("RIGHT", y + width / 2 + 0.08, z, (0.14, 0.16, height + 0.32)),
            ("TOP", y, z + height / 2 + 0.08, (0.14, width + 0.32, 0.16)),
            ("BOTTOM", y, z - height / 2 - 0.08, (0.14, width + 0.32, 0.16)),
        )):
            box(f"SCREEN_{name}_FRAME_{suffix}", (x - 0.02, sy, sz), size, frame_mat, target_collection="Screens", bevel=0.02)
    surface = bpy.data.objects[f"SCREEN_{name}_SURFACE"]
    surface["castle_of_ideas_role"] = "video-surface"
    surface["stream_slot"] = "program"


def create_media_markers(config: dict) -> None:
    main = config["screens"]["main"]
    x, y, z = main["center"]
    player = empty("PROGRAM_FEED_PLAYER", (x, y, z), target_collection="Gimmicks")
    player["castle_of_ideas_role"] = "shared-cvr-video-player"
    player["stream_slot"] = "program"


def create_speakers_and_sign(config: dict) -> None:
    for room in ("MAIN", "SUB"):
        screen = config["screens"][room.lower()]
        x,y,z = screen["center"]
        for ch, sign in (("L",-1),("R",1)):
            offset = sign * (screen["width"]/2 + .42)
            center = (x+offset,y-.18,z) if room == "MAIN" else (x+.18,y+offset,z)
            root = empty(f"SPEAKER_{room}_{ch}",center,0 if room == "MAIN" else math.pi/2,target_collection="Screens")
            box(f"SPEAKER_{room}_{ch}_CABINET",(0,0,0),(.36,.22,.76),"MAT_BLACKOUT" if room == "MAIN" else "MAT_SUB_TRIM_WHITE",parent=root,target_collection="Screens")
            for i,(h,r) in enumerate(((.18,.07),(-.13,.125))):
                cylinder(f"SPEAKER_{room}_{ch}_DRIVER_{i}",(0,-.118,h),r,.02,"MAT_BLACKOUT",parent=root,rotation=(math.pi/2,0,0))
            empty(f"SPEAKER_{room}_{ch}_AUDIO",(0,-.14,0),parent=root,target_collection="Gimmicks")
    target = material("MAT_WELCOME_BOARD",(1,1,1,1),roughness=.9)
    texture=target.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image=bpy.data.images.load(str(PROJECT_DIR/'unity/Assets/CastleOfIdeas/Textures/Signs/welcome-board.png'))
    target.node_tree.links.new(texture.outputs['Color'],target.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    w,h = config['screens']['portal']['width'],config['screens']['portal']['height']
    box('WELCOME_BOARD_BACKING',(17.5,5.475,1.5),(w,.025,h),'MAT_BLACKOUT',target_collection='Screens',bevel=0)
    video_surface('WELCOME_BOARD_SURFACE',(17.5,5.461,1.5),w,h,'south','MAT_WELCOME_BOARD')


def trim_coplanar_intersections() -> None:
    """Give intersecting solid boxes a single owner for coincident faces.

    Subtract only pairs sharing an outward plane and a volume. A 0.5-mm
    clearance removes ambiguous depth fragments without moving visible parts.
    Keep separate named meshes and source UVs for editing and CCK colliders.
    """
    from mathutils import Vector
    objects=[o for o in bpy.data.objects if o.type=='MESH' and
             ('_WALL_' in o.name or 'PILASTER_' in o.name or o.name.startswith('FIREPLACE_')
              or (o.name.startswith('MAIN_TABLE_') and o.parent is not None))]
    bpy.context.view_layer.update()
    for obj in objects:
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    def bounds(o):
        pts=[o.matrix_world @ Vector(v) for v in o.bound_box]
        return [(min(p[a] for p in pts),max(p[a] for p in pts)) for a in range(3)]
    trimmed=[]
    for i,a in enumerate(objects):
        ba=bounds(a)
        for b in objects[i+1:]:
            bb=bounds(b)
            if not all(min(ba[k][1],bb[k][1])-max(ba[k][0],bb[k][0]) > .00001 for k in range(3)):
                continue
            if not any(abs(ba[k][side]-bb[k][side]) < .00001 for k in range(3) for side in (0,1)):
                continue
            cutter=b.copy(); cutter.data=b.data.copy(); bpy.context.scene.collection.objects.link(cutter)
            cutter.scale=tuple(s*(1+.001/max(.001,bb[k][1]-bb[k][0])) for k,s in enumerate(cutter.scale))
            bpy.context.view_layer.update()
            modifier=a.modifiers.new('Remove coincident internal surfaces','BOOLEAN')
            modifier.operation='DIFFERENCE'; modifier.solver='EXACT'; modifier.object=cutter
            bpy.context.view_layer.objects.active=a
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            bpy.data.objects.remove(cutter,do_unlink=True)
            trimmed.append([a.name,b.name])
    path=PROJECT_DIR/'build/coplanar-trim-report.json'
    path.parent.mkdir(exist_ok=True)
    path.write_text(json.dumps({'clearanceMeters':.0005,'trimmedPairs':trimmed},indent=2)+'\n')


def create_portal(portal: dict) -> None:
    width = portal["width"]
    height = portal["height"]
    x, y, z = portal["center"]
    faces = portal["faces"]
    reference = portal.get("physicalReference", {})
    enclosure_width, enclosure_height, enclosure_depth = width, height, 0.02
    if faces not in ("north", "south", "east", "west"):
        raise ValueError(f"Unsupported portal direction: {faces}")

    # Thin, frameless wall display; the Unity build decorates all four edges
    # with original procedural mist. Physical reference dimensions remain JSON.
    if faces in ("north", "south"):
        box(
            "PORTAL_BRIDGE_BACKING",
            (x, y, z),
            (enclosure_width, enclosure_depth, enclosure_height),
            "MAT_BLACKOUT",
            target_collection="Screens",
            bevel=0.025,
        )
        surface_offset = -(enclosure_depth / 2 + 0.001) if faces == "south" else enclosure_depth / 2 + 0.001
        video_surface("PORTAL_BRIDGE_SURFACE", (x, y + surface_offset, z), width, height, faces, "MAT_PORTAL_PLACEHOLDER")
    else:
        box(
            "PORTAL_BRIDGE_BACKING",
            (x, y, z),
            (enclosure_depth, enclosure_width, enclosure_height),
            "MAT_BLACKOUT",
            target_collection="Screens",
            bevel=0.025,
        )
        surface_offset = -(enclosure_depth / 2 + 0.001) if faces == "west" else enclosure_depth / 2 + 0.001
        video_surface("PORTAL_BRIDGE_SURFACE", (x + surface_offset, y, z), width, height, faces, "MAT_PORTAL_PLACEHOLDER")

    if reference.get("mount") == "floor-stand":
        inward = {"south": (0, -1), "north": (0, 1), "west": (-1, 0), "east": (1, 0)}[faces]
        stand_offset = (x + inward[0] * 0.08, y + inward[1] * 0.08)
        bottom = z - enclosure_height / 2
        post_height = max(0.1, bottom - 0.08)
        for side in (-1, 1):
            if faces in ("north", "south"):
                post_position = (x + side * enclosure_width * 0.28, stand_offset[1], post_height / 2)
                foot_position = (post_position[0], stand_offset[1] + inward[1] * 0.12, 0.05)
                foot_size = (0.12, 0.5, 0.08)
            else:
                post_position = (stand_offset[0], y + side * enclosure_width * 0.28, post_height / 2)
                foot_position = (stand_offset[0] + inward[0] * 0.12, post_position[1], 0.05)
                foot_size = (0.5, 0.12, 0.08)
            box(
                f"PORTAL_BRIDGE_STAND_POST_{'A' if side < 0 else 'B'}",
                post_position,
                (0.08, 0.08, post_height),
                "MAT_BLACKOUT",
                target_collection="Screens",
                bevel=0.012,
            )
            box(
                f"PORTAL_BRIDGE_STAND_FOOT_{'A' if side < 0 else 'B'}",
                foot_position,
                foot_size,
                "MAT_BLACKOUT",
                target_collection="Screens",
                bevel=0.018,
            )

    surface = bpy.data.objects["PORTAL_BRIDGE_SURFACE"]
    surface["castle_of_ideas_role"] = "portal-video-surface"
    surface["stream_slot"] = "bridge"

    inward = {"south": (0, -1), "north": (0, 1), "west": (-1, 0), "east": (1, 0)}[faces]
    def toward_room(distance: float, marker_z: float):
        return (x + inward[0] * distance, y + inward[1] * distance, marker_z)

    for marker_name, marker_location, role in (
        ("PORTAL_BRIDGE_PLAYER", toward_room(0.15, z), "future-cvr-video-player"),
        ("PORTAL_BRIDGE_VIEW_ANCHOR", toward_room(1.8, 1.65), "portal-viewpoint"),
        ("PORTAL_BRIDGE_VOICE_ANCHOR", toward_room(0.5, 1.65), "portal-voice-anchor"),
        ("PORTAL_BRIDGE_CAMERA_TARGET", toward_room(0.12, 1.65), "portal-camera-target"),
    ):
        marker = empty(
            marker_name,
            marker_location,
            target_collection="Gimmicks",
            display="ARROWS",
        )
        marker["castle_of_ideas_role"] = role
        marker["stream_slot"] = "bridge"


def add_light(name: str, location, color, energy: float, light_type: str = "POINT", size: float = 2.0):
    data = bpy.data.lights.new(name, type=light_type)
    data.color = color
    data.energy = energy
    if light_type == "AREA":
        data.shape = "DISK"
        data.size = size
    obj = bpy.data.objects.new(name, data)
    obj.location = location
    collection("Lighting").objects.link(obj)
    return obj


def create_lighting(config: dict) -> None:
    main = config["dimensions"]["main"]
    sub = config["dimensions"]["sub"]
    corridor = config["dimensions"]["corridor"]
    lift = config["dimensions"]["elevatorLobby"]
    main_xs = (main["minX"] + 2.1, main["maxX"] - 2.1)
    main_ys = (main["minY"] + 1.8, (main["minY"] + main["maxY"]) / 2, main["maxY"] - 1.8)
    for index, (x, y) in enumerate(((x, y) for y in reversed(main_ys) for x in main_xs), start=1):
        cylinder(f"MAIN_LANTERN_{index:02d}", (x, y, 2.64), 0.12, 0.22, "MAT_LIGHT_WARM", target_collection="Lighting")
        light = add_light(f"MAIN_LIGHT_{index:02d}", (x, y, 2.54), (1.0, 0.65, 0.35), 130.0)
        light["castle_of_ideas_group"] = "main-room-lights"
    sub_center_y = (sub["minY"] + sub["maxY"]) / 2
    for index in range(4):
        x = sub["minX"] + 1.8 + index * (sub["maxX"] - sub["minX"] - 3.6) / 3
        cylinder(f"SUB_PENDANT_{index + 1:02d}", (x, sub_center_y, 2.70), 0.2, 0.15, "MAT_LIGHT_SUB", target_collection="Lighting")
        light = add_light(f"SUB_LIGHT_{index + 1:02d}", (x, sub_center_y, 2.55), (1.0, 0.94, 0.84), 200.0)
        light["castle_of_ideas_group"] = "sub-room-lights"
    add_light("PREVIEW_FILL_MAIN", ((main["minX"] + main["maxX"]) / 2, (main["minY"] + main["maxY"]) / 2, 2.85), (1.0, 0.79, 0.59), 220.0, "AREA", 6.0)
    add_light("PREVIEW_FILL_SUB", ((sub["minX"] + sub["maxX"]) / 2, sub_center_y, 2.85), (1.0, 0.95, 0.87), 350.0, "AREA", 6.0)
    add_light("PREVIEW_CORRIDOR", (17.5, 2.75, 2.65), (0.65, 0.79, 1.0), 100.0)
    fireplace = config["fireplace"]["center"]
    add_light("FIREPLACE_GLOW", (fireplace[0] + 0.35, fireplace[1], 0.6), (1, 0.22, 0.02), 75)
    elevator_light = add_light(
        "ELEVATOR_LIGHT",
        ((lift["minX"] + lift["maxX"]) / 2, (lift["minY"] + lift["maxY"]) / 2, 2.85),
        (0.68, 0.84, 1.0),
        470.0,
        "AREA",
        1.4,
    )
    elevator_light["castle_of_ideas_group"] = "elevator-lights"
    empty("SWITCH_MAIN_LIGHTS", (main["minX"] + 0.35, 2.0, 1.35), target_collection="Gimmicks")["castle_of_ideas_role"] = "light-switch"
    empty("SWITCH_SUB_LIGHTS", (sub["maxX"] - 0.4, sub["minY"] + 0.25, 1.35), target_collection="Gimmicks")["castle_of_ideas_role"] = "light-switch"


def create_markers(config: dict) -> None:
    spawn_config = config["spawns"]
    markers = [
        (
            spawn_config["primary"]["name"],
            spawn_config["primary"]["position"],
            math.radians(spawn_config["primary"]["rotationDegrees"]),
            "primary-spawn",
        ),
        (
            spawn_config["safeRespawn"]["name"],
            spawn_config["safeRespawn"]["position"],
            math.radians(spawn_config["safeRespawn"]["rotationDegrees"]),
            "safe-respawn",
        ),
    ]
    markers.extend(
        (
            item["name"],
            item["position"],
            math.radians(item["rotationDegrees"]),
            "test-spawn",
        )
        for item in spawn_config["testOnly"]
    )
    for name, location, rotation, role in markers:
        marker = empty(name, location, rotation, target_collection="Markers", display="ARROWS")
        marker["castle_of_ideas_role"] = role


def configure_scene(config: dict) -> None:
    scene = bpy.context.scene
    scene.name = "Castle_of_Ideas"
    scene["castle_of_ideas_layout_schema"] = config["schemaVersion"]
    scene["castle_of_ideas_units"] = config["units"]
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.unit_settings.scale_length = 1.0
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if hasattr(scene, "eevee") and hasattr(scene.eevee, "shadow_pool_size"):
        scene.eevee.shadow_pool_size = "1024"
    scene.world.color = (0.025, 0.025, 0.035)
    world_nodes = scene.world.node_tree.nodes if scene.world.use_nodes else None
    scene.world.use_nodes = True
    world_nodes = scene.world.node_tree.nodes
    background = world_nodes.get("Background")
    if background:
        background.inputs["Color"].default_value = (0.025, 0.025, 0.04, 1)
        background.inputs["Strength"].default_value = 0.2


def add_preview_camera(config: dict) -> None:
    dimensions = config["dimensions"]
    bounds_values = [value for value in dimensions.values() if isinstance(value, dict) and "minX" in value]
    min_x = min(bounds["minX"] for bounds in bounds_values)
    max_x = max(bounds["maxX"] for bounds in bounds_values)
    min_y = min(bounds["minY"] for bounds in bounds_values)
    max_y = max(bounds["maxY"] for bounds in bounds_values)
    center = Vector(((min_x + max_x) / 2, (min_y + max_y) / 2, 1.2))
    span = max(max_x - min_x, max_y - min_y)
    camera_data = bpy.data.cameras.new("PREVIEW_CAMERA")
    camera = bpy.data.objects.new("PREVIEW_CAMERA", camera_data)
    collection("PreviewOnly").objects.link(camera)
    camera.location = (center.x + span * 0.9, center.y - span * 1.05, span * 1.1)
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = span * 1.65
    bpy.context.scene.camera = camera
    sun_data = bpy.data.lights.new("PREVIEW_SUN", type="SUN")
    sun_data.energy = 1.5
    sun_data.color = (0.72, 0.82, 1.0)
    sun = bpy.data.objects.new("PREVIEW_SUN", sun_data)
    sun.rotation_euler = (math.radians(28), math.radians(-18), math.radians(-35))
    collection("PreviewOnly").objects.link(sun)


def render_elevator_closeup(path: Path, config: dict) -> None:
    lift = config["dimensions"]["elevatorLobby"]
    center_x = (lift["minX"] + lift["maxX"]) / 2
    camera = bpy.context.scene.camera
    camera.location = (center_x - 0.2, lift["maxY"] - 0.3, 1.55)
    target = Vector((center_x, lift["minY"] + 0.7, 1.45))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    camera.data.lens = 24.0
    camera.data.clip_start = 0.05
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def render_interior(path: Path, location, target, lens: float = 24.0) -> None:
    camera = bpy.context.scene.camera
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    camera.data.lens = lens
    camera.data.clip_start = 0.05
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def export_fbx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    excluded = {"Lighting", "PreviewOnly", "FirePreview"}
    for obj in bpy.context.scene.objects:
        if obj.type not in {"MESH", "EMPTY"}:
            continue
        if any(user_collection.name in excluded for user_collection in obj.users_collection):
            continue
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next(
        (obj for obj in bpy.context.selected_objects if obj.type == "MESH"), None
    )
    # Unity assigns photo textures by stable material names. Do not let the FBX
    # exporter embed workstation absolute image paths in its Video records.
    export_materials = {}
    original_slots = []
    textured_names = (*PHOTO_TEXTURES, *LITERATURE_TEXTURES, 'MAT_WELCOME_BOARD')
    for name in textured_names:
        MATERIALS[name].name = name + "_PHOTO_SOURCE"
        plain = bpy.data.materials.new(name)
        plain.diffuse_color = (1, 1, 1, 1)
        export_materials[name] = plain
    for obj in bpy.context.selected_objects:
        if obj.type != "MESH":
            continue
        for index, mat in enumerate(obj.data.materials):
            for name in textured_names:
                if mat == MATERIALS[name]:
                    original_slots.append((obj.data, index, mat))
                    obj.data.materials[index] = export_materials[name]
                    break
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            object_types={"EMPTY", "MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            use_mesh_modifiers=True,
            path_mode="STRIP",
        )
    finally:
        for mesh, index, original in original_slots:
            mesh.materials[index] = original
        for name, temporary in export_materials.items():
            bpy.data.materials.remove(temporary)
            MATERIALS[name].name = name


def main() -> None:
    args = parse_args()
    config_path = args.config.expanduser().resolve()
    config = json.loads(config_path.read_text(encoding="utf-8"))
    reset_scene()
    for name in (
        "Architecture",
        "Ceilings",
        "Furniture",
        "Screens",
        "Lighting",
        "Gimmicks",
        "Markers",
        "PreviewOnly",
        "FirePreview",
        "WallArt",
    ):
        collection(name)
    create_materials()
    configure_scene(config)
    create_architecture(config)
    create_furniture(config)
    create_fireplace(config)
    create_alice_wall_art(config)
    create_screen("MAIN", config["screens"]["main"])
    create_screen("SUB", config["screens"]["sub"])
    create_media_markers(config)
    create_portal(config["screens"]["portal"])
    create_speakers_and_sign(config)
    trim_coplanar_intersections()
    create_lighting(config)
    create_markers(config)
    add_preview_camera(config)

    output_path = args.output.expanduser().resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Export and render before saving the .blend so Blender does not embed the
    # workstation's absolute source path in FBX or PNG metadata.
    if args.export_fbx:
        export_fbx(args.export_fbx.expanduser().resolve())

    camera = bpy.context.scene.camera
    camera_matrix = camera.matrix_world.copy()
    camera_type = camera.data.type
    camera_lens = camera.data.lens
    camera_clip_start = camera.data.clip_start
    if args.render:
        for obj in collection("Ceilings").objects:
            obj.hide_render = True
        render_path = args.render.expanduser().resolve()
        render_path.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.scene.render.filepath = str(render_path)
        bpy.ops.render.render(write_still=True)
        for obj in collection("Ceilings").objects:
            obj.hide_render = False
    if args.render_elevator:
        render_elevator_closeup(args.render_elevator.expanduser().resolve(), config)
    if args.render_main:
        main_bounds = config["dimensions"]["main"]
        center_x = (main_bounds["minX"] + main_bounds["maxX"]) / 2
        render_interior(args.render_main.expanduser().resolve(), (center_x, 0.05, 1.65), (center_x, main_bounds["maxY"], 1.5))
    if args.render_fireplace:
        x, y, _ = config["fireplace"]["center"]
        render_interior(args.render_fireplace.expanduser().resolve(), (x + 2.7, y - 0.3, 1.50), (x, y, 0.85), 32)

    camera.matrix_world = camera_matrix
    camera.data.type = camera_type
    camera.data.lens = camera_lens
    camera.data.clip_start = camera_clip_start
    bpy.context.scene.render.filepath = ""
    for image in bpy.data.images:
        if image.source == "FILE":
            image.filepath = bpy.path.relpath(image.filepath, start=str(output_path.parent))
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path))

    print(f"Castle of Ideas generated: {output_path}")


if __name__ == "__main__":
    main()
