# MainCharacter

Chunky stylized survivor hero authored for Reborn's top-down survival shooter. The model uses +Y as forward in Blender and imports with +Z as forward in Unity.

## Files

- `MainCharacter.fbx` — Unity skeletal import with baked animation clips.
- `MainCharacter.glb` — portable glTF export with embedded materials and animations.
- `MainCharacter.blend` — editable Blender source in ArtSource/MainCharacter.

## Animation clips

`Idle`, `Walk`, `Run`, `Aim`, `Fire`, `Reload`, `GetShot`, `Stagger`, and `Death`. Root motion is authored in place so PlayerController remains responsible for movement.

## Attachment points

`WeaponSocket_R`, `WeaponSocket_L`, `BackSocket`, and `HeadSocket` are exported as empty transforms for prefab wiring.
