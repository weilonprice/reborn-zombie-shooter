using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Charges the pistol's ultimate on kills and runs it: a flourish reload, then the guns
    /// aim themselves while the player spins.
    /// <para>
    /// Kept off <see cref="Weapon"/> because the ability is not a property of a firearm - it
    /// owns the player's facing, the camera and an input key, and only borrows the weapon to
    /// shoot with. The weapon exposes start/end and a kill event; everything else is here.
    /// </para>
    /// </summary>
    public class UltimateAbility : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] PlayerController movement;
        [SerializeField] Health health;

        [Tooltip("How fast the player spins during the ability. Roughly one and a half " +
                 "turns a second reads as a flourish rather than a glitch.")]
        [SerializeField] float spinDegreesPerSecond = 540f;

        [Header("Activation")]
        [Tooltip("A brief drop into slow motion so the ability starts on an accent rather " +
                 "than just beginning.")]
        [SerializeField] float openingSlowMotion = 0.35f;
        [SerializeField, Range(0.05f, 1f)] float openingTimeScale = 0.35f;
        [SerializeField] float openingTrauma = 0.5f;

        int kills;

        // Buying the tier changes nothing the ability itself does, so there is no event to
        // hang off. One bool compared per frame is cheaper than another subscription that
        // would have to survive the same Awake-ordering race as everything else.
        bool wasAvailable;

        /// <summary>Charge or activity changed; the HUD listens.</summary>
        public event Action Changed;

        public int Kills => kills;
        public int Required => weapon != null ? weapon.UltimateKillsRequired : 0;

        /// <summary>Whether the player has bought the tier at all.</summary>
        public bool Available => weapon != null && weapon.HasUltimate;
        public bool Charged => Available && kills >= Required;
        public bool Active => weapon != null && weapon.UltimateActive;

        void OnEnable()
        {
            if (weapon == null) return;

            weapon.Killed += OnKilled;
            weapon.UltimateEnded += OnUltimateEnded;
        }

        void OnDisable()
        {
            if (weapon != null)
            {
                weapon.Killed -= OnKilled;
                weapon.UltimateEnded -= OnUltimateEnded;
            }

            // Never leave the player spinning because this object went away mid-ability.
            if (movement != null) movement.AimOverridden = false;
        }

        void Update()
        {
            if (Available != wasAvailable)
            {
                wasAvailable = Available;
                Changed?.Invoke();
            }

            bool alive = health == null || health.IsAlive;
            bool playing = GameManager.Instance == null
                        || GameManager.Instance.State == GameState.Playing;

            // Dying or hitting the break with the ability running would otherwise strand the
            // player spinning with no way to stop, since Weapon stops updating when either
            // happens and would never reach an empty magazine.
            if (Active && (!alive || !playing))
            {
                weapon.EndUltimate();
                return;
            }

            if (!alive || !playing || Time.timeScale <= 0f) return;

            if (InputReader.UltimatePressed) TryActivate();
        }

        public void TryActivate()
        {
            if (!Charged || Active) return;

            kills = 0;
            weapon.StartUltimate();

            if (movement != null)
            {
                movement.SpinDegreesPerSecond = spinDegreesPerSecond;
                movement.AimOverridden = true;
            }

            HitStop.Instance?.SlowMotion(openingSlowMotion, openingTimeScale);
            CameraShake.Instance?.AddTrauma(openingTrauma);

            Changed?.Invoke();
        }

        void OnKilled()
        {
            // Kills scored by the ability do not pay for the next one.
            if (!Available || Active || kills >= Required) return;

            kills++;
            Changed?.Invoke();
        }

        void OnUltimateEnded()
        {
            if (movement != null) movement.AimOverridden = false;
            Changed?.Invoke();
        }
    }
}
