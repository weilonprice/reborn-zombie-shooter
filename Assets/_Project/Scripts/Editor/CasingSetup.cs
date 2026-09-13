using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZombieShooter.EditorTools
{
    public static class CasingSetup
    {
        const string Art = "Assets/_Project/Art/Casings";
        const string Data = "Assets/_Project/Casings";
        public static readonly string[] Names = { "Pistol", "SMG", "AssaultRifle", "SniperRifle", "Shotgun", "GrenadeLauncher", "SiphonRifle" };
        static readonly float[] Diameters = { .012f, .010f, .010f, .013f, .020f, .042f, .012f };
        static readonly Color[] Colours = {
            new(.66f,.43f,.13f),new(.78f,.58f,.22f),new(.69f,.48f,.16f),new(.54f,.35f,.10f),
            new(.48f,.025f,.018f),new(.38f,.32f,.12f),new(.53f,.60f,.64f) };

        public static WeaponDefinition WeaponFor(string name) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            $"Assets/_Project/Weapons/WPN_{(name == "SniperRifle" ? "Sniper" : name)}.asset");
        public static CasingDefinition ProfileFor(string name) =>
            AssetDatabase.LoadAssetAtPath<CasingDefinition>($"{Data}/CASE_{name}.asset");

        [MenuItem("Tools/Zombie Shooter/Install Weapon Casings")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing casing assets.");
            BuildAssets();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) throw new InvalidOperationException("Open Arena before installing casings.");
            ConfigurePlayer(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            RenderCatalogue();
            Debug.Log("CASINGS_INSTALLED: seven case profiles, 21 floor sounds, current player updated.");
        }

        /// <summary>Cells per side of the casing palette. Three colours need one row.</summary>
        const int PaletteCells = 4;
        const int PaletteCellPixels = 16;

        /// <summary>
        /// One flat cell per source material, sampled point-filtered from the centre. No
        /// mipmaps: the cells are flat, so there is nothing to lose by minifying, and a
        /// mipchain would blend neighbouring cells into each other at distance - which is
        /// exactly the range these are seen at.
        /// </summary>
        static Texture2D PaletteTexture(string name, List<Color> colours)
        {
            int size = PaletteCells * PaletteCellPixels;
            string path = $"{Art}/Baked/{name}_Palette.asset";

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool created = texture == null;
            if (created) texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cell = (y / PaletteCellPixels) * PaletteCells + x / PaletteCellPixels;
                    pixels[y * size + x] = cell < colours.Count ? colours[cell] : Color.black;
                }

            texture.name = name + " casing palette";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            if (created) AssetDatabase.CreateAsset(texture, path);
            else EditorUtility.SetDirty(texture);
            return texture;
        }

        public static void ConfigurePlayer(GameObject player)
        {
            if (ProfileFor(Names[0]) == null) BuildAssets();
            var emitter = player.GetComponent<CasingEjector>();
            if (emitter == null) emitter = player.AddComponent<CasingEjector>();
            var fields = new SerializedObject(emitter);
            var weapons = fields.FindProperty("weapons");
            var profiles = fields.FindProperty("profiles");
            weapons.arraySize = profiles.arraySize = Names.Length;
            for (int i = 0; i < Names.Length; i++)
            {
                weapons.GetArrayElementAtIndex(i).objectReferenceValue = WeaponFor(Names[i]);
                profiles.GetArrayElementAtIndex(i).objectReferenceValue = ProfileFor(Names[i]);
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            var weapon = player.GetComponent<Weapon>();
            if (weapon != null)
            {
                var wf = new SerializedObject(weapon);
                var old = wf.FindProperty("shellEject");
                if (old.objectReferenceValue is ParticleSystem particles) particles.gameObject.SetActive(false);
                old.objectReferenceValue = null;
                wf.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void BuildAssets()
        {
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Art + "/Baked");
            AssetDatabase.Refresh();
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i];
                string path = $"{Art}/{name}_SpentCase.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing export " + path);
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var clone = Object.Instantiate(source);
                try
                {
                    clone.transform.position = Vector3.zero;
                    var parts = new List<CombineInstance>();
                    var palette = new List<Color>();
                    foreach (var filter in clone.GetComponentsInChildren<MeshFilter>())
                    {
                        var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                        for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                        {
                            parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = s,
                                transform = filter.transform.localToWorldMatrix });
                            string sourceName = s < materials.Length && materials[s] != null ? materials[s].name : "Body";
                            bool interior = sourceName.Contains("Interior");
                            bool rim = sourceName.Contains("Rim");
                            palette.Add(interior ? new Color(.055f,.043f,.029f)
                                : rim ? (name == "SiphonRifle" ? new Color(.7f,.73f,.75f) : new Color(.62f,.42f,.15f))
                                : Colours[i]);
                        }
                    }

                    var baked = new Mesh { name = name + " spent case" };
                    baked.CombineMeshes(parts.ToArray(), false, true);

                    // Body, rim and interior become one palette texture and one material.
                    //
                    // Three submeshes is three draw calls on every case, and ninety-six of
                    // them in flight out-draws the entire sixty-strong horde, which costs one
                    // apiece because every character already bakes its colours this way. What
                    // is given up is the per-part metallic value - and at 23 metres a 2cm case
                    // is a handful of pixels, so the difference between brass at 0.78 and a
                    // dark interior at 0.2 was never visible.
                    var cells = new Vector2[baked.subMeshCount];
                    for (int c = 0; c < cells.Length; c++)
                        cells[c] = new Vector2((c % PaletteCells + .5f) / PaletteCells,
                                               (c / PaletteCells + .5f) / PaletteCells);

                    var uv = new Vector2[baked.vertexCount];
                    var merged = new List<int>();
                    for (int s = 0; s < baked.subMeshCount; s++)
                    {
                        var triangles = baked.GetTriangles(s);
                        foreach (int vertex in triangles) uv[vertex] = cells[s];
                        merged.AddRange(triangles);
                    }
                    baked.uv = uv;
                    baked.subMeshCount = 1;
                    baked.SetTriangles(merged, 0);
                    baked.RecalculateBounds();

                    var atlas = PaletteTexture(name, palette);
                    string singlePath = $"{Art}/Baked/{name}.mat";
                    var single = AssetDatabase.LoadAssetAtPath<Material>(singlePath);
                    if (single == null)
                    {
                        single = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(single, singlePath);
                    }
                    single.SetTexture("_BaseMap", atlas);
                    single.SetColor("_BaseColor", Color.white);
                    // Brass everywhere except the shotgun, whose hull is plastic.
                    single.SetFloat("_Metallic", name == "Shotgun" ? .2f : .78f);
                    single.SetFloat("_Smoothness", .42f);
                    EditorUtility.SetDirty(single);

                    string meshPath = $"{Art}/Baked/{name}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(baked, meshPath);
                    else { EditorUtility.CopySerialized(baked, existing); Object.DestroyImmediate(baked); baked = existing; }
                    var profile = ProfileFor(name);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance<CasingDefinition>();
                        profile.displayScale = name == "Shotgun" || name == "GrenadeLauncher" ? 3.2f : 5f;
                        profile.bounce = name == "Shotgun" ? .23f : name == "GrenadeLauncher" ? .29f : .38f;
                        profile.impactVolume = name == "GrenadeLauncher" ? .34f : name == "Shotgun" ? .28f : .24f;
                        AssetDatabase.CreateAsset(profile, $"{Data}/CASE_{name}.asset");
                    }
                    profile.mesh = baked;
                    profile.materials = new[] { single };
                    profile.diameter = Diameters[i];
                    profile.floorImpacts = new AudioClip[3];
                    for (int v = 0; v < 3; v++)
                    {
                        string audioPath = $"Assets/_Project/Audio/Casings/{name}_Floor_{v+1}.wav";
                        if (AssetImporter.GetAtPath(audioPath) is AudioImporter audio)
                        {
                            audio.forceToMono = true;
                            var settings = audio.defaultSampleSettings;
                            settings.loadType = AudioClipLoadType.DecompressOnLoad;
                            settings.compressionFormat = AudioCompressionFormat.PCM;
                            audio.defaultSampleSettings = settings;
                            audio.SaveAndReimport();
                        }
                        profile.floorImpacts[v] = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                        if (profile.floorImpacts[v] == null) throw new InvalidOperationException("Missing sound " + audioPath);
                    }
                    EditorUtility.SetDirty(profile);
                }
                finally { Object.DestroyImmediate(clone); }
            }
            AssetDatabase.SaveAssets();
        }

        static void RenderCatalogue()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Casing catalogue");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    var profile = ProfileFor(Names[i]);
                    var go = new GameObject(Names[i], typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3((i-3)*.62f, .4f, 0);
                    go.transform.rotation = Quaternion.Euler(-22f, 0, -12f);
                    go.transform.localScale = Vector3.one * 10f;
                    go.GetComponent<MeshFilter>().sharedMesh = profile.mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterials = profile.materials;
                    var label = new GameObject("Label").AddComponent<TextMesh>();
                    label.transform.SetParent(root.transform, false);
                    label.transform.position = new Vector3((i-3)*.62f, -.22f, 0);
                    label.text = Names[i].Replace("Rifle", "\nRifle").Replace("Launcher", "\nLauncher");
                    label.fontSize = 48; label.characterSize = .024f;
                    label.anchor = TextAnchor.UpperCenter; label.alignment = TextAlignment.Center;
                }
                var cam = new GameObject("Camera").AddComponent<Camera>();
                cam.transform.SetParent(root.transform);
                cam.scene = scene;
                cam.transform.position = new Vector3(0,1.2f,-5);
                cam.transform.LookAt(new Vector3(0,.25f,0));
                cam.orthographic = true; cam.orthographicSize = 1.25f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.075f,.085f,.10f);
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(root.transform);
                light.type = LightType.Directional; light.intensity = 2.4f;
                light.transform.rotation = Quaternion.Euler(35, -25, 0);
                var rt = new RenderTexture(1680,840,24);
                var previous = RenderTexture.active;
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1680,840,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1680,840),0,0); tex.Apply();
                Directory.CreateDirectory("ArtSource/Casings");
                File.WriteAllBytes("ArtSource/Casings/CasingCatalogue.png",tex.EncodeToPNG());
                RenderTexture.active = previous; cam.targetTexture = null;
                Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
