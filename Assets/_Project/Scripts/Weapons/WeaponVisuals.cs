using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Shows the model for whatever the player is carrying, and moves the muzzle and eject
    /// port to that model's own sockets.
    /// <para>
    /// Every model is instantiated once at build time and simply toggled, rather than
    /// spawned on each swap: weapon switching happens mid-fight and an Instantiate there
    /// would be a hitch for no benefit. Their lightweight Animator components stay dormant
    /// until the model is shown.
    /// </para>
    /// <para>
    /// Muzzle placement is split deliberately. This component writes the muzzle's local Y and
    /// Z from the socket, and <see cref="Weapon"/> writes its X for the akimbo offset. Two
    /// owners on one transform only works because they touch different axes - if either
    /// starts writing a full position, the other silently loses.
    /// </para>
    /// </summary>
    public class WeaponVisuals : MonoBehaviour
    {
        [SerializeField] WeaponLoadout loadout;

        [Header("Parallel arrays, one entry per weapon")]
        [SerializeField] WeaponDefinition[] definitions;
        [SerializeField] GameObject[] mainModels;
        [SerializeField] GameObject[] offHandModels;
        [SerializeField] Transform[] muzzleSockets;
        [SerializeField] Transform[] ejectSockets;

        [Header("Scene transforms to place")]
        [SerializeField] Transform muzzle;
        [SerializeField] Transform offHandMuzzle;
        [SerializeField] Transform ejectPort;

        [Tooltip("Animator on each model, parallel to the arrays above.")]
        [SerializeField] Animator[] animators;

        int shown = -1;

        /// <summary>The carried weapon's animator, or null while nothing is shown.</summary>
        public Animator Current => At(animators, shown);

        /// <summary>Raised after the shown weapon changes, so a driver can rebind.</summary>
        public event System.Action<Animator> Changed;
        Animator shownAnimator;
        Animator shownOffHandAnimator;

        /// <summary>Animator on the currently visible weapon model, if its FBX has one.</summary>
        public Animator CurrentAnimator => shownAnimator;
        /// <summary>Animator on the matching off-hand copy when dual wielding is active.</summary>
        public Animator CurrentOffHandAnimator => shownOffHandAnimator;

        void Start()
        {
            if (loadout == null) loadout = GetComponent<WeaponLoadout>();

            if (loadout != null)
            {
                loadout.WeaponChanged += Show;
                Show(loadout.CurrentDefinition);
            }
        }

        void OnDestroy()
        {
            if (loadout != null) loadout.WeaponChanged -= Show;
        }

        void LateUpdate()
        {
            // Recoil and reload clips animate the model root. Keep the gameplay sockets in
            // lockstep so muzzle flashes, tracers, and shell ejection follow the moving mesh.
            if (shown < 0) return;
            PlaceOnSocket(muzzle, At(muzzleSockets, shown));
            PlaceOnSocket(offHandMuzzle, At(muzzleSockets, shown));
            PlaceOnSocket(ejectPort, At(ejectSockets, shown));
        }

        void Show(WeaponDefinition definition)
        {
            int index = IndexOf(definition);
            if (index == shown) return;

            for (int i = 0; i < mainModels.Length; i++)
            {
                bool active = i == index;

                if (mainModels[i] != null) mainModels[i].SetActive(active);

                // The off-hand copy is shown too. Weapon hides the whole off-hand holder
                // unless the weapon is dual wielding, so this only decides WHICH model is
                // inside it, never whether it is visible.
                if (i < offHandModels.Length && offHandModels[i] != null)
                    offHandModels[i].SetActive(active);
            }

            shown = index;
            Changed?.Invoke(Current);
            shownAnimator = index >= 0 && index < mainModels.Length && mainModels[index] != null
                ? mainModels[index].GetComponentInChildren<Animator>(true)
                : null;
            shownOffHandAnimator = index >= 0 && index < offHandModels.Length && offHandModels[index] != null
                ? offHandModels[index].GetComponentInChildren<Animator>(true)
                : null;
            if (index < 0) return;

            PlaceOnSocket(muzzle, At(muzzleSockets, index));
            PlaceOnSocket(offHandMuzzle, At(muzzleSockets, index));
            PlaceOnSocket(ejectPort, At(ejectSockets, index));
        }

        /// <summary>
        /// Moves a transform to a socket without touching its X, which belongs to the akimbo
        /// offset. Works in the player's local space so it survives the player rotating.
        /// </summary>
        void PlaceOnSocket(Transform target, Transform socket)
        {
            if (target == null || socket == null) return;

            var local = transform.InverseTransformPoint(socket.position);
            var current = target.localPosition;

            target.localPosition = new Vector3(current.x, local.y, local.z);
        }

        int IndexOf(WeaponDefinition definition)
        {
            if (definition == null || definitions == null) return -1;

            for (int i = 0; i < definitions.Length; i++)
                if (definitions[i] == definition) return i;

            return -1;
        }

        static T At<T>(T[] array, int index) where T : Object =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;
    }
}
