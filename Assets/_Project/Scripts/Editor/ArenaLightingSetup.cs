using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieShooter.EditorTools
{
    /// <summary>Shared dusk treatment for generated arenas and existing arena scenes.</summary>
    public static class ArenaLightingSetup
    {
        const string ProfilePath = "Assets/_Project/Materials/ArenaDusk.asset";
        const string RigName = "Arena Dusk Lighting";

        public static void CreateProfile()
        {
            // Like upgrade assets, artist tuning survives an arena rebuild.
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = 0.55f;
            grade.contrast.value = 8f;
            grade.saturation.value = -12f;
            var split = profile.Add<SplitToning>(true);
            split.shadows.value = new Color(0.43f, 0.49f, 0.53f);
            split.highlights.value = new Color(0.54f, 0.51f, 0.46f);
            split.balance.value = -15f;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.1f;
            bloom.intensity.value = 0.22f;
            bloom.scatter.value = 0.55f;
            var vignette = profile.Add<Vignette>(true);
            vignette.color.value = new Color(0.015f, 0.025f, 0.035f);
            vignette.intensity.value = 0.12f;
            vignette.smoothness.value = 0.4f;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        public static void BuildEnvironment(float halfSize)
        {
            var existing = GameObject.Find(RigName);
            if (existing != null) Object.DestroyImmediate(existing);
            var rig = new GameObject(RigName);
            var sunObject = GameObject.Find("Directional Light");
            var sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
            if (sun == null)
            {
                sunObject = new GameObject("Directional Light");
                sun = sunObject.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.color = new Color(0.67f, 0.79f, 1f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.43f, 0.53f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.29f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.18f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.085f, 0.12f, 0.16f);
            // Camera is ~23 m above the action: haze starts beyond the combat foreground.
            RenderSettings.fogStartDistance = 38f;
            RenderSettings.fogEndDistance = 105f;

            var volume = rig.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            // Fixtures sit atop boundary walls, so they never obstruct movement or spawn paths.
            for (int side = 0; side < 4; side++)
                for (int slot = -1; slot <= 1; slot += 2)
                {
                    var pos = side < 2
                        ? new Vector3(slot * halfSize * 0.48f, 3.6f, (side == 0 ? 1 : -1) * (halfSize - 0.6f))
                        : new Vector3((side == 2 ? 1 : -1) * (halfSize - 0.6f), 3.6f, slot * halfSize * 0.48f);
                    var lamp = new GameObject("Sodium floodlight");
                    lamp.transform.SetParent(rig.transform, false);
                    lamp.transform.position = pos;
                    lamp.transform.rotation = Quaternion.LookRotation(new Vector3(-pos.x * 0.08f, -1f, -pos.z * 0.08f));
                    var light = lamp.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = new Color(1f, 0.59f, 0.25f);
                    light.intensity = 9f;
                    light.range = 20f;
                    light.spotAngle = 100f;
                    light.innerSpotAngle = 55f;
                    // One shadow-casting sun; no eight extra shadow maps per frame.
                    light.shadows = LightShadows.None;
                }
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.allowHDR = true;
            camera.backgroundColor = new Color(0.085f, 0.12f, 0.16f);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1; // Default layer, containing the arena's global volume.
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        [MenuItem("Tools/Zombie Shooter/Apply Arena Dusk Lighting")]
        public static void ApplyToArena()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Arena.unity")
                throw new System.InvalidOperationException("Open Arena.unity before applying arena lighting.");
            CreateProfile();
            BuildEnvironment(45f);
            var camera = Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Arena needs a main camera.");
            ConfigureCamera(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Arena dusk lighting applied: cool sun, sodium lights, ACES, bloom and vignette.");
        }

        // Batch entry point also serves as an import/serialization validation.
        public static void ApplyBatch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity");
            ApplyToArena();
            var volume = GameObject.Find(RigName).GetComponent<Volume>();
            if (volume.sharedProfile == null || volume.sharedProfile.components.Count != 5)
                throw new System.InvalidOperationException("Arena dusk volume did not serialize correctly.");
        }
    }
}
