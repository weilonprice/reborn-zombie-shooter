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
        const string MaterialDir = Root + "/Materials";

        const float ArenaHalfSize = 30f;
        const float WallHeight = 3f;

        [MenuItem("Tools/Zombie Shooter/Build Playable Arena")]
        public static void Build()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Build Playable Arena",
                $"This replaces {ScenePath} and {ZombiePrefabPath}, then opens the new scene.\n\nContinue?",
                "Build", "Cancel");
            if (!confirmed) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var groundMat = CreateMaterial("M_Ground", new Color(0.20f, 0.21f, 0.23f));
            var wallMat = CreateMaterial("M_Wall", new Color(0.32f, 0.33f, 0.36f));
            var playerMat = CreateMaterial("M_Player", new Color(0.25f, 0.65f, 1.00f));
            var gunMat = CreateMaterial("M_Gun", new Color(0.90f, 0.90f, 0.95f));
            var zombieMat = CreateMaterial("M_Zombie", new Color(0.35f, 0.70f, 0.28f));
            var tracerMat = CreateUnlitMaterial("M_Tracer", new Color(1.00f, 0.85f, 0.35f));

            var zombiePrefab = BuildZombiePrefab(zombieMat);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment(groundMat, wallMat);
            var player = BuildPlayer(playerMat, gunMat, tracerMat);
            var camera = BuildCamera(player.transform);
            var waves = BuildManagers(player, zombiePrefab);
            BuildHud(player, waves);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<b>Zombie Shooter</b>: arena built at {ScenePath}. Press Play. " +
                      "WASD to move, mouse to aim, left click to fire, R to reload.");
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
            Vector3[] spots =
            {
                new(-12f, 1.25f, 8f), new(14f, 1.25f, -6f), new(4f, 1.25f, 17f),
                new(-18f, 1.25f, -14f), new(9f, 1.25f, 9f), new(-6f, 1.25f, -19f),
            };
            for (int i = 0; i < spots.Length; i++)
                CreateWall(cover, $"Block_{i}", spots[i], new Vector3(3.5f, 2.5f, 3.5f), wallMat);
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

        static GameObject BuildPlayer(Material bodyMat, Material gunMat, Material tracerMat)
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

            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;

            var health = player.AddComponent<Health>();
            using (var f = new Fields(health)) f.F("maxHealth", 100f);

            var move = player.AddComponent<PlayerController>();
            using (var f = new Fields(move))
                f.F("moveSpeed", 7f).F("acceleration", 60f).F("turnSpeed", 900f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(player.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.35f);

            var tracer = muzzle.gameObject.AddComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.widthMultiplier = 0.06f;
            tracer.numCapVertices = 2;
            tracer.sharedMaterial = tracerMat;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.positionCount = 0;
            tracer.enabled = false;

            var weapon = player.AddComponent<Weapon>();
            using (var f = new Fields(weapon))
            {
                f.F("damage", 25f).F("fireRate", 480f).F("range", 60f).F("spread", 1.5f)
                 .I("pelletsPerShot", 1).I("magazineSize", 30).F("reloadTime", 1.4f)
                 .Obj("muzzle", muzzle).Obj("tracer", tracer).F("tracerDuration", 0.03f);
            }

            return player;
        }

        static GameObject BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            // Solid dark clear reads better top-down than a skybox you never see.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            go.AddComponent<AudioListener>();

            var follow = go.AddComponent<CameraFollow>();
            using (var f = new Fields(follow))
            {
                f.Obj("target", target)
                 .V3("offset", new Vector3(0f, 20f, -11f))
                 .F("smoothTime", 0.12f).F("aimLead", 0.18f).F("maxLead", 5f);
            }

            go.transform.position = target.position + new Vector3(0f, 20f, -11f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -20f, 11f).normalized, Vector3.up);
            return go;
        }

        // ---------------------------------------------------------------- zombie

        static ZombieAI BuildZombiePrefab(Material mat)
        {
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
                 .I("scoreValue", 10);
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(zombie, ZombiePrefabPath);
            Object.DestroyImmediate(zombie);

            return saved.GetComponent<ZombieAI>();
        }

        // ---------------------------------------------------------------- managers

        static WaveManager BuildManagers(GameObject player, ZombieAI zombiePrefab)
        {
            var go = new GameObject("--- Systems ---");

            var gm = go.AddComponent<GameManager>();
            using (var f = new Fields(gm)) f.Obj("playerHealth", player.GetComponent<Health>());

            var waves = go.AddComponent<WaveManager>();
            using (var f = new Fields(waves))
            {
                f.Obj("zombiePrefab", zombiePrefab).Obj("player", player.transform)
                 .F("spawnRadius", 24f).F("minDistanceFromPlayer", 12f)
                 .I("firstWaveCount", 5).F("countGrowth", 2.5f).I("maxAliveAtOnce", 60)
                 .F("timeBetweenSpawns", 0.45f).F("timeBetweenWaves", 5f);
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

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
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
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(340f, 60f), "30 / 30");

            var waveLabel = CreateText(canvasGo.transform, "WaveLabel", font, 30, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(520f, 44f), "WAVE 1");

            var scoreLabel = CreateText(canvasGo.transform, "ScoreLabel", font, 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(400f, 44f), "SCORE 0");

            var centreLabel = CreateText(canvasGo.transform, "CentreLabel", font, 46, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 300f), string.Empty);

            var hud = canvasGo.AddComponent<HUD>();
            using (var f = new Fields(hud))
            {
                f.Obj("playerHealth", player.GetComponent<Health>())
                 .Obj("weapon", player.GetComponent<Weapon>())
                 .Obj("waves", waves)
                 .Obj("healthFill", fill)
                 .Obj("healthLabel", healthLabel)
                 .Obj("ammoLabel", ammoLabel)
                 .Obj("waveLabel", waveLabel)
                 .Obj("scoreLabel", scoreLabel)
                 .Obj("centreLabel", centreLabel);
            }
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
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            // Stretched rects (min != max on an axis) use sizeDelta as an inset, so zero it.
            rect.pivot = new Vector2(
                Mathf.Approximately(anchorMin.x, anchorMax.x) ? anchorMin.x : 0.5f,
                Mathf.Approximately(anchorMin.y, anchorMax.y) ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
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

            public void Dispose() => so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
