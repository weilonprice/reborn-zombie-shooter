using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Vaults barricades instead of clawing through them.
    /// <para>
    /// The archetype that says a wall buys time, not safety. Every other melee enemy treats
    /// a barricade as something to break; this one treats it as something to get over, which
    /// is the only way the fortification hook stays a decision rather than a solved problem.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BarricadeLeaper : MonoBehaviour, IEnemyMotionOverride
    {
        [Tooltip("How close to a wall before it jumps.")]
        [SerializeField] float triggerRange = 3f;
        [Tooltip("How far past the wall it aims to land.")]
        [SerializeField] float overshoot = 2.2f;
        [SerializeField] float leapSeconds = 0.65f;
        [SerializeField] float leapHeight = 2.6f;
        [Tooltip("Seconds before it can leap again, so a wall line is not cleared in one go.")]
        [SerializeField] float cooldown = 2.5f;

        CharacterController controller;

        Vector3 from, to;
        float elapsed;
        float nextLeapTime;
        bool leaping;

        void Awake() => controller = GetComponent<CharacterController>();

        void OnEnable()
        {
            leaping = false;
            nextLeapTime = 0f;
        }

        public bool MoveSelf(float deltaTime)
        {
            if (leaping) return Advance(deltaTime);
            if (Time.time < nextLeapTime) return false;

            var wall = WallInFront();
            if (wall == null) return false;

            // Aim past the wall along the direction we are already travelling, so a leap
            // cannot carry it sideways into the barricade it was trying to clear.
            var across = wall.transform.position - transform.position;
            across.y = 0f;
            if (across.sqrMagnitude < 0.0001f) return false;

            from = transform.position;
            to = wall.transform.position + across.normalized * overshoot;
            to.y = from.y;

            elapsed = 0f;
            leaping = true;
            return Advance(deltaTime);
        }

        bool Advance(float deltaTime)
        {
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / leapSeconds);

            var flat = Vector3.Lerp(from, to, t);
            // Sine rather than a parabola: it leaves and lands flat, so the arc reads as a
            // vault rather than as being fired out of something.
            flat.y = from.y + Mathf.Sin(t * Mathf.PI) * leapHeight;

            controller.Move(flat - transform.position);

            if (t < 1f) return true;

            leaping = false;
            nextLeapTime = Time.time + cooldown;
            return true;
        }

        Barricade WallInFront()
        {
            var barricades = Barricade.ActiveBarricades;
            if (barricades == null) return null;

            float sqrRange = triggerRange * triggerRange;

            for (int i = 0; i < barricades.Count; i++)
            {
                var barricade = barricades[i];
                if (barricade == null || !barricade.IsAlive) continue;

                var offset = barricade.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrRange) continue;

                // Only what is actually ahead of it - leaping over a wall already behind it
                // would look like a twitch and achieve nothing.
                if (Vector3.Dot(transform.forward, offset.normalized) > 0.4f) return barricade;
            }

            return null;
        }
    }
}
