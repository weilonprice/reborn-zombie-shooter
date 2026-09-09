using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Reports missing scene singletons once, loudly, at startup.
    /// <para>
    /// Every singleton is reached through null-conditional calls, so one absent from the
    /// scene produces no error at all - the effect simply never happens. That has cost real
    /// debugging time three separate times: silent audio, missing shell casings, and an
    /// unassigned zombie prefab all presented as "nothing happens". This turns the whole
    /// class of failure into one Console line naming exactly what is missing.
    /// </para>
    /// Runs in Start so every Awake has already assigned its instance.
    /// </summary>
    public class SystemsCheck : MonoBehaviour
    {
        void Start()
        {
            var missing = new List<string>();

            if (SfxPlayer.Instance == null) missing.Add(nameof(SfxPlayer));
            if (ImpactEffects.Instance == null) missing.Add(nameof(ImpactEffects));
            if (HitStop.Instance == null) missing.Add(nameof(HitStop));
            if (CameraShake.Instance == null) missing.Add(nameof(CameraShake));
            if (TracerPool.Instance == null) missing.Add(nameof(TracerPool));
            if (ArmoryManager.Instance == null) missing.Add(nameof(ArmoryManager));
            if (WaveManager.Instance == null) missing.Add(nameof(WaveManager));
            if (GameManager.Instance == null) missing.Add(nameof(GameManager));

            if (missing.Count == 0) return;

            Debug.LogError(
                $"<b>Zombie Shooter</b>: {missing.Count} system(s) missing from the scene - " +
                $"{string.Join(", ", missing)}. Anything depending on them will silently do " +
                "nothing. Re-run Tools > Zombie Shooter > Build Playable Arena.", this);
        }
    }
}
