using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Top-down chase camera. Leads slightly toward the aim point so the player can see
    /// more of the direction they're shooting into.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0f, 18f, -10f);
        [SerializeField] float smoothTime = 0.12f;

        [Header("Aim lead")]
        [Tooltip("How far the camera drifts toward the aim point, as a fraction of the aim distance.")]
        [SerializeField, Range(0f, 0.5f)] float aimLead = 0.18f;
        [SerializeField] float maxLead = 5f;

        PlayerController player;
        Vector3 velocity;

        void Start()
        {
            if (target == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) target = go.transform;
            }

            if (target != null)
            {
                player = target.GetComponent<PlayerController>();
                transform.position = target.position + offset;
            }

            transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
        }

        void LateUpdate()
        {
            if (target == null) return;

            var focus = target.position;

            if (player != null && player.AimPoint != Vector3.zero)
            {
                var lead = (player.AimPoint - target.position) * aimLead;
                lead.y = 0f;
                focus += Vector3.ClampMagnitude(lead, maxLead);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, focus + offset, ref velocity, smoothTime);
        }
    }
}
