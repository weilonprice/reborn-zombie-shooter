using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A patch of ground that hurts the player to stand in.
    /// <para>
    /// The answer to camping. Barricades, chokepoints and the whole fortification hook reward
    /// holding one spot, and nothing in the horde had a way to argue with that - this makes
    /// the ground itself a timer.
    /// </para>
    /// <para>
    /// Damages the player and their barricades only. A pool that also hurt zombies would turn
    /// the spitter into a liability for its own horde, which is the opposite of the pressure
    /// it exists to apply.
    /// </para>
    /// </summary>
    public class AcidPool : MonoBehaviour
    {
        [SerializeField] float radius = 2.6f;
        [SerializeField] float damagePerSecond = 14f;
        [SerializeField] float lifetime = 6f;
        [Tooltip("Seconds between damage ticks. Whole ticks, so the player can see what hit them.")]
        [SerializeField] float tickInterval = 0.5f;

        static readonly Collider[] Buffer = new Collider[16];

        float expiresAt;
        float nextTick;

        void OnEnable()
        {
            expiresAt = Time.time + lifetime;
            nextTick = Time.time + tickInterval;

            transform.localScale = new Vector3(radius * 2f, 0.06f, radius * 2f);
        }

        void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.time < nextTick) return;
            nextTick = Time.time + tickInterval;

            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, Buffer, ~0,
                                                      QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (Buffer[i] == null) continue;
                if (Buffer[i].GetComponentInParent<ZombieAI>() != null) continue;

                var target = Buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                target.TakeDamage(new DamageInfo(damagePerSecond * tickInterval,
                                                 Buffer[i].transform.position, Vector3.up,
                                                 0f, gameObject));
            }
        }
    }
}
