using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZombieShooter.EditorTools
{
    public static class DetailedBuildingSetup
    {
        [MenuItem("Tools/Zombie Shooter/Install Detailed Buildings")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode first.");
            var report = new List<string>();
            Directory.CreateDirectory("Assets/_Project/Prefabs/Buildings");
            AssetDatabase.Refresh();
            foreach (var name in new[] { "Shack", "Storefront", "Warehouse", "ApartmentBlock" })
            {
                string folder = $"Assets/_Project/Art/Buildings/{name}";
                string path = $"{folder}/{name}.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation = false;
                importer.bakeAxisConversion = true;
                importer.generateSecondaryUV = true;
                importer.isReadable = true; // ArenaBuilder computes fitted wall bounds from vertices.
                importer.SaveAndReimport();
                Directory.CreateDirectory(folder + "/Materials");
                AssetDatabase.Refresh();
                foreach (var original in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string matPath = folder + "/Materials/" + original.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.name = original.name;
                        AssetDatabase.CreateAsset(material, matPath);
                    }
                    var colour = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color;
                    material.SetColor("_BaseColor", colour);
                    material.SetFloat("_Smoothness", .18f);
                    material.SetFloat("_Metallic", original.name.Contains("steel") || original.name.Contains("metal") ? .25f : 0);
                    EditorUtility.SetDirty(material);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(original), material);
                }
                importer.SaveAndReimport();
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                try
                {
                    root.name = name;
                    var filters = root.GetComponentsInChildren<MeshFilter>();
                    int triangles = filters.Sum(f => f.sharedMesh.triangles.Length / 3);
                    if (filters.Length > 10 || triangles == 0 || triangles > 50000) throw new InvalidOperationException(name + " geometry budget failure");
                    var shell = filters.First(f => f.name == "MainShell");
                    var matrix = root.transform.worldToLocalMatrix * shell.transform.localToWorldMatrix;
                    var bounds = new Bounds(matrix.MultiplyPoint3x4(shell.sharedMesh.vertices[0]), Vector3.zero);
                    foreach (var vertex in shell.sharedMesh.vertices) bounds.Encapsulate(matrix.MultiplyPoint3x4(vertex));
                    if (Mathf.Abs(bounds.min.y) > .05f) throw new InvalidOperationException(name + " is not grounded: " + bounds);
                    var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.gameObject.isStatic = true;
                        if (renderer.sharedMaterials.Any(m => m == null || !m.shader.name.Contains("Universal"))) throw new InvalidOperationException(name + " invalid URP material");
                    }
                    root.isStatic = true;
                    PrefabUtility.SaveAsPrefabAsset(root, $"Assets/_Project/Prefabs/Buildings/{name}.prefab");
                    RenderPreview(root, name, bounds);
                    report.Add($"PASS {name}: {triangles} triangles, {filters.Length} renderers, UV0 + lightmap UVs, URP materials, grounded wall collider {bounds.size}");
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("ArtSource/Buildings/unity_validation.txt",report);
            Debug.Log("DETAILED_BUILDINGS_INSTALLED: " + string.Join("\n",report));
        }

        static void RenderPreview(GameObject root, string name, Bounds bounds)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var cameraObject = new GameObject("Building preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
            camera.transform.position = new Vector3(18,19,-24);
            camera.transform.LookAt(bounds.center + (name == "ApartmentBlock" ? Vector3.up : Vector3.zero));
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * .70f;
            if (name == "ApartmentBlock") camera.orthographicSize *= 1.18f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f,.18f,.20f);
            var lightObject = new GameObject("Preview sun");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2; light.transform.rotation = Quaternion.Euler(45,-30,0);
            var target = new RenderTexture(1200,900,24);
            var old = RenderTexture.active;
            var texture = new Texture2D(1200,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply();
                File.WriteAllBytes($"ArtSource/Buildings/{name}/{name}_Unity.png",texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; camera.targetTexture = null;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
