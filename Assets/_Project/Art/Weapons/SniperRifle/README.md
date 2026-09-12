# SniperRifle

Stylized low-poly weapon prop for Reborn. The model is authored around the player's existing +Z aim direction after Unity FBX import.

## Files

- `SniperRifle.fbx` — Unity import.
- `SniperRifle.glb` — portable glTF export.
- `SniperRifle.blend` — editable Blender source in ArtSource.

## Attachment points

The exported hierarchy includes `GripSocket`, `MuzzleSocket`, and `EjectPortSocket` for prefab wiring. Animation clips are authored on the root so the sockets follow recoil and reload motion: `Idle`, `Equip`, `Unequip`, `Fire`, `Reload`, `Charge`, `Inspect`, `Melee`, plus a class-specific mechanical cycle.
