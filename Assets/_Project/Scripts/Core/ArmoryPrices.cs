using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Every Armory cost, as an asset rather than compile-time constants.
    /// <para>
    /// The economy is the thing most likely to need repeated tuning now that a run has a
    /// fixed 15-wave length to balance against, and tuning it should not be a recompile.
    /// Lives as an asset rather than serialized fields on the manager so the values survive
    /// an ArenaBuilder re-run.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Armory Prices", fileName = "ArmoryPrices")]
    public class ArmoryPrices : ScriptableObject
    {
        [Header("Weapons")]
        [SerializeField] int shotgun = 150;
        [SerializeField] int assaultRifle = 250;
        [SerializeField] int sniper = 350;

        [Header("Consumables")]
        [SerializeField] int ammoCrate = 50;
        [SerializeField] int barricade = 40;

        [Header("Mod cores")]

        public int Shotgun => shotgun;
        public int AssaultRifle => assaultRifle;
        public int Sniper => sniper;
        public int AmmoCrate => ammoCrate;
        public int Barricade => barricade;
    }
}
