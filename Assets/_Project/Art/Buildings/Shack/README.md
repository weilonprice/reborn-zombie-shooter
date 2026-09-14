# Shack — detailed replacement

Editable Blender source: ArtSource/Buildings/Shack/Shack.blend.
FBX is the Unity model; GLB is the portable version. Rebuild with Blender
--background --python ArtSource/Buildings/upgrade_buildings.py, then run
Tools > Zombie Shooter > Install Detailed Buildings in Unity.

Ready prefab: Assets/_Project/Prefabs/Buildings/Shack.prefab.
URP materials, UV0, generated lightmap UVs, combined decorative geometry,
one wall box collider. Exterior-only static cover; doors are decorative.
Existing map placement retains the FBX path/GUID and footprint scaling.
