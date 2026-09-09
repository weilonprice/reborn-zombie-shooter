using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// The weapons the player carries, selected with 1-4 or the gamepad d-pad.
    /// <para>
    /// The catalogue is every weapon in the game; <c>carried</c> is the handful you can hold
    /// at once. Buying moves a weapon from the first into a free slot of the second, and
    /// once the slots are full there are no more purchases - a run commits to what it picked.
    /// That is the design: with one upgrade path per weapon and only enough gold to max one,
    /// carrying everything would leave the nine guns you did not invest in as a safety net,
    /// which is exactly the commitment the run is supposed to be about.
    /// </para>
    /// <para>
    /// Slots used to be a fixed array of four known weapons with a parallel unlocked[] flag.
    /// That could not express ten. A slot is now live simply by holding something.
    /// </para>
    /// </summary>
    public class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] Weapon weapon;
        [Tooltip("Every weapon the Armory can sell, in shop order.")]
        [SerializeField] WeaponDefinition[] catalogue = new WeaponDefinition[0];
        [Tooltip("What the player is holding. Index 0 is the starting weapon; the rest fill " +
                 "as they are bought. Length is the carry limit.")]
        [SerializeField] WeaponDefinition[] carried = new WeaponDefinition[4];
        [Tooltip("Minimum seconds between swaps, so mashing the number row cannot cancel every reload for free.")]
        [SerializeField] float swapCooldown = 0.25f;

        int[] ammoInSlot;
        float[] holsterTimers;
        int[] reserveAmmo;
        int current = -1;
        float nextSwapTime;

        public int CurrentSlot => current;
        public WeaponDefinition CurrentDefinition =>
            current >= 0 && current < carried.Length ? carried[current] : null;

        public IReadOnlyList<WeaponDefinition> Catalogue => catalogue;
        public int CarryCapacity => carried != null ? carried.Length : 0;

        public WeaponDefinition CarriedAt(int slot) =>
            carried != null && slot >= 0 && slot < carried.Length ? carried[slot] : null;

        public bool HasFreeSlot
        {
            get
            {
                for (int i = 0; i < carried.Length; i++)
                    if (carried[i] == null) return true;
                return false;
            }
        }

        public bool Owns(WeaponDefinition definition)
        {
            if (definition == null) return false;

            for (int i = 0; i < carried.Length; i++)
                if (carried[i] == definition) return true;
            return false;
        }

        public int CurrentReserveAmmo =>
            current >= 0 && current < reserveAmmo.Length ? reserveAmmo[current] : -1;

        public event Action<WeaponDefinition> WeaponChanged;
        public event Action<int, int> ReserveAmmoChanged;
        /// <summary>A weapon was added to a carried slot.</summary>
        public event Action<int> LoadoutChanged;

        void Awake()
        {
            if (weapon == null) weapon = GetComponent<Weapon>();

            ammoInSlot = new int[carried.Length];
            holsterTimers = new float[carried.Length];
            reserveAmmo = new int[carried.Length];

            for (int i = 0; i < carried.Length; i++)
            {
                if (carried[i] == null) continue;
                ammoInSlot[i] = ArmoryManager.EffectiveMagazineSize(carried[i]);
                reserveAmmo[i] = carried[i].MaxReserveAmmo;
            }
        }

        void Start()
        {
            for (int i = 0; i < carried.Length; i++)
            {
                if (carried[i] == null) continue;
                Select(i, force: true);
                return;
            }

            Debug.LogError($"{nameof(WeaponLoadout)}: carrying nothing - slot 0 needs a " +
                           "starting weapon.", this);
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (Time.timeScale <= 0f) return;

            TickHolsteredReload();

            int requested = InputReader.WeaponSlotPressed;
            if (requested >= 0) Select(requested);
        }

        /// <summary>
        /// Puts a weapon in the first free carried slot and equips it. Fails when the
        /// loadout is full or the weapon is already held - the caller checks before charging.
        /// </summary>
        public bool TryCarry(WeaponDefinition definition)
        {
            if (definition == null || Owns(definition)) return false;

            for (int slot = 0; slot < carried.Length; slot++)
            {
                if (carried[slot] != null) continue;

                carried[slot] = definition;
                ammoInSlot[slot] = ArmoryManager.EffectiveMagazineSize(definition);
                reserveAmmo[slot] = definition.MaxReserveAmmo;
                holsterTimers[slot] = 0f;

                LoadoutChanged?.Invoke(slot);
                ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);

                Select(slot, force: true);
                return true;
            }

            return false;
        }

        public int GetReserveAmmo(int slot) =>
            slot >= 0 && slot < reserveAmmo.Length ? reserveAmmo[slot] : -1;

        public int ConsumeReserve(int slot, int needed)
        {
            if (slot < 0 || slot >= carried.Length || carried[slot] == null) return needed;
            if (carried[slot].MaxReserveAmmo < 0) return needed; // Infinite reserve

            int available = reserveAmmo[slot];
            int taken = Mathf.Min(needed, available);
            reserveAmmo[slot] -= taken;

            ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);
            return taken;
        }

        public void RefillAllReserves()
        {
            for (int i = 0; i < carried.Length; i++)
            {
                if (carried[i] != null && carried[i].MaxReserveAmmo > 0)
                {
                    reserveAmmo[i] = carried[i].MaxReserveAmmo;
                    ammoInSlot[i] = ArmoryManager.EffectiveMagazineSize(carried[i]);
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
            if (carried[slot] == null || carried[slot].MaxReserveAmmo < 0) return;

            reserveAmmo[slot] = Mathf.Min(reserveAmmo[slot] + amount, carried[slot].MaxReserveAmmo);
            ReserveAmmoChanged?.Invoke(slot, reserveAmmo[slot]);
        }

        // Holstered weapons with the upgrade quietly refill from their own reserve, so the
        // sidearm is full when you swap to it in an emergency.
        void TickHolsteredReload()
        {
            if (carried == null) return;

            for (int i = 0; i < carried.Length; i++)
            {
                if (i == current || carried[i] == null) continue;

                var stats = UpgradeManager.Resolve(carried[i]);
                if (!stats.HolsteredReload) continue;

                int capacity = ArmoryManager.EffectiveMagazineSize(carried[i]);
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
            if (weapon == null || slot < 0 || slot >= carried.Length) return;
            if (carried[slot] == null) return;
            if (!force && (slot == current || Time.time < nextSwapTime)) return;

            // Bank what the outgoing weapon had left before its magazine is replaced.
            if (current >= 0) ammoInSlot[current] = weapon.Ammo;

            current = slot;
            nextSwapTime = Time.time + swapCooldown;

            weapon.Equip(carried[slot], slot);
            weapon.SetAmmo(ammoInSlot[slot]);

            WeaponChanged?.Invoke(carried[slot]);
            ReserveAmmoChanged?.Invoke(current, CurrentReserveAmmo);
        }
    }
}
