using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>Exercises sustained flame visuals and their lifecycle in Arena.</summary>
    [InitializeOnLoad]
    public static class FlamethrowerValidation
    {
        const string Pending = "ZombieShooter.FlamethrowerValidation";
        static IEnumerator checks;
        static double resumeAt;
        static readonly List<string> results = new();
        static readonly List<string> errors = new();
        static FlamethrowerValidation() => EditorApplication.playModeStateChanged += OnMode;

        [MenuItem("Tools/Zombie Shooter/Validate Flamethrower")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (GameObject.FindGameObjectWithTag("Player")?.GetComponent<WeaponVisuals>() == null)
                throw new InvalidOperationException("Open Arena with a player first.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
        static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); errors.Clear();
                Application.logMessageReceived += OnLog;
                checks = Exercise(); resumeAt = EditorApplication.timeSinceStartup + .2;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; SessionState.SetBool(Pending, false); }
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < resumeAt) return;
            try
            {
                if (checks.MoveNext())
                {
                    resumeAt = EditorApplication.timeSinceStartup + (checks.Current is float delay ? delay : .05f);
                    return;
                }
                Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception error)
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            Time.timeScale = 1;
            if (error != null) results.Add("FAIL: " + error);
            Directory.CreateDirectory("ArtSource/Weapons/Flamethrower");
            File.WriteAllLines("ArtSource/Weapons/Flamethrower/unity_validation.txt", results);
            if (error != null) Debug.LogException(error);
            else Debug.Log("FLAMETHROWER_VALIDATION_PASSED: " + results.Count + " checks.");
            EditorApplication.isPlaying = false;
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }
        static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }

        static IEnumerator Exercise()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var weapon = player.GetComponent<Weapon>();
            var loadout = player.GetComponent<WeaponLoadout>();
            var visuals = player.GetComponent<WeaponVisuals>();
            weapon.enabled = false;
            player.GetComponent<PlayerController>().enabled = false;
            var waves = WaveManager.Instance;
            waves.StopAllCoroutines(); waves.enabled = false;
            foreach (var zombie in ZombieAI.ActiveZombies.ToArray()) zombie.gameObject.SetActive(false);
            player.transform.position = new Vector3(0, 1.01f, 0);
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            yield return .25f;
            var definition = CasingSetup.WeaponFor("Flamethrower");
            loadout.TryCarry(definition);
            for (int i=0;i<loadout.CarryCapacity;i++)
                if(loadout.CarriedAt(i)==definition) loadout.Select(i,true);
            yield return .7f;
            var flames = player.GetComponent<FlamethrowerEffects>();
            Check(flames != null, "Flame effects authored onto the player by the builder");
            Check(!flames.IsEmitting && flames.LiveParticles == 0, "No flames while idle");
            for(int i=0;i<15;i++) { weapon.TryFire(); yield return .105f; }
            Check(flames.EmittedParticles > 60 && flames.LiveParticles > 0, "Sustained fuel ticks emit continuous flames");
            Check(visuals.Current.GetCurrentAnimatorStateInfo(0).IsName("Fire"), "Weapon plays sustained Fire motion");
            var cameraGo = new GameObject("Flame validation camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(12,14,-8);
            camera.transform.LookAt(new Vector3(0,1,4));
            camera.orthographic = true; camera.orthographicSize = 8;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.09f,.1f);
            var rt = new RenderTexture(1200,800,24); camera.targetTexture = rt;
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(1200,800,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1200,800),0,0); image.Apply();
            File.WriteAllBytes("ArtSource/Weapons/Flamethrower/FlamePreview.png",image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(cameraGo);
            yield return .8f;
            Check(!flames.IsEmitting && flames.LiveParticles == 0, "Release extinguishes stream and remaining particles");
            Check(visuals.Current.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Weapon returns to Idle on release");
            weapon.TryFire(); yield return .05f;
            weapon.BeginReload(); yield return .05f;
            Check(!flames.IsEmitting, "Reload stops emission immediately");
            yield return weapon.ReloadSeconds + .2f;
            weapon.TryFire(); yield return .05f;
            Time.timeScale = 0; int emitted = flames.EmittedParticles; yield return .2f;
            Check(flames.EmittedParticles == emitted, "Pause suspends flame emission");
            Time.timeScale = 1;
            loadout.Select(0,true); yield return .1f;
            Check(!flames.IsEmitting, "Switching weapons stops flame emission");
            yield return .6f;
            Check(flames.LiveParticles == 0, "Switching leaves no stuck particles");
            Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
        }
    }
}
