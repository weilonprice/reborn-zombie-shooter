using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Walks at the nearest barricade rather than the player, and chews it down.
    /// <para>
    /// The archetype that makes a wall something you have to defend rather than something you
    /// hide behind. Everything else in the horde treats a barricade as an obstacle in the
    /// way; this one treats it as the objective.
    /// </para>
    /// <para>
    /// Steering only. Once it reaches the wall, ZombieAI's ordinary blocking check is what
    /// actually damages it, so there is no second attack path to keep in step with the first.
    /// </para>
    /// </summary>
    public class BarricadeSapper : MonoBehaviour, IEnemySteerOverride
    {
        [Tooltip("How far it will look for a wall before giving up and chasing the player.")]
        [SerializeField] float searchRadius = 26f;
        [Tooltip("Seconds between searches. Barricades do not move, so this is cheap to stale.")]
        [SerializeField] float searchInterval = 0.6f;

        Barricade current;
        float nextSearch;

        void OnEnable()
        {
            current = null;
            nextSearch = 0f;
        }

        public Transform SteerTarget()
        {
            if (current != null && current.IsAlive) return current.transform;

            if (Time.time < nextSearch) return null;
            nextSearch = Time.time + searchInterval;

            current = NearestBarricade();
            return current != null ? current.transform : null;
        }

        Barricade NearestBarricade()
        {
            var barricades = Barricade.ActiveBarricades;
            if (barricades == null) return null;

            Barricade best = null;
            float bestSqr = searchRadius * searchRadius;

            for (int i = 0; i < barricades.Count; i++)
            {
                var barricade = barricades[i];
                if (barricade == null || !barricade.IsAlive) continue;

                float sqr = (barricade.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = barricade;
            }

            return best;
        }
    }
}
