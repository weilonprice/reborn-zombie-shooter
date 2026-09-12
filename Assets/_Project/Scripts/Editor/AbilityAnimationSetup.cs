using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>Installs the four ability clips without regenerating the arena or its tuning.</summary>
    public static class AbilityAnimationSetup
    {
        const string Art = "Assets/_Project/Art";
        public const string ZombiePack = Art + "/Animations/Abilities/ZombieAbilities.fbx";
        public const string SurvivorPack = Art + "/Animations/Abilities/SurvivorAbilities.fbx";
        const string ZombieModel = Art + "/Characters/NormalZombie/NormalZombie.fbx";
        const string ZombieController = Art + "/Characters/NormalZombie/NormalZombie.controller";
        const string PlayerController = Art + "/Characters/MainCharacter/MainCharacter.controller";

        [MenuItem("Tools/Zombie Shooter/Install Ability Animations")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing animation assets.");
            ConfigureImports();
            RefreshControllers();
            foreach (string name in new[] { "Screamer", "Revenant", "Leaper" })
            {
                string path = $"Assets/_Project/Prefabs/{name}.prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    InstallOnEnemy(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ABILITY_ANIMATIONS_INSTALLED: Scream, GetUp, Leap, Ultimate; three rigged prefabs and both controllers saved.");
        }

        public static void ConfigureImports()
        {
            ConfigureImport(ZombiePack);
            ConfigureImport(SurvivorPack);
        }

        static void ConfigureImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"Missing animation export: {path}");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException($"No exported takes in {path}");
            foreach (var clip in clips)
            {
                clip.name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
                clip.loopTime = clip.name == "Ultimate";
                clip.lockRootRotation = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        public static UnityEngine.Object[] WithAbilities(string modelPath, string pack)
        {
            var result = new List<UnityEngine.Object>(AssetDatabase.LoadAllAssetsAtPath(modelPath));
            result.AddRange(AssetDatabase.LoadAllAssetsAtPath(pack));
            return result.ToArray();
        }

        static void RefreshControllers()
        {
            AddStates(ZombieController, ZombiePack, new[] { "Scream", "GetUp", "Leap" });
            AddStates(PlayerController, SurvivorPack, new[] { "Ultimate" });
        }

        static void AddStates(string controllerPath, string pack, string[] names)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) throw new InvalidOperationException($"Missing controller: {controllerPath}");
            var machine = controller.layers[0].stateMachine;
            foreach (string name in names)
            {
                AnimationClip clip = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pack))
                    if (asset is AnimationClip candidate && candidate.name == name) clip = candidate;
                if (clip == null) throw new InvalidOperationException($"Missing {name} in {pack}");
                AnimatorState state = null;
                foreach (var child in machine.states)
                    if (child.state.name == name) state = child.state;
                if (state == null) state = machine.AddState(name);
                state.motion = clip;
                state.speed = 1f;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
        }

        public static void InstallOnEnemy(GameObject enemy)
        {
            var model = enemy.transform.Find("AbilityModel");
            if (model == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieModel);
                if (source == null) throw new InvalidOperationException("Import NormalZombie.fbx first.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, enemy.transform);
                instance.name = "AbilityModel";
                model = instance.transform;
                // These were visual markers on the unrigged prototypes, not gameplay roots.
                foreach (string childName in new[] { "Body", "Maw", "Haunches" })
                {
                    var child = enemy.transform.Find(childName);
                    if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            var capsule = enemy.GetComponent<CharacterController>();
            float height = capsule != null ? capsule.height : 1.9f;
            float scale = height / 2.1f;
            model.localPosition = Vector3.down * (height * .5f);
            model.localRotation = Quaternion.identity;
            model.localScale = enemy.name == "Screamer"
                ? new Vector3(scale * .85f, scale * 1.12f, scale * .85f) : Vector3.one * scale;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieController);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var driver = enemy.GetComponent<ZombieAnimator>();
            if (driver == null) driver = enemy.AddComponent<ZombieAnimator>();
            var fields = new SerializedObject(driver);
            fields.FindProperty("animator").objectReferenceValue = animator;
            fields.ApplyModifiedPropertiesWithoutUndo();

            // The old shrinking/spinning primitive effect hides a skeletal death in 0.18s.
            var pop = enemy.GetComponent<DeathPop>();
            if (pop != null) UnityEngine.Object.DestroyImmediate(pop);
            var ai = new SerializedObject(enemy.GetComponent<ZombieAI>());
            ai.FindProperty("deathLinger").floatValue = .75f;
            ai.ApplyModifiedPropertiesWithoutUndo();

            var material = ArchetypeMaterial(enemy.name);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMaterial = material;
                skin.quality = SkinQuality.Bone2;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skin.skinnedMotionVectors = false;
                // Includes the prone recovery and spread hands even when the bind bounds
                // are off-screen; fixed bounds avoid per-frame recalculation for the horde.
                skin.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(4, 5, 5));
            }
        }

        static Material ArchetypeMaterial(string name)
        {
            string path = $"Assets/_Project/Materials/M_{name}_Skin.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                Art + "/Characters/NormalZombie/NormalZombie_Palette.png"));
            mat.SetColor("_BaseColor", name == "Screamer" ? new Color(1.25f, .7f, 1.4f)
                : name == "Leaper" ? new Color(1.3f, 1.05f, .65f) : new Color(.75f, 1.1f, .9f));
            mat.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
