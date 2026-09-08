using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The weapons the player carries, selected with 1-4 or the gamepad d-pad.
    /// <para>
    /// Ammo is tracked per slot and restored on equip. Without that, swapping would refill
    /// the magazine and therefore <em>become</em> reloading - you would never reload a pistol,
    /// just cycle 1-2-1, and its twelve-round magazine would stop meaning anything.
    /// </para>
    /// </summary>
    public class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] WeaponDefinition[] slots = new WeaponDefinition[4];
        [Tooltip("Minimum seconds between swaps, so mashing the number row cannot cancel " +
                 "every reload for free.")]
        [SerializeField] float swapCooldown = 0.25f;

        int[] ammoInSlot;
        int current = -1;
        float nextSwapTime;

        public WeaponDefinition CurrentDefinition =>
            current >= 0 && current < slots.Length ? slots[current] : null;

        public event Action<WeaponDefinition> WeaponChanged;

        void Awake()
        {
            if (weapon == null) weapon = GetComponent<Weapon>();

            ammoInSlot = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                ammoInSlot[i] = slots[i] != null ? slots[i].MagazineSize : 0;
        }

        void Start()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                Select(i, force: true);
                return;
            }

            Debug.LogError($"{nameof(WeaponLoadout)}: no weapons in any slot.", this);
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            int requested = InputReader.WeaponSlotPressed;
            if (requested >= 0) Select(requested);
        }

        public void Select(int slot, bool force = false)
        {
            if (weapon == null || slot < 0 || slot >= slots.Length) return;
            if (slots[slot] == null) return;
            if (!force && (slot == current || Time.time < nextSwapTime)) return;

            // Bank what the outgoing weapon had left before its magazine is replaced.
            if (current >= 0) ammoInSlot[current] = weapon.Ammo;

            current = slot;
            nextSwapTime = Time.time + swapCooldown;

            weapon.Equip(slots[slot]);
            weapon.SetAmmo(ammoInSlot[slot]);

            WeaponChanged?.Invoke(slots[slot]);
        }
    }
}
