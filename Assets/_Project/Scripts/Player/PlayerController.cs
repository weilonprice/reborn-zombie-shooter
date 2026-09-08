using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Twin-stick locomotion: movement and facing are fully independent. Mouse aim is used
    /// unless the right stick is deflected, so pad and mouse both work without a mode switch.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 7f;
        [SerializeField] float acceleration = 60f;
        [SerializeField] float gravity = -20f;

        [Header("Aiming")]
        [SerializeField] float turnSpeed = 900f;
        [Tooltip("Height of the plane the mouse ray is projected onto, relative to the player pivot.")]
        [SerializeField] float aimPlaneOffset = 0f;

        CharacterController controller;
        Health health;
        Camera cam;

        Vector3 velocity;      // horizontal, smoothed
        float verticalVelocity;

        /// <summary>World-space point the player is currently aiming at, on the aim plane.</summary>
        public Vector3 AimPoint { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            cam = Camera.main;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;

            bool alive = health == null || health.IsAlive;
            bool playing = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

            if (alive && playing)
            {
                HandleAim();
                HandleMove();
            }
            else
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * Time.deltaTime);
            }

            ApplyMotion();
        }

        void HandleMove()
        {
            var input = InputReader.Move;
            // Camera looks down the world Z axis, so screen-up maps straight to world +Z.
            var desired = new Vector3(input.x, 0f, input.y) * moveSpeed;
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * Time.deltaTime);
        }

        void HandleAim()
        {
            Vector3 target;

            var stick = InputReader.AimStick;
            if (stick != Vector2.zero)
            {
                target = transform.position + new Vector3(stick.x, 0f, stick.y).normalized * 10f;
            }
            else if (!TryGetMouseAimPoint(out target))
            {
                return;
            }

            AimPoint = target;

            var flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            var look = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        bool TryGetMouseAimPoint(out Vector3 point)
        {
            point = default;
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return false;
            }

            // Intersect the cursor ray with a horizontal plane at weapon height.
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + aimPlaneOffset, 0f));
            var ray = cam.ScreenPointToRay(InputReader.MouseScreenPosition);

            if (!plane.Raycast(ray, out float distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }

        void ApplyMotion()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;   // keep the controller pinned to the ground
            else
                verticalVelocity += gravity * Time.deltaTime;

            var motion = velocity;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
