using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    public static class WeaponGripValidation
    {
        [MenuItem("Tools/Zombie Shooter/Validate Weapon Grips")]
        public static void Run()
        {
            Directory.CreateDirectory("ArtSource/Weapons/GripValidation");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Grip preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Art/Characters/MainCharacter/MainCharacter.fbx"), root.transform);
                model.name = "MainCharacter_Model";
                model.transform.localPosition = Vector3.down;
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Art/Characters/MainCharacter/MainCharacter.controller");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0);
                var holder = new GameObject("GunHolder").transform;
                holder.SetParent(root.transform, false);
                var offHolder = new GameObject("OffHandHolder").transform;
                offHolder.SetParent(root.transform, false);
                string[] names = { "Pistol", "Shotgun", "AssaultRifle", "SniperRifle", "Flamethrower",
                    "TeslaCoil", "GrenadeLauncher", "SMG", "NailGun", "SiphonRifle" };
                GameObject[] main = names.Select(n => Make(n, holder)).ToArray();
                GameObject[] off = names.Select(n => Make(n, offHolder)).ToArray();
                var grip = new PlayerWeaponGrip(root.transform, main, off);
                var lines = new System.Collections.Generic.List<string>();
                Directory.CreateDirectory("ArtSource/Weapons/GripValidation");
                for (int i = 0; i < names.Length; i++)
                {
                    for (int j = 0; j < names.Length; j++) { main[j].SetActive(i == j); off[j].SetActive(i == j); }
                    foreach (bool dual in new[] { false, true })
                    foreach (string pose in new[] { "Aim", "Fire", "Reload", "GetShot", "Stagger", "Ultimate", "Death" })
                    foreach (float sample in new[] { 0f, .4f, .9f })
                    {
                        offHolder.gameObject.SetActive(dual);
                        bool full = pose == "Ultimate" || pose == "Death";
                        animator.SetLayerWeight(1, full ? 0 : 1);
                        animator.Play(full ? pose : "Idle", 0, sample);
                        animator.Play(full ? "Aim" : pose, 1, sample);
                        animator.Update(0);
                        foreach (var held in new[] { main[i], off[i] })
                        {
                            var gunAnimator = held.GetComponent<Animator>();
                            gunAnimator.Play(pose == "Fire" || pose == "Reload" ? pose : "Idle", 0, sample);
                            if (held.activeInHierarchy) gunAnimator.Update(0);
                        }
                        grip.Apply(i);
                        var hand = PlayerWeaponGrip.Find(model.transform, "Hand.R");
                        var socket = PlayerWeaponGrip.Find(main[i].transform, "GripSocket");
                        float error = Vector3.Distance(hand.TransformPoint(new Vector3(0, .12f, 0)), socket.position);
                        if (pose == "Aim")
                        {
                            var tip = PlayerWeaponGrip.Find(main[i].transform, "MuzzleSocket");
                            if (Vector3.Dot(tip.position - socket.position, root.transform.forward) <= 0)
                                throw new Exception(names[i] + " points away from gameplay aim");
                        }
                        if (error > .025f) throw new Exception($"{names[i]} {pose} dual={dual}: grip error {error:F4}m");
                        if (dual)
                        {
                            var lh = PlayerWeaponGrip.Find(model.transform, "Hand.L");
                            var ls = PlayerWeaponGrip.Find(off[i].transform, "GripSocket");
                            float leftError = Vector3.Distance(lh.TransformPoint(new Vector3(0, .12f, 0)), ls.position);
                            if (leftError > .025f) throw new Exception($"{names[i]} left grip error {leftError:F4}m");
                        }
                        lines.Add($"PASS {names[i]} {pose} t={sample:F1} dual={dual}: grip error {error:F4}m");
                        if (!dual && pose == "Aim" && sample == .4f && (i == 0 || i == 2)) Render(root.transform, scene, names[i]);
                    }
                }
                File.WriteAllLines("ArtSource/Weapons/GripValidation/results.txt", lines);
                Debug.Log("WEAPON_GRIP_VALIDATION_PASSED: " + lines.Count + " poses.");
            }
            catch (Exception e) { File.WriteAllText("ArtSource/Weapons/GripValidation/results.txt", e.ToString()); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static GameObject Make(string name, Transform parent)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Art/Weapons/{name}/{name}.fbx"), parent);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"Assets/_Project/Art/Weapons/{name}/{name}.controller");
            animator.Rebind();
            animator.Play("Idle", 0, 0);
            animator.Update(0);
            return model;
        }

        static void Render(Transform root, UnityEngine.SceneManagement.Scene scene, string name)
        {
            var cam = new GameObject("Grip camera").AddComponent<Camera>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
            cam.scene = scene;
            cam.transform.position = new Vector3(3, 3.5f, 3.5f);
            cam.transform.LookAt(new Vector3(0, .1f, .55f));
            cam.orthographic = true;
            cam.orthographicSize = 1.7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.2f, .23f, .26f);
            var light = new GameObject("Grip light").AddComponent<Light>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            var rt = new RenderTexture(900, 900, 24);
            var previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"ArtSource/Weapons/GripValidation/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = previous;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(light.gameObject);
        }
    }
}
