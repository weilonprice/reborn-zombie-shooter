using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Armory costs that do not belong to a definition.
    /// <para>
    /// Almost nothing is left here: weapons carry their own price, deployables carry theirs,
    /// and upgrade tiers are priced from one shared curve. What remains is the ammo crate,
    /// which is not an object anyone owns. Kept as an asset rather than a constant so the
    /// economy stays tunable without a recompile.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Armory Prices", fileName = "ArmoryPrices")]
    public class ArmoryPrices : ScriptableObject
    {
        [SerializeField] int ammoCrate = 50;

        public int AmmoCrate => ammoCrate;
    }
}
