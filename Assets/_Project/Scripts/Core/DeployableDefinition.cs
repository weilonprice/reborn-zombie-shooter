using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// One placeable structure, as data. A new deployable should be an asset and a prefab,
    /// never a new placer script - the same contract weapons already have.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Deployable", fileName = "DEP_NewDeployable")]
    public class DeployableDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string displayName = "Deployable";
        [SerializeField] GameObject prefab;
        [SerializeField] int cost = 40;

        [Header("Placement")]
        [Tooltip("World units the ghost snaps to. 1 keeps structures on a shared grid so " +
                 "walls line up with each other.")]
        [SerializeField] float gridSnap = 1f;
        [SerializeField] bool rotatable = true;
        [Tooltip("Clearance required from other deployables. Roughly the footprint radius.")]
        [SerializeField] float footprint = 0.9f;
        [Tooltip("How close the player must stand to place one.")]
        [SerializeField] float maxPlacementRange = 4.2f;
        [Tooltip("Size of the placement ghost. A plain box rather than the real prefab, so " +
                 "the preview cannot run the deployable's own logic or colliders.")]
        [SerializeField] Vector3 ghostSize = new(1.6f, 0.95f, 0.6f);
        [Tooltip("Height the object is placed at, measured from the ground.")]
        [SerializeField] float placementHeight = 0.5f;

        [Header("Audio")]
        [SerializeField] AudioClip placeClip;
        [SerializeField, Range(0f, 1f)] float placeVolume = 0.55f;

        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int Cost => cost;
        public float GridSnap => Mathf.Max(0.1f, gridSnap);
        public bool Rotatable => rotatable;
        public float Footprint => footprint;
        public float MaxPlacementRange => maxPlacementRange;
        public Vector3 GhostSize => ghostSize;
        public float PlacementHeight => placementHeight;
        public AudioClip PlaceClip => placeClip;
        public float PlaceVolume => placeVolume;
    }
}
