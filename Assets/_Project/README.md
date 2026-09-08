# Zombie Shooter — top-down twin-stick wave survival

Unity 6000.6.0f1 · URP · new Input System

## First run

1. In the Editor: **Tools ▸ Zombie Shooter ▸ Build Playable Arena**
2. It generates and opens `Assets/_Project/Scenes/Arena.unity`, plus the zombie prefab and materials.
3. Press **Play**.

Re-running the builder rebuilds the scene from scratch — treat `Arena.unity` as generated
until we start hand-authoring the level, then stop using the tool.

## Controls

| Action  | Keyboard/Mouse | Gamepad      |
|---------|----------------|--------------|
| Move    | WASD           | Left stick   |
| Aim     | Mouse          | Right stick  |
| Fire    | Left click     | Right trigger|
| Reload  | R              | West button  |
| Restart | Space          | Start        |

## Layout

- `Scripts/Core` — `GameManager` (run state, score), `WaveManager` (spawning + pooling),
  `Health`, `IDamageable`, `CameraFollow`
- `Scripts/Player` — `PlayerController` (twin-stick locomotion), `InputReader`
- `Scripts/Weapons` — `Weapon` (hitscan, magazine, reload)
- `Scripts/Enemies` — `ZombieAI` (steering + local separation)
- `Scripts/UI` — `HUD`
- `Scripts/Editor` — `ArenaBuilder` (scene generator)

## Design notes

- **No NavMesh.** The arena is open, so zombies steer straight at the player and push off
  neighbours. This scales to far more agents than NavMesh avoidance and needs no bake step.
  If the map ever gains corridors or dead ends, swap `ZombieAI` to a `NavMeshAgent` —
  `com.unity.ai.navigation` is already installed.
- **Input is polled directly** from `Keyboard`/`Mouse`/`Gamepad` in `InputReader` rather than
  bound to an `.inputactions` asset, so nothing needs inspector wiring. Rebinding support
  means changing only that one file.
- **HUD uses legacy uGUI `Text`** to avoid the TextMeshPro essentials import prompt.
  Worth moving to TMP once art direction is settled.
- **Zombies are pooled** by `WaveManager`; `Health` resets in `OnEnable` so recycled
  instances come back at full HP.
