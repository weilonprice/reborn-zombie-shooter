using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    public static class HitZoneRepair
    {
        [MenuItem("Tools/Zombie Shooter/Repair And Validate Zombie Hitboxes")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            int repaired = 0, probes = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponent<ZombieAI>() == null || asset.GetComponent<HitZoneSet>() == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var box in root.GetComponentsInChildren<Hitbox>(true))
                        UnityEngine.Object.DestroyImmediate(box.gameObject);
                    var marker = root.transform.Find("Hitbox_Limbs");
                    if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
                    ArenaBuilder.AddHitZones(root, root.transform);
                    if (root.GetComponentsInChildren<Hitbox>(true).Count(h => h.name.StartsWith("Hit_Shoulder.")) != 2)
                        throw new InvalidOperationException(path + " missing shoulder coverage");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    repaired++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // Probe each collider directly so arena geometry cannot mask failures.
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                try
                {
                    var animator = instance.GetComponentInChildren<Animator>();
                    var clips = animator != null && animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.animationClips.Distinct().ToArray() : Array.Empty<AnimationClip>();
                    foreach (var clip in clips)
                        for (int frame = 0; frame < 4; frame++)
                        {
                            clip.SampleAnimation(instance, clip.length * frame / 4f);
                            Physics.SyncTransforms();
                            foreach (var box in instance.GetComponentsInChildren<Hitbox>())
                            {
                                if (!box.name.StartsWith("Hit_Shoulder.") && !box.name.StartsWith("Hit_UpperArm.") && !box.name.StartsWith("Hit_Forearm.")) continue;
                                foreach (var collider in box.GetComponents<Collider>())
                                {
                                    var center = collider.bounds.center;
                                    foreach (var direction in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                                    {
                                        if (!collider.Raycast(new Ray(center - direction * 3f, direction), out _, 6f))
                                            throw new InvalidOperationException(path + ": missed " + box.name + " in " + clip.name);
                                        probes++;
                                    }
                                }
                            }
                        }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
            if (repaired == 0 || probes == 0) throw new InvalidOperationException("No zombie hitboxes tested.");
            Debug.Log($"HITBOX_REPAIR_PASSED: {repaired} prefabs repaired, {probes} animated shoulder/arm ray checks passed.");
        }
    }
}
