using UnityEngine;

namespace ZombieShooter
{
    [CreateAssetMenu(menuName = "Zombie Shooter/Casing", fileName = "CASE_New")]
    public sealed class CasingDefinition : ScriptableObject
    {
        public Mesh mesh;
        public Material[] materials;
        public AudioClip[] floorImpacts;
        [Min(.1f)] public float displayScale = 3.2f;
        [Min(.001f)] public float diameter = .012f;
        [Range(0f, 1f)] public float bounce = .35f;
        [Range(0f, 1f)] public float impactVolume = .24f;
        public float CollisionRadius => diameter * displayScale * .5f;
    }
}
