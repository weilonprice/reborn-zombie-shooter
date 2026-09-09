using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A shared vector field pointing every enemy toward the player, around geometry.
    /// <para>
    /// The horde used to steer straight at the player, which was correct while the arena was
    /// an empty box and wrong the moment it gained cover. A zombie behind a slab pressed into
    /// it forever, held there by its own CharacterController.
    /// </para>
    /// <para>
    /// A field rather than pathfinding per agent: every enemy shares one goal, so the answer
    /// is computed once for the whole arena and each zombie reads the cell it stands in. That
    /// is one array lookup per enemy - cheaper than the normalize it replaces - against sixty
    /// separate A* queries or sixty NavMeshAgents fighting the CharacterController for
    /// control of the transform.
    /// </para>
    /// </summary>
    public class FlowField : MonoBehaviour
    {
        public static FlowField Instance { get; private set; }

        [SerializeField] Transform target;

        [Header("Grid")]
        [Tooltip("Half the arena's width. The grid covers the playable square.")]
        [SerializeField] float arenaHalfSize = 45f;
        [Tooltip("Metres per cell. 1 matches the barricade placement grid.")]
        [SerializeField] float cellSize = 1f;
        [Tooltip("Height the passability probe sits at, above the floor and below the wall tops.")]
        [SerializeField] float probeHeight = 1f;
        [SerializeField] LayerMask blockingMask = ~0;

        [Header("Rebuild")]
        [Tooltip("Seconds between rebuild checks. A rebuild only happens if the player moved " +
                 "to another cell or the barricades changed.")]
        [SerializeField] float rebuildInterval = 0.2f;

        [Header("Cost")]
        [Tooltip("Extra cost of stepping through a barricade. Barricades are expensive rather " +
                 "than impassable ON PURPOSE: impassable would make the horde walk around a " +
                 "wall instead of clawing through it, which quietly deletes the fortification " +
                 "hook. At this cost a short wall is a detour and a long one is a door.")]
        [SerializeField] float barricadeCost = 30f;

        int width, height;
        bool[] blocked;
        float[] extra;
        float[] distance;
        Vector2[] flow;

        // Lazy-deletion heap: pushing a duplicate is cheaper than a decrease-key, and a stale
        // entry is discarded on pop by comparing against the recorded distance.
        int[] heapCell;
        float[] heapDistance;
        int heapCount;

        int lastTargetCell = -1;
        int lastBarricadeVersion = -1;
        float nextRebuild;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            width = height = Mathf.Max(1, Mathf.RoundToInt(arenaHalfSize * 2f / cellSize));

            int cells = width * height;
            blocked = new bool[cells];
            extra = new float[cells];
            distance = new float[cells];
            flow = new Vector2[cells];

            heapCell = new int[Mathf.Max(64, cells)];
            heapDistance = new float[heapCell.Length];

            BakeStaticBlocking();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            Rebuild();
        }

        void Update()
        {
            if (target == null || Time.time < nextRebuild) return;
            nextRebuild = Time.time + rebuildInterval;

            int cell = CellAt(target.position);
            if (cell == lastTargetCell && Barricade.Version == lastBarricadeVersion) return;

            Rebuild();
        }

        /// <summary>
        /// Which way an enemy standing here should walk. Zero when the position is off the
        /// grid or nothing can reach the player from it, and callers fall back to a straight
        /// line - a field that fails should degrade to the old behaviour, not to standing still.
        /// </summary>
        public Vector3 DirectionAt(Vector3 worldPosition)
        {
            if (flow == null) return Vector3.zero;

            // Continuous sample rather than the raw cell, so the horde does not visibly snap
            // between eight directions as it crosses cell boundaries.
            float fx = (worldPosition.x + arenaHalfSize) / cellSize - 0.5f;
            float fz = (worldPosition.z + arenaHalfSize) / cellSize - 0.5f;

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0;
            float tz = fz - z0;

            var sum = Vector2.zero;
            sum += SampleCell(x0, z0) * ((1f - tx) * (1f - tz));
            sum += SampleCell(x0 + 1, z0) * (tx * (1f - tz));
            sum += SampleCell(x0, z0 + 1) * ((1f - tx) * tz);
            sum += SampleCell(x0 + 1, z0 + 1) * (tx * tz);

            if (sum.sqrMagnitude < 0.0001f) return Vector3.zero;

            sum.Normalize();
            return new Vector3(sum.x, 0f, sum.y);
        }

        Vector2 SampleCell(int x, int z)
        {
            if (x < 0 || z < 0 || x >= width || z >= height) return Vector2.zero;
            return flow[z * width + x];
        }

        static readonly Collider[] ProbeBuffer = new Collider[8];

        /// <summary>
        /// Marks cells the level itself blocks. Run once: the arena is static, and everything
        /// placed later - barricades, barrels, bodies - is either a cost or simply ignored.
        /// <para>
        /// Only geometry counts. The player is standing on this grid when it bakes, and a
        /// plain CheckBox would brick whichever cell they spawned in for the rest of the run.
        /// Anything damageable is a body, a wall you built or a barrel, and none of those are
        /// level.
        /// </para>
        /// </summary>
        void BakeStaticBlocking()
        {
            // Queries read the last synced collider positions, and nothing has stepped physics
            // yet on the frame the scene loads.
            Physics.SyncTransforms();

            var halfExtents = new Vector3(cellSize * 0.45f, 0.5f, cellSize * 0.45f);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var centre = CellCentre(x, z) + Vector3.up * probeHeight;

                    int count = Physics.OverlapBoxNonAlloc(
                        centre, halfExtents, ProbeBuffer, Quaternion.identity,
                        blockingMask, QueryTriggerInteraction.Ignore);

                    bool solid = false;
                    for (int i = 0; i < count; i++)
                    {
                        if (ProbeBuffer[i] == null) continue;
                        if (ProbeBuffer[i].GetComponentInParent<IDamageable>() != null) continue;

                        solid = true;
                        break;
                    }

                    blocked[z * width + x] = solid;
                }
            }
        }

        void StampBarricades()
        {
            System.Array.Clear(extra, 0, extra.Length);

            var barricades = Barricade.ActiveBarricades;
            if (barricades == null) return;

            for (int i = 0; i < barricades.Count; i++)
            {
                var barricade = barricades[i];
                if (barricade == null || !barricade.IsAlive) continue;

                int cell = CellAt(barricade.transform.position);
                if (cell >= 0) extra[cell] += barricadeCost;
            }
        }

        void Rebuild()
        {
            if (target == null) return;

            int goal = CellAt(target.position);
            lastTargetCell = goal;
            lastBarricadeVersion = Barricade.Version;

            if (goal < 0) return;

            StampBarricades();

            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = float.PositiveInfinity;
                flow[i] = Vector2.zero;
            }

            heapCount = 0;
            distance[goal] = 0f;
            Push(goal, 0f);

            while (heapCount > 0)
            {
                Pop(out int cell, out float dist);

                // Lazy deletion: a cheaper route to this cell was found after it was pushed.
                if (dist > distance[cell]) continue;

                int cx = cell % width;
                int cz = cell / width;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;

                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;

                        int neighbour = nz * width + nx;
                        if (blocked[neighbour]) continue;

                        // No cutting a diagonal between two blocked cells - a zombie would
                        // walk through the seam where two slabs meet at a corner.
                        if (dx != 0 && dz != 0 &&
                            (blocked[cz * width + nx] || blocked[nz * width + cx])) continue;

                        float step = (dx != 0 && dz != 0) ? 1.41421f : 1f;
                        float next = dist + step + extra[neighbour];

                        if (next >= distance[neighbour]) continue;

                        distance[neighbour] = next;

                        // The field points back down the route it was reached by, so the
                        // direction is stored on the neighbour rather than derived later.
                        flow[neighbour] = new Vector2(-dx, -dz).normalized;

                        Push(neighbour, next);
                    }
                }
            }
        }

        void Push(int cell, float dist)
        {
            // Lazy deletion means a cell can be queued more than once, so the heap can exceed
            // the cell count. Growing is the only safe answer: dropping a push would leave a
            // silently wrong field, which is far harder to spot than an allocation.
            if (heapCount >= heapCell.Length)
            {
                System.Array.Resize(ref heapCell, heapCell.Length * 2);
                System.Array.Resize(ref heapDistance, heapDistance.Length * 2);
            }

            int i = heapCount++;
            heapCell[i] = cell;
            heapDistance[i] = dist;

            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapDistance[parent] <= heapDistance[i]) break;

                Swap(parent, i);
                i = parent;
            }
        }

        void Pop(out int cell, out float dist)
        {
            cell = heapCell[0];
            dist = heapDistance[0];

            heapCount--;
            heapCell[0] = heapCell[heapCount];
            heapDistance[0] = heapDistance[heapCount];

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;

                if (left < heapCount && heapDistance[left] < heapDistance[smallest]) smallest = left;
                if (right < heapCount && heapDistance[right] < heapDistance[smallest]) smallest = right;
                if (smallest == i) break;

                Swap(smallest, i);
                i = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (heapCell[a], heapCell[b]) = (heapCell[b], heapCell[a]);
            (heapDistance[a], heapDistance[b]) = (heapDistance[b], heapDistance[a]);
        }

        int CellAt(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x + arenaHalfSize) / cellSize);
            int z = Mathf.FloorToInt((worldPosition.z + arenaHalfSize) / cellSize);

            if (x < 0 || z < 0 || x >= width || z >= height) return -1;
            return z * width + x;
        }

        Vector3 CellCentre(int x, int z) => new(
            -arenaHalfSize + (x + 0.5f) * cellSize,
            0f,
            -arenaHalfSize + (z + 0.5f) * cellSize);
    }
}
