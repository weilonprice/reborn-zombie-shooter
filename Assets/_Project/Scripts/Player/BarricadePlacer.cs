using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Manages player inventory and tactical placement of wooden barricades.
    /// Snaps holographic ghost to a 1.0m grid, validates clearance, and instantiates barricades.
    /// </summary>
    public class BarricadePlacer : MonoBehaviour
    {
        [Header("Prefab & Materials")]
        [SerializeField] Barricade barricadePrefab;
        [SerializeField] Material validGhostMat;
        [SerializeField] Material invalidGhostMat;

        [Header("Placement Tuning")]
        [SerializeField] float maxPlacementRange = 4.2f;
        [SerializeField] int startingBarricades = 1;
        [SerializeField] AudioClip placeClip;
        [SerializeField, Range(0f, 1f)] float placeVolume = 0.55f;

        public int BarricadesInStock { get; private set; }
        public bool IsPlacing { get; private set; }
        public float CurrentAngle { get; private set; }

        public event Action<int> BarricadesChanged;

        GameObject ghostGo;
        Renderer ghostRenderer;
        Camera mainCam;
        Plane groundPlane;
        bool isValidLocation;
        Vector3 currentPlacementPos;

        static readonly LayerMask OverlapMask = ~0;

        void Awake()
        {
            BarricadesInStock = startingBarricades;
            groundPlane = new Plane(Vector3.up, new Vector3(0f, 0.5f, 0f));
            CreateGhost();
        }

        void Start()
        {
            mainCam = Camera.main;
            BarricadesChanged?.Invoke(BarricadesInStock);
        }

        void CreateGhost()
        {
            ghostGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghostGo.name = "Barricade_PlacementGhost";
            ghostGo.transform.localScale = new Vector3(1.6f, 0.95f, 0.6f);

            var col = ghostGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ghostRenderer = ghostGo.GetComponent<Renderer>();
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
            if (validGhostMat != null) ghostRenderer.material = validGhostMat;

            ghostGo.SetActive(false);
        }

        void OnDestroy()
        {
            if (ghostGo != null) Destroy(ghostGo);
        }

        public void AddBarricades(int count)
        {
            if (count <= 0) return;
            BarricadesInStock += count;
            BarricadesChanged?.Invoke(BarricadesInStock);
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                if (IsPlacing) CancelPlacement();
                return;
            }

            // In shop/pause menu with time frozen, don't handle world placement
            if (Time.timeScale <= 0f)
            {
                if (IsPlacing) CancelPlacement();
                return;
            }

            // Toggle placement with F / right-click
            if (InputReader.DeployPressed)
            {
                if (!IsPlacing)
                {
                    if (BarricadesInStock > 0)
                        BeginPlacement();
                }
                else
                {
                    // If already placing, another tap of F or right click places if valid, or cancels if invalid
                    if (isValidLocation) TryPlace();
                    else CancelPlacement();
                }
            }

            if (!IsPlacing) return;

            // Cancel with Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            // Left-click confirms placement
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (isValidLocation)
                {
                    TryPlace();
                    return;
                }
            }

            // Rotate 90 degrees with R or Q
            if (InputReader.RotateDeployablePressed)
            {
                CurrentAngle = Mathf.Approximately(CurrentAngle, 0f) ? 90f : 0f;
            }

            UpdateGhost();
        }

        void BeginPlacement()
        {
            IsPlacing = true;
            if (ghostGo != null) ghostGo.SetActive(true);
            UpdateGhost();
        }

        public void CancelPlacement()
        {
            IsPlacing = false;
            if (ghostGo != null) ghostGo.SetActive(false);
        }

        void UpdateGhost()
        {
            if (ghostGo == null) return;
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            var ray = mainCam.ScreenPointToRay(InputReader.MouseScreenPosition);
            if (!groundPlane.Raycast(ray, out float enter)) return;

            var rawPoint = ray.GetPoint(enter);
            var playerPos = transform.position;

            // Clamp reach distance from player
            var offset = rawPoint - playerPos;
            offset.y = 0f;
            if (offset.magnitude > maxPlacementRange)
                offset = offset.normalized * maxPlacementRange;

            var targetPoint = playerPos + offset;

            // Snap to 1.0m grid for building clean funnels and walls
            float snappedX = Mathf.Round(targetPoint.x);
            float snappedZ = Mathf.Round(targetPoint.z);
            currentPlacementPos = new Vector3(snappedX, 0.5f, snappedZ);

            var rot = Quaternion.Euler(0f, CurrentAngle, 0f);
            ghostGo.transform.SetPositionAndRotation(currentPlacementPos, rot);

            // Validation: Arena boundaries, player distance, and collision overlaps
            bool insideArena = Mathf.Abs(snappedX) <= 27f && Mathf.Abs(snappedZ) <= 27f;
            float distToPlayer = Vector2.Distance(new Vector2(snappedX, snappedZ), new Vector2(playerPos.x, playerPos.z));
            bool clearOfPlayer = distToPlayer >= 0.85f;

            // CheckBox half-extents slightly shrunk to allow touching barricades side-by-side
            Vector3 halfExtents = rot * new Vector3(0.72f, 0.45f, 0.25f);
            halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));

            var hits = Physics.OverlapBox(currentPlacementPos, halfExtents, rot, OverlapMask, QueryTriggerInteraction.Ignore);
            bool noOverlap = true;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.root == transform.root) continue; // ignore player capsule handled by clearOfPlayer
                if (hits[i].gameObject.name.Contains("Ground")) continue;
                noOverlap = false;
                break;
            }

            isValidLocation = insideArena && clearOfPlayer && noOverlap;

            if (ghostRenderer != null)
            {
                var mat = isValidLocation ? validGhostMat : invalidGhostMat;
                if (mat != null && ghostRenderer.sharedMaterial != mat)
                    ghostRenderer.sharedMaterial = mat;
            }
        }

        void TryPlace()
        {
            if (!isValidLocation || BarricadesInStock <= 0 || barricadePrefab == null) return;

            var rot = Quaternion.Euler(0f, CurrentAngle, 0f);
            var placed = Instantiate(barricadePrefab, currentPlacementPos, rot);
            placed.gameObject.SetActive(true);

            BarricadesInStock--;
            BarricadesChanged?.Invoke(BarricadesInStock);

            if (placeClip != null)
                SfxPlayer.Instance?.PlayAt(placeClip, currentPlacementPos, placeVolume);

            if (BarricadesInStock <= 0)
                CancelPlacement();
        }
    }
}
