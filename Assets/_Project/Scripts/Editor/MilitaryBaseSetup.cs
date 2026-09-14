using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieShooter.EditorTools
{
    /// <summary>Authoring tools for the open military base and its barbed-wire perimeter.</summary>
    public static class MilitaryBaseSetup
    {
        const string MeshPath = "Assets/_Project/Materials/BasePerimeter.asset";
        const string MaterialPath = "Assets/_Project/Materials/M_BaseWire.mat";

        public static void BuildPerimeter(Transform environment, float halfSize)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.38f, 0.41f, 0.36f));
                material.SetFloat("_Metallic", 0.55f);
                material.SetFloat("_Smoothness", 0.3f);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Rod(Vector3 a, Vector3 b, float radius)
            {
                var direction = (b - a).normalized;
                var u = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var v = Vector3.Cross(direction, u);
                int start = vertices.Count;
                const int sides = 6;
                for (int end = 0; end < 2; end++)
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * Mathf.PI * 2f / sides;
                        vertices.Add((end == 0 ? a : b) + radius * (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)));
                    }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    triangles.Add(start + i); triangles.Add(start + j); triangles.Add(start + sides + i);
                    triangles.Add(start + j); triangles.Add(start + sides + j); triangles.Add(start + sides + i);
                }
            }
            // Steel pickets, tension strands, crossed barbs, and a continuous concertina coil.
            int posts = Mathf.CeilToInt(halfSize * 2f / 3f);
            for (int i = 0; i <= posts; i++)
            {
                float x = Mathf.Lerp(-halfSize, halfSize, (float)i / posts);
                Rod(new Vector3(x, 0, 0), new Vector3(x, 2.25f, 0), 0.075f);
                Rod(new Vector3(x, 2.25f, 0), new Vector3(x, 2.65f, 0.3f), 0.06f);
            }
            for (int strand = 0; strand < 4; strand++)
            {
                float y = 0.4f + strand * 0.5f;
                Rod(new Vector3(-halfSize, y, 0), new Vector3(halfSize, y, 0), 0.025f);
                for (float x = -halfSize + 0.75f; x < halfSize; x += 1.5f)
                {
                    Rod(new Vector3(x - 0.13f, y - 0.13f, -0.07f), new Vector3(x + 0.13f, y + 0.13f, 0.07f), 0.018f);
                    Rod(new Vector3(x - 0.13f, y + 0.13f, 0.07f), new Vector3(x + 0.13f, y - 0.13f, -0.07f), 0.018f);
                }
            }
            int steps = Mathf.CeilToInt(halfSize * 2f / 1.5f) * 24;
            Vector3 Coil(int i)
            {
                float x = Mathf.Lerp(-halfSize, halfSize, (float)i / steps);
                float angle = (x + halfSize) / 1.5f * Mathf.PI * 2f;
                return new Vector3(x, 2.23f + 0.42f * Mathf.Sin(angle), 0.42f * Mathf.Cos(angle));
            }
            for (int i = 0; i < steps; i++)
            {
                var a = Coil(i); var b = Coil(i + 1);
                Rod(a, b, 0.025f);
                if (i % 6 == 0)
                    Rod(a - new Vector3(0.1f, 0.1f, 0.1f), a + new Vector3(0.1f, 0.1f, 0.1f), 0.018f);
            }
            // Existing perimeter floodlights now stand on steel poles.
            foreach (float x in new[] { -halfSize * 0.48f, halfSize * 0.48f })
                Rod(new Vector3(x, 0, -0.6f), new Vector3(x, 3.6f, -0.6f), 0.09f);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Military barbed-wire fence" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var root = new GameObject("Barbed Wire Perimeter").transform;
            root.SetParent(environment, false);
            for (int side = 0; side < 4; side++)
            {
                var fence = new GameObject(new[] { "North Barbed Wire", "East Barbed Wire", "South Barbed Wire", "West Barbed Wire" }[side]);
                fence.transform.SetParent(root, false);
                fence.transform.localRotation = Quaternion.Euler(0, side * 90f, 0);
                fence.transform.localPosition = fence.transform.localRotation * new Vector3(0, 0, halfSize);
                fence.AddComponent<MeshFilter>().sharedMesh = mesh;
                fence.AddComponent<MeshRenderer>().sharedMaterial = material;
                // Keep the original playable boundary and flow-field blocking continuous.
                var collider = fence.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 1.5f, 0);
                collider.size = new Vector3(halfSize * 2f + 2f, 3f, 2f);
                fence.isStatic = true;
            }
        }

        [MenuItem("Tools/Zombie Shooter/Apply Military Base")]
        public static void ApplyToArena()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode before editing the arena.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Arena.unity")
                throw new System.InvalidOperationException("Open Arena.unity first.");
            var environment = GameObject.Find("Environment");
            if (environment == null) throw new System.InvalidOperationException("Arena environment is missing.");
            foreach (string name in new[] { "Walls", "Barbed Wire Perimeter", "The Yard", "The Corridors", "The Ring", "The Warren", "Military Base" })
            {
                var old = environment.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var layout = new GameObject("Military Base");
            layout.transform.SetParent(environment.transform, false);
            var selector = new SerializedObject(environment.GetComponent<ArenaSelector>());
            var layouts = selector.FindProperty("layouts");
            layouts.arraySize = 1;
            layouts.GetArrayElementAtIndex(0).objectReferenceValue = layout;
            selector.FindProperty("forcedIndex").intValue = -1;
            selector.ApplyModifiedPropertiesWithoutUndo();
            BuildPerimeter(environment.transform, 45f);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MILITARY_BASE_APPLIED: zero buildings; four barbed-wire boundaries; one Military Base layout.");
        }
    }
}
