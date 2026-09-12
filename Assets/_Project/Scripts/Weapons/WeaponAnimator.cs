using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Plays the carried weapon's own clips from the weapon's own events.
    /// <para>
    /// Lives on the player rather than on each model, because the thing that changes is
    /// WHICH animator to talk to, not what to say to it. WeaponVisuals owns the swap and
    /// hands over the new animator; everything here is weapon-agnostic.
    /// </para>
    /// </summary>
    public class WeaponAnimator : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] WeaponVisuals visuals;
        [SerializeField] WeaponLoadout loadout;

        [Header("Fallback timings")]
        [Tooltip("Used only when a clip's real length cannot be read off the controller. " +
                 "Measured lengths are preferred, so a re-exported weapon with different " +
                 "timings needs no change here.")]
        [SerializeField] float fireDuration = 0.20f;
        [SerializeField] float reloadDuration = 1.90f;
        [SerializeField] float equipDuration = 0.55f;

        const string Idle = "Idle";
        const string Fire = "Fire";
        const string Reload = "Reload";
        const string Equip = "Equip";

        static readonly int FireSpeed = Animator.StringToHash("FireSpeed");
        static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");

        /// <summary>
        /// The per-weapon action clip - a bolt, a pump, a slide. Only one of these exists on
        /// any given weapon, so the first match is the right one.
        /// </summary>
        static readonly string[] CycleClips =
        {
            "BoltCycle", "SlideCycle", "Pump", "DrumCycle", "DriverCycle",
            "Discharge", "Drain", "Ignite",
        };

        Animator current;
        string cycleClip;
        float fireClipSeconds;
        float reloadClipSeconds;
        float cycleClipSeconds;

        float settleAt;
        bool reloading;
        string state;

        void Awake()
        {
            if (weapon == null) weapon = GetComponent<Weapon>();
            if (visuals == null) visuals = GetComponent<WeaponVisuals>();
            if (loadout == null) loadout = GetComponent<WeaponLoadout>();
        }

        void OnEnable()
        {
            if (visuals != null)
            {
                visuals.Changed += Rebind;
                Rebind(visuals.Current);
            }

            if (weapon == null) return;

            weapon.Fired += OnFired;
            weapon.ReloadStarted += OnReloadStarted;
            weapon.ReloadFinished += OnReloadFinished;
        }

        void OnDisable()
        {
            if (visuals != null) visuals.Changed -= Rebind;
            if (weapon == null) return;

            weapon.Fired -= OnFired;
            weapon.ReloadStarted -= OnReloadStarted;
            weapon.ReloadFinished -= OnReloadFinished;
        }

        void Update()
        {
            if (current == null || reloading) return;
            if (settleAt <= 0f || Time.time < settleAt) return;

            settleAt = 0f;
            Play(Idle, 0.08f);
        }

        void Rebind(Animator animator)
        {
            current = animator;
            state = null;
            reloading = false;
            settleAt = 0f;

            if (current == null) return;

            // Clip lengths come off the controller rather than from authored numbers, so a
            // re-exported weapon with different timings needs no code change.
            fireClipSeconds = Fallback(LengthOf(Fire), fireDuration);
            reloadClipSeconds = Fallback(LengthOf(Reload), reloadDuration);

            cycleClip = null;
            cycleClipSeconds = 0f;
            for (int i = 0; i < CycleClips.Length; i++)
            {
                float length = LengthOf(CycleClips[i]);
                if (length <= 0f) continue;

                cycleClip = CycleClips[i];
                cycleClipSeconds = length;
                break;
            }

            Play(Equip, 0f);
            settleAt = Time.time + Fallback(LengthOf(Equip), equipDuration);
        }

        void OnFired()
        {
            if (current == null) return;

            float interval = weapon != null ? weapon.SecondsBetweenShots : 1f;

            // Fit the recoil to the rate of fire. A gun firing every 0.067s would otherwise
            // only ever show the first frames of its clip and read as a vibration; sped up,
            // the whole action plays between shots.
            if (fireClipSeconds > 0f)
                current.SetFloat(FireSpeed, Mathf.Clamp(fireClipSeconds / interval, 1f, 6f));

            // Restarted rather than cross-faded: each shot is its own recoil, and blending
            // one into the next is what makes fast weapons look like they are idling.
            current.Play(Fire, 0, 0f);
            state = Fire;

            float fireSeconds = fireClipSeconds > 0f
                ? Mathf.Min(fireClipSeconds, interval)
                : interval;

            // Only bolt or pump when there is actually room between shots. On an SMG the
            // cycle would be cut off every time and just add noise.
            if (cycleClip != null && interval > fireSeconds + cycleClipSeconds)
            {
                settleAt = Time.time + fireSeconds + cycleClipSeconds;
                Invoke(nameof(PlayCycle), fireSeconds);
            }
            else
            {
                settleAt = Time.time + fireSeconds;
            }
        }

        void PlayCycle()
        {
            if (current == null || reloading || cycleClip == null) return;
            Play(cycleClip, 0.04f);
        }

        void OnReloadStarted()
        {
            if (current == null) return;

            reloading = true;
            CancelInvoke(nameof(PlayCycle));

            float actual = weapon != null ? Mathf.Max(0.1f, weapon.ReloadSeconds) : 1f;
            if (reloadClipSeconds > 0f)
                current.SetFloat(ReloadSpeed, Mathf.Clamp(reloadClipSeconds / actual, 0.4f, 6f));

            Play(Reload, 0.05f);
        }

        void OnReloadFinished()
        {
            reloading = false;
            settleAt = 0f;

            if (current != null) Play(Idle, 0.08f);
        }

        void Play(string clip, float fade)
        {
            if (current == null || state == clip) return;

            if (fade <= 0f) current.Play(clip, 0, 0f);
            else current.CrossFadeInFixedTime(clip, fade, 0, 0f);

            state = clip;
        }

        static float Fallback(float measured, float authored) => measured > 0f ? measured : authored;

        float LengthOf(string clipName)
        {
            var controller = current != null ? current.runtimeAnimatorController : null;
            if (controller == null) return 0f;

            foreach (var clip in controller.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;

            return 0f;
        }
    }
}
