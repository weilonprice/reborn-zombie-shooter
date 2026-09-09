using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Briefly freezes time so an impact reads as force.
    /// <para>
    /// Deliberately gated to kills. The rifle fires eight rounds a second into a crowd, so
    /// freezing on every hit would be a permanent stutter rather than an accent. Turn the
    /// whole effect off with <c>enableHitStop</c> if it fights the twin-stick pacing.
    /// </para>
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Tooltip("Master switch. Off restores normal time immediately.")]
        [SerializeField] bool enableHitStop = true;
        [Tooltip("Seconds of freeze on a kill. Past ~0.08 it starts to feel like a hitch.")]
        [SerializeField] float killFreeze = 0.05f;
        [Tooltip("0 is a hard stop; a small value reads as heavy slow-motion instead.")]
        [SerializeField, Range(0f, 1f)] float frozenTimeScale = 0f;

        float resumeAt;
        float activeScale;
        Coroutine routine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDisable() => ForceResume();

        void OnDestroy()
        {
            ForceResume();
            if (Instance == this) Instance = null;
        }

        public void FreezeForKill() => Freeze(killFreeze);

        public void Freeze(float seconds)
        {
            if (!enableHitStop) return;
            Request(seconds, frozenTimeScale);
        }

        /// <summary>
        /// A held slow-motion window rather than an impact accent.
        /// <para>
        /// Deliberately not gated by <c>enableHitStop</c>: that switch exists to kill the
        /// per-kill stutter if it fights the pacing, but bullet time is an upgrade the player
        /// paid gold for. Turning off one must not silently refund the other.
        /// </para>
        /// </summary>
        public void SlowMotion(float seconds, float scale) => Request(seconds, scale);

        void Request(float seconds, float scale)
        {
            if (seconds <= 0f) return;

            float until = Time.realtimeSinceStartup + seconds;

            // The longest outstanding request owns both the end time and the scale. Without
            // that, the 0.05s kill freeze fired by a kill would hard-stop the 1.5s bullet
            // time that the very same kill just started.
            if (until <= resumeAt) return;

            resumeAt = until;
            activeScale = Mathf.Clamp01(scale);

            if (routine != null) Time.timeScale = activeScale;
            routine ??= StartCoroutine(FreezeRoutine());
        }

        IEnumerator FreezeRoutine()
        {
            Time.timeScale = activeScale;

            // yield return null still ticks at timeScale 0 - it waits on frames, not seconds.
            while (Time.realtimeSinceStartup < resumeAt)
                yield return null;

            Time.timeScale = 1f;
            routine = null;
        }

        /// <summary>Never leave the game frozen because this object went away mid-freeze.</summary>
        void ForceResume()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (!Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = 1f;
        }
    }
}
