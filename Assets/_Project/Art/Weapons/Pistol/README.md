# Pistol

Stylized low-poly weapon prop for Reborn. The model is authored around the player's existing +Z aim direction after Unity FBX import.

## Files

- `Pistol.fbx` — Unity import.
- `Pistol.glb` — portable glTF export.
- `Pistol.blend` — editable Blender source in ArtSource.

## Attachment points

The exported hierarchy includes `GripSocket`, `MuzzleSocket`, and `EjectPortSocket` for later prefab wiring. Root motion and animation are intentionally absent: the current weapon runtime owns aim, firing, muzzle flash, and shell effects.
