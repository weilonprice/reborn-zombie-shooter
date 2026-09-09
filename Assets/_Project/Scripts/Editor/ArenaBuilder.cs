using UnityEditor;
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
        const string DeployableDir = Root + "/Deployables";
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
            BuildBossPrefab();
            BuildBarricadePrefab();
            BuildBarrelPrefab();
            BuildClaymorePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildRunnerPrefab();
            BuildRangedPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Pass 2 - build the scene from assets loaded fresh off disk.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var groundMat = LoadMaterial("M_Ground");
            var wallMat = LoadMaterial("M_Wall");
            var playerMat = LoadMaterial("M_Player");
            var gunMat = LoadMaterial("M_Gun");
            var tracerMat = LoadMaterial("M_Tracer");
            var sparkMat = LoadMaterial("M_Spark");
            var brassMat = LoadMaterial("M_Brass");
            var zombiePrefab = LoadZombiePrefab();
            var brutePrefab = LoadBrutePrefab();
            var runnerPrefab = LoadRunnerPrefab();
            var rangedPrefab = LoadRangedPrefab();

            BuildEnvironment(groundMat, wallMat);
            var player = BuildPlayer(playerMat, gunMat, tracerMat, sparkMat, brassMat);
            BuildCamera(player.transform);
            var waves = BuildManagers(player, zombiePrefab, brutePrefab, runnerPrefab, rangedPrefab, sparkMat);
            BuildHud(player, waves);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Validate(waves);
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

            // A few blocks so the empty field has landmarks and sightline breaks.
            var cover = new GameObject("Cover").transform;
            cover.SetParent(env, false);
            // Scaled out with the arena and roughly doubled. Sparse cover in a 90x90 space
            // reads as an empty field; these are the anchors funnels get built against.
            (Vector3 pos, Vector3 size)[] spots =
            {
                (new(-18f, 1.25f, 12f), new(3.5f, 2.5f, 3.5f)),
                (new(21f, 1.25f, -9f),  new(3.5f, 2.5f, 3.5f)),
                (new(6f, 1.25f, 26f),   new(3.5f, 2.5f, 3.5f)),
                (new(-27f, 1.25f, -21f),new(3.5f, 2.5f, 3.5f)),
                (new(14f, 1.25f, 14f),  new(3.5f, 2.5f, 3.5f)),
                (new(-9f, 1.25f, -28f), new(3.5f, 2.5f, 3.5f)),
                // Longer slabs give barricade lines something to anchor against.
                (new(0f, 1.25f, -14f),  new(9f, 2.5f, 3f)),
                (new(-33f, 1.25f, 4f),  new(3f, 2.5f, 9f)),
                (new(33f, 1.25f, 18f),  new(3f, 2.5f, 9f)),
                (new(18f, 1.25f, -30f), new(9f, 2.5f, 3f)),
                (new(-20f, 1.25f, 30f), new(7f, 2.5f, 3f)),
                (new(30f, 1.25f, -20f), new(3.5f, 2.5f, 3.5f)),
                (new(-34f, 1.25f, -34f),new(3.5f, 2.5f, 3.5f)),
                (new(34f, 1.25f, 34f),  new(3.5f, 2.5f, 3.5f)),
            };
            for (int i = 0; i < spots.Length; i++)
                CreateWall(cover, $"Block_{i}", spots[i].pos, spots[i].size, wallMat);
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

        // ---------------------------------------------------------------- player

        static GameObject BuildPlayer(Material bodyMat, Material gunMat, Material tracerMat, Material sparkMat, Material brassMat)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            // The CharacterController is the physical collider; drop the primitive's own.
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            // A visible barrel makes facing readable from a top-down camera.
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Gun";
            gun.transform.SetParent(player.transform, false);
            gun.transform.localPosition = new Vector3(0f, 0f, 0.75f);
            gun.transform.localScale = new Vector3(0.22f, 0.22f, 1.2f);
            gun.GetComponent<MeshRenderer>().sharedMaterial = gunMat;
            Object.DestroyImmediate(gun.GetComponent<BoxCollider>());

            // Second pistol, hidden until the Akimbo tier is bought. Built here rather than
            // instantiated on purchase so nothing has to load a prefab mid-fight.
            var offHandGun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            offHandGun.name = "OffHandGun";
            offHandGun.transform.SetParent(player.transform, false);
            offHandGun.transform.localPosition = new Vector3(-0.30f, 0f, 0.75f);
            offHandGun.transform.localScale = new Vector3(0.22f, 0.22f, 1.2f);
            offHandGun.GetComponent<MeshRenderer>().sharedMaterial = gunMat;
            Object.DestroyImmediate(offHandGun.GetComponent<BoxCollider>());
            offHandGun.SetActive(false);

            player.AddComponent<AudioListener>();

            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
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

            var loadout = player.AddComponent<WeaponLoadout>();
            using (var f = new Fields(loadout))
            {
                f.Obj("weapon", weapon).F("swapCooldown", 0.25f).Arr("slots", arsenal);
            }

            var ultimate = player.AddComponent<UltimateAbility>();
            using (var f = new Fields(ultimate))
            {
                f.Obj("weapon", weapon).Obj("movement", move).Obj("health", health)
                 .F("spinDegreesPerSecond", 540f)
                 .F("openingSlowMotion", 0.35f).F("openingTimeScale", 0.35f)
                 .F("openingTrauma", 0.5f);
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

            PrefabUtility.SaveAsPrefabAsset(zombie, ZombiePrefabPath);
            Object.DestroyImmediate(zombie);
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

        static WaveManager BuildManagers(GameObject player, ZombieAI zombiePrefab, ZombieAI brutePrefab,
            ZombieAI runnerPrefab, ZombieAI rangedPrefab, Material sparkMat)
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
                f.Arr("trees", new Object[] { LoadOrCreatePistolUpgrades() });
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

            var waves = go.AddComponent<WaveManager>();
            using (var f = new Fields(waves))
            {
                f.Obj("zombiePrefab", zombiePrefab)
                 .Obj("brutePrefab", brutePrefab)
                 .Obj("runnerPrefab", runnerPrefab)
                 .Obj("rangedPrefab", rangedPrefab)
                 .Obj("player", player.transform)
                 .F("spawnRadius", 38f).F("minDistanceFromPlayer", 14f).F("spawnHeight", 1f)
                 .I("firstWaveCount", 5).F("countGrowth", 2.5f).I("maxAliveAtOnce", 60).I("finalWave", 15)
                 .Obj("bossPrefab", LoadBossPrefab()).F("bossArrivesAt", 0.4f)
                 .F("timeBetweenSpawns", 0.45f)
                 .I("bruteStartWave", 2).I("runnerStartWave", 3).I("rangedStartWave", 4);
            }

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

            // Main shop card in the center
            var shopCard = CreatePanel(overlay.transform, "ShopCard", uiSprite,
                new Color(0.11f, 0.13f, 0.18f, 0.96f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 720f));
            shopCard.GetComponent<Image>().raycastTarget = true;

            // Header Title
            var titleText = CreateText(shopCard.transform, "Title", font, 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(800f, 48f), "ARMORY SHOP");
            titleText.color = new Color(0.95f, 0.95f, 0.95f);

            // Gold counter
            var goldText = CreateText(shopCard.transform, "Gold", font, 24, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(400f, 36f), "GOLD: $0");
            goldText.color = new Color(1.0f, 0.85f, 0.25f);

            // Section 1: Firearms & Ammo (Left column: X = -260)
            var wpnHeader = CreateText(shopCard.transform, "WpnHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -135f), new Vector2(480f, 30f), "— WEAPONS —");
            wpnHeader.color = new Color(0.45f, 0.82f, 1.0f);

            var (shotgunBtn, shotgunTxt) = CreateShopItemButton(shopCard.transform, "BuyShotgun", uiSprite, font,
                new Vector2(-260f, -180f), new Vector2(480f, 48f),
                "SHOTGUN (Slot 2)\n8 Shells · Heavy Spread Knockback", "$150 BUY");

            var (arBtn, arTxt) = CreateShopItemButton(shopCard.transform, "BuyAR", uiSprite, font,
                new Vector2(-260f, -236f), new Vector2(480f, 48f),
                "ASSAULT RIFLE (Slot 3)\n30 Rounds · 600 RPM Rapid Auto", "$250 BUY");

            var (sniperBtn, sniperTxt) = CreateShopItemButton(shopCard.transform, "BuySniper", uiSprite, font,
                new Vector2(-260f, -292f), new Vector2(480f, 48f),
                "SNIPER RIFLE (Slot 4)\n5 Rounds · 150 Dmg Heavy Pierce", "$350 BUY");

            var ammoHeader = CreateText(shopCard.transform, "AmmoHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -348f), new Vector2(480f, 26f), "— AMMO —");
            ammoHeader.color = new Color(0.45f, 0.82f, 1.0f);

            var (ammoBtn, ammoTxt) = CreateShopItemButton(shopCard.transform, "BuyAmmo", uiSprite, font,
                new Vector2(-260f, -392f), new Vector2(480f, 48f),
                "FULL AMMO CRATE\nRestocks reserve ammo for all guns", "$50 REFILL ALL");

            // Right column, vacated by the mod cores. The deployables used to sit in the
            // left column under the ammo crate at spacings tighter than the 48px buttons
            // are tall, so the barrel overlapped the crate and the claymore overlapped the
            // barricade. They get a column of their own rather than a nudge.
            var fortHeader = CreateText(shopCard.transform, "FortHeader", font, 20, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(260f, -135f), new Vector2(480f, 30f), "— FORTIFICATIONS —");
            fortHeader.color = new Color(1.0f, 0.55f, 0.35f);

            var (barricadeBtn, barricadeTxt) = CreateShopItemButton(shopCard.transform, "BuyBarricade", uiSprite, font,
                new Vector2(260f, -180f), new Vector2(480f, 48f),
                "WOODEN BARRICADE (150 HP)\nBlocks horde path & enemy fire · [F] Place", "$40 BUY");

            var (barrelBtn, barrelTxt) = CreateShopItemButton(shopCard.transform, "BuyBarrel", uiSprite, font,
                new Vector2(260f, -236f), new Vector2(480f, 48f),
                "EXPLOSIVE BARREL\n220 dmg blast, chains to other barrels. Hurts you too.", "$60 BUY");

            var (claymoreBtn, claymoreTxt) = CreateShopItemButton(shopCard.transform, "BuyClaymore", uiSprite, font,
                new Vector2(260f, -292f), new Vector2(480f, 48f),
                "CLAYMORE\nDirectional mine, 160 dmg in a cone. One use.", "$75 BUY");

            // Footer action buttons
            var (deployBtn, deployTxt) = CreateActionButton(shopCard.transform, "DeployButton", uiSprite, font,
                new Vector2(-100f, 46f), new Vector2(380f, 54f),
                "DEPLOY / NEXT WAVE [SPACE]", new Color(0.18f, 0.55f, 0.28f, 1f));

            var (closeBtn, closeTxt) = CreateActionButton(shopCard.transform, "CloseButton", uiSprite, font,
                new Vector2(280f, 46f), new Vector2(240f, 54f),
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
                Vector2.zero, new Vector2(1180f, 760f));

            var upgradeWeaponLabel = CreateText(upgradeCard.transform, "UpgradeWeapon", font, 34,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(900f, 44f), "PISTOL");

            var upgradeRuleLabel = CreateText(upgradeCard.transform, "UpgradeRule", font, 18,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(1000f, 28f),
                "One path to 5  ·  a second to 3  ·  the third stays locked");
            upgradeRuleLabel.color = new Color(0.72f, 0.62f, 0.42f);

            var pathTitles = new Object[3];
            var tierButtons = new Object[15];
            var tierLabels = new Object[15];

            for (int path = 0; path < 3; path++)
            {
                float columnX = -370f + path * 370f;

                pathTitles[path] = CreateText(upgradeCard.transform, $"PathTitle_{path}", font, 22,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(columnX, -126f), new Vector2(340f, 30f), "PATH");

                for (int tier = 0; tier < 5; tier++)
                {
                    int index = path * 5 + tier;
                    var (btn, btnLabel) = CreateButton(upgradeCard.transform, $"Tier_{path}_{tier}",
                        uiSprite, new Color(0.18f, 0.19f, 0.22f, 0.85f),
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(columnX, -178f - tier * 96f), new Vector2(340f, 84f),
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
                 .Obj("shotgunButton", shotgunBtn).Obj("shotgunBtnText", shotgunTxt)
                 .Obj("arButton", arBtn).Obj("arBtnText", arTxt)
                 .Obj("sniperButton", sniperBtn).Obj("sniperBtnText", sniperTxt)
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
                .Str("displayName", "Pistol")
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
                .Str("displayName", "Shotgun")
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
                .Str("displayName", "Assault Rifle")
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
                .Str("displayName", "Sniper Rifle")
                .F("damage", 150f).F("fireRate", 45f).F("range", 200f).F("spread", 0.1f)
                .I("pelletsPerShot", 1).E("fireMode", (int)FireMode.SemiAuto)
                .I("tags", (int)WeaponTags.Precision)
                .I("pierceCount", 6).F("penetrationFalloff", 0.85f)
                .I("magazineSize", 5).I("maxReserveAmmo", 30).F("reloadTime", 2.8f)
                .F("fireTrauma", 0.42f).F("recoilKick", 0.26f).F("knockbackMultiplier", 3f)
                .Obj("fireClip", gunshot).Obj("impactClip", impact)
                .F("fireVolume", 0.7f).F("impactVolume", 0.5f)
                .F("tracerWidth", 0.13f).F("tracerDuration", 0.1f).I("shellsPerShot", 1));

            return new[] { pistol, shotgun, assault, sniper };
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

        static WeaponUpgradeTree LoadOrCreatePistolUpgrades()
        {
            const string path = UpgradeDir + "/UPG_Pistol.asset";

            var existing = AssetDatabase.LoadAssetAtPath<WeaponUpgradeTree>(path);
            if (existing != null) return existing;

            EnsureFolder(UpgradeDir);

            // One authored path for now; the other two columns are placeholders so the panel
            // still lays out three and the 5-3-0 rule has somewhere to go once they exist.
            var gunslinger = UpgradePath(
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

            var secondPath = UpgradePath("- TO BE DESIGNED -", "");
            var thirdPath = UpgradePath("- TO BE DESIGNED -", "");

            var tree = ScriptableObject.CreateInstance<WeaponUpgradeTree>();
            tree.EditorInitialise(LoadOrCreateWeapon("WPN_Pistol", _ => { }),
                                  new[] { gunslinger, secondPath, thirdPath });

            AssetDatabase.CreateAsset(tree, path);
            return tree;
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

        static ZombieAI LoadZombiePrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {ZombiePrefabPath}.");
                return null;
            }

            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) Debug.LogError($"ArenaBuilder: {ZombiePrefabPath} has no ZombieAI component.");
            return ai;
        }

        static ZombieAI LoadBrutePrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {BrutePrefabPath}.");
                return null;
            }

            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) Debug.LogError($"ArenaBuilder: {BrutePrefabPath} has no ZombieAI component.");
            return ai;
        }

        static ZombieAI LoadRunnerPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {RunnerPrefabPath}.");
                return null;
            }

            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) Debug.LogError($"ArenaBuilder: {RunnerPrefabPath} has no ZombieAI component.");
            return ai;
        }

        static ZombieAI LoadRangedPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
            if (go == null)
            {
                Debug.LogError($"ArenaBuilder: no prefab at {RangedPrefabPath}.");
                return null;
            }

            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) Debug.LogError($"ArenaBuilder: {RangedPrefabPath} has no ZombieAI component.");
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
            var zombie = so.FindProperty("zombiePrefab");
            var brute = so.FindProperty("brutePrefab");
            var runner = so.FindProperty("runnerPrefab");
            var ranged = so.FindProperty("rangedPrefab");

            if (zombie == null || zombie.objectReferenceValue == null ||
                brute == null || brute.objectReferenceValue == null ||
                runner == null || runner.objectReferenceValue == null ||
                ranged == null || ranged.objectReferenceValue == null)
            {
                Debug.LogError("<b>Zombie Shooter</b>: one or more enemy prefabs did NOT serialize onto " +
                               "WaveManager - enemies will fail to spawn. This is a builder bug, not a " +
                               "setup mistake; re-run the builder.", waves);
                return;
            }

            Debug.Log($"<b>Zombie Shooter</b>: arena built at {ScenePath}, all 4 enemy archetypes wired. " +
                      "Press Play. WASD to move, mouse to aim, left click to fire, 1-4 to switch weapons, R to reload.");
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
