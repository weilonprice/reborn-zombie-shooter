using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A light pop and a short spit of particles at the barrel, once per shot.
    /// The light does most of the work - it briefly throws real illumination onto nearby
    /// geometry, which sells the shot far better than a billboard would.
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        [SerializeField] Light flashLight;
        [SerializeField] ParticleSystem spark;
        [SerializeField] float lightIntensity = 12f;
        [Tooltip("Seconds the light stays on. This is a flash, not a lamp.")]
        [SerializeField] float duration = 0.035f;
        [SerializeField] int particlesPerShot = 4;

        Coroutine routine;

        void Awake()
        {
            if (flashLight == null) flashLight = GetComponentInChildren<Light>(true);
            if (spark == null) spark = GetComponentInChildren<ParticleSystem>(true);

            if (flashLight != null) flashLight.enabled = false;
        }

        void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            if (flashLight != null) flashLight.enabled = false;
        }

        public void Play()
        {
            if (spark != null) spark.Emit(particlesPerShot);
            if (flashLight == null) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            flashLight.intensity = lightIntensity;
            flashLight.enabled = true;

            // Realtime: at 8 rounds a second the flash must not stretch under a hit-stop.
            yield return new WaitForSecondsRealtime(duration);

            flashLight.enabled = false;
            routine = null;
        }
    }
}
