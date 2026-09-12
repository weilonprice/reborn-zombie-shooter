using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Generates the greybox arena scene, the zombie prefab and their materials, fully wired.
    /// Everything it makes is a normal asset — edit or delete freely, then re-run to reset.
    /// </summary>
    public static class ArenaBuilder
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Arena.unity";
        const string ZombiePrefabPath = Root + "/Prefabs/Zombie.prefab";
        const string BrutePrefabPath = Root + "/Prefabs/Brute.prefab";
        const string RunnerPrefabPath = Root + "/Prefabs/Runner.prefab";
        const string RangedPrefabPath = Root + "/Prefabs/RangedZombie.prefab";
        const string ProjectilePrefabPath = Root + "/Prefabs/EnemyProjectile.prefab";
        const string BarricadePrefabPath = Root + "/Prefabs/Barricade.prefab";
        const string BossPrefabPath = Root + "/Prefabs/Boss.prefab";
        const string BarrelPrefabPath = Root + "/Prefabs/ExplosiveBarrel.prefab";
        const string ClaymorePrefabPath = Root + "/Prefabs/Claymore.prefab";
        const string WeaponProjectilePrefabPath = Root + "/Prefabs/WeaponProjectile.prefab";
        const string BloaterPrefabPath = Root + "/Prefabs/Bloater.prefab";
        const string ArmoredPrefabPath = Root + "/Prefabs/Armored.prefab";
        const string SapperPrefabPath = Root + "/Prefabs/Sapper.prefab";
        const string LeaperPrefabPath = Root + "/Prefabs/Leaper.prefab";
        const string ScreamerPrefabPath = Root + "/Prefabs/Screamer.prefab";
        const string RevenantPrefabPath = Root + "/Prefabs/Revenant.prefab";
        const string CrawlerPrefabPath = Root + "/Prefabs/Crawler.prefab";
        const string NormalZombieModelPath = Root + "/Art/Characters/NormalZombie/NormalZombie.fbx";
        const string NormalZombieControllerPath = Root + "/Art/Characters/NormalZombie/NormalZombie.controller";
        const string MainCharacterModelPath = Root + "/Art/Characters/MainCharacter/MainCharacter.fbx";
        const string MainCharacterControllerPath = Root + "/Art/Characters/MainCharacter/MainCharacter.controller";
        const string SpitterPrefabPath = Root + "/Prefabs/Spitter.prefab";
        const string AcidPoolPrefabPath = Root + "/Prefabs/AcidPool.prefab";
        const string AcidProjectilePrefabPath = Root + "/Prefabs/AcidProjectile.prefab";
        const string DeployableDir = Root + "/Deployables";
        const string DeliveryDir = Root + "/Delivery";
        const string UpgradeDir = Root + "/Upgrades";
        const string MaterialDir = Root + "/Materials";
        const string AudioDir = Root + "/Audio";
        const string WeaponDir = Root + "/Weapons";

        // 90x90. Enlarged 2026-09-08 to give killbox construction room to breathe -
        // barricade funnels need space to be a choice rather than a formality.
        const float ArenaHalfSize = 45f;
        /// <summary>Deployables stay this far inside the walls.</summary>
        const float PlacementMargin = 3f;
        const float WallHeight = 3f;

        [MenuItem("Tools/Zombie Shooter/Build Playable Arena")]
        public static void Build()
        {
            if (!Application.isBatchMode)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Build Playable Arena",
                    $"This replaces {ScenePath} and the enemy prefabs, then opens the new scene.\n\nContinue?",
                    "Build", "Cancel");
                if (!confirmed) return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AbilityAnimationSetup.ConfigureImports();

            // Two strict passes. Object references do NOT survive NewScene: Unity reimports
            // assets written moments earlier, and the stale reference then serializes as null
            // without any error. So every asset is written and flushed first, and reloaded by
            // path only after the scene exists.

            // Pass 1 - generate assets.
            CreateMaterial("M_Ground", new Color(0.20f, 0.21f, 0.23f));
            CreateMaterial("M_Wall", new Color(0.32f, 0.33f, 0.36f));
            CreateMaterial("M_Player", new Color(0.25f, 0.65f, 1.00f));
            CreateMaterial("M_Gun", new Color(0.90f, 0.90f, 0.95f));
            CreateMaterial("M_Zombie", new Color(0.35f, 0.70f, 0.28f));
            CreateMaterial("M_Brute", new Color(0.65f, 0.17f, 0.17f));
            CreateMaterial("M_Runner", new Color(0.95f, 0.55f, 0.15f));
            CreateMaterial("M_Ranged", new Color(0.55f, 0.20f, 0.75f));
            CreateMaterial("M_Boss", new Color(0.42f, 0.05f, 0.07f));
            CreateMaterial("M_Barrel", new Color(0.72f, 0.26f, 0.06f));
            CreateMaterial("M_Claymore", new Color(0.20f, 0.26f, 0.16f));
            CreateUnlitMaterial("M_Claymore_Light", new Color(0.95f, 0.25f, 0.20f));
            CreateMaterial("M_Barricade_Wood", new Color(0.48f, 0.28f, 0.12f));
            CreateMaterial("M_Barricade_Metal", new Color(0.20f, 0.22f, 0.26f));
            CreateUnlitMaterial("M_Placement_Valid", new Color(0.2f, 0.9f, 0.3f, 0.45f));
            CreateUnlitMaterial("M_Placement_Invalid", new Color(0.9f, 0.2f, 0.2f, 0.45f));
            CreateUnlitMaterial("M_HealthBar_Bg", new Color(0.12f, 0.12f, 0.12f, 0.85f));
            CreateUnlitMaterial("M_HealthBar_Fill", new Color(0.3f, 0.85f, 0.35f, 0.95f));
            CreateUnlitMaterial("M_Projectile", new Color(0.95f, 0.30f, 1.00f));
            CreateUnlitMaterial("M_Tracer", new Color(1.00f, 0.85f, 0.35f));
            CreateUnlitMaterial("M_Spark", new Color(1.00f, 0.82f, 0.35f));
            CreateUnlitMaterial("M_Brass", new Color(0.85f, 0.64f, 0.24f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildZombiePrefab();
            BuildBrutePrefab();
            BuildProjectilePrefab();
            BuildWeaponProjectilePrefab();
            BuildBossPrefab();
            BuildBarricadePrefab();
            BuildBarrelPrefab();
            BuildClaymorePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildRunnerPrefab();
            BuildRangedPrefab();
            BuildBloaterPrefab();
            BuildArmoredPrefab();
            BuildSapperPrefab();
            BuildLeaperPrefab();
            BuildScreamerPrefab();
            BuildRevenantPrefab();
            BuildCrawlerPrefab();
            BuildAcidPrefabs();
            BuildSpitterPrefab();
            LoadOrCreateDeliveries();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Pass 2 - build the scene from assets loaded fresh off disk.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var groundMat = LoadMaterial("M_Ground");
            var wallMat = LoadMaterial("M_Wall");
            var playerMat = LoadMaterial("M_Player");
            var tracerMat = LoadMaterial("M_Tracer");
            var sparkMat = LoadMaterial("M_Spark");
            var brassMat = LoadMaterial("M_Brass");
            BuildEnvironment(groundMat, wallMat);
            var player = BuildPlayer(playerMat, tracerMat, sparkMat, brassMat);
            BuildCamera(player.transform);
            var waves = BuildManagers(player, sparkMat);
            BuildHud(player, waves);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Validate(waves);
        }

        /// <summary>
        /// One arena's cover. Height and elevation are shared, so a layout only states where
        /// its blocks are and how big they are on the floor plane.
        /// </summary>
        readonly struct ArenaLayout
        {
            public readonly string Name;
            readonly (float x, float z, float sx, float sz)[] blocks;

            public ArenaLayout(string name, (float, float, float, float)[] blocks)
            {
                Name = name;
                this.blocks = blocks;
            }

            public (Vector3 position, Vector3 size)[] Blocks()
            {
                var result = new (Vector3, Vector3)[blocks.Length];

                for (int i = 0; i < blocks.Length; i++)
                {
                    result[i] = (new Vector3(blocks[i].x, CoverHeight * 0.5f, blocks[i].z),
                                 new Vector3(blocks[i].sx, CoverHeight, blocks[i].sz));
                }

                return result;
            }
        }

        const float CoverHeight = 2.5f;

        /// <summary>
        /// Four spaces that ask different questions of the same arsenal. All 90x90 and all
        /// with a clear centre, so spawn radius, placement bounds and the player's start need
        /// no per-arena tuning - the difference is entirely in what the cover does.
        /// <para>
        /// Footprints are sized in BUILDINGS, not in abstract blocks. The narrowest source
        /// mesh is 8.95m across, so a 3m-wide slab squashed it to a quarter of its width -
        /// these were authored when cover was featureless boxes, and the models changed the
        /// unit. Nothing below is narrower than about five metres.
        /// </para>
        /// </summary>
        static readonly ArenaLayout[] ArenaLayouts =
        {
            // Scattered blocks and long anchors. The balanced baseline everything else is
            // measured against, and the one the game was tuned in.
            new("The Yard", new (float, float, float, float)[]
            {
                (-18f, 12f, 6f, 6f), (21f, -9f, 6f, 6f), (6f, 26f, 6f, 6f),
                (-27f, -21f, 6f, 6f), (14f, 14f, 6f, 6f), (-9f, -28f, 6f, 6f),
                (0f, -14f, 12f, 5.5f), (-33f, 4f, 5.5f, 12f), (33f, 18f, 5.5f, 12f),
                (18f, -30f, 12f, 5.5f), (-20f, 30f, 10f, 5.5f), (30f, -20f, 6f, 6f),
                (-34f, -34f, 6f, 6f), (34f, 34f, 6f, 6f),
            }),

            // Long parallel lanes. Sightlines run one way and not the other, which is the
            // sniper's and rifle's arena and the flamethrower's worst. A barricade across a
            // lane closes it completely, so fortification is at its strongest here.
            new("The Corridors", new (float, float, float, float)[]
            {
                (-31f, -22f, 7f, 30f), (-31f, 18f, 7f, 24f),
                (-15f, -2f, 7f, 38f),
                (1f, -28f, 7f, 22f), (1f, 16f, 7f, 26f),
                (17f, -2f, 7f, 38f),
                (33f, -22f, 7f, 30f), (33f, 18f, 7f, 24f),
                // Two cross-pieces, so the lanes are not perfectly parallel and a player
                // cannot simply hold one line forever.
                (-23f, 34f, 18f, 6f), (23f, -34f, 18f, 6f),
            }),

            // Open centre, a dense band of cover at mid radius, open again at the edge. You
            // fight in a donut: kiting around the band is easy, holding a spot is hard, and
            // ranged enemies get clean shots across the middle.
            new("The Ring", new (float, float, float, float)[]
            {
                (0f, 21f, 14f, 6f), (0f, -21f, 14f, 6f),
                (21f, 0f, 6f, 14f), (-21f, 0f, 6f, 14f),
                (16f, 16f, 8f, 6f), (-16f, 16f, 8f, 6f),
                (16f, -16f, 8f, 6f), (-16f, -16f, 8f, 6f),
                (25f, 25f, 6f, 8f), (-25f, 25f, 6f, 8f),
                (25f, -25f, 6f, 8f), (-25f, -25f, 6f, 8f),
            }),

            // Dense small cover everywhere, no long sightlines. The shotgun's and
            // flamethrower's arena and the sniper's worst, and the hardest test the flow
            // field gets - every route is a sequence of corners.
            new("The Warren", WarrenBlocks()),
        };

        /// <summary>
        /// A deterministic scatter. Seeded rather than hand-placed because two dozen blocks
        /// are data, not design, and the centre is kept clear so the player never starts
        /// inside cover.
        /// </summary>
        static (float, float, float, float)[] WarrenBlocks()
        {
            var random = new System.Random(20260911);
            var blocks = new List<(float, float, float, float)>();

            // Fewer and larger than the original scatter: 26 boxes of 3m became 26 buildings
            // filling the arena once cover stopped being abstract.
            const float clearRadius = 11f;
            const float spacing = 12f;

            for (int attempt = 0; attempt < 600 && blocks.Count < 18; attempt++)
            {
                float x = (float)(random.NextDouble() * 72f - 36f);
                float z = (float)(random.NextDouble() * 72f - 36f);

                if (x * x + z * z < clearRadius * clearRadius) continue;

                bool tooClose = false;
                foreach (var placed in blocks)
                {
                    float dx = placed.Item1 - x;
                    float dz = placed.Item2 - z;
                    if (dx * dx + dz * dz < spacing * spacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                float size = 5.5f + (float)random.NextDouble() * 3f;
                blocks.Add((x, z, size, size));
            }

            return blocks.ToArray();
        }

        // ---------------------------------------------------------------- environment

        static void BuildEnvironment(Material groundMat, Material wallMat)
        {
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.90f);
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var env = new GameObject("Environment").transform;

            // Unity's Plane primitive is 10x10 units at unit scale.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env, false);
            ground.transform.localScale = Vector3.one * (ArenaHalfSize * 2f / 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(env, false);

            CreateWall(walls, "North", new Vector3(0f, WallHeight / 2f, ArenaHalfSize),
                new Vector3(ArenaHalfSize * 2f + 2f, WallHeight, 2f), wallMat);
            CreateWall(walls, "South", new Vector3(0f, WallHeight / 2f, -ArenaHalfSize),
                new Vector3(ArenaHalfSize * 2f + 2f, WallHeight, 2f), wallMat);
            CreateWall(walls, "East", new Vector3(ArenaHalfSize, WallHeight / 2f, 0f),
                new Vector3(2f, WallHeight, ArenaHalfSize * 2f + 2f), wallMat);
            CreateWall(walls, "West", new Vector3(-ArenaHalfSize, WallHeight / 2f, 0f),
                new Vector3(2f, WallHeight, ArenaHalfSize * 2f + 2f), wallMat);

            // Every layout is built and all but one switched off at runtime. The builder
            // generates the scene once and cannot know which arena a future run wants. Each
            // old cover footprint now receives a scaled authored building instead of a cube.
            var layoutRoots = new Object[ArenaLayouts.Length];

            for (int i = 0; i < ArenaLayouts.Length; i++)
            {
                var layout = ArenaLayouts[i];

                var root = new GameObject(layout.Name).transform;
                root.SetParent(env, false);

                var blocks = layout.Blocks();
                for (int b = 0; b < blocks.Length; b++)
                    FillFootprint(root, blocks[b], wallMat, i * 100 + b);

                layoutRoots[i] = root.gameObject;
            }

            var selector = env.gameObject.AddComponent<ArenaSelector>();
            using (var f = new Fields(selector))
            {
                f.Arr("layouts", layoutRoots).I("forcedIndex", -1);
            }
        }

        static void CreateWall(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = size;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static readonly Dictionary<string, Vector3> BuildingSourceDimensions =
            new Dictionary<string, Vector3>
            {
                { "Shack", new Vector3(8.95f, 7.13f, 7.27f) },
                { "Storefront", new Vector3(12.54f, 5.21f, 8.85f) },
                { "Warehouse", new Vector3(16.55f, 9.43f, 10.36f) },
                { "ApartmentBlock", new Vector3(10.54f, 9.41f, 9.81f) },
            };

        /// <summary>
        /// Covers one layout block with buildings, tiling along its long axis rather than
        /// stretching a single mesh to fit.
        /// <para>
        /// The Corridors has slabs up to 38m. Fitting one Warehouse to a 3x38m footprint
        /// scaled it 0.18 across and 3.67 along - a twenty-to-one distortion that smears
        /// every door, window and roof ridge. A row of buildings reads as a terrace; one
        /// stretched building reads as a bug.
        /// </para>
        /// </summary>
        static void FillFootprint(Transform parent, (Vector3 position, Vector3 size) block,
                                  Material fallbackMat, int index)
        {
            bool alongX = block.size.x >= block.size.z;

            float length = alongX ? block.size.x : block.size.z;
            float width = alongX ? block.size.z : block.size.x;

            // One building per roughly nine metres of run, so a 38m slab becomes four rather
            // than one enormous one. Ceil, so a footprint is always fully covered.
            int count = Mathf.Max(1, Mathf.CeilToInt(length / BuildingRunPerUnit));
            float step = length / count;

            for (int i = 0; i < count; i++)
            {
                // Centre of this slice along the run.
                float offset = -length * 0.5f + step * (i + 0.5f);

                var slice = alongX
                    ? new Vector3(step, block.size.y, width)
                    : new Vector3(width, block.size.y, step);

                var position = alongX
                    ? new Vector3(block.position.x + offset, 0f, block.position.z)
                    : new Vector3(block.position.x, 0f, block.position.z + offset);

                var artName = BuildingArtForFootprint(slice, index * 10 + i);
                float yaw = slice.x >= slice.z ? 0f : 90f;

                // Alternate the facing along a terrace so a row does not read as one mesh
                // repeated, which is the other way tiling can look wrong.
                if (count > 1 && i % 2 == 1) yaw += 180f;

                CreateBuilding(parent, artName, position, yaw,
                               BuildingScaleForFootprint(artName, slice), fallbackMat,
                               index * 10 + i);
            }
        }

        /// <summary>Metres of footprint each building covers before another is placed.</summary>
        const float BuildingRunPerUnit = 9f;

        /// <summary>
        /// How much of a building's measured footprint is actual wall. Roof overhang and
        /// debris push the bounds wider than the thing the player can walk into.
        /// </summary>
        const float WallInsetFromBounds = 0.88f;

        /// <summary>
        /// Picks a building for a footprint, varying by index so a terrace is not one mesh
        /// repeated down its whole length.
        /// <para>
        /// Candidates are filtered by how evenly they would have to scale, not just by size:
        /// squashing a 16m warehouse onto a 6m footprint distorts it as badly as stretching
        /// a shack. Only when nothing fits well does the least-bad option win outright.
        /// </para>
        /// </summary>
        static string BuildingArtForFootprint(Vector3 size, int index)
        {
            string[] options = { "Shack", "Storefront", "ApartmentBlock", "Warehouse" };

            var good = new List<string>();
            string best = options[0];
            float bestPenalty = float.MaxValue;

            foreach (var option in options)
            {
                if (!BuildingSourceDimensions.TryGetValue(option, out var source)) continue;

                float sx = Mathf.Max(size.x, 0.8f) / source.x;
                float sz = Mathf.Max(size.z, 0.8f) / source.z;

                // Penalty is how far from square the two scales are, and how far the overall
                // scale strays from life size.
                float aspect = Mathf.Max(sx, sz) / Mathf.Max(0.01f, Mathf.Min(sx, sz));
                float shrink = 1f / Mathf.Max(0.01f, Mathf.Min(sx, sz));
                float penalty = aspect + shrink * 0.35f;

                if (penalty < bestPenalty) { bestPenalty = penalty; best = option; }
                if (aspect <= 1.6f && sx >= 0.35f && sz >= 0.35f) good.Add(option);
            }

            if (good.Count == 0) return best;
            return good[Mathf.Abs(index) % good.Count];
        }

        static Vector3 BuildingScaleForFootprint(string artName, Vector3 footprint)
        {
            if (!BuildingSourceDimensions.TryGetValue(artName, out var source))
                return Vector3.one;

            float sx = Mathf.Max(footprint.x, 0.8f) / source.x;
            float sz = Mathf.Max(footprint.z, 0.8f) / source.z;
            // Thin corridor buildings stay readable without making tiny Warren props tower
            // over the player. The source meshes are allowed to squash in X/Z because the
            // layouts contain long slab footprints.
            // Capped well below life size on purpose. The camera looks down at 61 degrees,
            // so a building of height h hides roughly 0.55h of ground behind it - and the
            // screen-space occlusion fade that would make tall cover safe is not due until
            // Phase 3. Until then, shorter buildings lose the player less often.
            float sy = Mathf.Clamp(Mathf.Min(sx, sz) * 1.6f, 0.45f, 0.72f);
            return new Vector3(sx, sy, sz);
        }

        static void CreateBuilding(Transform parent, string artName, Vector3 position, float yaw,
                                   Vector3 scale, Material fallbackMat, int index)
        {
            string path = $"{Root}/Art/Buildings/{artName}/{artName}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                // Keep the editor menu useful if someone runs it before importing the art.
                CreateWall(parent, $"BuildingFallback_{index}", position, new Vector3(7f, 2.5f, 7f), fallbackMat);
                Debug.LogWarning($"ArenaBuilder: no building model at {path}; using a fallback cover block.");
                return;
            }

            var building = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (building == null) return;

            building.name = $"{artName}_{index:00}";
            building.transform.localPosition = position;
            building.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            building.transform.localScale = scale;
            MarkBuildingStaticAndAddColliders(building);

            // One box per building, and it is the only collider. Sized to the walls rather
            // than to the mesh bounds: bounds include the roof overhang, which would block
            // movement in the open air beside the building.
            if (BuildingSourceDimensions.TryGetValue(artName, out var sourceDimensions))
            {
                var cover = building.GetComponent<BoxCollider>();
                if (cover == null) cover = building.AddComponent<BoxCollider>();

                var walls = new Vector3(sourceDimensions.x * WallInsetFromBounds,
                                        sourceDimensions.y,
                                        sourceDimensions.z * WallInsetFromBounds);

                cover.center = new Vector3(0f, walls.y * 0.5f, 0f);
                cover.size = walls;
            }
        }

        static void MarkBuildingStaticAndAddColliders(GameObject building)
        {
            building.isStatic = true;
            foreach (var renderer in building.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.gameObject.isStatic = true;
            }

            // Deliberately NO per-part MeshColliders. The apartment alone is 67 parts, and
            // The Warren places 26 buildings - that is well over a thousand non-convex mesh
            // colliders for the flow field's 8,100-cell bake and every bullet to test
            // against. They also buy nothing: the solid root box below already blocks
            // anything that would have reached them, so the detail is unreachable. Doors and
            // windows are decorative at this scale; if a building ever becomes enterable,
            // the box is what has to go, not the other way round.
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
                filter.gameObject.isStatic = true;
        }

        /// <summary>
        /// How many weapons a run can hold, mapped to the number row 1-9 then 0. Equal to
        /// the catalogue size: the run's commitment is which weapon it pours gold into, not
        /// which ones it carries, and gold only ever maxes one path.
        /// </summary>
        const int CarryCapacity = 10;

        /// <summary>
        /// The player's capsule height. Named because the authored model has to be dropped by
        /// half of it to stand on the floor, and that happens before the controller exists.
        /// </summary>
        const float PlayerControllerHeight = 2f;

        /// <summary>Where a weapon's grip sits, relative to the player's centre.</summary>
        static readonly Vector3 GunHolderOffset = new(0f, 0f, 0.12f);

        /// <summary>
        /// Uniform scale applied to every weapon model. One knob, because the authored
        /// lengths run from a 1.1m pistol to a 2.62m sniper against a 2m player - readable
        /// from above, but the sniper's muzzle ends up nearly a body length ahead.
        /// </summary>
        const float WeaponModelScale = 1f;

        /// <summary>
        /// Art folder per weapon, in the same order as LoadOrCreateWeapons returns them.
        /// Explicit rather than derived from the asset name: the sniper's definition is
        /// WPN_Sniper and its art folder is SniperRifle.
        /// </summary>
        static readonly string[] WeaponArtNames =
        {
            "Pistol", "Shotgun", "AssaultRifle", "SniperRifle", "Flamethrower",
            "TeslaCoil", "GrenadeLauncher", "SMG", "NailGun", "SiphonRifle",
        };

        static readonly string[] WeaponAnimationNames =
        {
            "Idle", "Equip", "Unequip", "Fire", "Reload", "Charge", "Inspect", "Melee",
            "Pump", "BoltCycle", "SlideCycle", "DrumCycle", "Ignite", "Discharge", "Drain",
            "DriverCycle",
        };

        // ---------------------------------------------------------------- player

        static GameObject BuildPlayer(Material bodyMat, Material tracerMat, Material sparkMat, Material brassMat)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            // The CharacterController is the physical collider; drop the primitive's own.
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            // Replace the greybox capsule with the authored survivor when the model has
            // imported. Keep the capsule as a fallback so the builder remains usable while
            // art assets are being iterated on or are temporarily absent.
            Animator characterAnimator = null;
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MainCharacterModelPath);
            if (modelAsset != null)
            {
                Object.DestroyImmediate(body);

                var model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (model != null)
                {
                    model.name = "MainCharacter_Model";
                    model.transform.SetParent(player.transform, false);

                    // Feet to the floor, not to the pivot - the same offset the zombie needs.
                    // The model stands on zero but a CharacterController is centred on its
                    // pivot, so local zero would leave the survivor hovering a metre up.
                    model.transform.localPosition = new Vector3(0f, -PlayerControllerHeight * 0.5f, 0f);
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    characterAnimator = model.GetComponent<Animator>();
                    if (characterAnimator == null) characterAnimator = model.AddComponent<Animator>();
                    ConfigureMainCharacterImportSettings();
                    characterAnimator.runtimeAnimatorController = LoadOrCreateMainCharacterController(model);
                    characterAnimator.applyRootMotion = false;
                    characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            // Holders rather than models. WeaponVisuals swaps which model is active inside
            // them, and Weapon slides the holders for akimbo - so neither has to know what
            // the other is doing to one transform.
            var gun = new GameObject("GunHolder");
            gun.transform.SetParent(player.transform, false);
            gun.transform.localPosition = GunHolderOffset;

            // Second gun, hidden until a dual-wield tier is bought. Built here rather than
            // instantiated on purchase so nothing has to load a prefab mid-fight.
            var offHandGun = new GameObject("OffHandHolder");
            offHandGun.transform.SetParent(player.transform, false);
            offHandGun.transform.localPosition = new Vector3(-0.30f, 0f, GunHolderOffset.z);
            offHandGun.SetActive(false);

            player.AddComponent<AudioListener>();

            var controller = player.AddComponent<CharacterController>();
            controller.height = PlayerControllerHeight;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;

            var health = player.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 100f);

            var damageShake = player.AddComponent<DamageShake>();
            using (var f = new Fields(damageShake))
            {
                f.Obj("health", health)
                 .F("traumaAtFullHealthLoss", 4.6f)
                 .F("minTrauma", 0.45f).F("maxTrauma", 0.85f);
            }

            var move = player.AddComponent<PlayerController>();
            using (var f = new Fields(move))
                f.F("moveSpeed", 7f).F("acceleration", 60f).F("turnSpeed", 900f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.35f);

            // Light does most of the work: it throws real illumination on nearby geometry
            // for a few frames, which sells a shot far better than a billboard.
            var flashLightGo = new GameObject("FlashLight");
            flashLightGo.transform.SetParent(muzzle, false);
            var flashLight = flashLightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.87f, 0.55f);
            flashLight.range = 8f;
            flashLight.intensity = 12f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;

            var muzzleSparks = CreateBurstSystem(muzzle, "FlashSparks", sparkMat,
                new Color(1f, 0.88f, 0.5f), 3f, 7f, 0.04f, 0.1f, 0.03f, 0.07f, 14f, 0.2f);

            var muzzleFlash = muzzle.gameObject.AddComponent<MuzzleFlash>();
            using (var f = new Fields(muzzleFlash))
            {
                f.Obj("flashLight", flashLight).Obj("spark", muzzleSparks)
                 .F("lightIntensity", 12f).F("duration", 0.035f).I("particlesPerShot", 4);
            }

            var offHandMuzzle = new GameObject("OffHandMuzzle").transform;
            offHandMuzzle.SetParent(player.transform, false);
            offHandMuzzle.localPosition = new Vector3(-0.30f, 0f, 1.35f);

            var offHandFlashLightGo = new GameObject("FlashLight");
            offHandFlashLightGo.transform.SetParent(offHandMuzzle, false);
            var offHandFlashLight = offHandFlashLightGo.AddComponent<Light>();
            offHandFlashLight.type = LightType.Point;
            offHandFlashLight.color = new Color(1f, 0.87f, 0.55f);
            offHandFlashLight.range = 8f;
            offHandFlashLight.intensity = 12f;
            offHandFlashLight.shadows = LightShadows.None;
            offHandFlashLight.enabled = false;

            var offHandSparks = CreateBurstSystem(offHandMuzzle, "FlashSparks", sparkMat,
                new Color(1f, 0.88f, 0.5f), 3f, 7f, 0.04f, 0.1f, 0.03f, 0.07f, 14f, 0.2f);

            // Left alive even when the gun is holstered: nothing calls Play on it until the
            // weapon is actually dual wielding, and a disabled object cannot run coroutines.
            var offHandMuzzleFlash = offHandMuzzle.gameObject.AddComponent<MuzzleFlash>();
            using (var f = new Fields(offHandMuzzleFlash))
            {
                f.Obj("flashLight", offHandFlashLight).Obj("spark", offHandSparks)
                 .F("lightIntensity", 12f).F("duration", 0.035f).I("particlesPerShot", 4);
            }

            // Right-hand side of the gun, angled out, up and slightly back - roughly where
            // a real ejection port throws brass.
            var ejectPort = new GameObject("EjectPort").transform;
            ejectPort.SetParent(player.transform, false);
            ejectPort.localPosition = new Vector3(0.2f, 0.08f, 0.85f);
            ejectPort.localRotation = Quaternion.Euler(-35f, 110f, 0f);

            var shells = CreateBurstSystem(ejectPort, "Shells", brassMat,
                new Color(0.9f, 0.7f, 0.3f), 2.5f, 4.5f, 1.2f, 1.8f, 0.035f, 0.055f, 12f, 2.5f);
            ConfigureAsShellCasing(shells);

            // Stats now live entirely in the definition asset; only scene wiring is set here.
            var arsenal = LoadOrCreateWeapons();

            var weapon = player.AddComponent<Weapon>();
            using (var f = new Fields(weapon))
            {
                // Slot 0 up front so Weapon.Awake has something to equip; WeaponLoadout
                // re-equips it on Start and owns the choice from then on.
                f.Obj("definition", arsenal[0])
                 .Obj("rayOrigin", player.transform)
                 .Obj("muzzle", muzzle)
                 .Obj("muzzleFlash", muzzleFlash)
                 .Obj("shellEject", shells)
                 .Obj("mainGunVisual", gun.transform)
                 .Obj("offHandGun", offHandGun)
                 .Obj("offHandMuzzle", offHandMuzzle)
                 .Obj("offHandMuzzleFlash", offHandMuzzleFlash);
            }

            if (characterAnimator != null)
            {
                var characterDriver = player.AddComponent<PlayerAnimator>();
                using (var f = new Fields(characterDriver))
                {
                    f.Obj("animator", characterAnimator)
                     .Obj("health", health)
                     .Obj("weapon", weapon)
                     .F("fireDuration", 0.37f)
                     .F("reloadDuration", 1.90f)
                     .F("getShotDuration", 0.77f)
                     .F("staggerDuration", 1.43f)
                     .F("heavyHealthFraction", 0.40f)
                     .F("heavyKnockback", 2.5f);
                }
            }

            var loadout = player.AddComponent<WeaponLoadout>();
            using (var f = new Fields(loadout))
            {
                // The catalogue is everything the Armory can sell; carried is what the run
                // is holding. Slot 0 starts with the pistol, the other three are bought into
                // and there is no fourth purchase after that - the run is committed.
                var carried = new Object[CarryCapacity];
                carried[0] = arsenal[0];

                f.Obj("weapon", weapon).F("swapCooldown", 0.25f)
                 .Arr("catalogue", arsenal).Arr("carried", carried);
            }

            BuildWeaponVisuals(player, loadout, arsenal, gun.transform, offHandGun.transform,
                               muzzle, offHandMuzzle, ejectPort);

            var weaponAnimator = player.AddComponent<WeaponAnimator>();
            using (var f = new Fields(weaponAnimator))
            {
                f.Obj("weapon", weapon)
                 .Obj("loadout", loadout)
                 .Obj("visuals", player.GetComponent<WeaponVisuals>())
                 .F("fireDuration", 0.20f)
                 .F("reloadDuration", 1.90f)
                 .F("equipDuration", 0.55f);
            }

            var ultimate = player.AddComponent<UltimateAbility>();
            using (var f = new Fields(ultimate))
            {
                f.Obj("weapon", weapon).Obj("movement", move).Obj("health", health)
                 .F("spinDegreesPerSecond", 540f)
                 .F("openingSlowMotion", 0.35f).F("openingTimeScale", 0.35f)
                 .F("openingTrauma", 0.5f);
            }

            var lastStand = player.AddComponent<LastStand>();
            using (var f = new Fields(lastStand))
            {
                f.F("survivingHealth", 1f).F("slowMotionSeconds", 0.9f)
                 .F("slowMotionScale", 0.3f);
            }

            var placer = player.AddComponent<DeployablePlacer>();
            using (var f = new Fields(placer))
            {
                f.Arr("deployables", LoadOrCreateDeployables())
                 .Obj("validGhostMat", LoadMaterial("M_Placement_Valid"))
                 .Obj("invalidGhostMat", LoadMaterial("M_Placement_Invalid"))
                 .F("arenaBound", ArenaHalfSize - PlacementMargin)
                 .IArr("startingStock", new[] { 1, 0, 0 });
            }

            return player;
        }

        static GameObject BuildCamera(Transform target)
        {
            var offset = new Vector3(0f, 20f, -11f);

            // Two objects, not one: the rig does the following, and the camera hangs off it
            // so shake can be a purely local offset. That makes the shake independent of
            // LateUpdate ordering between the two scripts, which is otherwise unspecified.
            var rig = new GameObject("Camera Rig");
            rig.transform.position = target.position + offset;
            rig.transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

            var follow = rig.AddComponent<CameraFollow>();
            using (var f = new Fields(follow))
            {
                f.Obj("target", target)
                 .V3("offset", offset)
                 .F("smoothTime", 0.12f).F("aimLead", 0.18f).F("maxLead", 5f);
            }

            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(rig.transform, false);

            var cam = go.AddComponent<Camera>();
            // Solid dark clear reads better top-down than a skybox you never see.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;

            var shake = go.AddComponent<CameraShake>();
            using (var f = new Fields(shake))
            {
                f.F("maxOffset", 0.4f).F("maxRoll", 1.84f).F("decay", 1.6f)
                 .F("frequency", 22f).F("responseCurve", 2f)
                 .F("recoilDecay", 9f).F("maxRecoil", 0.25f);
            }

            return rig;
        }

        static void ConfigureMainCharacterImportSettings()
        {
            var importer = AssetImporter.GetAtPath(MainCharacterModelPath) as ModelImporter;
            if (importer == null) return;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            var looping = new[] { "Idle", "Walk", "Run", "Aim" };
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = false;
                for (int j = 0; j < looping.Length; j++)
                    if (ClipNameMatches(clips[i].name, looping[j])) shouldLoop = true;

                if (clips[i].loopTime == shouldLoop) continue;
                clips[i].loopTime = shouldLoop;
                changed = true;
            }

            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>Clips that play on the legs, full body. The base layer.</summary>
        static readonly string[] LocomotionClips = { "Idle", "Walk", "Run", "Death", "Ultimate" };

        /// <summary>Clips that play on the arms only, over whatever the legs are doing.</summary>
        static readonly string[] UpperBodyClips = { "Aim", "Fire", "Reload", "GetShot", "Stagger" };

        /// <summary>
        /// Two layers, because a twin-stick player moves and shoots at the same time almost
        /// constantly. On one layer the firing clip wins and the survivor is never seen to
        /// walk; masked to the arms, the legs keep running underneath.
        /// </summary>
        static RuntimeAnimatorController LoadOrCreateMainCharacterController(GameObject model)
        {
            EnsureFolder(Root + "/Art");
            EnsureFolder(Root + "/Art/Characters");
            EnsureFolder(Root + "/Art/Characters/MainCharacter");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainCharacterControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(MainCharacterControllerPath);

            var clips = AbilityAnimationSetup.WithAbilities(MainCharacterModelPath, AbilityAnimationSetup.SurvivorPack);

            FillLayer(controller, 0, "Base Layer", LocomotionClips, "Idle", clips, null);

            var mask = LoadOrCreateUpperBodyMask(model);
            int upper = IndexOfLayer(controller, "UpperBody");
            if (upper < 0)
            {
                upper = controller.layers.Length;
                AddLayer(controller, "UpperBody", mask);
            }

            FillLayer(controller, upper, "UpperBody", UpperBodyClips, "Aim", clips, mask);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        public const string ReloadSpeedParameter = "ReloadSpeed";

        static void EnsureFloatParameter(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name) return;

            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        static int IndexOfLayer(AnimatorController controller, string name)
        {
            for (int i = 0; i < controller.layers.Length; i++)
                if (controller.layers[i].name == name) return i;
            return -1;
        }

        static void AddLayer(AnimatorController controller, string name, AvatarMask mask)
        {
            // The state machine has to live inside the controller asset, or the layer
            // serializes with a dangling reference and the whole controller comes back empty.
            var machine = new AnimatorStateMachine
            {
                name = name,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(machine, controller);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = name,
                stateMachine = machine,
                defaultWeight = 1f,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
        }

        static void FillLayer(AnimatorController controller, int index, string name,
                              string[] wanted, string defaultState, Object[] clips, AvatarMask mask)
        {
            if (index < 0 || index >= controller.layers.Length) return;

            // Layers are returned by value, so weight and mask have to be written back
            // through the array rather than onto the copy.
            var layers = controller.layers;
            layers[index].name = name;
            layers[index].defaultWeight = 1f;
            if (mask != null) layers[index].avatarMask = mask;
            controller.layers = layers;

            var machine = controller.layers[index].stateMachine;

            for (int i = 0; i < wanted.Length; i++)
            {
                AnimationClip clip = null;
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c] is AnimationClip candidate && ClipNameMatches(candidate.name, wanted[i]))
                    {
                        clip = candidate;
                        break;
                    }
                }
                if (clip == null) continue;

                AnimatorState state = null;
                for (int st = 0; st < machine.states.Length; st++)
                {
                    if (machine.states[st].state.name == wanted[i])
                    {
                        state = machine.states[st].state;
                        break;
                    }
                }

                if (state == null) state = machine.AddState(wanted[i]);
                state.motion = clip;
                if (wanted[i] == defaultState) machine.defaultState = state;

                // Reload is fitted to the weapon's actual reload time, which upgrades cut to
                // as little as 0.45s against a 1.9s clip. Driven by a parameter rather than
                // Animator.speed, which is global and would sprint the legs at the same rate.
                if (wanted[i] == "Reload")
                {
                    EnsureFloatParameter(controller, ReloadSpeedParameter);
                    state.speedParameterActive = true;
                    state.speedParameter = ReloadSpeedParameter;
                }
            }

            // A state left over from the single-layer version would still be reachable by
            // name and would play on the wrong half of the body. Collected before removing,
            // rather than removed while walking an array that reindexes underneath.
            var stale = new List<AnimatorState>();
            foreach (var child in machine.states)
            {
                bool belongs = false;
                for (int i = 0; i < wanted.Length; i++)
                    if (child.state.name == wanted[i]) belongs = true;

                if (!belongs) stale.Add(child.state);
            }

            foreach (var state in stale) machine.RemoveState(state);
        }

        /// <summary>
        /// Everything from the spine up. Pelvis and Root stay with locomotion so the walk
        /// keeps its hip motion - masking those away makes the legs swing under a body that
        /// never shifts its weight.
        /// </summary>
        static AvatarMask LoadOrCreateUpperBodyMask(GameObject model)
        {
            const string path = Root + "/Art/Characters/MainCharacter/UpperBody.mask";

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            if (model != null)
            {
                mask.transformCount = 0;

                mask.AddTransformPath(model.transform, true);

                for (int i = 0; i < mask.transformCount; i++)
                    mask.SetTransformActive(i, IsUpperBodyPath(mask.GetTransformPath(i)));
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>Matches the Spine segment exactly, so a bone merely named "Spine2" elsewhere
        /// in a future rig cannot quietly opt itself in.</summary>
        static bool IsUpperBodyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            foreach (var segment in path.Split('/'))
                if (segment == "Spine") return true;

            return false;
        }


        // Blender's FBX exporter preserves the action and rig names in Unity's
        // imported clip names (for example, "MainCharacter_Rig|MainCharacter_Rig|Idle").
        // Match the authored state name at the end so the controller remains stable
        // if the rig is renamed or the exporter adds another prefix.
        static bool ClipNameMatches(string importedName, string authoredName)
        {
            if (string.IsNullOrEmpty(importedName) || string.IsNullOrEmpty(authoredName)) return false;
            string normalized = importedName;
            // Unity appends .001, .002, ... when an FBX take collides with an existing
            // imported take. Treat that suffix as an importer detail rather than a clip name.
            if (normalized.Length > 4 && normalized[normalized.Length - 4] == '.')
            {
                bool numeric = true;
                for (int i = normalized.Length - 3; i < normalized.Length; i++)
                    numeric &= normalized[i] >= '0' && normalized[i] <= '9';
                if (numeric) normalized = normalized.Substring(0, normalized.Length - 4);
            }

            if (normalized == authoredName) return true;
            return normalized.EndsWith("|" + authoredName, System.StringComparison.Ordinal)
                || normalized.EndsWith("/" + authoredName, System.StringComparison.Ordinal)
                || normalized.EndsWith("_" + authoredName, System.StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------- zombie

        static void BuildZombiePrefab()
        {
            var mat = LoadMaterial("M_Zombie");

            var zombie = new GameObject("Zombie");
            zombie.transform.position = Vector3.zero;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(zombie.transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.95f, 0.9f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            // Snout marks facing, so you can read which way a zombie is lunging.
            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(zombie.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.35f, 0.45f);
            snout.transform.localScale = new Vector3(0.3f, 0.3f, 0.4f);
            snout.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(snout.GetComponent<BoxCollider>());

            var controller = zombie.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.42f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.35f;

            var health = zombie.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 100f);

            var ai = zombie.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                f.F("moveSpeed", 2.6f).F("turnSpeed", 360f)
                 .F("separationRadius", 1.1f).F("separationStrength", 2.2f)
                 .F("attackRange", 1.6f).F("attackDamage", 12f).F("attackCooldown", 1.1f)
                 .I("scoreValue", 10).I("goldReward", 10)
                 .F("knockbackForce", 4f).F("knockbackDecay", 14f)
                 .F("deathLinger", 0.22f)
                 .Obj("deathClip", LoadClip("SFX_Death"))
                 .F("deathVolume", 0.55f).F("killTrauma", 0.22f);
            }

            var pop = zombie.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
            {
                // body left null - DeathPop falls back to its own transform, scaling the
                // whole zombie including the snout.
                f.Obj("health", health).F("duration", 0.2f)
                 .F("squash", 1.5f).F("spinDegrees", 110f);
            }

            var flash = zombie.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
            {
                f.Obj("health", health)
                 .Col("flashColor", Color.white)
                 .F("duration", 0.06f);
            }

            // Replace the prototype primitives with the authored, skinned normal zombie.
            // Keep the gameplay root and its existing components in place: WaveManager pools
            // this object, ZombieAI owns movement, and the imported Animator owns only visuals.
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NormalZombieModelPath);
            if (modelAsset != null)
            {
                // The capsule/cube above are only a fallback prototype. Remove them before
                // saving the authored skinned model so the prefab has one visible character.
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(snout);

                var model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (model != null)
                {
                    model.name = "NormalZombie_Model";
                    model.transform.SetParent(zombie.transform, false);

                    // Feet to the floor, not to the pivot. The model is authored standing on
                    // zero, but a CharacterController is centred on its pivot, so parenting
                    // at local zero leaves the zombie hovering at waist height - half its
                    // controller height off the ground.
                    model.transform.localPosition = new Vector3(0f, -controller.height * 0.5f, 0f);
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    var animator = model.GetComponent<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    ConfigureNormalZombieImportSettings();
                    animator.runtimeAnimatorController = LoadOrCreateNormalZombieController();
                    animator.applyRootMotion = false;

                    TuneForHorde(model);

                    var driver = zombie.AddComponent<ZombieAnimator>();
                    using (var f = new Fields(driver))
                    {
                        f.Obj("animator", animator)
                         .F("attackDuration", 1.27f)
                         .F("getShotDuration", 0.57f)
                         .F("staggerDuration", 1.5f)
                         .F("deathPlaybackSpeed", 2.5f)
                         .F("heavyHealthFraction", 0.4f).F("heavyKnockback", 2.5f)
                         .F("lightReactionInterval", 1.2f).F("heavyReactionInterval", 4f)
                         .B("stopDuringStagger", true);
                    }

                    // The model's authored death is 1.8s; play it at 2.5x during the existing
                    // prototype linger so it completes without holding a dead horde slot.
                    using (var f = new Fields(ai)) f.F("deathLinger", 0.75f);
                }
            }
            else
            {
                Debug.LogWarning($"ArenaBuilder: no authored normal zombie model at {NormalZombieModelPath}; keeping greybox visuals.");
            }

            PrefabUtility.SaveAsPrefabAsset(zombie, ZombiePrefabPath);
            Object.DestroyImmediate(zombie);
        }

        /// <summary>
        /// Trims the per-instance cost of a character that appears sixty times at once.
        /// <para>
        /// These are worth more than the polygon budget. Skinned meshes do not batch and the
        /// GPU Resident Drawer does not touch them, so every horde member is its own draw
        /// call and its own skinning pass - and a shadow-casting one pays both twice.
        /// </para>
        /// </summary>
        static void TuneForHorde(GameObject model)
        {
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Individual shadows from a horde read as noise from 23m up, and cost a
                // second skinning pass plus a second draw each. The arena's own geometry
                // still casts, so the scene keeps its grounding.
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skin.receiveShadows = true;

                // Two influences rather than four. On a rig this chunky the difference is
                // invisible and it halves the skinning maths.
                skin.quality = SkinQuality.Bone2;

                // The bounds are authored, so there is no reason to recompute them per frame.
                skin.updateWhenOffscreen = false;
                skin.skinnedMotionVectors = false;
            }

            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                // Off-screen zombies keep their state machine ticking - they are still
                // walking toward the player and must arrive in the right place - but stop
                // writing transforms nothing can see.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        static void ConfigureNormalZombieImportSettings()
        {
            var importer = AssetImporter.GetAtPath(NormalZombieModelPath) as ModelImporter;
            if (importer == null) return;

            // Start from the takes Blender currently exports. Keeping a prior custom
            // clipAnimations array lets removed takes survive in the .meta file, which can
            // make a fresh checkout import a different controller than the editor cache.
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            var existing = importer.clipAnimations;
            if (existing == null || existing.Length != clips.Length)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!string.Equals(existing[i].name, clips[i].name) ||
                        !string.Equals(existing[i].takeName, clips[i].takeName))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = clips[i].name == "Chase";
                if (clips[i].loopTime == shouldLoop) continue;
                clips[i].loopTime = shouldLoop;
                changed = true;
            }

            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static RuntimeAnimatorController LoadOrCreateNormalZombieController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(NormalZombieControllerPath);
            if (controller == null)
            {
                EnsureFolder(Root + "/Art");
                EnsureFolder(Root + "/Art/Characters");
                EnsureFolder(Root + "/Art/Characters/NormalZombie");
                controller = AnimatorController.CreateAnimatorControllerAtPath(NormalZombieControllerPath);
            }

            var clips = AbilityAnimationSetup.WithAbilities(NormalZombieModelPath, AbilityAnimationSetup.ZombiePack);
            var machine = controller.layers[0].stateMachine;
            var names = new[] { "Chase", "Attack", "GetShot", "Stagger", "Death", "Scream", "GetUp", "Leap" };

            for (int i = 0; i < names.Length; i++)
            {
                AnimationClip clip = null;
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c] is AnimationClip candidate && candidate.name == names[i])
                    {
                        clip = candidate;
                        break;
                    }
                }
                if (clip == null) continue;

                AnimatorState state = null;
                for (int s = 0; s < machine.states.Length; s++)
                {
                    if (machine.states[s].state.name == names[i])
                    {
                        state = machine.states[s].state;
                        break;
                    }
                }

                if (state == null) state = machine.AddState(names[i]);
                state.motion = clip;
                if (names[i] == "Chase") machine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void BuildBrutePrefab()
        {
            var mat = LoadMaterial("M_Brute");

            var brute = new GameObject("Brute");
            brute.transform.position = Vector3.zero;

            // 1.4x scale over standard zombie: wider and taller silhouette that reads instantly.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(brute.transform, false);
            body.transform.localScale = new Vector3(1.26f, 1.33f, 1.26f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(brute.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.49f, 0.63f);
            snout.transform.localScale = new Vector3(0.42f, 0.42f, 0.56f);
            snout.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(snout.GetComponent<BoxCollider>());

            var controller = brute.AddComponent<CharacterController>();
            controller.height = 2.5f;
            controller.radius = 0.58f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.4f;

            var health = brute.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 300f);

            var ai = brute.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                // Slower movement, heavy knockback resistance (1.2 vs 4.0), and hard-hitting melee.
                f.F("moveSpeed", 1.6f).F("turnSpeed", 240f)
                 .F("separationRadius", 1.5f).F("separationStrength", 3.2f)
                 .F("attackRange", 1.9f).F("attackDamage", 25f).F("attackCooldown", 1.4f)
                 .I("scoreValue", 30).I("goldReward", 40)
                 .F("knockbackForce", 1.2f).F("knockbackDecay", 16f)
                 .F("deathLinger", 0.25f)
                 .Obj("deathClip", LoadClip("SFX_Death"))
                 .F("deathVolume", 0.75f).F("killTrauma", 0.40f);
            }

            var pop = brute.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
            {
                f.Obj("health", health).F("duration", 0.25f)
                 .F("squash", 1.5f).F("spinDegrees", 90f);
            }

            var flash = brute.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
            {
                f.Obj("health", health)
                 .Col("flashColor", Color.white)
                 .F("duration", 0.06f);
            }

            PrefabUtility.SaveAsPrefabAsset(brute, BrutePrefabPath);
            Object.DestroyImmediate(brute);
        }

        static void BuildBossPrefab()
        {
            var mat = LoadMaterial("M_Boss");

            var boss = new GameObject("Boss");
            boss.transform.position = Vector3.zero;

            // 2.2x the standard zombie. It has to be unmistakable at a glance while forty
            // other bodies are on screen - this arrives during wave 15, not instead of it.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(boss.transform, false);
            body.transform.localScale = new Vector3(1.98f, 2.09f, 1.98f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(boss.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.77f, 0.99f);
            snout.transform.localScale = new Vector3(0.66f, 0.66f, 0.88f);
            snout.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(snout.GetComponent<BoxCollider>());

            var controller = boss.AddComponent<CharacterController>();
            controller.height = 3.9f;
            controller.radius = 0.92f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.5f;

            var health = boss.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 2000f);

            var shootPoint = new GameObject("ShootPoint").transform;
            shootPoint.SetParent(boss.transform, false);
            shootPoint.localPosition = new Vector3(0f, 0.8f, 1.3f);

            var ai = boss.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                // Starts ranged; BossController flips it to a charger at half health.
                // knockbackForce 0.35 against a standard 4.0 - it is barely movable, so the
                // shotgun cannot simply hold it at arm's length the way it does a Brute.
                f.F("moveSpeed", 1.7f).F("turnSpeed", 200f)
                 .F("separationRadius", 2.1f).F("separationStrength", 4f)
                 .F("attackRange", 2.8f).F("attackDamage", 22f).F("attackCooldown", 2.2f)
                 .I("scoreValue", 500).I("goldReward", 250)
                 .F("knockbackForce", 0.35f).F("knockbackDecay", 18f)
                 .F("deathLinger", 0.6f)
                 .Obj("deathClip", LoadClip("SFX_Death"))
                 .F("deathVolume", 0.9f).F("killTrauma", 1f)
                 .B("isRanged", true).F("preferredRange", 14f)
                 .Obj("projectilePrefab", LoadProjectilePrefab())
                 .Obj("shootPoint", shootPoint)
                 .Obj("shootClip", LoadClip("SFX_Gunshot"))
                 .F("shootVolume", 0.6f);
            }

            var bossController = boss.AddComponent<BossController>();
            using (var f = new Fields(bossController))
            {
                f.F("phaseOneMoveSpeed", 1.7f).F("phaseOneAttackCooldown", 2.2f)
                 .F("phaseTwoAt", 0.5f)
                 .F("phaseTwoMoveSpeed", 5f).F("phaseTwoAttackDamage", 35f)
                 .F("phaseTwoAttackCooldown", 0.9f)
                 // 35 x 10 clears a 150 HP barricade in one blow. If it took two, the phase
                 // would not read as "walls stopped working".
                 .F("phaseTwoBarricadeMultiplier", 10f)
                 .Obj("phaseChangeClip", LoadClip("SFX_Barricade_Break"))
                 .F("phaseChangeVolume", 0.9f).F("phaseChangeTrauma", 0.75f)
                 .F("phaseChangeFreeze", 0.12f);
            }

            var pop = boss.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
            {
                f.Obj("health", health).F("duration", 0.55f)
                 .F("squash", 1.7f).F("spinDegrees", 200f);
            }

            var flash = boss.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
            {
                f.Obj("health", health).Col("flashColor", Color.white).F("duration", 0.06f);
            }

            PrefabUtility.SaveAsPrefabAsset(boss, BossPrefabPath);
            Object.DestroyImmediate(boss);
        }

        static ZombieAI LoadBossPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {BossPrefabPath}.");
                return null;
            }
            return go.GetComponent<ZombieAI>();
        }

        static void BuildBarrelPrefab()
        {
            var mat = LoadMaterial("M_Barrel");
            var sparkMat = LoadMaterial("M_Spark");

            var go = new GameObject("ExplosiveBarrel");

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(go.transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(visuals.transform, false);
            body.transform.localScale = new Vector3(0.85f, 0.62f, 0.85f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.24f;
            col.radius = 0.43f;

            var blast = CreateBurstSystem(go.transform, "Blast", sparkMat,
                new Color(1f, 0.6f, 0.15f), 6f, 16f, 0.25f, 0.6f, 0.12f, 0.3f, 65f, 1.2f);

            var barrel = go.AddComponent<ExplosiveBarrel>();
            using (var f = new Fields(barrel))
            {
                f.F("maxHealth", 30f)
                 .F("blastRadius", 6f).F("centreDamage", 220f).F("edgeDamageFraction", 0.25f)
                 .F("knockbackMultiplier", 4f)
                 .B("damagesPlayer", true).B("damagesStructures", false)
                 .F("chainDelay", 0.08f)
                 .Obj("visualRoot", visuals).Obj("blastParticles", blast)
                 .Obj("explodeClip", LoadClip("SFX_Barricade_Break"))
                 .F("explodeVolume", 0.85f).F("blastTrauma", 0.7f).F("despawnDelay", 1.4f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, BarrelPrefabPath);
            Object.DestroyImmediate(go);
        }

        static void BuildClaymorePrefab()
        {
            var mat = LoadMaterial("M_Claymore");
            var lightMat = LoadMaterial("M_Claymore_Light");
            var sparkMat = LoadMaterial("M_Spark");

            var go = new GameObject("Claymore");

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(go.transform, false);

            // A low wedge that clearly points somewhere - the facing is the whole mechanic.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(visuals.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.34f, 0.18f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "ArmedIndicator";
            indicator.transform.SetParent(visuals.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            indicator.transform.localScale = new Vector3(0.16f, 0.1f, 0.16f);
            var indicatorRenderer = indicator.GetComponent<MeshRenderer>();
            indicatorRenderer.sharedMaterial = lightMat;
            Object.DestroyImmediate(indicator.GetComponent<BoxCollider>());

            var blast = CreateBurstSystem(go.transform, "Blast", sparkMat,
                new Color(1f, 0.75f, 0.3f), 5f, 13f, 0.2f, 0.45f, 0.07f, 0.18f, 45f, 1.4f);

            var claymore = go.AddComponent<Claymore>();
            using (var f = new Fields(claymore))
            {
                f.F("armTime", 0.7f).F("triggerRadius", 4.5f).F("coneHalfAngle", 55f)
                 .F("damage", 160f).F("blastRange", 9f).F("knockbackMultiplier", 3f)
                 .B("damagesPlayer", false)
                 .Obj("visualRoot", visuals).Obj("armedIndicator", indicatorRenderer)
                 .Col("disarmedColor", new Color(0.35f, 0.35f, 0.38f))
                 .Col("armedColor", new Color(0.95f, 0.25f, 0.20f))
                 .Obj("blastParticles", blast)
                 .Obj("detonateClip", LoadClip("SFX_Barricade_Break"))
                 .F("detonateVolume", 0.8f).F("blastTrauma", 0.45f).F("despawnDelay", 1.2f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, ClaymorePrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// The deployable catalogue, created once then left to hand-tuning - the same
        /// create-if-missing contract as weapons and prices.
        /// </summary>
        static Object[] LoadOrCreateDeployables()
        {
            var barricade = LoadOrCreateDeployable("DEP_Barricade", d => d
                .Str("displayName", "Barricade")
                .Obj("prefab", AssetDatabase.LoadAssetAtPath<GameObject>(BarricadePrefabPath))
                .I("cost", 40).F("gridSnap", 1f).B("rotatable", true)
                .F("footprint", 0.9f).F("maxPlacementRange", 4.2f)
                .V3("ghostSize", new Vector3(1.6f, 0.95f, 0.6f)).F("placementHeight", 0.5f)
                .Obj("placeClip", LoadClip("SFX_Barricade_Place")).F("placeVolume", 0.55f));

            var barrel = LoadOrCreateDeployable("DEP_Barrel", d => d
                .Str("displayName", "Explosive Barrel")
                .Obj("prefab", AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath))
                .I("cost", 60).F("gridSnap", 1f).B("rotatable", false)
                .F("footprint", 0.5f).F("maxPlacementRange", 4.2f)
                .V3("ghostSize", new Vector3(0.9f, 1.24f, 0.9f)).F("placementHeight", 0.62f)
                .Obj("placeClip", LoadClip("SFX_Barricade_Place")).F("placeVolume", 0.5f));

            var claymore = LoadOrCreateDeployable("DEP_Claymore", d => d
                .Str("displayName", "Claymore")
                .Obj("prefab", AssetDatabase.LoadAssetAtPath<GameObject>(ClaymorePrefabPath))
                .I("cost", 75).F("gridSnap", 1f).B("rotatable", true)
                .F("footprint", 0.4f).F("maxPlacementRange", 4.2f)
                .V3("ghostSize", new Vector3(0.7f, 0.34f, 0.25f)).F("placementHeight", 0.2f)
                .Obj("placeClip", LoadClip("SFX_Barricade_Place")).F("placeVolume", 0.5f));

            return new Object[] { barricade, barrel, claymore };
        }

        static DeployableDefinition LoadOrCreateDeployable(string fileName, System.Action<Fields> configure)
        {
            string path = $"{DeployableDir}/{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<DeployableDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DeployableDir);

            var asset = ScriptableObject.CreateInstance<DeployableDefinition>();
            using (var f = new Fields(asset)) configure(f);

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void BuildProjectilePrefab()
        {
            var mat = LoadMaterial("M_Projectile");

            var go = new GameObject("EnemyProjectile");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Model";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.45f;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.95f, 0.35f, 1.0f);
            light.range = 3.5f;
            light.intensity = 4.0f;
            light.shadows = LightShadows.None;

            var proj = go.AddComponent<EnemyProjectile>();
            using (var f = new Fields(proj))
            {
                f.F("speed", 12f).F("maxLifetime", 4.5f).F("radius", 0.25f)
                 .Obj("impactClip", LoadClip("SFX_Impact")).F("impactVolume", 0.45f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// The player's projectile - grenades and nails. Built now so the delivery has
        /// something to spawn; nothing references it until a weapon that lobs exists.
        /// </summary>
        static void BuildWeaponProjectilePrefab()
        {
            var mat = LoadMaterial("M_Brass");

            var go = new GameObject("WeaponProjectile");
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Model";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one * 0.3f;
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<SphereCollider>());

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.3f);
            light.range = 3f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;

            var projectile = go.AddComponent<WeaponProjectile>();
            using (var f = new Fields(projectile))
            {
                f.F("speed", 26f).F("gravity", 9f).F("maxLifetime", 4f).F("radius", 0.22f)
                 .Obj("impactClip", LoadClip("SFX_Impact")).F("impactVolume", 0.5f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, WeaponProjectilePrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// The parts every melee archetype shares. A new one is this call plus whatever
        /// component makes it different, rather than sixty lines copied and edited.
        /// </summary>
        /// <summary>
        /// Hit tests start at the player's centre and travel flat. Anything whose collider
        /// tops out below this is unhittable by every hitscan weapon in the game.
        /// </summary>
        const float PlayerRayHeight = 1.1f;

        static (GameObject go, Health health, ZombieAI ai) BuildMeleeArchetype(
            string name, Material mat, Vector3 bodyScale, float controllerHeight,
            float controllerRadius, float maxHealth, System.Action<Fields> configureAi,
            float bodyOffsetY = 0f)
        {
            // The controller centre sits on the pivot, so a grounded enemy occupies 0 to
            // controllerHeight. Caught the hard way: the crawler was 0.9 tall and every
            // bullet in the game passed over it.
            if (controllerHeight <= PlayerRayHeight)
            {
                Debug.LogError($"ArenaBuilder: '{name}' has a {controllerHeight}m collider, " +
                               $"which tops out below the {PlayerRayHeight}m hit ray - no " +
                               "hitscan weapon can touch it. Raise the collider and offset " +
                               "the body down if it should still look small.");
            }

            var go = new GameObject(name);
            go.transform.position = Vector3.zero;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = bodyScale;
            body.transform.localPosition = new Vector3(0f, bodyOffsetY, 0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var controller = go.AddComponent<CharacterController>();
            controller.height = controllerHeight;
            controller.radius = controllerRadius;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.35f;

            var health = go.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", maxHealth);

            var ai = go.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                f.Obj("deathClip", LoadClip("SFX_Death")).F("deathVolume", 0.45f);
                configureAi(f);
            }

            var pop = go.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
                f.Obj("health", health).F("duration", 0.18f).F("squash", 1.5f).F("spinDegrees", 120f);

            var flash = go.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
                f.Obj("health", health).Col("flashColor", Color.white).F("duration", 0.06f);

            return (go, health, ai);
        }

        /// <summary>
        /// Slow, fat, and lethal to stand next to when it dies. Exists to punish the
        /// close-range weapons - shotgun, flamethrower - that answer everything else.
        /// </summary>
        static void BuildBloaterPrefab()
        {
            var (go, health, _) = BuildMeleeArchetype(
                "Bloater", LoadMaterial("M_Brute"),
                new Vector3(1.25f, 1.05f, 1.25f), 2.1f, 0.62f, 180f,
                f => f.F("moveSpeed", 1.5f).F("turnSpeed", 220f)
                      .F("separationRadius", 1.4f).F("separationStrength", 2.0f)
                      .F("attackRange", 1.8f).F("attackDamage", 10f).F("attackCooldown", 1.3f)
                      .I("scoreValue", 25).I("goldReward", 25)
                      .F("knockbackForce", 2.2f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.1f).F("killTrauma", 0.2f));

            var burst = go.AddComponent<ExplodeOnDeath>();
            using (var f = new Fields(burst))
            {
                f.F("radius", 4.5f).F("damage", 34f).F("edgeFalloff", 0.35f).F("trauma", 0.5f)
                 .Obj("blastClip", LoadClip("SFX_Impact")).F("blastVolume", 0.7f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, BloaterPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Plated at the front. Breaks the hold-the-trigger-and-back-away pattern that works
        /// on everything else: it has to be flanked, pierced, or hit with a blast.
        /// </summary>
        static void BuildArmoredPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Armored", LoadMaterial("M_Wall"),
                new Vector3(1.0f, 1.0f, 1.0f), 1.9f, 0.46f, 140f,
                f => f.F("moveSpeed", 2.1f).F("turnSpeed", 200f)
                      .F("separationRadius", 1.1f).F("separationStrength", 2.2f)
                      .F("attackRange", 1.6f).F("attackDamage", 14f).F("attackCooldown", 1.2f)
                      .I("scoreValue", 30).I("goldReward", 30)
                      .F("knockbackForce", 1.6f).F("knockbackDecay", 16f)
                      .F("deathLinger", 0.2f).F("killTrauma", 0.16f));

            // A plate you can see, so "shoot it from the side" is readable rather than a
            // number the player has to infer from damage they cannot feel.
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(go.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.1f, 0.42f);
            plate.transform.localScale = new Vector3(0.95f, 1.1f, 0.18f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial("M_Gun");
            Object.DestroyImmediate(plate.GetComponent<BoxCollider>());

            var armor = go.AddComponent<FrontalArmor>();
            using (var f = new Fields(armor))
                f.F("frontalMultiplier", 0.25f).F("arcHalfAngle", 70f);

            PrefabUtility.SaveAsPrefabAsset(go, ArmoredPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Goes for the walls, not for you. Makes a barricade something to defend rather than
        /// something to hide behind.
        /// </summary>
        static void BuildSapperPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Sapper", LoadMaterial("M_Ranged"),
                new Vector3(0.85f, 0.9f, 0.85f), 1.7f, 0.38f, 70f,
                f => f.F("moveSpeed", 3.4f).F("turnSpeed", 420f)
                      .F("separationRadius", 0.95f).F("separationStrength", 2.0f)
                      .F("attackRange", 1.4f).F("attackDamage", 7f).F("attackCooldown", 0.9f)
                      // Four times as dangerous to a wall as to the player: 150 HP of
                      // barricade falls in about five swings rather than twenty.
                      .F("barricadeDamageMultiplier", 4f)
                      .I("scoreValue", 20).I("goldReward", 20)
                      .F("knockbackForce", 5f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.18f).F("killTrauma", 0.12f));

            var sapper = go.AddComponent<BarricadeSapper>();
            using (var f = new Fields(sapper))
                f.F("searchRadius", 26f).F("searchInterval", 0.6f);

            PrefabUtility.SaveAsPrefabAsset(go, SapperPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>Vaults walls. A barricade buys time against this one, never safety.</summary>
        static void BuildLeaperPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Leaper", LoadMaterial("M_Runner"),
                new Vector3(0.78f, 0.86f, 0.78f), 1.7f, 0.36f, 60f,
                f => f.F("moveSpeed", 3.8f).F("turnSpeed", 460f)
                      .F("separationRadius", 0.95f).F("separationStrength", 2.0f)
                      .F("attackRange", 1.4f).F("attackDamage", 9f).F("attackCooldown", 0.85f)
                      .I("scoreValue", 22).I("goldReward", 20)
                      .F("knockbackForce", 5.2f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.18f).F("killTrauma", 0.12f));

            var legs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legs.name = "Haunches";
            legs.transform.SetParent(go.transform, false);
            legs.transform.localPosition = new Vector3(0f, -0.35f, -0.18f);
            legs.transform.localScale = new Vector3(0.6f, 0.3f, 0.5f);
            legs.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial("M_Runner");
            Object.DestroyImmediate(legs.GetComponent<BoxCollider>());

            var leap = go.AddComponent<BarricadeLeaper>();
            using (var f = new Fields(leap))
            {
                f.F("triggerRange", 3f).F("overshoot", 2.2f)
                 .F("leapSeconds", 0.65f).F("leapHeight", 2.6f).F("cooldown", 2.5f);
            }

            AbilityAnimationSetup.InstallOnEnemy(go);
            PrefabUtility.SaveAsPrefabAsset(go, LeaperPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>Drives the pack faster while it lives. A target you have to pick out.</summary>
        static void BuildScreamerPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Screamer", LoadMaterial("M_Ranged"),
                new Vector3(0.9f, 1.15f, 0.9f), 2.0f, 0.4f, 55f,
                f => f.F("moveSpeed", 2.0f).F("turnSpeed", 300f)
                      .F("separationRadius", 1.1f).F("separationStrength", 2.4f)
                      .F("attackRange", 1.5f).F("attackDamage", 5f).F("attackCooldown", 1.4f)
                      .I("scoreValue", 35).I("goldReward", 35)
                      .F("knockbackForce", 5.5f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.16f).F("killTrauma", 0.14f));

            // Deliberately tall and thin. It has to be pickable out of a crowd at a glance,
            // or "kill that one first" is not a decision the player can actually act on.
            var horn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horn.name = "Maw";
            horn.transform.SetParent(go.transform, false);
            horn.transform.localPosition = new Vector3(0f, 0.62f, 0.12f);
            horn.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            horn.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial("M_Placement_Invalid");
            Object.DestroyImmediate(horn.GetComponent<BoxCollider>());

            var aura = go.AddComponent<HordeAura>();
            using (var f = new Fields(aura))
                f.F("radius", 12f).F("speedMultiplier", 1.5f).F("tickInterval", 0.25f);

            AbilityAnimationSetup.InstallOnEnemy(go);
            PrefabUtility.SaveAsPrefabAsset(go, ScreamerPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>Gets up once. Punishes the habit every other archetype teaches.</summary>
        static void BuildRevenantPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Revenant", LoadMaterial("M_Zombie"),
                new Vector3(0.95f, 1.0f, 0.95f), 1.9f, 0.44f, 90f,
                f => f.F("moveSpeed", 2.4f).F("turnSpeed", 340f)
                      .F("separationRadius", 1.1f).F("separationStrength", 2.2f)
                      .F("attackRange", 1.6f).F("attackDamage", 11f).F("attackCooldown", 1.1f)
                      .I("scoreValue", 28).I("goldReward", 28)
                      .F("knockbackForce", 4f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.2f).F("killTrauma", 0.14f));

            var revenant = go.AddComponent<Revenant>();
            using (var f = new Fields(revenant))
            {
                f.F("reviveFraction", 0.45f).I("revivesPerLife", 1)
                 .Col("revivedTint", new Color(0.85f, 0.25f, 0.75f));
            }

            AbilityAnimationSetup.InstallOnEnemy(go);
            PrefabUtility.SaveAsPrefabAsset(go, RevenantPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>Arrives as a pack. Stresses crowd clear and the flow field at once.</summary>
        static void BuildCrawlerPrefab()
        {
            var (go, _, _) = BuildMeleeArchetype(
                "Crawler", LoadMaterial("M_Runner"),
                new Vector3(0.5f, 0.4f, 0.5f), 1.6f, 0.34f, 22f,
                f => f.F("moveSpeed", 5.4f).F("turnSpeed", 560f)
                      .F("separationRadius", 0.55f).F("separationStrength", 1.4f)
                      .F("attackRange", 1.1f).F("attackDamage", 4f).F("attackCooldown", 0.6f)
                      .I("scoreValue", 8).I("goldReward", 6)
                      .F("knockbackForce", 7.5f).F("knockbackDecay", 15f)
                      .F("deathLinger", 0.1f).F("killTrauma", 0.06f),
                // The collider is deliberately taller than the model. It has to reach the
                // hit ray, and the body is dropped to the floor so it still LOOKS knee-high -
                // a generous hitbox on something this small and this fast is invisible to
                // the player and the only way it is fair to shoot at.
                bodyOffsetY: -0.4f);

            PrefabUtility.SaveAsPrefabAsset(go, CrawlerPrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// The acid pool and the projectile that leaves it. Built together because the
        /// projectile is useless without the thing it drops.
        /// </summary>
        static void BuildAcidPrefabs()
        {
            var mat = LoadMaterial("M_Placement_Invalid");

            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "AcidPool";
            pool.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(pool.GetComponent<CapsuleCollider>());

            var acid = pool.AddComponent<AcidPool>();
            using (var f = new Fields(acid))
            {
                f.F("radius", 2.6f).F("damagePerSecond", 14f)
                 .F("lifetime", 6f).F("tickInterval", 0.5f);
            }

            PrefabUtility.SaveAsPrefabAsset(pool, AcidPoolPrefabPath);
            Object.DestroyImmediate(pool);

            var go = new GameObject("AcidProjectile");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Model";
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.4f;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            var projectile = go.AddComponent<EnemyProjectile>();
            using (var f = new Fields(projectile))
            {
                f.F("speed", 11f).F("maxLifetime", 4.5f).F("radius", 0.28f)
                 .Obj("impactSpawn", AssetDatabase.LoadAssetAtPath<GameObject>(AcidPoolPrefabPath))
                 .Obj("impactClip", LoadClip("SFX_Impact")).F("impactVolume", 0.4f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, AcidProjectilePrefabPath);
            Object.DestroyImmediate(go);
        }

        /// <summary>Denies ground. The answer to holding one spot behind a wall.</summary>
        static void BuildSpitterPrefab()
        {
            var (go, _, ai) = BuildMeleeArchetype(
                "Spitter", LoadMaterial("M_Ranged"),
                new Vector3(0.92f, 0.95f, 0.92f), 1.8f, 0.42f, 65f,
                // attackRange gates the ranged attack too, so it has to exceed preferredRange
                // or the spitter would walk to 15m, stop, and never fire. attackDamage IS the
                // projectile's damage - there is no separate field.
                f => f.F("moveSpeed", 1.9f).F("turnSpeed", 320f)
                      .F("separationRadius", 1.1f).F("separationStrength", 2.2f)
                      .F("attackRange", 17f).F("attackDamage", 9f).F("attackCooldown", 2.6f)
                      .I("scoreValue", 30).I("goldReward", 30)
                      .F("knockbackForce", 5f).F("knockbackDecay", 14f)
                      .F("deathLinger", 0.18f).F("killTrauma", 0.14f));

            // Reuses the ranged behaviour wholesale - it keeps its distance and lobs. The
            // only difference from a Ranged zombie is what its projectile leaves behind.
            using (var f = new Fields(ai))
            {
                f.B("isRanged", true).F("preferredRange", 15f)
                 .Obj("projectilePrefab",
                      AssetDatabase.LoadAssetAtPath<GameObject>(AcidProjectilePrefabPath)
                          ?.GetComponent<EnemyProjectile>())
                 .Obj("shootClip", LoadClip("SFX_Impact")).F("shootVolume", 0.4f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, SpitterPrefabPath);
            Object.DestroyImmediate(go);
        }

        static void BuildRunnerPrefab()
        {
            var mat = LoadMaterial("M_Runner");

            var runner = new GameObject("Runner");
            runner.transform.position = Vector3.zero;

            // 0.75x scale: smaller, faster silhouette that swarms quickly.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(runner.transform, false);
            body.transform.localScale = new Vector3(0.68f, 0.72f, 0.68f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(runner.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.26f, 0.34f);
            snout.transform.localScale = new Vector3(0.23f, 0.23f, 0.30f);
            snout.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(snout.GetComponent<BoxCollider>());

            var controller = runner.AddComponent<CharacterController>();
            controller.height = 1.5f;
            controller.radius = 0.32f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.3f;

            var health = runner.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 40f);

            var ai = runner.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                // High speed (4.5), low health (40), and high knockback vulnerability (6.0).
                f.F("moveSpeed", 4.5f).F("turnSpeed", 480f)
                 .F("separationRadius", 0.9f).F("separationStrength", 2.0f)
                 .F("attackRange", 1.3f).F("attackDamage", 6f).F("attackCooldown", 0.75f)
                 .I("scoreValue", 15).I("goldReward", 15)
                 .F("knockbackForce", 6.0f).F("knockbackDecay", 14f)
                 .F("deathLinger", 0.18f)
                 .Obj("deathClip", LoadClip("SFX_Death"))
                 .F("deathVolume", 0.45f).F("killTrauma", 0.12f);
            }

            var pop = runner.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
            {
                f.Obj("health", health).F("duration", 0.16f)
                 .F("squash", 1.6f).F("spinDegrees", 140f);
            }

            var flash = runner.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
            {
                f.Obj("health", health)
                 .Col("flashColor", Color.white)
                 .F("duration", 0.06f);
            }

            PrefabUtility.SaveAsPrefabAsset(runner, RunnerPrefabPath);
            Object.DestroyImmediate(runner);
        }

        static void BuildRangedPrefab()
        {
            var mat = LoadMaterial("M_Ranged");

            var ranged = new GameObject("RangedZombie");
            ranged.transform.position = Vector3.zero;

            // 0.9x scale: purple, lean, stays back and fires projectiles.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(ranged.transform, false);
            body.transform.localScale = new Vector3(0.82f, 0.86f, 0.82f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(ranged.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.32f, 0.41f);
            snout.transform.localScale = new Vector3(0.28f, 0.28f, 0.36f);
            snout.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(snout.GetComponent<BoxCollider>());

            var shootPoint = new GameObject("ShootPoint").transform;
            shootPoint.SetParent(ranged.transform, false);
            shootPoint.localPosition = new Vector3(0f, 0.35f, 0.70f);

            var controller = ranged.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.38f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.35f;

            var health = ranged.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 60f);

            var ai = ranged.AddComponent<ZombieAI>();
            using (var f = new Fields(ai))
            {
                // Slower speed (1.8), preferred range (11.0), and ranged projectile attack.
                f.F("moveSpeed", 1.8f).F("turnSpeed", 320f)
                 .F("separationRadius", 1.4f).F("separationStrength", 2.5f)
                 .F("attackRange", 13.5f).F("attackDamage", 15f).F("attackCooldown", 2.2f)
                 .I("scoreValue", 25).I("goldReward", 25)
                 .F("knockbackForce", 3.5f).F("knockbackDecay", 14f)
                 .F("deathLinger", 0.20f)
                 .Obj("deathClip", LoadClip("SFX_Death"))
                 .F("deathVolume", 0.5f).F("killTrauma", 0.20f)
                 .B("isRanged", true)
                 .Obj("projectilePrefab", LoadProjectilePrefab())
                 .F("preferredRange", 11.0f)
                 .Obj("shootPoint", shootPoint)
                 .Obj("shootClip", LoadClip("SFX_Gunshot"))
                 .F("shootVolume", 0.4f);
            }

            var pop = ranged.AddComponent<DeathPop>();
            using (var f = new Fields(pop))
            {
                f.Obj("health", health).F("duration", 0.20f)
                 .F("squash", 1.5f).F("spinDegrees", 100f);
            }

            var flash = ranged.AddComponent<HitFlash>();
            using (var f = new Fields(flash))
            {
                f.Obj("health", health)
                 .Col("flashColor", Color.white)
                 .F("duration", 0.06f);
            }

            PrefabUtility.SaveAsPrefabAsset(ranged, RangedPrefabPath);
            Object.DestroyImmediate(ranged);
        }

        static void BuildBarricadePrefab()
        {
            var woodMat = LoadMaterial("M_Barricade_Wood");
            var metalMat = LoadMaterial("M_Barricade_Metal");
            var sparkMat = LoadMaterial("M_Spark");
            var hbBgMat = LoadMaterial("M_HealthBar_Bg");
            var hbFillMat = LoadMaterial("M_HealthBar_Fill");

            var go = new GameObject("Barricade");
            go.transform.position = Vector3.zero;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.6f, 1.0f, 0.6f);
            col.center = new Vector3(0f, 0.5f, 0f);

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(go.transform, false);

            var wood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wood.name = "WoodBody";
            wood.transform.SetParent(visuals.transform, false);
            wood.transform.localPosition = new Vector3(0f, 0.475f, 0f);
            wood.transform.localScale = new Vector3(1.6f, 0.95f, 0.55f);
            wood.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
            Object.DestroyImmediate(wood.GetComponent<Collider>());

            var trim1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim1.name = "MetalTrimBottom";
            trim1.transform.SetParent(visuals.transform, false);
            trim1.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            trim1.transform.localScale = new Vector3(1.62f, 0.14f, 0.57f);
            trim1.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(trim1.GetComponent<Collider>());

            var trim2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim2.name = "MetalTrimTop";
            trim2.transform.SetParent(visuals.transform, false);
            trim2.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            trim2.transform.localScale = new Vector3(1.62f, 0.14f, 0.57f);
            trim2.GetComponent<MeshRenderer>().sharedMaterial = metalMat;
            Object.DestroyImmediate(trim2.GetComponent<Collider>());

            // Floating World-Space Health Bar
            var hbRoot = new GameObject("HealthBar");
            hbRoot.transform.SetParent(go.transform, false);
            hbRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            var hbBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hbBg.name = "Bg";
            hbBg.transform.SetParent(hbRoot.transform, false);
            hbBg.transform.localScale = new Vector3(1.2f, 0.12f, 1f);
            hbBg.GetComponent<MeshRenderer>().sharedMaterial = hbBgMat;
            Object.DestroyImmediate(hbBg.GetComponent<Collider>());

            var hbFillPivot = new GameObject("FillPivot");
            hbFillPivot.transform.SetParent(hbRoot.transform, false);
            hbFillPivot.transform.localPosition = new Vector3(-0.58f, 0f, -0.005f);

            var hbFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hbFill.name = "Fill";
            hbFill.transform.SetParent(hbFillPivot.transform, false);
            hbFill.transform.localPosition = new Vector3(0.58f, 0f, 0f);
            hbFill.transform.localScale = new Vector3(1.16f, 0.08f, 1f);
            hbFill.GetComponent<MeshRenderer>().sharedMaterial = hbFillMat;
            Object.DestroyImmediate(hbFill.GetComponent<Collider>());

            // Splinters burst
            var splinters = CreateBurstSystem(go.transform, "Splinters", sparkMat,
                new Color(0.65f, 0.42f, 0.22f), 2.5f, 6.5f, 0.15f, 0.4f, 0.04f, 0.12f, 35f, 2.5f);

            var barricade = go.AddComponent<Barricade>();
            using (var f = new Fields(barricade))
            {
                f.F("maxHealth", 150f)
                 .Obj("obstacleCollider", col)
                 .Obj("visualRoot", visuals)
                 .Obj("splinterParticles", splinters)
                 .Obj("healthBarRoot", hbRoot.transform)
                 .Obj("healthBarFill", hbFillPivot.transform)
                 .Obj("hitClip", LoadClip("SFX_Barricade_Hit"))
                 .Obj("breakClip", LoadClip("SFX_Barricade_Break"))
                 .F("hitVolume", 0.55f).F("breakVolume", 0.75f);
            }

            // Dormant on every wall. It switches itself on only while a carried weapon's
            // upgrades grant it, so the placer never has to know about upgrades at all.
            var turret = go.AddComponent<BarricadeTurret>();
            using (var f = new Fields(turret))
            {
                f.F("range", 18f).F("rateFraction", 0.34f).F("damageFraction", 1f)
                 .F("tracerWidth", 0.05f).F("tracerDuration", 0.05f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, BarricadePrefabPath);
            Object.DestroyImmediate(go);
        }

        static Barricade LoadBarricadePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Barricade>(BarricadePrefabPath);
            if (prefab == null) Debug.LogError($"ArenaBuilder: no barricade prefab at {BarricadePrefabPath}.");
            return prefab;
        }

        // ---------------------------------------------------------------- managers

        static WaveManager BuildManagers(GameObject player, Material sparkMat)
        {
            var go = new GameObject("--- Systems ---");

            var gm = go.AddComponent<GameManager>();
            using (var f = new Fields(gm)) f.Obj("playerHealth", player.GetComponent<Health>());

            var armory = go.AddComponent<ArmoryManager>();
            using (var f = new Fields(armory))
            {
                f.Obj("prices", LoadOrCreateArmoryPrices());
            }

            go.AddComponent<SystemsCheck>();

            var impactSparks = CreateBurstSystem(go.transform, "ImpactSparks", sparkMat,
                new Color(1f, 0.8f, 0.35f), 3f, 8f, 0.12f, 0.3f, 0.05f, 0.11f, 32f, 1.6f);

            var ambient = go.AddComponent<AmbientBed>();
            using (var f = new Fields(ambient))
            {
                f.Obj("clip", LoadClip("SFX_Ambient"))
                 .F("volume", 0.35f).F("fadeInSeconds", 2.5f);
            }

            var tracers = go.AddComponent<TracerPool>();
            using (var f = new Fields(tracers))
            {
                f.I("lines", 16).Obj("material", LoadMaterial("M_Tracer"));
            }

            var upgrades = go.AddComponent<UpgradeManager>();
            using (var f = new Fields(upgrades))
            {
                f.Arr("trees", LoadOrCreateUpgradeTrees());
            }

            var sfx = go.AddComponent<SfxPlayer>();
            using (var f = new Fields(sfx))
            {
                f.I("voices", 14).F("minDistance", 6f).F("maxDistance", 45f);
            }

            var impacts = go.AddComponent<ImpactEffects>();
            using (var f = new Fields(impacts))
            {
                f.Obj("sparks", impactSparks).I("particlesPerHit", 7);
            }

            var hitStop = go.AddComponent<HitStop>();
            using (var f = new Fields(hitStop))
            {
                f.B("enableHitStop", true).F("killFreeze", 0.05f).F("frozenTimeScale", 0f);
            }

            // Built before the horde so the field exists the first time a zombie steers.
            var flowField = go.AddComponent<FlowField>();
            using (var f = new Fields(flowField))
            {
                f.Obj("target", player.transform)
                 .F("arenaHalfSize", ArenaHalfSize).F("cellSize", 1f).F("probeHeight", 1f)
                 .F("rebuildInterval", 0.2f).F("barricadeCost", 30f);
            }

            var waves = go.AddComponent<WaveManager>();
            using (var f = new Fields(waves))
            {
                f.Obj("player", player.transform)
                 .F("spawnRadius", 38f).F("minDistanceFromPlayer", 14f).F("spawnHeight", 1f)
                 .I("firstWaveCount", 5).F("countGrowth", 2.5f).I("maxAliveAtOnce", 60).I("finalWave", 15)
                 .Obj("bossPrefab", LoadBossPrefab()).F("bossArrivesAt", 0.4f)
                 .F("timeBetweenSpawns", 0.45f);
            }

            // The roster. Entry 0 is the baseline and the fallback; adding a twelfth
            // archetype is a line here and a prefab, with no field and no branch anywhere.
            // The first four reproduce the old hardcoded formula exactly, so pacing that was
            // already play-tested is unchanged.
            WriteSpawnTable(waves, new[]
            {
                (LoadEnemy(ZombiePrefabPath),   1, 10f, 0f,   10f),
                (LoadEnemy(BrutePrefabPath),    2, 1.2f, 0.7f, 4.5f),
                (LoadEnemy(RunnerPrefabPath),   3, 1.8f, 0.8f, 5.0f),
                (LoadEnemy(RangedPrefabPath),   4, 1.4f, 0.6f, 4.0f),
                (LoadEnemy(SapperPrefabPath),   4, 1.0f, 0.6f, 3.5f),
                (LoadEnemy(BloaterPrefabPath),  5, 1.0f, 0.5f, 3.5f),
                (LoadEnemy(ArmoredPrefabPath),  6, 0.9f, 0.5f, 3.2f),
                (LoadEnemy(LeaperPrefabPath),   5, 1.0f, 0.5f, 3.4f),
                (LoadEnemy(ScreamerPrefabPath), 6, 0.7f, 0.3f, 2.0f),
                (LoadEnemy(RevenantPrefabPath), 7, 0.9f, 0.5f, 3.2f),
                (LoadEnemy(SpitterPrefabPath),  7, 0.9f, 0.5f, 3.0f),
                (LoadEnemy(CrawlerPrefabPath),  3, 1.4f, 0.7f, 4.2f),
            });

            return waves;
        }

        // ---------------------------------------------------------------- HUD

        static void BuildHud(GameObject player, WaveManager waves)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("HUD Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // Health bar, bottom left.
            var barBg = CreatePanel(canvasGo.transform, "HealthBarBG", uiSprite,
                new Color(0f, 0f, 0f, 0.55f),
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 40f), new Vector2(420f, 34f));

            var fillGo = CreatePanel(barBg.transform, "Fill", uiSprite,
                new Color(0.85f, 0.22f, 0.24f, 0.95f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillGo.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            var healthLabel = CreateText(barBg.transform, "HealthLabel", font, 20, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "100 / 100");

            var ammoLabel = CreateText(canvasGo.transform, "AmmoLabel", font, 44, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(340f, 60f), "12 / 12");

            var weaponLabel = CreateText(canvasGo.transform, "WeaponLabel", font, 22, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 106f), new Vector2(340f, 32f), "PISTOL");
            weaponLabel.color = new Color(0.78f, 0.80f, 0.84f);

            var barricadeLabel = CreateText(canvasGo.transform, "BarricadeLabel", font, 22, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 142f), new Vector2(340f, 32f), "[F] BARRICADE  x1");
            barricadeLabel.color = new Color(0.95f, 0.75f, 0.35f);

            var ultimateLabel = CreateText(canvasGo.transform, "UltimateLabel", font, 22, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 178f), new Vector2(340f, 32f), string.Empty);
            ultimateLabel.color = new Color(0.62f, 0.64f, 0.68f);

            var waveLabel = CreateText(canvasGo.transform, "WaveLabel", font, 30, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(520f, 44f), "WAVE 1");

            var scoreLabel = CreateText(canvasGo.transform, "ScoreLabel", font, 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(400f, 44f), "SCORE 0");

            var goldLabel = CreateText(canvasGo.transform, "GoldLabel", font, 24, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -88f), new Vector2(400f, 36f), "GOLD $0");
            goldLabel.color = new Color(1.0f, 0.85f, 0.25f);

            // Boss bar, top centre and hidden until one exists. Wave 15 puts a boss and a
            // full horde on screen together, so its health needs somewhere unmissable that
            // is not competing with the player's own bar in the corner.
            var bossBarRoot = CreatePanel(canvasGo.transform, "BossBarRoot", uiSprite,
                new Color(0f, 0f, 0f, 0.55f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(760f, 30f));

            var bossFillGo = CreatePanel(bossBarRoot.transform, "BossFill", uiSprite,
                new Color(0.72f, 0.10f, 0.12f, 0.95f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bossFill = bossFillGo.GetComponent<Image>();
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            bossFill.fillAmount = 1f;

            var bossLabel = CreateText(bossBarRoot.transform, "BossLabel", font, 18,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                "THE BUTCHER");

            bossBarRoot.SetActive(false);

            var centreLabel = CreateText(canvasGo.transform, "CentreLabel", font, 46, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 300f), string.Empty);

            var hud = canvasGo.AddComponent<HUD>();
            using (var f = new Fields(hud))
            {
                f.Obj("playerHealth", player.GetComponent<Health>())
                 .Obj("weapon", player.GetComponent<Weapon>())
                 .Obj("waves", waves)
                 .Obj("loadout", player.GetComponent<WeaponLoadout>())
                 .Obj("placer", player.GetComponent<DeployablePlacer>())
                 .Obj("ultimate", player.GetComponent<UltimateAbility>())
                 .Obj("healthFill", fill)
                 .Obj("healthLabel", healthLabel)
                 .Obj("ammoLabel", ammoLabel)
                 .Obj("weaponLabel", weaponLabel)
                 .Obj("bossBarRoot", bossBarRoot)
                 .Obj("bossFill", bossFill)
                 .Obj("bossLabel", bossLabel)
                 .Obj("barricadeLabel", barricadeLabel)
                 .Obj("ultimateLabel", ultimateLabel)
                 .Obj("waveLabel", waveLabel)
                 .Obj("scoreLabel", scoreLabel)
                 .Obj("goldLabel", goldLabel)
                 .Obj("centreLabel", centreLabel);
            }

            BuildArmoryShop(canvasGo.transform, font, uiSprite, player.GetComponent<WeaponLoadout>());
        }

        /// <summary>
        /// Instantiates every weapon model into both holders and wires WeaponVisuals.
        /// <para>
        /// All ten exist from the start and are simply toggled. Weapon swapping happens
        /// mid-fight, so instantiating on the swap would be a hitch for no benefit.
        /// </para>
        /// </summary>
        static void BuildWeaponVisuals(GameObject player, WeaponLoadout loadout, Object[] arsenal,
                                       Transform mainHolder, Transform offHandHolder,
                                       Transform muzzle, Transform offHandMuzzle, Transform ejectPort)
        {
            int count = Mathf.Min(arsenal.Length, WeaponArtNames.Length);

            var definitions = new Object[count];
            var mainModels = new Object[count];
            var offHandModels = new Object[count];
            var muzzleSockets = new Object[count];
            var ejectSockets = new Object[count];
            var animators = new Object[count];

            for (int i = 0; i < count; i++)
            {
                definitions[i] = arsenal[i];

                var main = InstantiateWeaponModel(WeaponArtNames[i], mainHolder);
                var off = InstantiateWeaponModel(WeaponArtNames[i], offHandHolder);

                mainModels[i] = main;
                offHandModels[i] = off;

                if (main != null)
                {
                    muzzleSockets[i] = FindDeep(main.transform, "MuzzleSocket");
                    ejectSockets[i] = FindDeep(main.transform, "EjectPortSocket");
                    animators[i] = AttachWeaponAnimator(main, WeaponArtNames[i]);
                }

                // Everything starts hidden; WeaponVisuals turns on whatever is carried.
                if (main != null) main.SetActive(false);
                if (off != null) off.SetActive(false);
            }

            var visuals = player.AddComponent<WeaponVisuals>();
            using (var f = new Fields(visuals))
            {
                f.Obj("loadout", loadout)
                 .Arr("definitions", definitions)
                 .Arr("mainModels", mainModels)
                 .Arr("offHandModels", offHandModels)
                 .Arr("muzzleSockets", muzzleSockets)
                 .Arr("ejectSockets", ejectSockets)
                 .Arr("animators", animators)
                 .Obj("muzzle", muzzle)
                 .Obj("offHandMuzzle", offHandMuzzle)
                 .Obj("ejectPort", ejectPort);
            }
        }

        /// <summary>
        /// Clips every weapon has. The per-weapon action clip - a bolt, a pump, a slide - is
        /// found by name at runtime instead, so a weapon that gains or loses one needs no
        /// change here.
        /// </summary>
        static readonly string[] SharedWeaponClips = { "Idle", "Fire", "Reload", "Equip" };

        static readonly string[] WeaponCycleClips =
        {
            "BoltCycle", "SlideCycle", "Pump", "DrumCycle", "DriverCycle",
            "Discharge", "Drain", "Ignite",
        };

        static Animator AttachWeaponAnimator(GameObject model, string artName)
        {
            var controller = LoadOrCreateWeaponController(artName);
            if (controller == null) return null;

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // The weapon is always in frame and always right next to the camera's subject,
            // so there is nothing to cull against.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return animator;
        }

        /// <summary>
        /// One controller per weapon, holding whichever of its clips the game can actually
        /// drive. Fire and Reload carry speed parameters so the clip can be fitted to the
        /// weapon's real rate of fire and reload time, both of which upgrades change.
        /// </summary>
        static AnimatorController LoadOrCreateWeaponController(string artName)
        {
            string modelPath = $"{Root}/Art/Weapons/{artName}/{artName}.fbx";
            string path = $"{Root}/Art/Weapons/{artName}/{artName}.controller";

            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            if (clips == null || clips.Length == 0) return null;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var machine = controller.layers[0].stateMachine;

            var wanted = new List<string>(SharedWeaponClips);
            foreach (var cycle in WeaponCycleClips)
                foreach (var asset in clips)
                    if (asset is AnimationClip c && c.name == cycle) wanted.Add(cycle);

            foreach (var name in wanted)
            {
                AnimationClip clip = null;
                foreach (var asset in clips)
                    if (asset is AnimationClip candidate && candidate.name == name) { clip = candidate; break; }

                if (clip == null) continue;

                AnimatorState state = null;
                foreach (var child in machine.states)
                    if (child.state.name == name) { state = child.state; break; }

                if (state == null) state = machine.AddState(name);
                state.motion = clip;
                if (name == "Idle") machine.defaultState = state;

                if (name == "Fire" || name == "Reload")
                {
                    string parameter = name == "Fire" ? "FireSpeed" : ReloadSpeedParameter;
                    EnsureFloatParameter(controller, parameter);
                    state.speedParameterActive = true;
                    state.speedParameter = parameter;
                }
            }

            ConfigureWeaponImportSettings(modelPath);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static GameObject InstantiateWeaponModel(string artName, Transform holder)
        {
            string path = $"{Root}/Art/Weapons/{artName}/{artName}.fbx";

            ConfigureWeaponImportSettings(path);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"ArenaBuilder: no weapon model at {path}; that weapon will " +
                                 "be invisible in the player's hands.");
                return null;
            }

            var model = PrefabUtility.InstantiatePrefab(asset, holder) as GameObject;
            if (model == null) return null;

            model.name = artName;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * WeaponModelScale;

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadOrCreateWeaponController(artName, path);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Sit the model so its grip lands on the holder rather than its mesh origin,
            // which is what makes ten different weapons line up in the same hand.
            var grip = FindDeep(model.transform, "GripSocket");
            model.transform.localPosition = grip != null
                ? -grip.localPosition * WeaponModelScale
                : Vector3.zero;

            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                // A weapon held at the player's centre would otherwise cast a shadow across
                // the player's own body from a 61-degree camera.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return model;
        }

        static void ConfigureWeaponImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            // Start from the takes Blender currently exports. Keeping a prior custom
            // clipAnimations array lets removed takes survive in the .meta file, which can
            // make a fresh checkout import a different controller than the editor cache.
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            var existing = importer.clipAnimations;
            if (existing == null || existing.Length != clips.Length)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!string.Equals(existing[i].name, clips[i].name) ||
                        !string.Equals(existing[i].takeName, clips[i].takeName))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            for (int i = 0; i < clips.Length; i++)
            {
                bool loop = ClipNameMatches(clips[i].name, "Idle") ||
                            ClipNameMatches(clips[i].name, "Charge");
                if (clips[i].loopTime == loop) continue;
                clips[i].loopTime = loop;
                changed = true;
            }

            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static RuntimeAnimatorController LoadOrCreateWeaponController(string artName, string modelPath)
        {
            string controllerPath = $"{Root}/Art/Weapons/{artName}/{artName}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                EnsureFolder($"{Root}/Art/Weapons/{artName}");
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            var machine = controller.layers[0].stateMachine;

            // Controllers are regenerated after every art export. Remove prior states so a
            // renamed or removed take cannot leave a stale motion (or a clip from an older
            // FBX import) behind in the weapon's graph.
            var oldStates = machine.states;
            for (int i = 0; i < oldStates.Length; i++)
                machine.RemoveState(oldStates[i].state);

            for (int i = 0; i < WeaponAnimationNames.Length; i++)
            {
                string name = WeaponAnimationNames[i];
                AnimationClip clip = null;
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c] is AnimationClip candidate && ClipNameMatches(candidate.name, name))
                    {
                        clip = candidate;
                        break;
                    }
                }
                if (clip == null) continue;

                AnimatorState state = null;
                for (int s = 0; s < machine.states.Length; s++)
                {
                    if (machine.states[s].state.name == name)
                    {
                        state = machine.states[s].state;
                        break;
                    }
                }

                if (state == null) state = machine.AddState(name);
                state.motion = clip;
                if (name == "Idle") machine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static GameObject CreatePanel(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            ApplyRect(go, anchorMin, anchorMax, anchoredPosition, size);
            return go;
        }

        static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, string value)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            ApplyRect(go, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            return text;
        }

        static void ApplyRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            // Stretched rects (min != max on an axis) use sizeDelta as an inset, so zero it.
            rect.pivot = new Vector2(
                Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
                Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        static ArmoryUI BuildArmoryShop(Transform canvasTransform, Font font, Sprite uiSprite, WeaponLoadout loadout)
        {
            var armoryRoot = new GameObject("ArmoryUI", typeof(RectTransform));
            armoryRoot.transform.SetParent(canvasTransform, false);
            ApplyRect(armoryRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Dark modal backdrop that covers the full screen and blocks raycasts
            var overlay = CreatePanel(armoryRoot.transform, "ArmoryShopOverlay", uiSprite,
                new Color(0.04f, 0.05f, 0.07f, 0.88f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.GetComponent<Image>().raycastTarget = true;

            // Main shop card, sized to whatever the catalogue holds. Ten weapons will not
            // fit a fixed card, and hardcoding a height means the shop silently overflows
            // the moment a weapon is added.
            var arsenal = LoadOrCreateWeapons();
            float cardHeight = Mathf.Max(600f, 210f + arsenal.Length * 50f + 150f);

            var shopCard = CreatePanel(overlay.transform, "ShopCard", uiSprite,
                new Color(0.11f, 0.13f, 0.18f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1400f, cardHeight));
            shopCard.GetComponent<Image>().raycastTarget = true;

            // Header Title
            var titleText = CreateText(shopCard.transform, "Title", font, 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(800f, 48f), "ARMORY SHOP");
            titleText.color = new Color(0.95f, 0.95f, 0.95f);

            // Gold counter
            var goldText = CreateText(shopCard.transform, "Gold", font, 24, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(400f, 36f), "GOLD: $0");
            goldText.color = new Color(1.0f, 0.85f, 0.25f);

            // Left column: every weapon in the catalogue, one row each.
            var wpnHeader = CreateText(shopCard.transform, "WpnHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-350f, -128f), new Vector2(620f, 30f), "— WEAPONS —");
            wpnHeader.color = new Color(0.45f, 0.82f, 1.0f);

            var loadoutLabel = CreateText(shopCard.transform, "LoadoutLabel", font, 16, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-350f, -158f), new Vector2(620f, 24f), "LOADOUT  1 / 4");
            loadoutLabel.color = new Color(0.62f, 0.64f, 0.68f);

            var weaponButtons = new Object[arsenal.Length];
            var weaponButtonTexts = new Object[arsenal.Length];

            for (int i = 0; i < arsenal.Length; i++)
            {
                var definition = arsenal[i] as WeaponDefinition;

                // Built from the definition rather than written out per weapon, so the row
                // stays true when a weapon is retuned and a tenth needs no code here.
                string blurb = definition != null
                    ? $"{definition.DisplayName.ToUpperInvariant()}\n" +
                      $"{definition.Damage:0} dmg · {definition.FireRate:0} RPM · {definition.MagazineSize} rounds"
                    : "—";

                var (btn, txt) = CreateShopItemButton(shopCard.transform, $"BuyWeapon{i}", uiSprite, font,
                    new Vector2(-350f, -196f - i * 50f), new Vector2(620f, 46f), blurb, "$0 BUY");

                weaponButtons[i] = btn;
                weaponButtonTexts[i] = txt;
            }

            // Right column: consumables and fortifications.
            var ammoHeader = CreateText(shopCard.transform, "AmmoHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(350f, -128f), new Vector2(620f, 30f), "— AMMO —");
            ammoHeader.color = new Color(0.45f, 0.82f, 1.0f);

            var (ammoBtn, ammoTxt) = CreateShopItemButton(shopCard.transform, "BuyAmmo", uiSprite, font,
                new Vector2(350f, -172f), new Vector2(620f, 46f),
                "FULL AMMO CRATE\nRestocks reserve ammo for all guns", "$50 REFILL ALL");

            var fortHeader = CreateText(shopCard.transform, "FortHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(350f, -248f), new Vector2(620f, 30f), "— FORTIFICATIONS —");
            fortHeader.color = new Color(1.0f, 0.55f, 0.35f);

            var (barricadeBtn, barricadeTxt) = CreateShopItemButton(shopCard.transform, "BuyBarricade", uiSprite, font,
                new Vector2(350f, -294f), new Vector2(620f, 46f),
                "WOODEN BARRICADE (150 HP)\nBlocks horde path & enemy fire · [F] Place", "$40 BUY");

            var (barrelBtn, barrelTxt) = CreateShopItemButton(shopCard.transform, "BuyBarrel", uiSprite, font,
                new Vector2(350f, -344f), new Vector2(620f, 46f),
                "EXPLOSIVE BARREL\n220 dmg blast, chains to other barrels. Hurts you too.", "$60 BUY");

            var (claymoreBtn, claymoreTxt) = CreateShopItemButton(shopCard.transform, "BuyClaymore", uiSprite, font,
                new Vector2(350f, -394f), new Vector2(620f, 46f),
                "CLAYMORE\nDirectional mine, 160 dmg in a cone. One use.", "$75 BUY");

            // Footer action buttons
            var (deployBtn, deployTxt) = CreateActionButton(shopCard.transform, "DeployButton", uiSprite, font,
                new Vector2(-40f, 46f), new Vector2(380f, 54f),
                "DEPLOY / NEXT WAVE [SPACE]", new Color(0.18f, 0.55f, 0.28f, 1f));

            var (closeBtn, closeTxt) = CreateActionButton(shopCard.transform, "CloseButton", uiSprite, font,
                new Vector2(400f, 46f), new Vector2(240f, 54f),
                "EXIT SHOP [B]", new Color(0.28f, 0.30f, 0.36f, 1f));

            // ---- weapon upgrade panel -------------------------------------------
            // Its own overlay rather than more rows in the shop card: fifteen tiers plus
            // three headers does not fit alongside weapons, ammo and deployables.
            var (upgradeOpenBtn, _) = CreateActionButton(shopCard.transform, "OpenUpgrades", uiSprite, font,
                new Vector2(-460f, 46f), new Vector2(300f, 54f),
                "WEAPON UPGRADES", new Color(0.42f, 0.30f, 0.10f, 1f));

            var upgradeOverlay = CreatePanel(armoryRoot.transform, "UpgradeOverlay", uiSprite,
                new Color(0.04f, 0.05f, 0.06f, 0.94f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var upgradeCard = CreatePanel(upgradeOverlay.transform, "UpgradeCard", uiSprite,
                new Color(0.10f, 0.11f, 0.13f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 760f));

            var upgradeWeaponLabel = CreateText(upgradeCard.transform, "UpgradeWeapon", font, 34,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(900f, 44f), "PISTOL");

            var upgradeRuleLabel = CreateText(upgradeCard.transform, "UpgradeRule", font, 18,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(700f, 28f),
                "One path per weapon  ·  a run has gold to max exactly one");
            upgradeRuleLabel.color = new Color(0.72f, 0.62f, 0.42f);

            // One column. The panel and UpgradeManager both still handle three - the 5-3-0
            // rule goes inert on its own with a single path, since one open path can never
            // breach it - so widening this back out later is a layout change and nothing more.
            const int ShopPaths = 1;

            var pathTitles = new Object[ShopPaths];
            var tierButtons = new Object[ShopPaths * 5];
            var tierLabels = new Object[ShopPaths * 5];

            for (int path = 0; path < ShopPaths; path++)
            {
                float columnX = ShopPaths == 1 ? 0f : -370f + path * 370f;

                pathTitles[path] = CreateText(upgradeCard.transform, $"PathTitle_{path}", font, 22,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(columnX, -126f), new Vector2(620f, 30f), "PATH");

                for (int tier = 0; tier < 5; tier++)
                {
                    int index = path * 5 + tier;
                    var (btn, btnLabel) = CreateButton(upgradeCard.transform, $"Tier_{path}_{tier}",
                        uiSprite, new Color(0.18f, 0.19f, 0.22f, 0.85f),
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(columnX, -178f - tier * 96f), new Vector2(620f, 84f),
                        "", font, 16);

                    tierButtons[index] = btn;
                    tierLabels[index] = btnLabel;
                }
            }

            var (upgradeCloseBtn, _) = CreateActionButton(upgradeCard.transform, "CloseUpgrades", uiSprite, font,
                new Vector2(0f, 42f), new Vector2(320f, 52f),
                "BACK TO SHOP", new Color(0.28f, 0.30f, 0.36f, 1f));

            var upgradePanel = armoryRoot.AddComponent<UpgradePanel>();
            using (var f = new Fields(upgradePanel))
            {
                f.Obj("root", upgradeOverlay)
                 .Obj("loadout", loadout)
                 .Obj("openButton", upgradeOpenBtn)
                 .Obj("closeButton", upgradeCloseBtn)
                 .Obj("weaponLabel", upgradeWeaponLabel)
                 .Obj("ruleLabel", upgradeRuleLabel)
                 .Arr("pathTitles", pathTitles)
                 .Arr("tierButtons", tierButtons)
                 .Arr("tierLabels", tierLabels);
            }

            var armoryUI = armoryRoot.AddComponent<ArmoryUI>();
            using (var f = new Fields(armoryUI))
            {
                f.Obj("panel", overlay)
                 .Obj("titleLabel", titleText)
                 .Obj("goldLabel", goldText)
                 .Obj("loadout", loadout)
                 .Arr("weaponButtons", weaponButtons).Arr("weaponBtnTexts", weaponButtonTexts)
                 .Obj("loadoutLabel", loadoutLabel)
                 .Obj("ammoButton", ammoBtn).Obj("ammoBtnText", ammoTxt)
                 .Obj("barricadeButton", barricadeBtn).Obj("barricadeBtnText", barricadeTxt)
                 .Obj("barrelButton", barrelBtn).Obj("barrelBtnText", barrelTxt)
                 .Obj("claymoreButton", claymoreBtn).Obj("claymoreBtnText", claymoreTxt)
                 .Obj("deployButton", deployBtn).Obj("deployBtnText", deployTxt)
                 .Obj("closeButton", closeBtn);
            }

            return armoryUI;
        }

        static (Button, Text) CreateShopItemButton(Transform parent, string name, Sprite sprite, Font font,
            Vector2 anchoredPosition, Vector2 sizeDelta, string descText, string buyText)
        {
            var cardGo = CreatePanel(parent, name + "_Card", sprite,
                new Color(0.16f, 0.18f, 0.24f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), anchoredPosition, sizeDelta);
            cardGo.GetComponent<Image>().raycastTarget = false;

            var label = CreateText(cardGo.transform, "Desc", font, 14, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(310f, sizeDelta.y), descText);
            label.color = new Color(0.9f, 0.9f, 0.9f);

            var (btn, btnText) = CreateButton(cardGo.transform, "Button", sprite,
                new Color(0.24f, 0.52f, 0.85f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(140f, 38f),
                buyText, font, 14);

            return (btn, btnText);
        }

        static (Button, Text) CreateActionButton(Transform parent, string name, Sprite sprite, Font font,
            Vector2 anchoredPosition, Vector2 sizeDelta, string labelText, Color color)
        {
            return CreateButton(parent, name, sprite, color,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPosition, sizeDelta,
                labelText, font, 18);
        }

        static (Button, Text) CreateButton(Transform parent, string name, Sprite sprite, Color normalColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
            string labelText, Font font, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = normalColor;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = normalColor * 1.25f;
            colors.pressedColor = normalColor * 0.75f;
            colors.disabledColor = new Color(0.22f, 0.22f, 0.25f, 0.6f);
            btn.colors = colors;

            ApplyRect(go, anchorMin, anchorMax, anchoredPosition, sizeDelta);

            var text = CreateText(go.transform, "Label", font, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, labelText);
            text.color = Color.white;

            return (btn, text);
        }

        // ---------------------------------------------------------------- assets

        static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", 0.15f);
            return SaveMaterial(mat, name);
        }

        static Material CreateUnlitMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            return SaveMaterial(mat, name);
        }

        static Material SaveMaterial(Material mat, string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Keep the existing asset's identity so scene references survive a rebuild.
                existing.shader = mat.shader;
                existing.CopyPropertiesFromMaterial(mat);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mat);
                return existing;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// A particle system that never emits on its own - every particle comes from an
        /// explicit Emit() call. It loops and plays on awake purely so the simulation keeps
        /// ticking; a stopped system would not advance particles pushed in by hand.
        /// Renders cube meshes rather than billboards, which needs no texture and suits the
        /// stylized-minimal direction.
        /// </summary>
        static ParticleSystem CreateBurstSystem(Transform parent, string name, Material mat,
            Color color, float minSpeed, float maxSpeed, float minLife, float maxLife,
            float minSize, float maxSize, float coneAngle, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.gravityModifier = gravity;
            // World space, so repositioning the system between shots leaves sparks
            // already in flight exactly where they were.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.02f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }

        /// <summary>
        /// Turns a generic burst system into tumbling brass: casings keep their size (they
        /// are objects, not sparks), spin as they fly, and bounce off the floor so they come
        /// to rest instead of vanishing in mid-air.
        /// </summary>
        static void ConfigureAsShellCasing(ParticleSystem ps)
        {
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = false;

            var main = ps.main;

            // Elongated rather than cubic, so a tumbling casing reads as brass and not as
            // a stray voxel. Deliberately oversized: at 45 px per world unit, a physically
            // accurate ~1cm shell would be well under a pixel. Readability wins here.
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.09f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.07f, 0.09f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.18f, 0.24f);

            main.startRotation3D = true;
            // Particle rotation is radians when set from script, not degrees.
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-7f, 7f);
            rotation.y = new ParticleSystem.MinMaxCurve(-7f, 7f);
            rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            collision.dampen = 0.45f;
            collision.lifetimeLoss = 0f;
        }

        static Material LoadMaterial(string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogError($"ArenaBuilder: no material at {path}.");
            return mat;
        }

        /// <summary>
        /// The four starting weapons, created once and then left alone. Unlike the scene and
        /// the materials, a weapon definition is design data a human will hand-tune, so
        /// regenerating it on every build would quietly discard that work. Delete an asset to
        /// get its defaults back.
        /// </summary>
        static WeaponDefinition[] LoadOrCreateWeapons()
        {
            var gunshot = LoadClip("SFX_Gunshot");
            var impact = LoadClip("SFX_Impact");

            // Zombies have 100 HP, so the damage column reads as: pistol 5 shots, rifle 5 but
            // in half a second, shotgun a point-blank one-shot, sniper a one-shot plus a lance
            // through whatever is lined up behind it.
            var pistol = LoadOrCreateWeapon("WPN_Pistol", f => f
                .Str("displayName", "Pistol").I("cost", 0)
                .F("damage", 20f).F("fireRate", 200f).F("range", 45f).F("spread", 1.2f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.SemiAuto)
                .I("tags", (int)WeaponTags.Sidearm)
                .I("pierceCount", 0).F("penetrationFalloff", 0.65f)
                .I("magazineSize", 12).I("maxReserveAmmo", 120).F("reloadTime", 1.0f)
                .F("fireTrauma", 0.07f).F("recoilKick", 0.05f).F("knockbackMultiplier", 0.6f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.4f).F("impactVolume", 0.4f)
                .F("tracerWidth", 0.07f).F("tracerDuration", 0.05f).I("shellsPerShot", 1));

            // Slowest cycle of the four, and short ranged: the trade for one-shotting at
            // touching distance is having to stand somewhere dangerous to do it.
            var shotgun = LoadOrCreateWeapon("WPN_Shotgun", f => f
                .Str("displayName", "Shotgun").I("cost", 150)
                .F("damage", 22f).F("fireRate", 75f).F("range", 30f).F("spread", 7f)
                .I("pelletsPerShot", 5).E("fireMode", (int)FireMode.SemiAuto)
                .I("tags", (int)WeaponTags.Shotgun)
                .I("pierceCount", 1).F("penetrationFalloff", 0.6f)
                .I("magazineSize", 8).I("maxReserveAmmo", 48).F("reloadTime", 2.4f)
                .F("fireTrauma", 0.3f).F("recoilKick", 0.18f).F("knockbackMultiplier", 2.2f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.6f).F("impactVolume", 0.45f)
                .F("tracerWidth", 0.085f).F("tracerDuration", 0.07f).I("shellsPerShot", 1));

            var assault = LoadOrCreateWeapon("WPN_AssaultRifle", f => f
                .Str("displayName", "Assault Rifle").I("cost", 250)
                .F("damage", 22f).F("fireRate", 600f).F("range", 60f).F("spread", 2.2f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.Rifle)
                .I("pierceCount", 2).F("penetrationFalloff", 0.6f)
                .I("magazineSize", 30).I("maxReserveAmmo", 180).F("reloadTime", 1.7f)
                .F("fireTrauma", 0.085f).F("recoilKick", 0.06f).F("knockbackMultiplier", 1f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.45f).F("impactVolume", 0.4f)
                .F("tracerWidth", 0.075f).F("tracerDuration", 0.045f).I("shellsPerShot", 1));

            // Range 200 against a 60-unit arena: it genuinely crosses the map. High
            // penetration retention is what makes it a line rather than a single kill.
            var sniper = LoadOrCreateWeapon("WPN_Sniper", f => f
                .Str("displayName", "Sniper Rifle").I("cost", 350)
                .F("damage", 150f).F("fireRate", 45f).F("range", 200f).F("spread", 0.1f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.SemiAuto)
                .I("tags", (int)WeaponTags.Precision)
                .I("pierceCount", 6).F("penetrationFalloff", 0.85f)
                .I("magazineSize", 5).I("maxReserveAmmo", 30).F("reloadTime", 2.8f)
                .F("fireTrauma", 0.42f).F("recoilKick", 0.26f).F("knockbackMultiplier", 3f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.7f).F("impactVolume", 0.5f)
                .F("tracerWidth", 0.13f).F("tracerDuration", 0.1f).I("shellsPerShot", 1));

            // Short ranged and continuous: a fuel tank is a magazine and a tick of flame is
            // a round, so the rate of fire IS the burn rate.
            var flamethrower = LoadOrCreateWeapon("WPN_Flamethrower", f => f
                .Str("displayName", "Flamethrower").I("cost", 250)
                .F("damage", 7f).F("fireRate", 600f).F("range", 11f).F("spread", 0f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 0).F("penetrationFalloff", 1f)
                .I("magazineSize", 100).I("maxReserveAmmo", 400).F("reloadTime", 2.2f)
                .F("fireTrauma", 0.012f).F("recoilKick", 0.004f).F("knockbackMultiplier", 0.15f)
                // DELIBERATELY SILENT. It fires ten times a second, so the shared gunshot
                // clip would machine-gun through the voice pool and sound like a stutter
                // rather than a flame. It needs a looping loop-point sample, which the
                // project does not have yet; silence is the better placeholder.
                .Obj("fireClip", null).Obj("impactClip", null)
                .F("fireVolume", 0f).F("impactVolume", 0f)
                .F("tracerWidth", 0f).F("tracerDuration", 0f).I("shellsPerShot", 0)
                .Obj("delivery", LoadDelivery("DLV_Flame")));

            var tesla = LoadOrCreateWeapon("WPN_TeslaCoil", f => f
                .Str("displayName", "Tesla Coil").I("cost", 300)
                .F("damage", 34f).F("fireRate", 150f).F("range", 30f).F("spread", 0.5f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 0).F("penetrationFalloff", 1f)
                .I("magazineSize", 16).I("maxReserveAmmo", 96).F("reloadTime", 2.0f)
                .F("fireTrauma", 0.09f).F("recoilKick", 0.03f).F("knockbackMultiplier", 0.8f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.4f).F("impactVolume", 0.35f)
                .F("tracerWidth", 0.06f).F("tracerDuration", 0.08f).I("shellsPerShot", 0)
                .Obj("delivery", LoadDelivery("DLV_Tesla")));

            // Range is meaningless here - the shell is a physical object with its own speed
            // and gravity, and the number is only kept sane for the delivery's spawn maths.
            var grenade = LoadOrCreateWeapon("WPN_GrenadeLauncher", f => f
                .Str("displayName", "Grenade Launcher").I("cost", 280)
                .F("damage", 110f).F("fireRate", 60f).F("range", 45f).F("spread", 1f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.SemiAuto)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 0).F("penetrationFalloff", 1f)
                .I("magazineSize", 4).I("maxReserveAmmo", 24).F("reloadTime", 2.6f)
                .F("fireTrauma", 0.34f).F("recoilKick", 0.2f).F("knockbackMultiplier", 2.5f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.6f).F("impactVolume", 0.5f)
                .F("tracerWidth", 0f).F("tracerDuration", 0f).I("shellsPerShot", 1)
                .Obj("delivery", LoadDelivery("DLV_Grenade")));

            var smg = LoadOrCreateWeapon("WPN_SMG", f => f
                .Str("displayName", "SMG").I("cost", 120)
                .F("damage", 14f).F("fireRate", 900f).F("range", 32f).F("spread", 3.4f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 0).F("penetrationFalloff", 0.6f)
                .I("magazineSize", 40).I("maxReserveAmmo", 320).F("reloadTime", 1.5f)
                .F("fireTrauma", 0.045f).F("recoilKick", 0.03f).F("knockbackMultiplier", 0.5f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.32f).F("impactVolume", 0.3f)
                .F("tracerWidth", 0.055f).F("tracerDuration", 0.035f).I("shellsPerShot", 1));

            // Hitscan, not a projectile, and that is a mechanical requirement rather than a
            // flavour choice: WeaponProjectile carries its own damage and never calls back
            // into Weapon.ApplyShot (debt 20), which is where repairing and pinning live. A
            // projectile nail gun would have had an entirely inert upgrade path.
            var nailGun = LoadOrCreateWeapon("WPN_NailGun", f => f
                .Str("displayName", "Nail Gun").I("cost", 180)
                .F("damage", 26f).F("fireRate", 260f).F("range", 30f).F("spread", 1.6f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 0).F("penetrationFalloff", 1f)
                .I("magazineSize", 24).I("maxReserveAmmo", 200).F("reloadTime", 1.6f)
                .F("fireTrauma", 0.05f).F("recoilKick", 0.03f).F("knockbackMultiplier", 0.6f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.34f).F("impactVolume", 0.3f)
                .F("tracerWidth", 0.05f).F("tracerDuration", 0.04f).I("shellsPerShot", 0));

            var siphon = LoadOrCreateWeapon("WPN_SiphonRifle", f => f
                .Str("displayName", "Siphon Rifle").I("cost", 380)
                .F("damage", 40f).F("fireRate", 260f).F("range", 45f).F("spread", 1.4f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.Automatic)
                .I("tags", (int)WeaponTags.None)
                .I("pierceCount", 1).F("penetrationFalloff", 0.7f)
                .I("magazineSize", 20).I("maxReserveAmmo", 140).F("reloadTime", 1.9f)
                .F("fireTrauma", 0.1f).F("recoilKick", 0.05f).F("knockbackMultiplier", 0.9f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.42f).F("impactVolume", 0.38f)
                .F("tracerWidth", 0.08f).F("tracerDuration", 0.06f).I("shellsPerShot", 1));

            return new[]
            {
                pistol, shotgun, assault, sniper, flamethrower, tesla, grenade,
                smg, nailGun, siphon,
            };
        }

        /// <summary>
        /// Economy prices as an asset, created once and then left alone - the same
        /// create-if-missing contract as the weapon definitions, so tuning survives rebuilds.
        /// </summary>
        static ArmoryPrices LoadOrCreateArmoryPrices()
        {
            const string path = WeaponDir + "/ArmoryPrices.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ArmoryPrices>(path);
            if (existing != null) return existing;

            EnsureFolder(WeaponDir);

            var asset = ScriptableObject.CreateInstance<ArmoryPrices>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// The pistol's three upgrade paths. Created once then left to hand-tuning, like the
        /// weapon definitions - balance numbers here are the whole point of the asset.
        /// </summary>
        /// <summary>
        /// The cost of tier 1 through 5, shared by every path on every weapon.
        /// <para>
        /// Steep on purpose. A flat curve meant a run could afford almost everything the
        /// game sells, so buying was sequencing rather than choosing. At these numbers one
        /// maxed path is most of a run's income, which is what makes committing to it a
        /// decision. It also lands the expensive tiers late, where the money actually is -
        /// over half a run's gold arrives in the last five waves.
        /// </para>
        /// </summary>
        static readonly int[] TierCosts = { 60, 150, 350, 800, 2000 };

        /// <summary>
        /// Builds a path and prices it from <see cref="TierCosts"/>. Authoring a path is
        /// choosing five effects; the costs are not a per-path decision, and with twelve
        /// paths to write there is no version of hand-pricing sixty tiers that stays
        /// consistent.
        /// </summary>
        static WeaponUpgradePath UpgradePath(string title, string summary, params UpgradeTier[] tiers)
        {
            for (int i = 0; i < tiers.Length && i < TierCosts.Length; i++)
                tiers[i].cost = TierCosts[i];

            return new WeaponUpgradePath { title = title, summary = summary, tiers = tiers };
        }

        /// <summary>
        /// One tree per weapon, each holding a single path. Create-if-missing like every
        /// other design asset, so hand-tuned tiers survive a rebuild - and so changing an
        /// authored tier in code means deleting the asset first.
        /// </summary>
        static Object[] LoadOrCreateUpgradeTrees()
        {
            var arsenal = LoadOrCreateWeapons();

            return new Object[]
            {
                LoadOrCreateTree("UPG_Pistol", arsenal[0] as WeaponDefinition, GunslingerPath()),
                LoadOrCreateTree("UPG_Shotgun", arsenal[1] as WeaponDefinition, StreetsweeperPath()),
                LoadOrCreateTree("UPG_AssaultRifle", arsenal[2] as WeaponDefinition, MarksmanPath()),
                LoadOrCreateTree("UPG_Sniper", arsenal[3] as WeaponDefinition, ExecutionerPath()),
                LoadOrCreateTree("UPG_Flamethrower", arsenal[4] as WeaponDefinition, WildfirePath()),
                LoadOrCreateTree("UPG_TeslaCoil", arsenal[5] as WeaponDefinition, StormPath()),
                LoadOrCreateTree("UPG_GrenadeLauncher", arsenal[6] as WeaponDefinition, BombardierPath()),
                LoadOrCreateTree("UPG_SMG", arsenal[7] as WeaponDefinition, AdrenalinePath()),
                LoadOrCreateTree("UPG_NailGun", arsenal[8] as WeaponDefinition, LawmanPath()),
                LoadOrCreateTree("UPG_SiphonRifle", arsenal[9] as WeaponDefinition, ReaperPath()),
            };
        }

        static WeaponUpgradeTree LoadOrCreateTree(string fileName, WeaponDefinition weapon,
                                                  WeaponUpgradePath path)
        {
            string assetPath = $"{UpgradeDir}/{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<WeaponUpgradeTree>(assetPath);
            if (existing != null) return existing;

            EnsureFolder(UpgradeDir);

            var tree = ScriptableObject.CreateInstance<WeaponUpgradeTree>();
            tree.EditorInitialise(weapon, new[] { path });

            AssetDatabase.CreateAsset(tree, assetPath);
            return tree;
        }

        // One path per weapon: the run's choice is which weapon to pour gold into, not how to
        // build one. The tree still holds an array, so a second path is data plus a wider panel.

        static WeaponUpgradePath GunslingerPath() => UpgradePath(
            "Gunslinger",
            "Open hot, close hot, and eventually stop needing to aim at all.",
            new UpgradeTier
            {
                title = "Quick Draw",
                description = "Fire rate 200 to 320, reload 1.0s to 0.75s. The first shot after every reload is a guaranteed crit for double damage.",
                fireRate = 320f, reloadTime = 0.75f,
                guaranteedCritAfterReload = true, critMultiplier = 2f,
            },
            new UpgradeTier
            {
                title = "Deadeye",
                description = "Damage 20 to 30, fire rate to 380, reload to 0.6s. 25% chance any shot crits for double damage.",
                damage = 30f, fireRate = 380f, reloadTime = 0.6f,
                critChance = 0.25f,
            },
            new UpgradeTier
            {
                title = "Fan the Hammer",
                description = "Hold to fire at 600 RPM, but accuracy bleeds away while you hold it. Tapping still fires a single accurate shot. Magazine 12 to 18, crit 35%.",
                magazineSize = 18, critChance = 0.35f,
                fanFireRate = 600f, fanMaxSpread = 9f, fanSpreadRamp = 1f,
            },
            new UpgradeTier
            {
                title = "True Gunslinger",
                description = "A second pistol, firing one after the other. Damage to 45, magazine to 30, reload to 0.45s, and the reserve never runs dry.",
                damage = 45f, magazineSize = 30, reloadTime = 0.45f,
                dualWield = true, infiniteReserve = true, fanFireRate = 750f,
            },
            new UpgradeTier
            {
                title = "Legend of the West",
                description = "30 pistol kills charge an ultimate. [V] reloads in a flourish, then the guns aim themselves while you spin - 80% crits until the magazine runs dry. Crit 45% the rest of the time.",
                critChance = 0.45f,
                ultimateKills = 30, ultimateCritChance = 0.8f, ultimateFireRate = 900f,
            });

        static WeaponUpgradePath StreetsweeperPath() => UpgradePath(
            "Streetsweeper",
            "Stops being a gun and becomes a wall of lead that moves the horde.",
            new UpgradeTier
            {
                title = "Choke",
                description = "7 pellets instead of 5, and the spread tightens from 7 to 5.5 degrees. More lead, better aimed.",
                pelletCount = 7, spread = 5.5f,
            },
            new UpgradeTier
            {
                title = "Speedloader",
                description = "Reload 2.4s to 1.2s, magazine 8 shells to 10. The old reload was long enough to lose a wave in.",
                magazineSize = 10, reloadTime = 1.2f,
            },
            new UpgradeTier
            {
                title = "Auto Loader",
                description = "Hold to fire, 75 to 160 RPM. It stops being a weapon you time and becomes one you steer.",
                convertToFullAuto = true, fireRate = 160f,
            },
            new UpgradeTier
            {
                title = "Riot Gun",
                description = "10 pellets at 26 damage, and knockback nearly doubles to 4. A pack hit at close range goes backwards, not just down.",
                pelletCount = 10, damage = 26f, knockbackMultiplier = 4f,
            },
            new UpgradeTier
            {
                title = "Streetsweeper",
                description = "12 pellets, 200 RPM, 20 shells, 0.9s reload, spread out to 9 degrees. Every kill feeds a shell back into the reserve, which is the only reason it can keep firing.",
                pelletCount = 12, damage = 26f, fireRate = 200f, magazineSize = 20,
                reloadTime = 0.9f, spread = 9f, reserveRefundPerKill = 1,
            });

        static WeaponUpgradePath MarksmanPath() => UpgradePath(
            "Marksman",
            "Rewards staying on one target - the exact opposite of spraying a crowd.",
            new UpgradeTier
            {
                title = "Match Barrel",
                description = "Damage 22 to 25, spread 2.2 to 1.2 degrees.",
                damage = 25f, spread = 1.2f,
            },
            new UpgradeTier
            {
                title = "Focus Fire",
                description = "Every consecutive hit on the SAME enemy adds 4% damage, up to 40%. Hitting anything else starts over.",
                focusBonusPerHit = 0.04f, focusMaxBonus = 0.4f,
            },
            new UpgradeTier
            {
                title = "Stabilised",
                description = "Holding the trigger tightens the spread to 0.4 degrees instead of widening it. Magazine 30 to 45.",
                magazineSize = 45, sustainedSpreadMin = 0.4f,
            },
            new UpgradeTier
            {
                title = "Armour Breaker",
                description = "Damage to 32, and focus climbs to 100%. Rounds also keep full damage through the first bodies they pierce.",
                damage = 32f, focusMaxBonus = 1.0f, penetrationFalloff = 1.0f,
            },
            new UpgradeTier
            {
                title = "Marksman",
                description = "Focus climbs to 200% - a held burst on one body ends up hitting three times as hard as it started. 700 RPM, pierce 4. Nothing large survives a sustained look.",
                fireRate = 700f, pierceCount = 4, focusMaxBonus = 2.0f,
            });

        static WeaponUpgradePath WildfirePath() => UpgradePath(
            "Wildfire",
            "Stops being a weapon you aim and becomes a shape you pour over the horde.",
            new UpgradeTier
            {
                title = "Pressurised",
                description = "Reach 11m to 15m, damage per tick 7 to 9. The reach was the problem - it burned well and could not get near anything.",
                damage = 9f, range = 15f,
            },
            new UpgradeTier
            {
                title = "Wide Burner",
                description = "The cone opens from 26 to 38 degrees. It stops being a jet and starts being a wall.",
                coneHalfAngle = 38f,
            },
            new UpgradeTier
            {
                title = "Backpack Tank",
                description = "Fuel 100 to 200, reload 2.2s to 1.4s. Ten seconds of fire was never enough for a wave.",
                magazineSize = 200, reloadTime = 1.4f,
            },
            new UpgradeTier
            {
                title = "White Heat",
                description = "Damage per tick 9 to 16, and it burns faster - 600 to 750 ticks a minute. Fuel goes the same way.",
                damage = 16f, fireRate = 750f,
            },
            new UpgradeTier
            {
                title = "Firestorm",
                description = "50 degree cone, 18m reach, 22 damage a tick, and the fuel never runs out. Everything in front of you is on fire, and there is no front rank left to speak of.",
                damage = 22f, range = 18f, coneHalfAngle = 50f, infiniteReserve = true,
            });

        static WeaponUpgradePath StormPath() => UpgradePath(
            "Storm",
            "One shot, and everything standing near it.",
            new UpgradeTier
            {
                title = "Capacitor",
                description = "Damage 34 to 44 on the body you actually hit.",
                damage = 44f,
            },
            new UpgradeTier
            {
                title = "Arc Extender",
                description = "The arc hops to 5 bodies instead of 3, and reaches 10m between them rather than 8.",
                chainBounces = 5, chainHopRange = 10f,
            },
            new UpgradeTier
            {
                title = "Superconductor",
                description = "The arc barely weakens down the chain - 95% carried per hop instead of 80%. Magazine 16 to 24.",
                chainDamagePerHop = 0.95f, magazineSize = 24,
            },
            new UpgradeTier
            {
                title = "Overcharge",
                description = "Damage 52, fire rate 150 to 240, reload 2.0s to 1.3s.",
                damage = 52f, fireRate = 240f, reloadTime = 1.3f,
            },
            new UpgradeTier
            {
                title = "Storm",
                description = "8 hops, 13m reach, and the arc GAINS 10% per hop instead of losing anything. The back of a tight pack is the worst place in the arena.",
                damage = 55f, chainBounces = 8, chainHopRange = 13f, chainDamagePerHop = 1.1f,
            });

        static WeaponUpgradePath BombardierPath() => UpgradePath(
            "Bombardier",
            "Four shells was never the problem. Four shells was the problem.",
            new UpgradeTier
            {
                title = "Heavy Charge",
                description = "Damage 110 to 145, blast radius 4.5m to 5.5m.",
                damage = 145f, blastRadius = 5.5f,
            },
            new UpgradeTier
            {
                title = "Quick Cycle",
                description = "Fire rate 60 to 100, reload 2.6s to 1.7s.",
                fireRate = 100f, reloadTime = 1.7f,
            },
            new UpgradeTier
            {
                title = "Drum",
                description = "Magazine 4 to 8, and every kill puts a shell back in the reserve. 24 spare shells does not survive a wave otherwise.",
                magazineSize = 8, reserveRefundPerKill = 1,
            },
            new UpgradeTier
            {
                title = "Cluster",
                description = "Three shells a trigger pull instead of one, fanned slightly. Still one round of ammunition.",
                pelletCount = 3, spread = 4f,
            },
            new UpgradeTier
            {
                title = "Bombardier",
                description = "Five shells a pull at 150 damage over a 7m blast, 12 in the tube, 1.1s reload. Fired into a crowd it is not really a weapon any more.",
                damage = 150f, pelletCount = 5, blastRadius = 7f,
                magazineSize = 12, reloadTime = 1.1f,
            });

        static WeaponUpgradePath AdrenalinePath() => UpgradePath(
            "Adrenaline",
            "Momentum you have to keep earning. Stop killing and it drains away.",
            new UpgradeTier
            {
                title = "Hair Trigger",
                description = "Fire rate 900 to 1050, spread 3.4 to 2.6 degrees.",
                fireRate = 1050f, spread = 2.6f,
            },
            new UpgradeTier
            {
                title = "Adrenaline",
                description = "Every kill stacks 4% move speed and 4% fire rate, up to eight. A stack lasts four seconds without another kill.",
                killSpeedBonus = 0.04f, killFireRateBonus = 0.04f,
                killStackMax = 8, killStackSeconds = 4f,
            },
            new UpgradeTier
            {
                title = "Deep Mag",
                description = "Damage 14 to 19, magazine 40 to 60, reload 1.5s to 1.1s.",
                damage = 19f, magazineSize = 60, reloadTime = 1.1f,
            },
            new UpgradeTier
            {
                title = "Overdrive",
                description = "Stacks climb to fifteen and hold for six seconds. At full stacks that is 60% faster on both counts.",
                killStackMax = 15, killStackSeconds = 6f,
            },
            new UpgradeTier
            {
                title = "Redline",
                description = "Damage 28, 6% per stack up to twenty - double speed and double rate of fire while you can keep the chain alive. The reserve never runs dry.",
                damage = 28f, killSpeedBonus = 0.05f, killFireRateBonus = 0.05f,
                killStackMax = 20, infiniteReserve = true,
            });

        static WeaponUpgradePath LawmanPath() => UpgradePath(
            "Lawman",
            "The only weapon that would rather you were shooting your own wall.",
            new UpgradeTier
            {
                title = "Field Repair",
                description = "Nails fired into your own barricade mend it, 22 health a hit, instead of passing through.",
                barricadeRepair = 22f,
            },
            new UpgradeTier
            {
                title = "Pinning Shot",
                description = "A nail holds whatever it hits still for 0.4s. Damage 26 to 32.",
                damage = 32f, pinSeconds = 0.4f,
            },
            new UpgradeTier
            {
                title = "Framing Nailer",
                description = "Fire rate 260 to 400, magazine 24 to 40, repair to 35 a hit.",
                fireRate = 400f, magazineSize = 40, barricadeRepair = 35f,
            },
            new UpgradeTier
            {
                title = "Rebar",
                description = "Damage 45, pins hold for 0.8s, repair to 50. A wall you are standing behind stops falling faster than you can mend it.",
                damage = 45f, pinSeconds = 0.8f, barricadeRepair = 50f,
            },
            new UpgradeTier
            {
                title = "Lawman",
                description = "Every barricade you place mounts a turret firing this weapon at a third its rate. Damage 60, repair 70, and the reserve never runs dry.",
                damage = 60f, barricadeRepair = 70f, infiniteReserve = true,
                grantsBarricadeTurret = true,
            });

        static WeaponUpgradePath ReaperPath() => UpgradePath(
            "Reaper",
            "The only healing in the game, and it only pays while you are winning.",
            new UpgradeTier
            {
                title = "Blood Money",
                description = "Every kill restores 2 health. Nothing else in the game gives any back.",
                lifestealPerKill = 2f,
            },
            new UpgradeTier
            {
                title = "Deathwish",
                description = "Up to 50% more damage as your health falls, scaling with how much is missing.",
                missingHealthDamageBonus = 0.5f,
            },
            new UpgradeTier
            {
                title = "Harvest",
                description = "Kills restore 5. Damage 40 to 52, magazine 20 to 30.",
                damage = 52f, magazineSize = 30, lifestealPerKill = 5f,
            },
            new UpgradeTier
            {
                title = "Pale Horse",
                description = "Up to 120% more damage at the edge of death, and kills restore 8. The lower you are, the harder it is to finish you.",
                missingHealthDamageBonus = 1.2f, lifestealPerKill = 8f,
            },
            new UpgradeTier
            {
                title = "Second Life",
                description = "Once a wave, a killing blow leaves you at 1 health instead - and at 1 health you hit more than twice as hard. Damage 70, kills restore 12.",
                damage = 70f, lifestealPerKill = 12f, revivesPerWave = 1,
            });

        static WeaponUpgradePath ExecutionerPath() => UpgradePath(
            "Executioner",
            "One round, one line, and everything standing in it.",
            new UpgradeTier
            {
                title = "Match Rounds",
                description = "Damage 150 to 190, and the round stops losing power through bodies - the last enemy in a line takes as much as the first.",
                damage = 190f, penetrationFalloff = 1.0f,
            },
            new UpgradeTier
            {
                title = "Breach",
                description = "Pierce 6 to 10, magazine 5 to 7. Line up a corridor.",
                pierceCount = 10, magazineSize = 7,
            },
            new UpgradeTier
            {
                title = "Cycled Bolt",
                description = "Fire rate 45 to 75, reload 2.8s to 1.8s. The damage was never the problem; the wait between shots was.",
                fireRate = 75f, reloadTime = 1.8f,
            },
            new UpgradeTier
            {
                title = "Overpenetration",
                description = "Damage 220, and each body the round passes through makes it 20% STRONGER rather than weaker. The back of the queue is the worst place to stand.",
                damage = 220f, penetrationFalloff = 1.2f,
            },
            new UpgradeTier
            {
                title = "Executioner",
                description = "300 damage, pierce 12, 20% gain per body. Any enemy at or under 30% health dies outright, whatever its maximum - which is what makes this the answer to the boss.",
                damage = 300f, pierceCount = 12, penetrationFalloff = 1.2f,
                executeThreshold = 0.3f,
            });

        /// <summary>
        /// Delivery assets. Only the ones a weapon actually references get created; hitscan
        /// needs none at all, since a weapon that leaves the field empty falls back to it.
        /// </summary>
        static WeaponDelivery LoadDelivery(string fileName) =>
            AssetDatabase.LoadAssetAtPath<WeaponDelivery>($"{DeliveryDir}/{fileName}.asset");

        static WeaponDelivery LoadOrCreateDelivery<T>(string fileName, System.Action<Fields> configure)
            where T : WeaponDelivery
        {
            string path = $"{DeliveryDir}/{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(DeliveryDir);

            var delivery = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(delivery, path);

            using (var f = new Fields(delivery)) configure(f);
            EditorUtility.SetDirty(delivery);
            return delivery;
        }

        /// <summary>
        /// Delivery assets. Hitscan needs none - a weapon that leaves the field empty falls
        /// back to it, which is why the first four weapons have no delivery of their own.
        /// </summary>
        static void LoadOrCreateDeliveries()
        {
            LoadOrCreateDelivery<ProjectileDelivery>("DLV_Grenade", f => f
                .Obj("projectilePrefab",
                     AssetDatabase.LoadAssetAtPath<GameObject>(WeaponProjectilePrefabPath)
                         ?.GetComponent<WeaponProjectile>())
                .F("blastRadius", 4.5f).F("spawnOffset", 0.6f));

            LoadOrCreateDelivery<ConeDelivery>("DLV_Flame", f => f
                .F("halfAngle", 26f).B("falloffWithDistance", true)
                .F("minimumFalloff", 0.45f).B("blockedByGeometry", true));

            LoadOrCreateDelivery<ChainDelivery>("DLV_Tesla", f => f
                .I("bounces", 3).F("hopRange", 8f).F("damagePerHop", 0.8f));

        }

        static WeaponDefinition LoadOrCreateWeapon(string fileName, System.Action<Fields> configure)
        {
            string path = $"{WeaponDir}/{fileName}.asset";

            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                EnsureFolder(WeaponDir);
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            using (var f = new Fields(weapon)) configure(f);
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            int split = assetPath.LastIndexOf('/');
            AssetDatabase.CreateFolder(assetPath[..split], assetPath[(split + 1)..]);
        }

        static AudioClip LoadClip(string name)
        {
            string path = $"{AudioDir}/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"ArenaBuilder: no audio clip at {path}.");
            return clip;
        }

        /// <summary>
        /// Writes the wave roster. Not a Fields call because SpawnEntry is a serialized class
        /// rather than an object reference, so each field is reached through its relative
        /// property.
        /// </summary>
        static void WriteSpawnTable(WaveManager waves,
            (ZombieAI prefab, int startWave, float weight, float growth, float cap)[] entries)
        {
            var so = new SerializedObject(waves);
            var table = so.FindProperty("spawnTable");
            table.arraySize = entries.Length;

            for (int i = 0; i < entries.Length; i++)
            {
                var element = table.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("startWave").intValue = entries[i].startWave;
                element.FindPropertyRelative("weightAtStart").floatValue = entries[i].weight;
                element.FindPropertyRelative("weightGrowthPerWave").floatValue = entries[i].growth;
                element.FindPropertyRelative("weightCap").floatValue = entries[i].cap;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static ZombieAI LoadEnemy(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {path}.");
                return null;
            }

            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) Debug.LogError($"ArenaBuilder: {path} has no ZombieAI component.");
            return ai;
        }

        static EnemyProjectile LoadProjectilePrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {ProjectilePrefabPath}.");
                return null;
            }

            var proj = go.GetComponent<EnemyProjectile>();
            if (proj == null) Debug.LogError($"ArenaBuilder: {ProjectilePrefabPath} has no EnemyProjectile component.");
            return proj;
        }

        /// <summary>
        /// Confirms the references that cannot fail loudly on their own actually landed.
        /// A null prefab here means zero zombies at runtime with no other symptom.
        /// </summary>
        static void Validate(WaveManager waves)
        {
            var so = new SerializedObject(waves);
            var table = so.FindProperty("spawnTable");

            if (table == null || table.arraySize == 0)
            {
                Debug.LogError("<b>Zombie Shooter</b>: the spawn table did NOT serialize onto " +
                               "WaveManager - nothing will spawn. This is a builder bug, not a " +
                               "setup mistake; re-run the builder.", waves);
                return;
            }

            // Every entry, not just the first: references not surviving serialization is the
            // exact failure this check exists for, and one silently null archetype would
            // otherwise just look like bad luck with the spawn weights.
            for (int i = 0; i < table.arraySize; i++)
            {
                var prefab = table.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");

                if (prefab == null || prefab.objectReferenceValue == null)
                {
                    Debug.LogError($"<b>Zombie Shooter</b>: spawn table entry {i} has no prefab - " +
                                   "that archetype will never appear. Re-run the builder.", waves);
                    return;
                }
            }

            var selector = Object.FindAnyObjectByType<ArenaSelector>();
            if (selector != null)
            {
                var layouts = new SerializedObject(selector).FindProperty("layouts");

                if (layouts == null || layouts.arraySize == 0)
                {
                    Debug.LogError("<b>Zombie Shooter</b>: no arena layouts serialized onto " +
                                   "ArenaSelector - every run would be fought in an empty box. " +
                                   "Re-run the builder.", selector);
                    return;
                }
            }

            Debug.Log($"<b>Zombie Shooter</b>: arena built at {ScenePath}, {table.arraySize} enemy " +
                      "archetypes wired. Press Play. WASD to move, mouse to aim, left click to fire, " +
                      "1-9 and 0 to switch weapons, R to reload, V for the pistol ultimate.");
        }

        static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath) return;

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        /// <summary>Writes private [SerializeField] fields without needing public setters.</summary>
        class Fields : System.IDisposable
        {
            readonly SerializedObject so;
            readonly Object owner;

            public Fields(Object target)
            {
                owner = target;
                so = new SerializedObject(target);
            }

            SerializedProperty Find(string name)
            {
                var p = so.FindProperty(name);
                if (p == null)
                    Debug.LogWarning($"ArenaBuilder: '{owner.GetType().Name}.{name}' not found — renamed?");
                return p;
            }

            public Fields Obj(string n, Object v) { var p = Find(n); if (p != null) p.objectReferenceValue = v; return this; }
            public Fields F(string n, float v) { var p = Find(n); if (p != null) p.floatValue = v; return this; }
            public Fields I(string n, int v) { var p = Find(n); if (p != null) p.intValue = v; return this; }
            public Fields V3(string n, Vector3 v) { var p = Find(n); if (p != null) p.vector3Value = v; return this; }
            public Fields B(string n, bool v) { var p = Find(n); if (p != null) p.boolValue = v; return this; }
            public Fields Col(string n, Color v) { var p = Find(n); if (p != null) p.colorValue = v; return this; }
            public Fields Str(string n, string v) { var p = Find(n); if (p != null) p.stringValue = v; return this; }
            public Fields E(string n, int v) { var p = Find(n); if (p != null) p.enumValueIndex = v; return this; }

            public Fields IArr(string n, int[] values)
            {
                var p = Find(n);
                if (p == null) return this;

                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).intValue = values[i];
                return this;
            }

            public Fields Arr(string n, Object[] values)
            {
                var p = Find(n);
                if (p == null) return this;

                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                return this;
            }

            public void Dispose() => so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
