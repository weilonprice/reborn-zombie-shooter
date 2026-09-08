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
            if (!enableHitStop || seconds <= 0f) return;

            // Simultaneous kills extend one freeze instead of stacking coroutines that
            // would each try to restore timeScale on their own schedule.
            resumeAt = Mathf.Max(resumeAt, Time.realtimeSinceStartup + seconds);
            routine ??= StartCoroutine(FreezeRoutine());
        }

        IEnumerator FreezeRoutine()
        {
            Time.timeScale = frozenTimeScale;

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
