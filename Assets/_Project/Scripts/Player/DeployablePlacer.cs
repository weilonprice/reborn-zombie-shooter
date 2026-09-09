using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Places any deployable the player carries: barricades, barrels, claymores and whatever
    /// comes next.
    /// <para>
    /// Generalised from the barricade-only placer. Copying it per structure would have
    /// repeated the mistake weapon mods made - three near-identical scripts diverging as each
    /// gets fixed - so the differences live in <see cref="DeployableDefinition"/> assets and
    /// this handles selection, preview, validation and stock for all of them.
    /// </para>
    /// Q cycles the selection, F or right-click begins placing, left-click confirms,
    /// R rotates, Escape cancels.
    /// </summary>
    public class DeployablePlacer : MonoBehaviour
    {
        [Header("Catalogue")]
        [SerializeField] DeployableDefinition[] deployables;
        [Tooltip("Starting stock per entry, index-matched to the catalogue above.")]
        [SerializeField] int[] startingStock;

        [Header("Ghost materials")]
        [SerializeField] Material validGhostMat;
        [SerializeField] Material invalidGhostMat;

        [Header("Bounds")]
        [Tooltip("Half-width of the placeable area. Set by ArenaBuilder from the arena size - " +
                 "hardcoding it meant a bigger arena silently kept the old placement bounds.")]
        [SerializeField] float arenaBound = 42f;

        public bool IsPlacing { get; private set; }
        public float CurrentAngle { get; private set; }
        public int SelectedIndex { get; private set; }

        public DeployableDefinition Selected =>
            deployables != null && SelectedIndex >= 0 && SelectedIndex < deployables.Length
                ? deployables[SelectedIndex]
                : null;

        public int SelectedStock => StockOf(SelectedIndex);

        /// <summary>(index, newCount)</summary>
        public event Action<int, int> StockChanged;
        public event Action<DeployableDefinition> SelectionChanged;

        int[] stock;
        GameObject ghostGo;
        Renderer ghostRenderer;
        Camera mainCam;
        Plane groundPlane;
        bool isValidLocation;
        Vector3 currentPlacementPos;

        void Awake()
        {
            int count = deployables != null ? deployables.Length : 0;
            stock = new int[count];
            for (int i = 0; i < count; i++)
                stock[i] = startingStock != null && i < startingStock.Length ? startingStock[i] : 0;

            groundPlane = new Plane(Vector3.up, Vector3.zero);
            CreateGhost();
        }

        void Start()
        {
            mainCam = Camera.main;

            for (int i = 0; i < stock.Length; i++) StockChanged?.Invoke(i, stock[i]);
            SelectionChanged?.Invoke(Selected);
        }

        void OnDestroy()
        {
            if (ghostGo != null) Destroy(ghostGo);
        }

        // ------------------------------------------------------------------ stock

        public int Count => deployables != null ? deployables.Length : 0;

        public DeployableDefinition DefinitionAt(int index) =>
            deployables != null && index >= 0 && index < deployables.Length ? deployables[index] : null;

        public int StockOf(int index) =>
            stock != null && index >= 0 && index < stock.Length ? stock[index] : 0;

        public void AddStock(int index, int count)
        {
            if (stock == null || index < 0 || index >= stock.Length || count <= 0) return;

            stock[index] += count;
            StockChanged?.Invoke(index, stock[index]);
        }

        public void Select(int index)
        {
            if (deployables == null || index < 0 || index >= deployables.Length) return;
            if (index == SelectedIndex) return;

            SelectedIndex = index;
            CancelPlacement();
            SelectionChanged?.Invoke(Selected);
        }

        void CycleSelection()
        {
            if (Count <= 1) return;
            Select((SelectedIndex + 1) % Count);
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            bool playable = (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing)
                            && Time.timeScale > 0f;

            if (!playable)
            {
                if (IsPlacing) CancelPlacement();
                return;
            }

            if (InputReader.CycleDeployablePressed) CycleSelection();

            if (InputReader.DeployPressed)
            {
                if (!IsPlacing)
                {
                    if (SelectedStock > 0 && Selected != null) BeginPlacement();
                }
                else if (isValidLocation) TryPlace();
                else CancelPlacement();
            }

            if (!IsPlacing) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && isValidLocation)
            {
                TryPlace();
                return;
            }

            if (InputReader.RotateDeployablePressed && Selected != null && Selected.Rotatable)
                CurrentAngle = Mathf.Repeat(CurrentAngle + 90f, 360f);

            UpdateGhost();
        }

        void BeginPlacement()
        {
            IsPlacing = true;
            ApplyGhostShape();
            if (ghostGo != null) ghostGo.SetActive(true);
            UpdateGhost();
        }

        public void CancelPlacement()
        {
            IsPlacing = false;
            if (ghostGo != null) ghostGo.SetActive(false);
        }

        // ------------------------------------------------------------------ ghost

        void CreateGhost()
        {
            ghostGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghostGo.name = "Deployable_PlacementGhost";

            var col = ghostGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ghostRenderer = ghostGo.GetComponent<Renderer>();
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
            if (validGhostMat != null) ghostRenderer.sharedMaterial = validGhostMat;

            ghostGo.SetActive(false);
        }

        void ApplyGhostShape()
        {
            if (ghostGo == null || Selected == null) return;
            ghostGo.transform.localScale = Selected.GhostSize;
        }

        void UpdateGhost()
        {
            var def = Selected;
            if (ghostGo == null || def == null) return;
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            groundPlane = new Plane(Vector3.up, new Vector3(0f, def.PlacementHeight, 0f));

            var ray = mainCam.ScreenPointToRay(InputReader.MouseScreenPosition);
            if (!groundPlane.Raycast(ray, out float enter)) return;

            var rawPoint = ray.GetPoint(enter);
            var playerPos = transform.position;

            var offset = rawPoint - playerPos;
            offset.y = 0f;
            if (offset.magnitude > def.MaxPlacementRange)
                offset = offset.normalized * def.MaxPlacementRange;

            var targetPoint = playerPos + offset;

            float snap = def.GridSnap;
            float snappedX = Mathf.Round(targetPoint.x / snap) * snap;
            float snappedZ = Mathf.Round(targetPoint.z / snap) * snap;
            currentPlacementPos = new Vector3(snappedX, def.PlacementHeight, snappedZ);

            var rot = Quaternion.Euler(0f, CurrentAngle, 0f);
            ghostGo.transform.SetPositionAndRotation(currentPlacementPos, rot);

            bool insideArena = Mathf.Abs(snappedX) <= arenaBound && Mathf.Abs(snappedZ) <= arenaBound;

            float distToPlayer = Vector2.Distance(new Vector2(snappedX, snappedZ),
                                                  new Vector2(playerPos.x, playerPos.z));
            bool clearOfPlayer = distToPlayer >= 0.85f;

            // Shrunk from the ghost size so structures can sit flush against each other -
            // a wall you cannot butt up to another wall is not much of a wall.
            var half = def.GhostSize * 0.45f;
            var hits = Physics.OverlapBox(currentPlacementPos, half, rot, ~0,
                                          QueryTriggerInteraction.Ignore);
            bool noOverlap = true;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.root == transform.root) continue;
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
            var def = Selected;
            if (!isValidLocation || def == null || def.Prefab == null || SelectedStock <= 0) return;

            var rot = Quaternion.Euler(0f, CurrentAngle, 0f);
            var placed = Instantiate(def.Prefab, currentPlacementPos, rot);
            placed.SetActive(true);

            stock[SelectedIndex]--;
            StockChanged?.Invoke(SelectedIndex, stock[SelectedIndex]);

            SfxPlayer.Instance?.PlayAt(def.PlaceClip, currentPlacementPos, def.PlaceVolume);

            if (SelectedStock <= 0) CancelPlacement();
        }
    }
}
