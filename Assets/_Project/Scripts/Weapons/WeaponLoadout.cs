using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The weapons the player carries, selected with 1-4 or the gamepad d-pad.
    /// Manages slot unlocking (progression) and finite reserve ammo pools.
    /// </summary>
    public class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [SerializeField] WeaponDefinition[] slots = new WeaponDefinition[4];
        [SerializeField] bool[] unlocked = new bool[4] { true, false, false, false };
        [Tooltip("Minimum seconds between swaps, so mashing the number row cannot cancel every reload for free.")]
        [SerializeField] float swapCooldown = 0.25f;

        int[] ammoInSlot;
        float[] holsterTimers;
        int[] reserveAmmo;
        int current = -1;
        float nextSwapTime;

        public int CurrentSlot => current;
        public WeaponDefinition CurrentDefinition =>
            current >= 0 && current < slots.Length ? slots[current] : null;

        public int CurrentReserveAmmo =>
            current >= 0 && current < reserveAmmo.Length ? reserveAmmo[current] : -1;

        public event Action<WeaponDefinition> WeaponChanged;
        public event Action<int, int> ReserveAmmoChanged;
        public event Action<int> SlotUnlocked;

        void Awake()
        {
            if (weapon == null) weapon = GetComponent<Weapon>();

            ammoInSlot = new int[slots.Length];
            holsterTimers = new float[slots.Length];
            reserveAmmo = new int[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                ammoInSlot[i] = ArmoryManager.EffectiveMagazineSize(slots[i]);
                reserveAmmo[i] = slots[i].MaxReserveAmmo;
            }
        }

        void Start()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !IsSlotUnlocked(i)) continue;
                Select(i, force: true);
                return;
            }

            Debug.LogError($"{nameof(WeaponLoadout)}: no unlocked weapons in any slot.", this);
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (Time.timeScale <= 0f) return;

            TickHolsteredReload();

            int requested = InputReader.WeaponSlotPressed;
            if (requested >= 0 && IsSlotUnlocked(requested))
                Select(requested);
        }

        public bool IsSlotUnlocked(int slot) =>
            slot >= 0 && slot < unlocked.Length && unlocked[slot];

        public void UnlockSlot(int slot)
        {
            if (slot < 0 || slot >= unlocked.Length) return;
            unlocked[slot] = true;

            if (slots[slot] != null && reserveAmmo[slot] <= 0)
                reserveAmmo[slot] = slots[slot].MaxReserveAmmo;

            SlotUnlocked?.Invoke(slot);
            ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);
        }

        public int GetReserveAmmo(int slot) =>
            slot >= 0 && slot < reserveAmmo.Length ? reserveAmmo[slot] : -1;

        public int ConsumeReserve(int slot, int needed)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot] == null) return needed;
            if (slots[slot].MaxReserveAmmo < 0) return needed; // Infinite reserve

            int available = reserveAmmo[slot];
            int taken = Mathf.Min(needed, available);
            reserveAmmo[slot] -= taken;

            ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);
            return taken;
        }

        public void RefillAllReserves()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].MaxReserveAmmo > 0)
                {
                    reserveAmmo[i] = slots[i].MaxReserveAmmo;
                    ammoInSlot[i] = ArmoryManager.EffectiveMagazineSize(slots[i]);
                    ReserveAmmoChanged?.Invoke(i, reserveAmmo[i]);
                }
            }

            if (current >= 0 && weapon != null)
                weapon.SetAmmo(ammoInSlot[current]);
        }

        /// <summary>Gives reserve rounds back, capped at the weapon's maximum.</summary>
        public void AddReserve(int slot, int amount)
        {
            if (reserveAmmo == null || slot < 0 || slot >= reserveAmmo.Length || amount <= 0) return;
            if (slots[slot] == null || slots[slot].MaxReserveAmmo < 0) return;

            reserveAmmo[slot] = Mathf.Min(reserveAmmo[slot] + amount, slots[slot].MaxReserveAmmo);
            ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);
        }

        // Holstered weapons with the upgrade quietly refill from their own reserve, so the
        // sidearm is full when you swap to it in an emergency.
        void TickHolsteredReload()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i == current || slots[i] == null || !IsSlotUnlocked(i)) continue;

                var stats = UpgradeManager.Resolve(slots[i]);
                if (!stats.HolsteredReload) continue;

                int capacity = ArmoryManager.EffectiveMagazineSize(slots[i]);
                if (ammoInSlot[i] >= capacity || reserveAmmo[i] <= 0) continue;

                holsterTimers[i] += Time.deltaTime;
                float perRound = stats.ReloadTime > 0f ? stats.ReloadTime : 0.5f;
                if (holsterTimers[i] < perRound) continue;

                holsterTimers[i] = 0f;
                ammoInSlot[i]++;
                reserveAmmo[i]--;
                ReserveAmmoChanged?.Invoke(i, reserveAmmo[i]);
            }
        }

        public void Select(int slot, bool force = false)
        {
            if (weapon == null || slot < 0 || slot >= slots.Length) return;
            if (slots[slot] == null || !IsSlotUnlocked(slot)) return;
            if (!force && (slot == current || Time.time < nextSwapTime)) return;

            // Bank what the outgoing weapon had left before its magazine is replaced.
            if (current >= 0) ammoInSlot[current] = weapon.Ammo;

            current = slot;
            nextSwapTime = Time.time + swapCooldown;

            weapon.Equip(slots[slot], slot);
            weapon.SetAmmo(ammoInSlot[slot]);

            WeaponChanged?.Invoke(slots[slot]);
            ReserveAmmoChanged?.Invoke(current, CurrentReserveAmmo);
        }
    }
}
