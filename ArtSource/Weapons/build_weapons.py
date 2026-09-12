import bpy
import json
import math
import os
from mathutils import Vector


BASE = os.path.dirname(os.path.abspath(__file__))
UNITY_OUT = os.path.abspath(os.path.join(BASE, "../../Assets/_Project/Art/Weapons"))
DETAIL = os.environ.get("WEAPON_DETAIL", "horde")
BEVEL_SEGMENTS = 2 if DETAIL == "hero" else 1


def clear_scene():
    # Strip animation data before deleting objects. NLA strips keep action datablocks alive
    # through the previous export; clearing the object animation data first makes the action
    # cleanup below deterministic between weapon builds.
    for obj in list(bpy.data.objects):
        if obj.animation_data is not None:
            obj.animation_data_clear()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.curves, bpy.data.meshes, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    # Actions are marked with fake users while they are being exported. Clear those fake
    # users between weapons so bake_anim_use_all_actions cannot leak one weapon's takes into
    # the next FBX.
    for action in list(bpy.data.actions):
        action.use_fake_user = False
        bpy.data.actions.remove(action)


def material(name, color, metallic=0.0, roughness=0.65):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def root_object(name, weapon_id):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root["weapon_id"] = weapon_id
    root["forward_axis"] = "+Y Blender / +Z Unity"
    root["muzzle_socket"] = "MuzzleSocket"
    root["grip_socket"] = "GripSocket"
    return root


def finish(obj, name, mat, parent, bevel=0.0, smooth=False):
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    if bevel > 0.0:
        mod = obj.modifiers.new("Manufactured edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = BEVEL_SEGMENTS
        mod.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def box(name, location, dimensions, mat, parent, rotation=(0.0, 0.0, 0.0), bevel=0.025):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, parent, bevel)


def cylinder(name, location, radius, depth, mat, parent, axis="Y", vertices=12, bevel=0.0):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0) if axis == "X" else (0.0, 0.0, 0.0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, parent, bevel, smooth=True)


def sphere(name, location, scale, mat, parent):
    seg = 16 if DETAIL == "hero" else 10
    rings = 10 if DETAIL == "hero" else 6
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, parent, 0.0, smooth=True)


def torus(name, location, major, minor, mat, parent, axis="Y"):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0)
    bpy.ops.mesh.primitive_torus_add(
        major_segments=16 if DETAIL == "hero" else 10,
        minor_segments=6 if DETAIL == "hero" else 4,
        major_radius=major,
        minor_radius=minor,
        location=location,
        rotation=rotation,
    )
    return finish(bpy.context.object, name, mat, parent, 0.0, smooth=True)


def cone(name, location, radius1, radius2, depth, mat, parent, axis="Y", vertices=12, bevel=0.0):
    rotation = (math.pi / 2.0, 0.0, 0.0) if axis == "Y" else (0.0, math.pi / 2.0, 0.0) if axis == "X" else (0.0, 0.0, 0.0)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, mat, parent, bevel, smooth=True)


def socket(name, location, parent):
    empty = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(empty)
    empty.location = location
    empty.parent = parent
    empty.empty_display_type = "ARROWS"
    empty.empty_display_size = 0.08
    empty["purpose"] = "Unity attachment point"
    return empty


def ground_and_lights(target):
    preview = bpy.data.collections.new("Preview_Only")
    bpy.context.scene.collection.children.link(preview)

    ground_mat = material("Preview ground", (0.035, 0.045, 0.055), metallic=0.1, roughness=0.9)
    bpy.ops.mesh.primitive_plane_add(size=7.0, location=(0.0, 0.35, -0.38))
    ground = bpy.context.object
    ground.name = "PreviewGround"
    ground.data.materials.append(ground_mat)
    ground.parent = None
    for collection in list(ground.users_collection):
        collection.objects.unlink(ground)
    preview.objects.link(ground)

    bpy.ops.object.camera_add(location=(2.55, -3.35, 2.25))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.data.lens = 58
    camera.data.sensor_width = 36
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    for collection in list(camera.users_collection):
        collection.objects.unlink(camera)
    preview.objects.link(camera)
    bpy.context.scene.camera = camera

    lights = [
        ("Key", (2.3, -1.8, 3.2), (1.0, 0.78, 0.55), 460.0, 2.8),
        ("Fill", (-2.4, -0.4, 1.7), (0.35, 0.55, 1.0), 300.0, 2.2),
        ("Rim", (0.0, 2.8, 2.6), (0.70, 0.92, 0.85), 520.0, 2.4),
    ]
    for name, location, color, energy, size in lights:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()
        for collection in list(light.users_collection):
            collection.objects.unlink(light)
        preview.objects.link(light)

    return preview


def bbox_for(root):
    points = []
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        return {"min": [0, 0, 0], "max": [0, 0, 0], "size": [0, 0, 0]}
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return {"min": list(lo), "max": list(hi), "size": list(hi - lo)}


def select_tree(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for child in root.children_recursive:
        child.select_set(True)


def _weapon_pose(root, frame, location=(0.0, 0.0, 0.0), rotation=(0.0, 0.0, 0.0)):
    """Key a whole weapon pose on the exported root.

    Weapon meshes are intentionally modular props rather than skinned characters. A root
    transform animation keeps every mesh, socket, and muzzle point together while still
    importing as normal Unity AnimationClips.
    """
    bpy.context.scene.frame_set(frame)
    root.location = location
    root.rotation_mode = "XYZ"
    root.rotation_euler = rotation
    root.keyframe_insert(data_path="location", frame=frame, group=root.name)
    root.keyframe_insert(data_path="rotation_euler", frame=frame, group=root.name)


def _weapon_action(root, name, poses, frames, loop=False):
    root.animation_data_create()
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    root.animation_data.action = action

    for frame, location, rotation in poses:
        _weapon_pose(root, frame, location, rotation)

    # Use the layered/NLA representation Blender 5.2 expects. The FBX export below uses
    # all actions as takes, which preserves these names for Unity's ModelImporter.
    action["clip_name"] = name
    action["loop"] = loop
    action["frames"] = frames
    track = root.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.mute = True
    root.animation_data.action = None


def build_weapon_animations(root, name):
    """Create the shared prop animation vocabulary for every weapon.

    Shared clips let the runtime stay data driven while the pose language still covers the
    weapon classes: equip/holster, recoil, reload, energy charge, inspection, and a melee
    bash. The class-specific clips are useful when a definition grows a unique secondary
    attack later, and cost little because they are transform-only takes.
    """
    d = math.radians
    rest = (0.0, 0.0, 0.0)

    _weapon_action(root, "Idle", [
        (1, (0.0, 0.0, 0.0), rest),
        (16, (0.0, 0.002, 0.003), (d(-0.55), d(0.35), d(0.45))),
        (31, (0.0, 0.0, 0.0), rest),
        (46, (0.0, -0.002, -0.003), (d(0.55), d(-0.35), d(-0.45))),
        (61, (0.0, 0.0, 0.0), rest),
    ], 61, True)

    _weapon_action(root, "Equip", [
        (1, (0.0, -0.10, -0.18), (d(-20), d(5), d(-8))),
        (8, (0.0, -0.045, -0.07), (d(-9), d(2), d(-3))),
        (16, (0.0, 0.0, 0.0), rest),
    ], 16, False)

    _weapon_action(root, "Unequip", [
        (1, (0.0, 0.0, 0.0), rest),
        (8, (0.0, -0.045, -0.07), (d(-9), d(2), d(-3))),
        (16, (0.0, -0.10, -0.18), (d(-20), d(5), d(-8))),
    ], 16, False)

    _weapon_action(root, "Fire", [
        (1, (0.0, 0.0, 0.0), rest),
        (3, (0.0, -0.055, -0.010), (d(-7.5), d(0.0), d(0.0))),
        (6, (0.0, -0.020, 0.0), (d(-2.0), d(0.0), d(0.0))),
        (12, (0.0, 0.0, 0.0), rest),
    ], 12, False)

    _weapon_action(root, "Reload", [
        (1, (0.0, 0.0, 0.0), rest),
        (8, (0.0, -0.045, -0.055), (d(-10), d(0), d(-4))),
        (20, (0.055, -0.065, -0.10), (d(-24), d(-10), d(-12))),
        (34, (-0.045, -0.045, -0.08), (d(-15), d(9), d(10))),
        (46, (0.0, -0.020, -0.025), (d(-5), d(0), d(0))),
        (58, (0.0, 0.0, 0.0), rest),
    ], 58, False)

    _weapon_action(root, "Charge", [
        (1, (0.0, 0.0, 0.0), rest),
        (10, (0.0, 0.0, 0.008), (d(-2), d(0), d(0))),
        (20, (0.0, 0.0, 0.016), (d(-4), d(0), d(0))),
        (30, (0.0, 0.0, 0.008), (d(-2), d(0), d(0))),
        (40, (0.0, 0.0, 0.0), rest),
    ], 40, True)

    _weapon_action(root, "Inspect", [
        (1, (0.0, 0.0, 0.0), rest),
        (18, (0.0, -0.015, 0.015), (d(0), d(22), d(9))),
        (36, (0.0, 0.0, 0.0), rest),
    ], 36, False)

    _weapon_action(root, "Melee", [
        (1, (0.0, 0.0, 0.0), rest),
        (8, (0.0, -0.02, 0.02), (d(-16), d(-8), d(-5))),
        (14, (0.0, 0.075, 0.025), (d(27), d(4), d(6))),
        (24, (0.0, 0.0, 0.0), rest),
    ], 24, False)

    # The named class clips keep a readable hook for future weapon-specific animator states.
    class_clip = {
        "Shotgun": "Pump",
        "SniperRifle": "BoltCycle",
        "AssaultRifle": "BoltCycle",
        "SMG": "BoltCycle",
        "Pistol": "SlideCycle",
        "GrenadeLauncher": "DrumCycle",
        "Flamethrower": "Ignite",
        "TeslaCoil": "Discharge",
        "SiphonRifle": "Drain",
        "NailGun": "DriverCycle",
    }.get(name)
    if class_clip:
        _weapon_action(root, class_clip, [
            (1, (0.0, 0.0, 0.0), rest),
            (5, (0.0, -0.035, 0.0), (d(-4), d(0), d(0))),
            (12, (0.0, 0.020, 0.0), (d(3), d(0), d(0))),
            (20, (0.0, 0.0, 0.0), rest),
        ], 20, False)

    # Leave the source in a neutral pose and leave all takes available for the exporter.
    root.animation_data.action = None
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = rest


def export_weapon(root, name, target):
    out_dir = os.path.join(UNITY_OUT, name)
    os.makedirs(out_dir, exist_ok=True)
    source_dir = os.path.join(BASE, name)
    os.makedirs(source_dir, exist_ok=True)

    # Save the editable source with the preview rig hidden from viewport and render.
    preview = ground_and_lights(target)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 520
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Weapon Preview World")
    scene.world.color = (0.012, 0.016, 0.025)
    scene.render.filepath = os.path.join(source_dir, f"{name}_Preview.png")
    bpy.ops.render.render(write_still=True)

    preview.hide_viewport = True
    preview.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source_dir, f"{name}.blend"))

    select_tree(root)
    for track in root.animation_data.nla_tracks:
        track.mute = False
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, f"{name}.fbx"),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_simplify_factor=0.0,
        apply_scale_options="FBX_SCALE_UNITS",
        path_mode="COPY",
    )
    select_tree(root)
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(out_dir, f"{name}.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_nla_strips=True,
        export_materials="EXPORT",
    )

    report = {
        "weapon": name,
        "style": "chunky stylized survival shooter prop",
        "forward_axis": "+Y in Blender, +Z after Unity FBX import",
        "parts": len([o for o in root.children_recursive if o.type == "MESH"]),
        "attachments": ["GripSocket", "MuzzleSocket", "EjectPortSocket"],
        "animations": [a.name for a in bpy.data.actions if a.name in {
            "Idle", "Equip", "Unequip", "Fire", "Reload", "Charge", "Inspect", "Melee",
            "Pump", "BoltCycle", "SlideCycle", "DrumCycle", "Ignite", "Discharge", "Drain",
            "DriverCycle",
        }],
        "bounds_meters": bbox_for(root),
    }
    with open(os.path.join(source_dir, "asset_report.json"), "w") as handle:
        json.dump(report, handle, indent=2)
    with open(os.path.join(out_dir, "README.md"), "w") as handle:
        handle.write(
            f"# {name}\n\n"
            "Stylized low-poly weapon prop for Reborn. The model is authored around the player's "
            "existing +Z aim direction after Unity FBX import.\n\n"
            "## Files\n\n"
            f"- `{name}.fbx` — Unity import.\n"
            f"- `{name}.glb` — portable glTF export.\n"
            f"- `{name}.blend` — editable Blender source in ArtSource.\n\n"
            "## Attachment points\n\n"
            "The exported hierarchy includes `GripSocket`, `MuzzleSocket`, and `EjectPortSocket` "
            "for prefab wiring. Animation clips are authored on the root so the sockets follow "
            "recoil and reload motion: `Idle`, `Equip`, `Unequip`, `Fire`, `Reload`, `Charge`, "
            "`Inspect`, `Melee`, plus a class-specific mechanical cycle.\n"
        )

    for track in root.animation_data.nla_tracks:
        track.mute = True


def build_pistol():
    root = root_object("Pistol_Root", "pistol")
    metal = material("Pistol • blue black metal", (0.045, 0.075, 0.095), metallic=0.82, roughness=0.34)
    slide = material("Pistol • worn slide", (0.12, 0.17, 0.19), metallic=0.75, roughness=0.40)
    accent = material("Pistol • safety teal", (0.10, 0.34, 0.34), metallic=0.45, roughness=0.45)
    grip = material("Pistol • rubber grip", (0.11, 0.07, 0.055), metallic=0.05, roughness=0.78)
    brass = material("Pistol • brass", (0.58, 0.38, 0.12), metallic=0.68, roughness=0.36)
    ember = material("Pistol • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Frame", (0.0, 0.22, 0.22), (0.30, 0.50, 0.23), metal, root, bevel=0.035)
    box("Slide", (0.0, 0.43, 0.39), (0.32, 0.52, 0.15), slide, root, bevel=0.025)
    box("Slide front cap", (0.0, 0.69, 0.39), (0.30, 0.055, 0.13), metal, root, bevel=0.014)
    cylinder("Barrel", (0.0, 0.72, 0.40), 0.052, 0.35, brass, root, axis="Y", vertices=12)
    torus("Muzzle ring", (0.0, 0.90, 0.40), 0.067, 0.018, ember, root, axis="Y")

    box("Grip", (0.0, -0.02, -0.04), (0.25, 0.23, 0.52), grip, root, rotation=(math.radians(-11), 0.0, 0.0), bevel=0.035)
    box("Grip spine", (0.0, -0.105, 0.01), (0.27, 0.045, 0.40), accent, root, rotation=(math.radians(-11), 0.0, 0.0), bevel=0.016)
    box("Magazine heel", (0.0, -0.08, -0.31), (0.18, 0.16, 0.10), metal, root, rotation=(math.radians(-11), 0.0, 0.0), bevel=0.018)

    # Chunky trigger guard and a readable trigger silhouette.
    box("Trigger guard lower", (0.0, 0.20, 0.09), (0.075, 0.23, 0.055), metal, root, bevel=0.018)
    box("Trigger guard front", (0.0, 0.31, 0.16), (0.075, 0.055, 0.16), metal, root, bevel=0.014)
    box("Trigger", (0.0, 0.20, 0.17), (0.045, 0.07, 0.11), brass, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.01)
    box("Safety", (0.17, 0.31, 0.26), (0.025, 0.11, 0.07), accent, root, bevel=0.008)

    box("Rear sight", (0.0, 0.22, 0.51), (0.11, 0.08, 0.075), metal, root, bevel=0.012)
    box("Front sight", (0.0, 0.72, 0.49), (0.065, 0.07, 0.08), ember, root, bevel=0.01)
    for i in range(4):
        box("Slide serration", (0.0, 0.24 + i * 0.065, 0.475), (0.245, 0.018, 0.022), metal, root, bevel=0.004)

    socket("GripSocket", (0.0, -0.05, -0.10), root)
    socket("MuzzleSocket", (0.0, 0.92, 0.40), root)
    socket("EjectPortSocket", (0.17, 0.33, 0.34), root)
    return root, (0.0, 0.35, 0.18)


def build_shotgun():
    root = root_object("Shotgun_Root", "shotgun")
    receiver = material("Shotgun • oxidized receiver", (0.095, 0.12, 0.13), metallic=0.82, roughness=0.40)
    barrel = material("Shotgun • dark barrels", (0.035, 0.048, 0.055), metallic=0.90, roughness=0.27)
    pump = material("Shotgun • faded petrol pump", (0.11, 0.22, 0.23), metallic=0.32, roughness=0.57)
    stock = material("Shotgun • walnut stock", (0.25, 0.105, 0.055), metallic=0.08, roughness=0.67)
    brass = material("Shotgun • shell brass", (0.62, 0.39, 0.12), metallic=0.72, roughness=0.35)
    shell = material("Shotgun • red shell", (0.48, 0.055, 0.035), metallic=0.28, roughness=0.46)
    ember = material("Shotgun • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Receiver", (0.0, 0.19, 0.34), (0.36, 0.52, 0.28), receiver, root, bevel=0.045)
    box("Receiver top rail", (0.0, 0.20, 0.53), (0.24, 0.38, 0.07), barrel, root, bevel=0.014)
    box("Stock", (0.0, -0.28, 0.31), (0.31, 0.62, 0.27), stock, root, rotation=(math.radians(-4), 0.0, 0.0), bevel=0.055)
    box("Butt pad", (0.0, -0.61, 0.30), (0.34, 0.09, 0.30), barrel, root, bevel=0.025)
    box("Pistol grip", (0.0, -0.02, 0.03), (0.28, 0.23, 0.43), stock, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.045)

    box("Pump housing", (0.0, 0.65, 0.28), (0.34, 0.38, 0.23), pump, root, bevel=0.045)
    box("Pump rib", (0.0, 0.65, 0.40), (0.23, 0.32, 0.055), receiver, root, bevel=0.012)
    for x in (-0.09, 0.09):
        cylinder("Barrel", (x, 0.91, 0.43), 0.064, 1.16, barrel, root, axis="Y", vertices=12)
        torus("Muzzle ring", (x, 1.50, 0.43), 0.076, 0.020, ember, root, axis="Y")
        cylinder("Muzzle bore", (x, 1.512, 0.43), 0.038, 0.028, receiver, root, axis="Y", vertices=12)

    box("Barrel band front", (0.0, 1.18, 0.43), (0.30, 0.08, 0.20), receiver, root, bevel=0.018)
    box("Barrel band rear", (0.0, 0.61, 0.43), (0.31, 0.07, 0.20), receiver, root, bevel=0.016)
    box("Front bead", (0.0, 1.42, 0.54), (0.06, 0.08, 0.07), brass, root, bevel=0.012)
    box("Ejection port", (0.19, 0.22, 0.39), (0.025, 0.24, 0.12), barrel, root, bevel=0.006)
    box("Safety button", (0.19, 0.10, 0.51), (0.055, 0.10, 0.045), shell, root, bevel=0.012)

    # Two visible shells turn the side profile into a readable pump shotgun.
    for i, y in enumerate((0.08, 0.18)):
        cylinder("Loaded shell", (0.205, y, 0.30), 0.036, 0.16, shell, root, axis="X", vertices=10)
        cylinder("Shell rim", (0.293, y, 0.30), 0.042, 0.018, brass, root, axis="X", vertices=10)

    box("Sling stud rear", (-0.19, -0.50, 0.22), (0.035, 0.08, 0.06), brass, root, bevel=0.008)
    box("Sling stud front", (-0.19, 1.20, 0.28), (0.035, 0.08, 0.06), brass, root, bevel=0.008)
    socket("GripSocket", (0.0, -0.05, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.52, 0.43), root)
    socket("EjectPortSocket", (0.22, 0.22, 0.39), root)
    return root, (0.0, 0.50, 0.25)


def build_assault_rifle():
    root = root_object("AssaultRifle_Root", "assault_rifle")
    receiver = material("Assault rifle • graphite receiver", (0.045, 0.060, 0.068), metallic=0.88, roughness=0.33)
    rail = material("Assault rifle • rail steel", (0.12, 0.15, 0.16), metallic=0.78, roughness=0.39)
    furniture = material("Assault rifle • faded teal furniture", (0.055, 0.19, 0.20), metallic=0.22, roughness=0.62)
    grip = material("Assault rifle • rubber grip", (0.08, 0.055, 0.045), metallic=0.04, roughness=0.82)
    brass = material("Assault rifle • brass", (0.58, 0.37, 0.11), metallic=0.68, roughness=0.36)
    ember = material("Assault rifle • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Lower receiver", (0.0, 0.19, 0.22), (0.34, 0.40, 0.22), receiver, root, bevel=0.035)
    box("Upper receiver", (0.0, 0.39, 0.38), (0.35, 0.56, 0.25), receiver, root, bevel=0.032)
    box("Top rail", (0.0, 0.39, 0.53), (0.20, 0.55, 0.065), rail, root, bevel=0.012)
    box("Buttstock", (0.0, -0.33, 0.34), (0.32, 0.52, 0.27), furniture, root, rotation=(math.radians(-2), 0.0, 0.0), bevel=0.048)
    box("Buttpad", (0.0, -0.61, 0.33), (0.33, 0.08, 0.27), grip, root, bevel=0.018)
    box("Pistol grip", (0.0, 0.05, 0.00), (0.24, 0.20, 0.44), grip, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.038)
    box("Magazine", (0.0, 0.18, -0.08), (0.18, 0.25, 0.45), furniture, root, rotation=(math.radians(-12), 0.0, 0.0), bevel=0.026)
    box("Magazine floorplate", (0.0, 0.04, -0.31), (0.20, 0.13, 0.07), brass, root, rotation=(math.radians(-12), 0.0, 0.0), bevel=0.012)
    box("Handguard", (0.0, 0.78, 0.37), (0.31, 0.58, 0.24), furniture, root, bevel=0.045)
    for i in range(4):
        box("Handguard rail", (0.0, 0.58 + i * 0.105, 0.515), (0.22, 0.045, 0.035), rail, root, bevel=0.006)
    cylinder("Barrel", (0.0, 1.24, 0.40), 0.050, 0.78, receiver, root, axis="Y", vertices=12)
    box("Muzzle brake", (0.0, 1.64, 0.40), (0.15, 0.16, 0.15), receiver, root, bevel=0.02)
    torus("Muzzle accent", (0.0, 1.73, 0.40), 0.076, 0.018, ember, root, axis="Y")
    box("Rear sight", (0.0, 0.18, 0.61), (0.12, 0.09, 0.10), rail, root, bevel=0.01)
    box("Front sight", (0.0, 1.34, 0.55), (0.065, 0.08, 0.12), brass, root, bevel=0.01)
    box("Charging handle", (0.19, 0.42, 0.44), (0.08, 0.13, 0.045), brass, root, bevel=0.008)
    socket("GripSocket", (0.0, 0.04, -0.09), root)
    socket("MuzzleSocket", (0.0, 1.75, 0.40), root)
    socket("EjectPortSocket", (0.20, 0.40, 0.39), root)
    return root, (0.0, 0.45, 0.25)


def build_sniper():
    root = root_object("SniperRifle_Root", "sniper_rifle")
    receiver = material("Sniper • blue black receiver", (0.035, 0.050, 0.065), metallic=0.9, roughness=0.28)
    barrel = material("Sniper • satin barrel", (0.14, 0.17, 0.18), metallic=0.82, roughness=0.34)
    stock = material("Sniper • dark walnut stock", (0.24, 0.085, 0.038), metallic=0.10, roughness=0.68)
    scope = material("Sniper • scope glass housing", (0.06, 0.11, 0.12), metallic=0.75, roughness=0.26)
    lens = material("Sniper • blue lens", (0.06, 0.34, 0.46), metallic=0.30, roughness=0.16)
    brass = material("Sniper • brass", (0.58, 0.37, 0.11), metallic=0.68, roughness=0.36)
    ember = material("Sniper • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Receiver", (0.0, 0.20, 0.35), (0.32, 0.47, 0.26), receiver, root, bevel=0.035)
    box("Long stock", (0.0, -0.34, 0.31), (0.29, 0.78, 0.27), stock, root, rotation=(math.radians(-3), 0.0, 0.0), bevel=0.052)
    box("Cheek rest", (0.0, -0.03, 0.53), (0.24, 0.33, 0.12), stock, root, rotation=(math.radians(-3), 0.0, 0.0), bevel=0.028)
    box("Grip", (0.0, 0.02, 0.03), (0.23, 0.20, 0.40), stock, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.035)
    box("Magazine", (0.0, 0.19, 0.05), (0.17, 0.22, 0.30), receiver, root, rotation=(math.radians(-8), 0.0, 0.0), bevel=0.022)
    cylinder("Barrel", (0.0, 1.00, 0.40), 0.048, 1.46, barrel, root, axis="Y", vertices=12)
    box("Muzzle brake", (0.0, 1.76, 0.40), (0.14, 0.20, 0.14), receiver, root, bevel=0.018)
    torus("Muzzle accent", (0.0, 1.87, 0.40), 0.072, 0.018, ember, root, axis="Y")
    cylinder("Scope body", (0.0, 0.38, 0.70), 0.085, 0.60, scope, root, axis="Y", vertices=12)
    cylinder("Scope objective", (0.0, 0.70, 0.70), 0.13, 0.10, scope, root, axis="Y", vertices=12)
    cylinder("Scope lens", (0.0, 0.76, 0.70), 0.092, 0.012, lens, root, axis="Y", vertices=12)
    for y in (0.17, 0.58):
        torus("Scope ring", (0.0, y, 0.70), 0.10, 0.016, brass, root, axis="Y")
    box("Bipod left", (-0.13, 0.92, 0.07), (0.045, 0.30, 0.07), receiver, root, rotation=(math.radians(-18), 0.0, math.radians(-12)), bevel=0.01)
    box("Bipod right", (0.13, 0.92, 0.07), (0.045, 0.30, 0.07), receiver, root, rotation=(math.radians(-18), 0.0, math.radians(12)), bevel=0.01)
    box("Bipod foot left", (-0.15, 1.03, 0.03), (0.10, 0.05, 0.045), brass, root, bevel=0.008)
    box("Bipod foot right", (0.15, 1.03, 0.03), (0.10, 0.05, 0.045), brass, root, bevel=0.008)
    socket("GripSocket", (0.0, 0.02, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.90, 0.40), root)
    socket("EjectPortSocket", (0.18, 0.28, 0.40), root)
    return root, (0.0, 0.50, 0.30)


def build_flamethrower():
    root = root_object("Flamethrower_Root", "flamethrower")
    body = material("Flamethrower • oxidized body", (0.11, 0.13, 0.12), metallic=0.68, roughness=0.46)
    tank = material("Flamethrower • faded military tank", (0.22, 0.30, 0.22), metallic=0.28, roughness=0.67)
    hose = material("Flamethrower • rubber hose", (0.035, 0.045, 0.038), metallic=0.08, roughness=0.78)
    brass = material("Flamethrower • brass fittings", (0.59, 0.36, 0.10), metallic=0.7, roughness=0.35)
    flame = material("Flamethrower • hot nozzle", (0.72, 0.18, 0.035), metallic=0.22, roughness=0.42)
    grip = material("Flamethrower • rubber grip", (0.09, 0.06, 0.045), metallic=0.04, roughness=0.8)

    box("Main body", (0.0, 0.20, 0.34), (0.36, 0.52, 0.30), body, root, bevel=0.045)
    box("Top handle", (0.0, 0.20, 0.60), (0.20, 0.38, 0.08), brass, root, bevel=0.018)
    box("Grip", (0.0, 0.02, 0.02), (0.25, 0.22, 0.43), grip, root, rotation=(math.radians(-14), 0.0, 0.0), bevel=0.04)
    box("Foregrip", (0.0, 0.60, 0.15), (0.25, 0.24, 0.30), grip, root, rotation=(math.radians(-10), 0.0, 0.0), bevel=0.035)
    cylinder("Fuel tank left", (-0.23, -0.20, 0.38), 0.20, 0.52, tank, root, axis="X", vertices=12)
    cylinder("Fuel tank right", (0.23, -0.20, 0.38), 0.20, 0.52, tank, root, axis="X", vertices=12)
    for x in (-0.23, 0.23):
        torus("Tank band", (x, -0.20, 0.38), 0.205, 0.014, brass, root, axis="X")
    box("Tank valve", (0.0, 0.08, 0.58), (0.18, 0.12, 0.10), brass, root, bevel=0.015)
    cone("Nozzle bell", (0.0, 1.04, 0.38), 0.15, 0.075, 0.30, body, root, axis="Y", vertices=12, bevel=0.012)
    cylinder("Nozzle throat", (0.0, 0.82, 0.38), 0.065, 0.34, body, root, axis="Y", vertices=12)
    torus("Nozzle ring", (0.0, 1.18, 0.38), 0.12, 0.018, flame, root, axis="Y")
    cylinder("Pilot light", (0.0, 0.98, 0.49), 0.025, 0.06, brass, root, axis="Y", vertices=10)
    # Short, chunky hose segments connect the tank block to the torch body.
    cylinder("Hose rear", (0.16, 0.12, 0.47), 0.045, 0.32, hose, root, axis="Y", vertices=10)
    cylinder("Hose bend", (0.16, 0.36, 0.42), 0.045, 0.20, hose, root, axis="Y", vertices=10)
    socket("GripSocket", (0.0, 0.02, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.20, 0.38), root)
    socket("EjectPortSocket", (0.20, 0.30, 0.42), root)
    return root, (0.0, 0.50, 0.30)


def build_tesla_coil():
    root = root_object("TeslaCoil_Root", "tesla_coil")
    body = material("Tesla • dark receiver", (0.045, 0.065, 0.075), metallic=0.82, roughness=0.32)
    coil = material("Tesla • copper coil", (0.55, 0.20, 0.075), metallic=0.74, roughness=0.34)
    battery = material("Tesla • battery teal", (0.05, 0.25, 0.28), metallic=0.38, roughness=0.48)
    energy = material("Tesla • charged cyan", (0.05, 0.68, 0.85), metallic=0.22, roughness=0.18)
    grip = material("Tesla • rubber grip", (0.08, 0.06, 0.05), metallic=0.04, roughness=0.78)

    box("Receiver", (0.0, 0.18, 0.32), (0.34, 0.48, 0.28), body, root, bevel=0.042)
    box("Battery pack", (0.20, 0.12, 0.33), (0.20, 0.36, 0.34), battery, root, bevel=0.032)
    box("Grip", (0.0, -0.02, 0.02), (0.24, 0.22, 0.44), grip, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.038)
    cylinder("Coil core", (0.0, 0.70, 0.35), 0.105, 0.64, body, root, axis="Y", vertices=12)
    for y, radius in ((0.42, 0.16), (0.64, 0.17), (0.86, 0.18), (1.08, 0.19)):
        torus("Copper coil ring", (0.0, y, 0.35), radius, 0.026, coil, root, axis="Y")
    sphere("Energy emitter", (0.0, 1.28, 0.35), (0.13, 0.13, 0.13), energy, root)
    torus("Emitter guard", (0.0, 1.28, 0.35), 0.18, 0.025, coil, root, axis="Y")
    box("Charge indicator", (0.0, 0.26, 0.52), (0.09, 0.17, 0.035), energy, root, bevel=0.009)
    box("Top rail", (0.0, 0.16, 0.52), (0.14, 0.30, 0.05), body, root, bevel=0.01)
    socket("GripSocket", (0.0, -0.02, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.30, 0.35), root)
    socket("EjectPortSocket", (0.20, 0.30, 0.38), root)
    return root, (0.0, 0.55, 0.30)


def build_grenade_launcher():
    root = root_object("GrenadeLauncher_Root", "grenade_launcher")
    body = material("Grenade launcher • receiver", (0.07, 0.09, 0.095), metallic=0.78, roughness=0.40)
    drum = material("Grenade launcher • oxidized drum", (0.16, 0.19, 0.18), metallic=0.67, roughness=0.44)
    stock = material("Grenade launcher • dark stock", (0.15, 0.065, 0.035), metallic=0.08, roughness=0.72)
    shell = material("Grenade launcher • shell green", (0.28, 0.35, 0.18), metallic=0.26, roughness=0.60)
    brass = material("Grenade launcher • brass", (0.60, 0.38, 0.10), metallic=0.7, roughness=0.35)
    ember = material("Grenade launcher • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Receiver", (0.0, 0.12, 0.33), (0.35, 0.42, 0.28), body, root, bevel=0.04)
    box("Stock", (0.0, -0.35, 0.31), (0.31, 0.58, 0.26), stock, root, rotation=(math.radians(-4), 0.0, 0.0), bevel=0.045)
    box("Grip", (0.0, -0.01, 0.01), (0.24, 0.20, 0.43), stock, root, rotation=(math.radians(-14), 0.0, 0.0), bevel=0.038)
    cylinder("Rotary drum", (0.0, 0.47, 0.32), 0.27, 0.34, drum, root, axis="X", vertices=12, bevel=0.012)
    # Six chunky chamber shells around the drum circumference.
    for i in range(6):
        angle = math.radians(i * 60.0)
        y = 0.47 + math.cos(angle) * 0.18
        z = 0.32 + math.sin(angle) * 0.18
        cylinder("Drum shell", (0.20, y, z), 0.050, 0.05, shell, root, axis="X", vertices=10)
        cylinder("Shell rim", (0.235, y, z), 0.058, 0.018, brass, root, axis="X", vertices=10)
    cylinder("Launch tube", (0.0, 0.95, 0.35), 0.105, 0.74, body, root, axis="Y", vertices=12)
    torus("Muzzle ring", (0.0, 1.34, 0.35), 0.13, 0.022, ember, root, axis="Y")
    box("Top sight", (0.0, 0.40, 0.62), (0.12, 0.20, 0.10), brass, root, bevel=0.01)
    box("Safety", (0.20, 0.16, 0.48), (0.04, 0.12, 0.06), shell, root, bevel=0.008)
    socket("GripSocket", (0.0, -0.01, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.37, 0.35), root)
    socket("EjectPortSocket", (0.20, 0.22, 0.39), root)
    return root, (0.0, 0.55, 0.30)


def build_smg():
    root = root_object("SMG_Root", "smg")
    body = material("SMG • graphite body", (0.040, 0.055, 0.062), metallic=0.86, roughness=0.30)
    furniture = material("SMG • petrol furniture", (0.055, 0.20, 0.21), metallic=0.25, roughness=0.58)
    grip = material("SMG • rubber grip", (0.08, 0.055, 0.045), metallic=0.04, roughness=0.80)
    brass = material("SMG • brass", (0.60, 0.38, 0.11), metallic=0.70, roughness=0.35)
    ember = material("SMG • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Receiver", (0.0, 0.22, 0.33), (0.31, 0.52, 0.25), body, root, bevel=0.038)
    box("Rear block", (0.0, -0.22, 0.34), (0.29, 0.35, 0.24), body, root, bevel=0.03)
    box("Folding stock", (0.0, -0.45, 0.49), (0.27, 0.33, 0.07), furniture, root, rotation=(math.radians(8), 0.0, 0.0), bevel=0.018)
    box("Grip", (0.0, 0.00, 0.02), (0.23, 0.20, 0.43), grip, root, rotation=(math.radians(-14), 0.0, 0.0), bevel=0.035)
    box("Magazine", (0.0, 0.20, -0.10), (0.16, 0.23, 0.44), furniture, root, rotation=(math.radians(-13), 0.0, 0.0), bevel=0.024)
    box("Front handguard", (0.0, 0.62, 0.35), (0.28, 0.36, 0.22), furniture, root, bevel=0.035)
    cylinder("Barrel", (0.0, 0.94, 0.38), 0.042, 0.60, body, root, axis="Y", vertices=12)
    cylinder("Suppressor", (0.0, 1.27, 0.38), 0.095, 0.34, body, root, axis="Y", vertices=12)
    torus("Muzzle ring", (0.0, 1.46, 0.38), 0.105, 0.018, ember, root, axis="Y")
    box("Top rail", (0.0, 0.30, 0.49), (0.15, 0.35, 0.05), brass, root, bevel=0.008)
    box("Charging handle", (0.17, 0.29, 0.45), (0.07, 0.12, 0.04), brass, root, bevel=0.007)
    socket("GripSocket", (0.0, 0.00, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.48, 0.38), root)
    socket("EjectPortSocket", (0.18, 0.28, 0.37), root)
    return root, (0.0, 0.55, 0.28)


def build_nail_gun():
    root = root_object("NailGun_Root", "nail_gun")
    body = material("Nail gun • painted steel", (0.18, 0.20, 0.18), metallic=0.66, roughness=0.48)
    teal = material("Nail gun • faded teal", (0.065, 0.25, 0.24), metallic=0.28, roughness=0.59)
    grip = material("Nail gun • rubber grip", (0.075, 0.052, 0.040), metallic=0.04, roughness=0.82)
    warning = material("Nail gun • safety yellow", (0.78, 0.49, 0.07), metallic=0.20, roughness=0.45)
    nail = material("Nail gun • nail steel", (0.58, 0.62, 0.60), metallic=0.86, roughness=0.29)
    ember = material("Nail gun • muzzle ember", (0.72, 0.19, 0.055), metallic=0.2, roughness=0.45)

    box("Main body", (0.0, 0.22, 0.35), (0.36, 0.56, 0.32), body, root, bevel=0.045)
    box("Top magazine hopper", (0.0, 0.13, 0.62), (0.28, 0.34, 0.28), teal, root, bevel=0.035)
    box("Hopper lid", (0.0, 0.13, 0.79), (0.30, 0.36, 0.07), warning, root, bevel=0.015)
    box("Grip", (0.0, -0.03, 0.02), (0.25, 0.22, 0.45), grip, root, rotation=(math.radians(-14), 0.0, 0.0), bevel=0.04)
    box("Nose housing", (0.0, 0.72, 0.35), (0.28, 0.32, 0.22), teal, root, bevel=0.032)
    cone("Nail nose", (0.0, 0.98, 0.35), 0.13, 0.075, 0.24, body, root, axis="Y", vertices=12, bevel=0.01)
    cylinder("Driver", (0.0, 1.12, 0.35), 0.042, 0.12, nail, root, axis="Y", vertices=10)
    torus("Nose accent", (0.0, 1.19, 0.35), 0.09, 0.018, ember, root, axis="Y")
    for i in range(3):
        box("Warning stripe", (0.18, 0.20 + i * 0.11, 0.38), (0.025, 0.065, 0.12), warning, root, rotation=(0.0, 0.0, math.radians(18)), bevel=0.004)
    for i in range(4):
        cylinder("Loose nail", (0.12, -0.01 + i * 0.09, 0.67), 0.018, 0.13, nail, root, axis="Y", vertices=8)
    socket("GripSocket", (0.0, -0.03, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.21, 0.35), root)
    socket("EjectPortSocket", (0.20, 0.33, 0.46), root)
    return root, (0.0, 0.55, 0.32)


def build_siphon_rifle():
    root = root_object("SiphonRifle_Root", "siphon_rifle")
    body = material("Siphon rifle • dark receiver", (0.06, 0.045, 0.09), metallic=0.72, roughness=0.38)
    bone = material("Siphon rifle • bone grip", (0.47, 0.39, 0.27), metallic=0.08, roughness=0.66)
    energy = material("Siphon rifle • blood cyan", (0.07, 0.58, 0.70), metallic=0.26, roughness=0.16)
    violet = material("Siphon rifle • violet coil", (0.38, 0.08, 0.55), metallic=0.46, roughness=0.26)
    brass = material("Siphon rifle • tarnished brass", (0.52, 0.28, 0.11), metallic=0.66, roughness=0.38)
    ember = material("Siphon rifle • muzzle ember", (0.82, 0.13, 0.20), metallic=0.25, roughness=0.38)

    box("Receiver", (0.0, 0.20, 0.34), (0.34, 0.50, 0.29), body, root, bevel=0.04)
    box("Stock", (0.0, -0.34, 0.32), (0.30, 0.62, 0.25), bone, root, rotation=(math.radians(-3), 0.0, 0.0), bevel=0.045)
    box("Grip", (0.0, -0.01, 0.01), (0.24, 0.22, 0.43), bone, root, rotation=(math.radians(-14), 0.0, 0.0), bevel=0.04)
    box("Reservoir housing", (0.20, 0.24, 0.36), (0.20, 0.38, 0.38), violet, root, bevel=0.035)
    sphere("Life reservoir", (0.20, 0.24, 0.36), (0.11, 0.15, 0.15), energy, root)
    cylinder("Siphon barrel", (0.0, 0.87, 0.36), 0.070, 1.05, body, root, axis="Y", vertices=12)
    for y in (0.50, 0.73, 0.96, 1.19):
        torus("Violet coil", (0.0, y, 0.36), 0.105, 0.022, violet, root, axis="Y")
    cone("Siphon emitter", (0.0, 1.50, 0.36), 0.14, 0.060, 0.26, body, root, axis="Y", vertices=12, bevel=0.01)
    torus("Emitter ring", (0.0, 1.63, 0.36), 0.105, 0.02, ember, root, axis="Y")
    box("Top sight", (0.0, 0.24, 0.55), (0.12, 0.25, 0.06), brass, root, bevel=0.009)
    box("Bone talon left", (-0.18, 0.90, 0.49), (0.04, 0.32, 0.07), bone, root, rotation=(math.radians(-25), 0.0, math.radians(-8)), bevel=0.012)
    box("Bone talon right", (0.18, 0.90, 0.49), (0.04, 0.32, 0.07), bone, root, rotation=(math.radians(-25), 0.0, math.radians(8)), bevel=0.012)
    socket("GripSocket", (0.0, -0.01, -0.08), root)
    socket("MuzzleSocket", (0.0, 1.66, 0.36), root)
    socket("EjectPortSocket", (0.20, 0.30, 0.42), root)
    return root, (0.0, 0.56, 0.30)


def build_one(name, builder):
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.fps = 30
    root, target = builder()
    build_weapon_animations(root, name)
    export_weapon(root, name, target)


build_one("Pistol", build_pistol)
build_one("Shotgun", build_shotgun)
build_one("AssaultRifle", build_assault_rifle)
build_one("SniperRifle", build_sniper)
build_one("Flamethrower", build_flamethrower)
build_one("TeslaCoil", build_tesla_coil)
build_one("GrenadeLauncher", build_grenade_launcher)
build_one("SMG", build_smg)
build_one("NailGun", build_nail_gun)
build_one("SiphonRifle", build_siphon_rifle)
print("WEAPON_BUILD_COMPLETE", flush=True)
