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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    parser.add_argument(
        "--output", type=Path, default=SCRIPT_DIR / "castle-of-ideas.blend"
    )
    parser.add_argument("--export-fbx", type=Path)
    parser.add_argument("--render", type=Path)
    parser.add_argument("--render-elevator", type=Path)
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
    material("MAT_ELEVATOR_WOOD", (0.075, 0.028, 0.025, 1), roughness=0.42)
    material("MAT_ELEVATOR_CARPET", (0.018, 0.055, 0.11, 1), roughness=0.88)
    material("MAT_ELEVATOR_MIRROR", (0.2, 0.28, 0.34, 1), roughness=0.08, metallic=0.9)
    material("MAT_BLACKOUT", (0.008, 0.009, 0.012, 1), roughness=0.9)
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
            vertices.reverse()
    elif faces in ("east", "west"):
        vertices = [
            (0.0, -width / 2, -height / 2),
            (0.0, -width / 2, height / 2),
            (0.0, width / 2, height / 2),
            (0.0, width / 2, -height / 2),
        ]
        if faces == "east":
            vertices.reverse()
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
    rotation=(0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(MATERIALS[material_name])
    move_to_collection(obj, target_collection)
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
        box(f"{prefix}_FLOOR", (center[0], center[1], -floor_t / 2), (width, depth, floor_t), floor_mat)
        ceiling = box(
            f"{prefix}_CEILING",
            (center[0], center[1], height + floor_t / 2),
            (width, depth, floor_t),
            floor_mat,
            target_collection="Ceilings",
        )
        ceiling.hide_render = True

    floor_and_ceiling("MAIN", main, "MAT_MAIN_FLOOR_STONE")
    floor_and_ceiling("SUB", sub, "MAT_SUB_FLOOR_MARBLE")
    floor_and_ceiling("CORRIDOR", corridor, "MAT_MAIN_FLOOR_STONE")
    lift_width = lift["maxX"] - lift["minX"]
    lift_depth = lift["maxY"] - lift["minY"]
    lift_center = (
        (lift["minX"] + lift["maxX"]) / 2,
        (lift["minY"] + lift["maxY"]) / 2,
    )
    lift_height = lift["height"]
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
        "MAT_ELEVATOR_WOOD",
        target_collection="Ceilings",
    )
    lift_ceiling.hide_render = True

    wall_x("MAIN_WALL_WEST", main["minX"], main["minY"], main["maxY"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_y("MAIN_WALL_SOUTH", main["minY"], main["minX"], main["maxX"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_y("MAIN_WALL_NORTH", main["maxY"], main["minX"], main["maxX"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    main_open = openings["mainToCorridor"]
    wall_x("MAIN_WALL_EAST_SOUTH", main["maxX"], main["minY"], main_open["min"], height, thickness, "MAT_MAIN_WALL_PLASTER")
    wall_x("MAIN_WALL_EAST_NORTH", main["maxX"], main_open["max"], main["maxY"], height, thickness, "MAT_MAIN_WALL_PLASTER")

    wall_y("SUB_WALL_NORTH", sub["maxY"], sub["minX"], sub["maxX"], height, thickness, "MAT_SUB_WALL_IVORY")
    wall_x("SUB_WALL_EAST", sub["maxX"], sub["minY"], sub["maxY"], height, thickness, "MAT_SUB_WALL_IVORY")
    sub_open = openings["corridorToSub"]
    wall_y("SUB_WALL_SOUTH_WEST", sub["minY"], sub["minX"], sub_open["min"], height, thickness, "MAT_SUB_WALL_IVORY")
    wall_y("SUB_WALL_SOUTH_EAST", sub["minY"], sub_open["max"], sub["maxX"], height, thickness, "MAT_SUB_WALL_IVORY")

    lift_open = openings["elevator"]
    wall_y("CORRIDOR_WALL_SOUTH_WEST", corridor["minY"], corridor["minX"], lift_open["min"], height, thickness, "MAT_CORRIDOR_WALL")
    wall_y("CORRIDOR_WALL_SOUTH_EAST", corridor["minY"], lift_open["max"], corridor["maxX"], height, thickness, "MAT_CORRIDOR_WALL")
    wall_x("CORRIDOR_WALL_EAST", corridor["maxX"], corridor["minY"], corridor["maxY"], height, thickness, "MAT_CORRIDOR_WALL")

    wall_x(
        "ELEVATOR_WALL_WEST",
        lift["minX"],
        lift["minY"],
        lift["maxY"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_WOOD",
    )
    wall_x(
        "ELEVATOR_WALL_EAST",
        lift["maxX"],
        lift["minY"],
        lift["maxY"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_WOOD",
    )
    wall_y(
        "ELEVATOR_WALL_BACK",
        lift["minY"],
        lift["minX"],
        lift["maxX"],
        lift_height,
        thickness,
        "MAT_ELEVATOR_WOOD",
    )

    # Keep the entrance visibly open for the arrival shot. Closed targets are
    # exported as markers for a later CVR-synced sliding-door animation.
    box(
        "ELEVATOR_DOOR_LEFT_OPEN",
        (16.52, 1.42, 1.45),
        (0.48, 0.12, 2.9),
        "MAT_MAIN_METAL",
    )
    box(
        "ELEVATOR_DOOR_RIGHT_OPEN",
        (18.48, 1.42, 1.45),
        (0.48, 0.12, 2.9),
        "MAT_MAIN_METAL",
    )
    empty(
        "ELEVATOR_DOOR_LEFT_CLOSED",
        (16.95, 1.42, 1.45),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-closed-target"
    empty(
        "ELEVATOR_DOOR_RIGHT_CLOSED",
        (18.05, 1.42, 1.45),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-closed-target"
    empty(
        "ELEVATOR_DOOR_TRIGGER",
        (17.5, 1.05, 1.0),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "door-trigger"

    # Cabin paneling bridges the dark lecture hall and bright literary salon.
    box(
        "ELEVATOR_BACK_MIRROR",
        (17.5, lift["minY"] + 0.11, 1.75),
        (1.55, 0.06, 1.75),
        "MAT_ELEVATOR_MIRROR",
    )
    for x in (16.32, 17.05, 17.95, 18.68):
        box(
            f"ELEVATOR_BACK_TRIM_{int(x * 100):04d}",
            (x, lift["minY"] + 0.075, 1.65),
            (0.055, 0.07, 3.05),
            "MAT_MAIN_METAL",
            bevel=0.01,
        )
    for side, x in (("W", lift["minX"] + 0.11), ("E", lift["maxX"] - 0.11)):
        for index, y in enumerate((-0.95, -0.2, 0.55), start=1):
            box(
                f"ELEVATOR_PANEL_{side}_{index:02d}",
                (x, y, 1.5),
                (0.07, 0.62, 2.45),
                "MAT_ELEVATOR_WOOD",
                bevel=0.035,
            )
    cylinder(
        "ELEVATOR_RAIL_BACK",
        (17.5, lift["minY"] + 0.24, 1.0),
        0.035,
        2.0,
        "MAT_MAIN_METAL",
        rotation=(0.0, math.pi / 2, 0.0),
    )
    cylinder(
        "ELEVATOR_RAIL_WEST",
        (lift["minX"] + 0.24, 0.0, 1.0),
        0.035,
        2.2,
        "MAT_MAIN_METAL",
        rotation=(math.pi / 2, 0.0, 0.0),
    )
    cylinder(
        "ELEVATOR_RAIL_EAST",
        (lift["maxX"] - 0.24, 0.0, 1.0),
        0.035,
        2.2,
        "MAT_MAIN_METAL",
        rotation=(math.pi / 2, 0.0, 0.0),
    )
    box(
        "ELEVATOR_CONTROL_PANEL",
        (lift["maxX"] - 0.115, 0.55, 1.45),
        (0.075, 0.55, 0.9),
        "MAT_MAIN_METAL",
        bevel=0.035,
    )
    for index, z in enumerate((1.2, 1.45, 1.7), start=1):
        box(
            f"ELEVATOR_BUTTON_{index:02d}",
            (lift["maxX"] - 0.065, 0.48, z),
            (0.035, 0.13, 0.13),
            "MAT_LIGHT_COOL",
            bevel=0.025,
        )
    box(
        "ELEVATOR_CEILING_LIGHT",
        (17.5, 0.0, lift_height - 0.08),
        (1.45, 1.15, 0.07),
        "MAT_LIGHT_COOL",
        target_collection="Lighting",
        bevel=0.04,
    )
    empty(
        "ELEVATOR_DING_AUDIO",
        (17.5, 1.2, 2.65),
        target_collection="Gimmicks",
    )["castle_of_ideas_role"] = "audio-source-placeholder"

    create_arch_frame("ARCH_MAIN_CORRIDOR", (8.0, 2.8, 0.0), 1.6, dims["doorHeight"], "x", "MAT_MAIN_WOOD")
    create_arch_frame("ARCH_CORRIDOR_SUB", (11.1, 4.0, 0.0), 2.2, dims["doorHeight"], "y", "MAT_SUB_TRIM_WHITE")
    create_arch_frame("ARCH_ELEVATOR", (17.5, 1.5, 0.0), 2.2, dims["doorHeight"], "y", "MAT_MAIN_METAL")

    # Dark-academia structural rhythm: timber pilasters and beams.
    for index, y in enumerate((-2.5, 1.0, 4.5, 8.0, 11.5)):
        for side, x in (("W", -7.72), ("E", 7.72)):
            box(f"MAIN_PILASTER_{side}_{index:02d}", (x, y, 2.35), (0.28, 0.38, 4.7), "MAT_MAIN_WOOD")
    for index, y in enumerate((-1.0, 3.0, 7.0, 11.0)):
        box(f"MAIN_CEILING_BEAM_{index:02d}", (0.0, y, 4.78), (15.5, 0.25, 0.3), "MAT_MAIN_WOOD", target_collection="Ceilings")

    # White literary salon: pale pilasters and a continuous dado rail.
    for index, x in enumerate((9.0, 12.75, 16.5, 20.25, 24.0)):
        box(f"SUB_PILASTER_N_{index:02d}", (x, 14.22, 2.3), (0.24, 0.34, 4.6), "MAT_SUB_TRIM_WHITE")
    box("SUB_DADO_NORTH", (16.5, 14.18, 1.05), (16.4, 0.18, 0.16), "MAT_SUB_ACCENT_BLUE")
    box("SUB_DADO_SOUTH_W", (9.0, 4.18, 1.05), (1.7, 0.18, 0.16), "MAT_SUB_ACCENT_BLUE")
    box("SUB_DADO_SOUTH_E", (18.6, 4.18, 1.05), (12.4, 0.18, 0.16), "MAT_SUB_ACCENT_BLUE")


def create_table(name: str, center, size, style: str) -> bpy.types.Object:
    mat = "MAT_MAIN_WOOD" if style == "main" else "MAT_SUB_TRIM_WHITE"
    accent = "MAT_MAIN_METAL" if style == "main" else "MAT_SUB_ACCENT_BLUE"
    root = empty(name, center, target_collection="Furniture")
    root["castle_of_ideas_role"] = "table"
    width, depth, height = size
    box(f"{name}_Top", (0.0, 0.0, height), (width, depth, 0.12), mat, target_collection="Furniture", parent=root, bevel=0.05)
    inset_x = width / 2 - 0.22
    inset_y = depth / 2 - 0.16
    for suffix, x, y in (
        ("NW", -inset_x, inset_y),
        ("NE", inset_x, inset_y),
        ("SW", -inset_x, -inset_y),
        ("SE", inset_x, -inset_y),
    ):
        box(f"{name}_Leg_{suffix}", (x, y, height / 2), (0.11, 0.11, height), accent, target_collection="Furniture", parent=root, bevel=0.015)
    return root


def create_chair(name: str, location, rotation_z: float, style: str) -> bpy.types.Object:
    frame = "MAT_MAIN_WOOD" if style == "main" else "MAT_SUB_TRIM_WHITE"
    upholstery = "MAT_MAIN_UPHOLSTERY" if style == "main" else "MAT_SUB_UPHOLSTERY_ROSE"
    root = empty(name, location, rotation_z, target_collection="Furniture")
    root["castle_of_ideas_role"] = "seat"
    root["castle_of_ideas_gimmick"] = "CVRInteractable.SitAtPosition"
    box(f"{name}_Seat", (0.0, 0.0, 0.5), (0.52, 0.52, 0.12), upholstery, target_collection="Furniture", parent=root, bevel=0.055)
    box(f"{name}_Pedestal", (0.0, 0.0, 0.25), (0.34, 0.34, 0.5), frame, target_collection="Furniture", parent=root, bevel=0.035)
    box(f"{name}_Back", (0.0, -0.23, 0.84), (0.52, 0.12, 0.68), upholstery, target_collection="Furniture", parent=root, bevel=0.055)
    seat_point = empty(f"{name}_SeatPoint", (0.0, 0.0, 0.64), target_collection="Markers", parent=root)
    seat_point["castle_of_ideas_role"] = "sit-position"
    exit_point = empty(f"{name}_ExitPoint", (0.0, -0.92, 0.0), target_collection="Markers", parent=root)
    exit_point["castle_of_ideas_role"] = "sit-exit"
    return root


def create_furniture(config: dict) -> None:
    furniture = config["furniture"]
    main = furniture["main"]
    chair_index = 0
    for row_index, y in enumerate(main["tableRows"]):
        for column_index, x in enumerate(main["tableColumns"]):
            table_name = f"MAIN_TABLE_R{row_index + 1:02d}_C{column_index + 1:02d}"
            create_table(table_name, (x, y, 0.0), main["tableSize"], "main")
            for seat in range(main["chairsPerTable"]):
                chair_index += 1
                spacing = main["tableSize"][0] / main["chairsPerTable"]
                chair_x = x - main["tableSize"][0] / 2 + spacing * (seat + 0.5)
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

    # A reading bench line close to the sub-room screen, matching the source plan.
    for index, y in enumerate((6.1, 7.35, 10.95, 12.2), start=1):
        create_chair(f"SUB_SCREEN_CHAIR_{index:02d}", (23.65, y, 0.0), math.pi / 2, "sub")

    # Lecterns and sparse bookshelves provide scale and style anchors.
    create_table("MAIN_LECTERN", (0.0, 11.9, 0.0), (1.3, 0.75, 1.05), "main")
    for index, y in enumerate((1.0, 6.8, 11.0), start=1):
        box(f"MAIN_BOOKCASE_{index:02d}", (-7.55, y, 1.4), (0.55, 2.0, 2.8), "MAT_MAIN_WOOD", target_collection="Furniture")
        for shelf in (0.45, 0.95, 1.45, 1.95, 2.45):
            box(f"MAIN_BOOKCASE_{index:02d}_SHELF_{int(shelf * 100):03d}", (-7.18, y, shelf), (0.18, 1.82, 0.08), "MAT_MAIN_METAL", target_collection="Furniture", bevel=0.01)
    box("SUB_READING_CABINET", (9.0, 12.65, 1.1), (1.6, 0.5, 2.2), "MAT_SUB_TRIM_WHITE", target_collection="Furniture")


def create_screen(name: str, screen: dict) -> None:
    width = screen["width"]
    height = screen["height"]
    x, y, z = screen["center"]
    faces = screen["faces"]
    frame_mat = "MAT_MAIN_METAL" if name == "MAIN" else "MAT_SUB_ACCENT_BLUE"
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
        box(f"SCREEN_{name}_BACKING", (x, y, z), (0.08, width, height), "MAT_BLACKOUT", target_collection="Screens", bevel=0.01)
        surface_offset = -0.041 if faces == "west" else 0.041
        video_surface(f"SCREEN_{name}_SURFACE", (x + surface_offset, y, z), width, height, faces, "MAT_SCREEN_PLACEHOLDER")
        for suffix, sy, sz, size in (
            ("LEFT", y - width / 2 - 0.08, z, (0.14, 0.16, height + 0.32)),
            ("RIGHT", y + width / 2 + 0.08, z, (0.14, 0.16, height + 0.32)),
            ("TOP", y, z + height / 2 + 0.08, (0.14, width + 0.32, 0.16)),
            ("BOTTOM", y, z - height / 2 - 0.08, (0.14, width + 0.32, 0.16)),
        ):
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
    panel = empty(
        "PROGRAM_CONTROL_PANEL",
        (15.25, 1.72, 1.55),
        target_collection="Gimmicks",
        display="CUBE",
    )
    panel["castle_of_ideas_role"] = "instance-owner-media-controls"


def create_portal(portal: dict) -> None:
    width = portal["width"]
    height = portal["height"]
    x, y, z = portal["center"]
    faces = portal["faces"]
    if faces not in ("north", "south", "east", "west"):
        raise ValueError(f"Unsupported portal direction: {faces}")

    if faces in ("north", "south"):
        box("PORTAL_BRIDGE_BACKING", (x, y, z), (width, 0.08, height), "MAT_BLACKOUT", target_collection="Screens", bevel=0.03)
        surface_offset = -0.041 if faces == "south" else 0.041
        video_surface("PORTAL_BRIDGE_SURFACE", (x, y + surface_offset, z), width, height, faces, "MAT_PORTAL_PLACEHOLDER")
        frame_specs = (
            ("LEFT_OUTER", (x - width / 2 - 0.16, y, z), (0.22, 0.18, height + 0.62), "MAT_SUB_TRIM_WHITE"),
            ("RIGHT_OUTER", (x + width / 2 + 0.16, y, z), (0.22, 0.18, height + 0.62), "MAT_SUB_TRIM_WHITE"),
            ("TOP_OUTER", (x, y, z + height / 2 + 0.16), (width + 0.54, 0.18, 0.22), "MAT_SUB_TRIM_WHITE"),
            ("BOTTOM_OUTER", (x, y, z - height / 2 - 0.16), (width + 0.54, 0.18, 0.22), "MAT_SUB_TRIM_WHITE"),
            ("LEFT_INNER", (x - width / 2 - 0.05, y, z), (0.08, 0.20, height + 0.18), "MAT_SUB_ACCENT_BLUE"),
            ("RIGHT_INNER", (x + width / 2 + 0.05, y, z), (0.08, 0.20, height + 0.18), "MAT_SUB_ACCENT_BLUE"),
            ("TOP_INNER", (x, y, z + height / 2 + 0.05), (width + 0.18, 0.20, 0.08), "MAT_SUB_ACCENT_BLUE"),
            ("BOTTOM_INNER", (x, y, z - height / 2 - 0.05), (width + 0.18, 0.20, 0.08), "MAT_SUB_ACCENT_BLUE"),
        )
        finials = ((x - width / 2 - 0.16, y, z + height / 2 + 0.38), (x + width / 2 + 0.16, y, z + height / 2 + 0.38))
    else:
        box("PORTAL_BRIDGE_BACKING", (x, y, z), (0.08, width, height), "MAT_BLACKOUT", target_collection="Screens", bevel=0.03)
        surface_offset = -0.041 if faces == "west" else 0.041
        video_surface("PORTAL_BRIDGE_SURFACE", (x + surface_offset, y, z), width, height, faces, "MAT_PORTAL_PLACEHOLDER")
        frame_specs = (
            ("LEFT_OUTER", (x, y - width / 2 - 0.16, z), (0.18, 0.22, height + 0.62), "MAT_SUB_TRIM_WHITE"),
            ("RIGHT_OUTER", (x, y + width / 2 + 0.16, z), (0.18, 0.22, height + 0.62), "MAT_SUB_TRIM_WHITE"),
            ("TOP_OUTER", (x, y, z + height / 2 + 0.16), (0.18, width + 0.54, 0.22), "MAT_SUB_TRIM_WHITE"),
            ("BOTTOM_OUTER", (x, y, z - height / 2 - 0.16), (0.18, width + 0.54, 0.22), "MAT_SUB_TRIM_WHITE"),
            ("LEFT_INNER", (x, y - width / 2 - 0.05, z), (0.20, 0.08, height + 0.18), "MAT_SUB_ACCENT_BLUE"),
            ("RIGHT_INNER", (x, y + width / 2 + 0.05, z), (0.20, 0.08, height + 0.18), "MAT_SUB_ACCENT_BLUE"),
            ("TOP_INNER", (x, y, z + height / 2 + 0.05), (0.20, width + 0.18, 0.08), "MAT_SUB_ACCENT_BLUE"),
            ("BOTTOM_INNER", (x, y, z - height / 2 - 0.05), (0.20, width + 0.18, 0.08), "MAT_SUB_ACCENT_BLUE"),
        )
        finials = ((x, y - width / 2 - 0.16, z + height / 2 + 0.38), (x, y + width / 2 + 0.16, z + height / 2 + 0.38))

    frame_offset = {"south": (0, -0.02, 0), "north": (0, 0.02, 0), "west": (-0.02, 0, 0), "east": (0.02, 0, 0)}[faces]
    for suffix, position, size, material_name in frame_specs:
        box(
            f"PORTAL_BRIDGE_FRAME_{suffix}",
            tuple(position[index] + frame_offset[index] for index in range(3)),
            size,
            material_name,
            target_collection="Screens",
            bevel=0.04,
        )

    for side, finial_position in zip(("LEFT", "RIGHT"), finials):
        cylinder(
            f"PORTAL_BRIDGE_FINIAL_{side}",
            finial_position,
            0.11,
            0.22,
            "MAT_SUB_ACCENT_BLUE",
            target_collection="Screens",
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


def create_lighting() -> None:
    for index, (x, y) in enumerate(((-4.2, 11.0), (4.2, 11.0), (-4.2, 6.0), (4.2, 6.0), (-4.2, 1.0), (4.2, 1.0)), start=1):
        cylinder(f"MAIN_LANTERN_{index:02d}", (x, y, 4.2), 0.16, 0.5, "MAT_LIGHT_WARM", target_collection="Lighting")
        light = add_light(f"MAIN_LIGHT_{index:02d}", (x, y, 4.0), (1.0, 0.32, 0.08), 520.0)
        light["castle_of_ideas_group"] = "main-room-lights"
    for index, x in enumerate((10.8, 14.6, 18.5, 22.3), start=1):
        cylinder(f"SUB_PENDANT_{index:02d}", (x, 9.2, 4.15), 0.2, 0.25, "MAT_LIGHT_COOL", target_collection="Lighting")
        light = add_light(f"SUB_LIGHT_{index:02d}", (x, 9.2, 4.0), (0.74, 0.88, 1.0), 700.0)
        light["castle_of_ideas_group"] = "sub-room-lights"
    add_light("PREVIEW_FILL_MAIN", (0.0, 5.0, 8.0), (1.0, 0.62, 0.38), 1100.0, "AREA", 8.0)
    add_light("PREVIEW_FILL_SUB", (17.0, 9.0, 8.0), (0.72, 0.85, 1.0), 1400.0, "AREA", 8.0)
    elevator_light = add_light(
        "ELEVATOR_LIGHT",
        (17.5, 0.0, 2.85),
        (0.68, 0.84, 1.0),
        470.0,
        "AREA",
        1.4,
    )
    elevator_light["castle_of_ideas_group"] = "elevator-lights"
    empty("SWITCH_MAIN_LIGHTS", (7.65, 2.0, 1.35), target_collection="Gimmicks")["castle_of_ideas_role"] = "light-switch"
    empty("SWITCH_SUB_LIGHTS", (10.0, 4.25, 1.35), target_collection="Gimmicks")["castle_of_ideas_role"] = "light-switch"


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
    scene.world.color = (0.025, 0.025, 0.035)
    world_nodes = scene.world.node_tree.nodes if scene.world.use_nodes else None
    scene.world.use_nodes = True
    world_nodes = scene.world.node_tree.nodes
    background = world_nodes.get("Background")
    if background:
        background.inputs["Color"].default_value = (0.025, 0.025, 0.04, 1)
        background.inputs["Strength"].default_value = 0.2


def add_preview_camera() -> None:
    camera_data = bpy.data.cameras.new("PREVIEW_CAMERA")
    camera = bpy.data.objects.new("PREVIEW_CAMERA", camera_data)
    collection("PreviewOnly").objects.link(camera)
    camera.location = (34.0, -31.0, 34.0)
    target = Vector((8.0, 5.0, 1.2))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 43.0
    bpy.context.scene.camera = camera
    sun_data = bpy.data.lights.new("PREVIEW_SUN", type="SUN")
    sun_data.energy = 1.5
    sun_data.color = (0.72, 0.82, 1.0)
    sun = bpy.data.objects.new("PREVIEW_SUN", sun_data)
    sun.rotation_euler = (math.radians(28), math.radians(-18), math.radians(-35))
    collection("PreviewOnly").objects.link(sun)


def render_elevator_closeup(path: Path) -> None:
    camera = bpy.context.scene.camera
    camera.location = (17.3, 1.2, 1.55)
    target = Vector((17.5, -0.7, 1.45))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    camera.data.lens = 24.0
    camera.data.clip_start = 0.05
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def export_fbx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    excluded = {"Ceilings", "Lighting", "PreviewOnly"}
    for obj in bpy.context.scene.objects:
        if obj.type not in {"MESH", "EMPTY"}:
            continue
        if any(user_collection.name in excluded for user_collection in obj.users_collection):
            continue
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next(
        (obj for obj in bpy.context.selected_objects if obj.type == "MESH"), None
    )
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
        path_mode="AUTO",
    )


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
    ):
        collection(name)
    create_materials()
    configure_scene(config)
    create_architecture(config)
    create_furniture(config)
    create_screen("MAIN", config["screens"]["main"])
    create_screen("SUB", config["screens"]["sub"])
    create_media_markers(config)
    create_portal(config["screens"]["portal"])
    create_lighting()
    create_markers(config)
    add_preview_camera()

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
        render_path = args.render.expanduser().resolve()
        render_path.parent.mkdir(parents=True, exist_ok=True)
        bpy.context.scene.render.filepath = str(render_path)
        bpy.ops.render.render(write_still=True)
    if args.render_elevator:
        render_elevator_closeup(args.render_elevator.expanduser().resolve())

    camera.matrix_world = camera_matrix
    camera.data.type = camera_type
    camera.data.lens = camera_lens
    camera.data.clip_start = camera_clip_start
    bpy.context.scene.render.filepath = ""
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path))

    print(f"Castle of Ideas generated: {output_path}")


if __name__ == "__main__":
    main()
