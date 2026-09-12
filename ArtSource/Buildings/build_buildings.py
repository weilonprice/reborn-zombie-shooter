import bpy
import json
import math
import os
from mathutils import Vector


BASE = os.path.dirname(os.path.abspath(__file__))
UNITY_OUT = os.path.abspath(os.path.join(BASE, "../../Assets/_Project/Art/Buildings"))


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection" and collection.users == 0:
            bpy.data.collections.remove(collection)
    for datablocks in (bpy.data.materials, bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(name, color, metallic=0.0, roughness=0.7, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.4
    return mat


def root_object(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root["building_id"] = name
    root["forward_axis"] = "+Z Unity"
    root["static_environment"] = True
    return root


def finish(obj, name, mat, parent, bevel=0.0):
    obj.name = name
    obj.parent = parent
    if mat is not None:
        obj.data.materials.append(mat)
    if bevel > 0:
        mod = obj.modifiers.new("Soft manufactured edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def box(name, location, scale, mat, parent, rotation=(0.0, 0.0, 0.0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.scale = (scale[0] * 0.5, scale[1] * 0.5, scale[2] * 0.5)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, parent, bevel)


def cylinder(name, location, radius, depth, mat, parent, rotation=(0.0, 0.0, 0.0), vertices=12, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, parent, bevel)


def cone(name, location, radius1, radius2, depth, mat, parent, rotation=(0.0, 0.0, 0.0), vertices=12):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, parent, 0.0)


def add_window(parent, x, y, z, width, height, concrete, glass, frame, side="front"):
    # Front is the -Y face in Blender. The frames are intentionally chunky/readable from
    # the arena camera, with a dark recessed glass plate for a mobile-game silhouette.
    if side == "front":
        glass_loc = (x, y - 0.045, z)
        box("WindowGlass", glass_loc, (width, 0.08, height), glass, parent, bevel=0.025)
        box("WindowFrameTop", (x, y - 0.095, z + height * 0.5), (width + 0.18, 0.12, 0.12), frame, parent, bevel=0.02)
        box("WindowFrameBottom", (x, y - 0.095, z - height * 0.5), (width + 0.18, 0.12, 0.12), frame, parent, bevel=0.02)
        box("WindowFrameLeft", (x - width * 0.5, y - 0.095, z), (0.12, 0.12, height), frame, parent, bevel=0.02)
        box("WindowFrameRight", (x + width * 0.5, y - 0.095, z), (0.12, 0.12, height), frame, parent, bevel=0.02)
        box("WindowMullion", (x, y - 0.11, z), (0.09, 0.13, height), frame, parent, bevel=0.015)
    else:
        glass_loc = (x, y, z)
        box("SideWindowGlass", glass_loc, (0.08, width, height), glass, parent, bevel=0.025)
        box("SideFrameTop", (x, y, z + height * 0.5), (0.12, width + 0.18, 0.12), frame, parent, bevel=0.02)
        box("SideFrameBottom", (x, y, z - height * 0.5), (0.12, width + 0.18, 0.12), frame, parent, bevel=0.02)
        box("SideFrameLeft", (x, y - width * 0.5, z), (0.12, 0.12, height), frame, parent, bevel=0.02)
        box("SideFrameRight", (x, y + width * 0.5, z), (0.12, 0.12, height), frame, parent, bevel=0.02)


def add_door(parent, x, y, z, width, height, door_mat, frame):
    box("Door", (x, y - 0.06, z), (width, 0.12, height), door_mat, parent, bevel=0.025)
    box("DoorFrameTop", (x, y - 0.14, z + height * 0.5), (width + 0.28, 0.16, 0.18), frame, parent, bevel=0.025)
    box("DoorFrameLeft", (x - width * 0.5, y - 0.14, z), (0.18, 0.16, height), frame, parent, bevel=0.025)
    box("DoorFrameRight", (x + width * 0.5, y - 0.14, z), (0.18, 0.16, height), frame, parent, bevel=0.025)
    cylinder("DoorHandle", (x + width * 0.28, y - 0.19, z), 0.045, 0.10, frame, parent, rotation=(math.pi / 2, 0.0, 0.0), vertices=10)


def add_roof(parent, width, depth, z, roof_mat, trim_mat, peak=False):
    if peak:
        slope = math.radians(27)
        run = depth * 0.56
        rise = run * math.tan(slope)
        length = math.sqrt(run * run + rise * rise)
        box("RoofLeft", (0.0, -depth * 0.24, z + rise * 0.35), (width + 0.45, 0.28, length), roof_mat, parent, rotation=(slope, 0.0, 0.0), bevel=0.035)
        box("RoofRight", (0.0, depth * 0.24, z + rise * 0.35), (width + 0.45, 0.28, length), roof_mat, parent, rotation=(-slope, 0.0, 0.0), bevel=0.035)
        box("RoofRidge", (0.0, 0.0, z + rise * 0.75), (width + 0.55, depth * 0.16, 0.18), trim_mat, parent, bevel=0.03)
    else:
        box("FlatRoof", (0.0, 0.0, z), (width + 0.5, depth + 0.5, 0.32), roof_mat, parent, bevel=0.04)
        box("RoofTrim", (0.0, -depth * 0.49, z - 0.22), (width + 0.54, 0.14, 0.46), trim_mat, parent, bevel=0.02)


def add_debris(parent, concrete, rust, points):
    for i, (x, y, z, sx, sy, sz, rot) in enumerate(points):
        box(f"Debris_{i}", (x, y, z), (sx, sy, sz), concrete if i % 2 else rust, parent, rotation=(0.0, 0.0, rot), bevel=0.025)


def make_shack():
    root = root_object("Shack")
    concrete = material("Shack concrete", (0.29, 0.31, 0.33), roughness=0.86)
    trim = material("Shack trim", (0.12, 0.15, 0.17), metallic=0.25, roughness=0.62)
    roof = material("Shack roof", (0.10, 0.12, 0.14), metallic=0.45, roughness=0.56)
    wood = material("Shack boards", (0.28, 0.17, 0.10), roughness=0.9)
    glass = material("Shack glass", (0.025, 0.045, 0.052), metallic=0.1, roughness=0.34)
    box("MainShell", (0.0, 0.0, 2.15), (8.4, 6.8, 4.3), concrete, root, bevel=0.10)
    add_roof(root, 8.4, 6.8, 4.48, roof, trim, peak=True)
    add_door(root, -2.1, -3.43, 1.35, 1.35, 2.55, wood, trim)
    add_window(root, 1.1, -3.43, 2.35, 2.0, 1.25, concrete, glass, trim)
    add_window(root, 4.23, -1.0, 2.20, 1.7, 1.2, concrete, glass, trim, side="side")
    box("Board_01", (0.8, -3.56, 2.18), (2.7, 0.10, 0.13), wood, root, rotation=(0.0, 0.0, math.radians(11)), bevel=0.015)
    box("Board_02", (1.1, -3.57, 2.45), (2.7, 0.10, 0.13), wood, root, rotation=(0.0, 0.0, math.radians(-9)), bevel=0.015)
    add_debris(root, concrete, roof, [(-3.7, -2.6, 0.18, 0.9, 0.5, 0.35, 0.3), (3.5, 2.8, 0.14, 0.7, 0.45, 0.25, -0.4)])
    return root, (0.0, 0.0, 2.2), (8.9, 7.4, 5.6)


def make_storefront():
    root = root_object("Storefront")
    concrete = material("Storefront stucco", (0.38, 0.34, 0.29), roughness=0.82)
    trim = material("Storefront trim", (0.16, 0.18, 0.20), metallic=0.25, roughness=0.58)
    roof = material("Storefront roof", (0.20, 0.12, 0.10), metallic=0.48, roughness=0.60)
    wood = material("Storefront wood", (0.32, 0.20, 0.12), roughness=0.9)
    glass = material("Storefront glass", (0.02, 0.06, 0.07), metallic=0.15, roughness=0.28)
    sign = material("Storefront neon", (0.64, 0.12, 0.16), metallic=0.1, roughness=0.42, emission=(0.95, 0.08, 0.04))
    box("MainShell", (0.0, 0.0, 2.45), (12.0, 7.6, 4.9), concrete, root, bevel=0.10)
    add_roof(root, 12.0, 7.6, 5.05, roof, trim, peak=False)
    add_door(root, -4.15, -3.84, 1.40, 1.55, 2.75, wood, trim)
    add_window(root, -1.6, -3.84, 2.4, 3.35, 1.55, concrete, glass, trim)
    add_window(root, 2.25, -3.84, 2.4, 3.35, 1.55, concrete, glass, trim)
    box("Awning", (0.9, -4.25, 3.75), (7.9, 1.0, 0.20), roof, root, rotation=(math.radians(-10), 0.0, 0.0), bevel=0.04)
    box("AwningEdge", (0.9, -4.72, 3.60), (7.9, 0.16, 0.38), sign, root, bevel=0.02)
    box("SignPanel", (0.9, -3.98, 4.65), (7.4, 0.14, 0.72), sign, root, bevel=0.03)
    for x in (-2.0, 0.0, 2.0):
        cylinder("SignBolt", (x + 0.9, -4.08, 4.65), 0.055, 0.08, trim, root, rotation=(math.pi / 2, 0.0, 0.0), vertices=10)
    add_window(root, 5.98, 0.8, 2.25, 1.6, 1.35, concrete, glass, trim, side="side")
    add_debris(root, wood, roof, [(-5.3, 2.8, 0.15, 1.2, 0.45, 0.25, 0.2), (4.9, -2.8, 0.18, 0.8, 0.5, 0.3, -0.5)])
    return root, (0.0, 0.0, 2.4), (12.6, 8.6, 6.0)


def make_warehouse():
    root = root_object("Warehouse")
    concrete = material("Warehouse concrete", (0.25, 0.29, 0.31), roughness=0.88)
    trim = material("Warehouse trim", (0.10, 0.14, 0.16), metallic=0.50, roughness=0.52)
    roof = material("Warehouse roof", (0.12, 0.15, 0.17), metallic=0.60, roughness=0.45)
    door = material("Warehouse door", (0.23, 0.24, 0.22), metallic=0.52, roughness=0.55)
    glass = material("Warehouse glass", (0.025, 0.045, 0.050), metallic=0.2, roughness=0.3)
    hazard = material("Warehouse hazard", (0.82, 0.39, 0.08), metallic=0.15, roughness=0.50)
    box("MainShell", (0.0, 0.0, 2.80), (16.0, 9.6, 5.6), concrete, root, bevel=0.10)
    add_roof(root, 16.0, 9.6, 5.72, roof, trim, peak=True)
    add_door(root, -5.25, -4.85, 1.70, 2.5, 3.25, door, trim)
    add_door(root, 0.0, -4.85, 1.70, 2.5, 3.25, door, trim)
    add_door(root, 5.25, -4.85, 1.70, 2.5, 3.25, door, trim)
    for x in (-6.0, -2.0, 2.0, 6.0):
        add_window(root, x, 4.85, 3.55, 1.65, 0.95, concrete, glass, trim)
    box("HazardStripe", (0.0, -4.97, 3.78), (13.8, 0.12, 0.20), hazard, root, rotation=(0.0, 0.0, math.radians(4)), bevel=0.02)
    for x in (-6.5, 6.5):
        cylinder("VentStack", (x, 0.9, 6.35), 0.32, 1.5, trim, root, vertices=12, bevel=0.04)
        cone("VentCap", (x, 0.9, 7.15), 0.52, 0.28, 0.42, roof, root, vertices=12)
    add_debris(root, concrete, hazard, [(-7.1, -3.9, 0.20, 1.5, 0.50, 0.30, 0.2), (7.2, 3.6, 0.24, 1.2, 0.55, 0.35, -0.4), (0.0, 4.6, 0.12, 1.0, 0.4, 0.22, 0.1)])
    return root, (0.0, 0.0, 2.8), (16.6, 10.4, 7.5)


def make_apartment():
    root = root_object("ApartmentBlock")
    concrete = material("Apartment plaster", (0.34, 0.30, 0.28), roughness=0.86)
    trim = material("Apartment trim", (0.16, 0.18, 0.20), metallic=0.28, roughness=0.58)
    roof = material("Apartment roof", (0.11, 0.13, 0.15), metallic=0.45, roughness=0.54)
    glass = material("Apartment glass", (0.03, 0.06, 0.07), metallic=0.2, roughness=0.30)
    wood = material("Apartment door", (0.27, 0.16, 0.10), roughness=0.9)
    sign = material("Apartment sign", (0.16, 0.39, 0.44), metallic=0.18, roughness=0.40, emission=(0.04, 0.20, 0.24))
    box("MainShell", (0.0, 0.0, 4.55), (10.0, 8.2, 9.1), concrete, root, bevel=0.10)
    add_roof(root, 10.0, 8.2, 9.25, roof, trim, peak=False)
    add_door(root, 0.0, -4.15, 1.40, 1.7, 2.75, wood, trim)
    for z in (3.25, 6.15):
        for x in (-3.2, 0.0, 3.2):
            add_window(root, x, -4.15, z, 1.7, 1.25, concrete, glass, trim)
    for z in (3.25, 6.15):
        box("BalconySlab", (0.0, -4.78, z - 0.98), (8.8, 1.35, 0.22), trim, root, bevel=0.03)
        box("BalconyRailTop", (0.0, -5.38, z + 0.05), (8.8, 0.12, 0.12), trim, root, bevel=0.02)
        for x in (-4.0, -2.0, 0.0, 2.0, 4.0):
            box("BalconyRail", (x, -5.38, z - 0.45), (0.10, 0.12, 0.95), trim, root, bevel=0.015)
    box("EntryCanopy", (0.0, -4.85, 3.10), (3.3, 1.2, 0.18), sign, root, rotation=(math.radians(-8), 0.0, 0.0), bevel=0.03)
    box("EntrySign", (0.0, -4.20, 2.65), (2.5, 0.12, 0.42), sign, root, bevel=0.02)
    add_window(root, 4.98, -0.5, 6.15, 1.55, 1.25, concrete, glass, trim, side="side")
    add_debris(root, concrete, roof, [(-4.6, 2.8, 0.16, 0.8, 0.45, 0.28, 0.2), (4.3, 2.8, 0.22, 1.0, 0.5, 0.32, -0.3)])
    return root, (0.0, 0.0, 4.5), (10.6, 9.8, 9.9)


BUILDERS = {
    "Shack": make_shack,
    "Storefront": make_storefront,
    "Warehouse": make_warehouse,
    "ApartmentBlock": make_apartment,
}


def bbox_for(root):
    points = []
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        return {"min": [0, 0, 0], "max": [0, 0, 0], "size": [0, 0, 0]}
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    size = high - low
    return {"min": list(low), "max": list(high), "size": list(size)}


def preview_scene(target):
    preview = bpy.data.collections.new("Preview_Only")
    bpy.context.scene.collection.children.link(preview)
    ground_mat = material("Preview ground", (0.035, 0.045, 0.055), roughness=0.92)
    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0.0, 0.0, -0.03))
    ground = bpy.context.object
    ground.name = "PreviewGround"
    ground.data.materials.append(ground_mat)
    for collection in list(ground.users_collection):
        collection.objects.unlink(ground)
    preview.objects.link(ground)

    camera = bpy.data.cameras.new("PreviewCamera")
    cam = bpy.data.objects.new("PreviewCamera", camera)
    preview.objects.link(cam)
    cam.location = (max(target[0] * 1.4, 13.0), -max(target[1] * 1.65, 16.0), max(target[2] * 0.95, 9.0))
    camera.lens = 54
    camera.sensor_width = 36
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam

    for name, location, color, energy, size in [
        ("Key", (7.0, -10.0, 13.0), (1.0, 0.72, 0.50), 1500.0, 5.0),
        ("Fill", (-8.0, -2.0, 8.0), (0.30, 0.50, 1.0), 900.0, 4.0),
        ("Rim", (2.0, 9.0, 11.0), (0.56, 0.85, 0.78), 1300.0, 4.0),
    ]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        preview.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()
    return preview


def export_one(name, builder):
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 520
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Building Preview World")
    scene.world.color = (0.012, 0.016, 0.025)

    root, target, dimensions = builder()
    preview = preview_scene(target)
    source_dir = os.path.join(BASE, name)
    output_dir = os.path.join(UNITY_OUT, name)
    os.makedirs(source_dir, exist_ok=True)
    os.makedirs(output_dir, exist_ok=True)
    scene.render.filepath = os.path.join(source_dir, f"{name}_Preview.png")
    bpy.ops.render.render(write_still=True)
    preview.hide_viewport = True
    preview.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, f"{name}.blend"))

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(output_dir, f"{name}.fbx"),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=False,
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="COPY",
    )
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(output_dir, f"{name}.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_materials="EXPORT",
    )
    report = {
        "building": name,
        "style": "stylized low-poly Dark War survival arena building",
        "parts": len([o for o in root.children_recursive if o.type == "MESH"]),
        "bounds_meters": bbox_for(root),
        "unity_forward_axis": "+Z",
        "static_colliders": "generated on MeshFilters by ArenaBuilder",
    }
    with open(os.path.join(source_dir, "asset_report.json"), "w") as handle:
        json.dump(report, handle, indent=2)
    with open(os.path.join(output_dir, "README.md"), "w") as handle:
        handle.write(
            f"# {name}\n\n"
            "Stylized low-poly survival arena building for the Zombie Shooter map.\n\n"
            f"- `{name}.fbx` — Unity environment import.\n"
            f"- `{name}.glb` — portable geometry export.\n"
            f"- `{name}.blend` — editable Blender source in ArtSource.\n\n"
            "The arena builder adds static mesh colliders when it places this building.\n"
        )


for building_name, building_builder in BUILDERS.items():
    export_one(building_name, building_builder)

print("BUILDING_BUILD_COMPLETE", flush=True)
