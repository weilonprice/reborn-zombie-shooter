using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// What kind of weapon this is, for mod compatibility. Mods match against these rather
    /// than against loadout position, so reordering slots or adding a fifth weapon cannot
    /// silently reassign every mod to the wrong gun.
    /// </summary>
    [System.Flags]
    public enum WeaponTags
    {
        None      = 0,
        Sidearm   = 1 << 0,
        Shotgun   = 1 << 1,
        Rifle     = 1 << 2,
        Precision = 1 << 3,
    }

    public enum FireMode
    {
        /// <summary>Fires continuously while the trigger is held.</summary>
        Automatic,
        /// <summary>One shot per trigger pull, however long it is held.</summary>
        SemiAuto,
    }

    /// <summary>
    /// Everything that makes one weapon feel different from another, as data.
    /// A new weapon should be a new asset, never a new script.
    /// <para>
    /// Scene-level wiring - muzzle, tracer, ejection port - deliberately stays on the
    /// <see cref="Weapon"/> component, because those are properties of the object holding
    /// the gun rather than of the gun's design.
    /// </para>
    /// <para>
    /// Fields are private with read-only accessors on purpose: a ScriptableObject is a
    /// shared asset, and writing to one at runtime in the editor silently persists the
    /// change into the project.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Weapon Definition", fileName = "WPN_NewWeapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string displayName = "Weapon";

        [Header("Ballistics")]
        [SerializeField] float damage = 25f;
        [Tooltip("Rounds per minute.")]
        [SerializeField] float fireRate = 480f;
        [SerializeField] float range = 60f;
        [Tooltip("Cone half-angle in degrees applied to every pellet.")]
        [SerializeField] float spread = 1.5f;
        [Tooltip("Pellets per trigger pull. Above 1 makes it a shotgun.")]
        [SerializeField] int pelletsPerShot = 1;
        [SerializeField] FireMode fireMode = FireMode.Automatic;

        [Header("Mod compatibility")]
        [Tooltip("What this weapon counts as. Mods apply by tag, never by loadout slot.")]
        [SerializeField] WeaponTags tags = WeaponTags.None;
        [Tooltip("Fire rate multiplier when Overclocked Receiver is installed. 1 means the " +
                 "mod does nothing to this weapon.")]
        [SerializeField] float overclockedFireRateMultiplier = 1f;
        [Tooltip("Whether Overclocked Receiver converts this weapon to full auto.")]
        [SerializeField] bool overclockedConvertsToAuto;

        [Header("Penetration")]
        [Tooltip("Extra bodies a shot passes through beyond the first. 0 stops at the first.")]
        [SerializeField] int pierceCount;
        [Tooltip("Fraction of damage carried into each subsequent body. Per weapon on " +
                 "purpose: a sniper should genuinely carve a line, while piercing is only " +
                 "a bonus on a rifle.")]
        [SerializeField, Range(0f, 1f)] float penetrationFalloff = 0.65f;

        [Header("Ammo")]
        [SerializeField] int magazineSize = 30;
        [Tooltip("Maximum reserve ammo carried. -1 for infinite (e.g. Pistol).")]
        [SerializeField] int maxReserveAmmo = -1;
        [SerializeField] float reloadTime = 1.4f;

        [Header("Feel")]
        [Tooltip("Camera trauma per shot. Keep small on fast weapons or the shake saturates.")]
        [SerializeField] float fireTrauma = 0.085f;
        [Tooltip("Camera kick per shot, in world units, opposite the barrel.")]
        [SerializeField] float recoilKick = 0.06f;
        [Tooltip("Scales how hard targets are shoved. The target's own resistance still " +
                 "applies, so this is a multiplier rather than an absolute force.")]
        [SerializeField] float knockbackMultiplier = 1f;

        [Header("Audio")]
        [SerializeField] AudioClip fireClip;
        [SerializeField] AudioClip impactClip;
        [SerializeField, Range(0f, 1f)] float fireVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float impactVolume = 0.4f;

        [Header("Presentation")]
        [SerializeField] float tracerWidth = 0.06f;
        [SerializeField] float tracerDuration = 0.03f;
        [Tooltip("Casings ejected per trigger pull, regardless of pellet count.")]
        [SerializeField] int shellsPerShot = 1;

        public string DisplayName => displayName;

        public float Damage => damage;
        public float FireRate => fireRate;
        public float Range => range;
        public float Spread => spread;
        public int PelletsPerShot => Mathf.Max(1, pelletsPerShot);
        public FireMode Mode => fireMode;

        public WeaponTags Tags => tags;
        public bool HasAnyTag(WeaponTags any) => (tags & any) != 0;
        public float OverclockedFireRateMultiplier => overclockedFireRateMultiplier;
        public bool OverclockedConvertsToAuto => overclockedConvertsToAuto;

        public int PierceCount => Mathf.Max(0, pierceCount);
        public float PenetrationFalloff => penetrationFalloff;

        public int MagazineSize => Mathf.Max(1, magazineSize);
        public int MaxReserveAmmo => maxReserveAmmo;
        public float ReloadTime => reloadTime;

        public float FireTrauma => fireTrauma;
        public float RecoilKick => recoilKick;
        public float KnockbackMultiplier => knockbackMultiplier;

        public AudioClip FireClip => fireClip;
        public AudioClip ImpactClip => impactClip;
        public float FireVolume => fireVolume;
        public float ImpactVolume => impactVolume;

        public float TracerWidth => tracerWidth;
        public float TracerDuration => tracerDuration;
        public int ShellsPerShot => Mathf.Max(0, shellsPerShot);

        /// <summary>Cooldown between shots, derived from rounds per minute.</summary>
        public float SecondsBetweenShots => 60f / Mathf.Max(1f, fireRate);
    }
}
