using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Final pose pass for the generic survivor rig. Ready poses reach the weapon; authored
    /// actions carry it with the palm. Gameplay aim remains on the player's forward axis.
    /// </summary>
    public sealed class PlayerWeaponGrip
    {
        public const float ModelScale = .65f;
        readonly Transform root;
        readonly Animator animator;
        readonly Weapon weapon;
        readonly Health health;
        readonly Arm right, left;
        readonly Held[] main, off;

        public PlayerWeaponGrip(Transform root, GameObject[] mainModels, GameObject[] offModels)
        {
            this.root = root;
            var model = root.Find("MainCharacter_Model");
            if (model == null) return;
            animator = model.GetComponentInChildren<Animator>();
            weapon = root.GetComponent<Weapon>();
            health = root.GetComponent<Health>();
            right = new Arm(model, "R");
            left = new Arm(model, "L");
            main = Bind(mainModels);
            off = Bind(offModels);
        }

        static Held[] Bind(GameObject[] models)
        {
            if (models == null) return new Held[0];
            var result = new Held[models.Length];
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) result[i] = new Held(models[i].transform);
            return result;
        }

        public void Apply(int index)
        {
            if (animator == null || right?.Valid != true || left?.Valid != true ||
                index < 0 || index >= main.Length || main[index]?.socket == null) return;
            var primary = main[index];
            var secondary = index < off.Length ? off[index] : null;
            bool dual = secondary?.socket != null && secondary.model.gameObject.activeInHierarchy;
            bool action = (health != null && !health.IsAlive) ||
                          (weapon != null && (weapon.IsReloading || weapon.UltimateActive));
            var body = animator.GetCurrentAnimatorStateInfo(0);
            action |= body.IsName("Ultimate") || body.IsName("Death");
            if (animator.layerCount > 1)
            {
                var state = animator.IsInTransition(1) ? animator.GetNextAnimatorStateInfo(1)
                                                      : animator.GetCurrentAnimatorStateInfo(1);
                action |= state.IsName("Reload") || state.IsName("GetShot") || state.IsName("Stagger");
            }

            if (action)
            {
                primary.Follow(right.Palm);
                if (dual) secondary.Follow(left.Palm);
                return;
            }

            // The grip sits just ahead of the torso, within both arms' reach.
            primary.Place(root.TransformPoint(new Vector3(dual ? .30f : .15f, .04f, .20f)));
            var handRotation = Quaternion.LookRotation(root.forward, -root.up);
            right.Reach(primary.socket.position, handRotation, root.right - root.up);
            if (dual)
            {
                secondary.Place(root.TransformPoint(new Vector3(-.30f, .04f, .20f)));
                left.Reach(secondary.socket.position, handRotation, -root.right - root.up);
            }
            else
            {
                bool pistol = primary.model.name == "Pistol";
                var support = primary.socket.position + root.TransformVector(pistol
                    ? new Vector3(-.07f, .015f, .015f)
                    : new Vector3(-.06f, .16f, .30f));
                left.Reach(support, handRotation, -root.right - root.up);
            }
        }

        public static Transform Find(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        sealed class Held
        {
            public readonly Transform model, socket;
            readonly Transform holder;
            public Held(Transform model)
            {
                this.model = model;
                holder = model.parent;
                // Weapon exports face -Z; the survivor and firing ray face +Z.
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                model.localScale = Vector3.one * ModelScale;
                socket = Find(model, "GripSocket");
            }
            // Anchor the imported socket after animation (FBX root axes can be animated).
            // Recoil rotation and moving weapon parts remain authored by the weapon clip.
            public void Place(Vector3 palm) => Follow(palm);
            public void Follow(Vector3 palm)
            {
                // FBX takes contain scale curves too, so apply the character fit after them.
                model.localScale = Vector3.one * ModelScale;
                holder.position += palm - socket.position;
            }
        }

        sealed class Arm
        {
            readonly Transform upper, forearm, hand;
            // The mesh's glove centre is 12 cm along the hand bone from its wrist.
            static readonly Vector3 PalmOffset = new(0f, .12f, 0f);
            public bool Valid => upper != null && forearm != null && hand != null;
            public Vector3 Palm => hand.TransformPoint(PalmOffset);
            public Arm(Transform model, string side)
            {
                upper = Find(model, "UpperArm." + side);
                forearm = Find(model, "Forearm." + side);
                hand = Find(model, "Hand." + side);
            }
            public void Reach(Vector3 palm, Quaternion rotation, Vector3 pole)
            {
                Vector3 wrist = palm - rotation * Vector3.Scale(PalmOffset, hand.lossyScale);
                Vector3 origin = upper.position;
                float a = Vector3.Distance(origin, forearm.position);
                float b = Vector3.Distance(forearm.position, hand.position);
                Vector3 delta = wrist - origin;
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
                Vector3 direction = delta.normalized;
                Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
                float along = (a * a - b * b + distance * distance) / (2f * distance);
                Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
                upper.rotation = Quaternion.FromToRotation(forearm.position - origin, elbow - origin) * upper.rotation;
                forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position,
                    origin + direction * distance - forearm.position) * forearm.rotation;
                hand.rotation = rotation;
            }
        }
    }
}
