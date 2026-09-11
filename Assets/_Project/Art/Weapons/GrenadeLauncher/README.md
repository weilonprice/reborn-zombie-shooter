# GrenadeLauncher

Stylized low-poly weapon prop for Reborn. The model is authored around the player's existing +Z aim direction after Unity FBX import.

## Files

- `GrenadeLauncher.fbx` — Unity import.
- `GrenadeLauncher.glb` — portable glTF export.
- `GrenadeLauncher.blend` — editable Blender source in ArtSource.

## Attachment points

The exported hierarchy includes `GripSocket`, `MuzzleSocket`, and `EjectPortSocket` for later prefab wiring. Root motion and animation are intentionally absent: the current weapon runtime owns aim, firing, muzzle flash, and shell effects.
