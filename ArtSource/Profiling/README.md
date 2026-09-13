# Horde profiling

```
Tools ▸ Zombie Shooter ▸ Profile The Horde
```

Enters Play mode, ramps the live enemy count through 15 / 30 / 45 / 60 / 90,
samples 120 frames at each step, and writes `horde_profile.txt`.

## What it is for

Debt 6 has been deferred since the beginning on the reasoning that `ZombieAI.Separation`
is O(n²) and will break first. That reasoning was written when the horde was four
capsules. Since then:

- twelve **skinned** archetypes, which do not batch
- **nine collider hit zones per body**, mounted on animated bones — ~540 moving
  colliders at 60 enemies
- up to **96 spent cases**, each sweeping the scene and carrying three materials

Any of those could now be the ceiling. Nobody knows which, and that is the whole
problem — debt 6 names a culprit that has never been measured.

## Read the second table, not the first

Absolute milliseconds at one population tell you almost nothing, because the
editor is not a build and this machine is not a phone.

**Cost per enemy across a ramp** is the finding:

- **Separation ms/enemy RISING with population** → the O(n²) walk is the ceiling,
  and a spatial hash is the fix. Debt 6 was right.
- **Separation flat while frame time rises** → the cost moved somewhere else while
  debt 6 was being blamed. Rewrite the register before optimising the wrong loop.

The 90 step deliberately exceeds `maxAliveAtOnce` (60), because the useful question
is where the curve goes, not whether today's cap is survivable.

## Caveats worth stating

Measured in the **editor**, which carries its own overhead and a different renderer
path than a player build. Treat the shape of the curves as the result and the
absolute numbers as indicative. If the curve says the answer is ambiguous, the next
step is a development build with the profiler attached, not more editor runs.

The wave loop is stopped so each step holds a steady population, and the player is
healed each frame so a death does not end the run mid-ramp. Enemies still chase and
attack throughout — that load is part of what is being measured.
