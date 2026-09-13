using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>Real weapon fire, socket selection, surface contacts and sound playback in Arena.</summary>
    [InitializeOnLoad]
    public static class CasingValidation
    {
        const string Pending = "ZombieShooter.CasingValidation";
        static IEnumerator checks;
        static double resumeAt;
        static readonly List<string> results = new();
        static readonly List<string> errors = new();
        static CasingValidation() => EditorApplication.playModeStateChanged += OnMode;

        [MenuItem("Tools/Zombie Shooter/Validate Weapon Casings")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (GameObject.FindGameObjectWithTag("Player")?.GetComponent<CasingEjector>() == null)
                throw new InvalidOperationException("Install Weapon Casings in Arena first.");
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
            { EditorApplication.update -= Tick; SessionState.SetBool(Pending, false); }
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
            Directory.CreateDirectory("ArtSource/Casings");
            File.WriteAllLines("ArtSource/Casings/unity_validation.txt", results);
            if (error != null) Debug.LogException(error);
            else Debug.Log("CASING_VALIDATION_PASSED: " + results.Count + " checks.");
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
            var emitter = player.GetComponent<CasingEjector>();
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
            var clips = new HashSet<AudioClip>();
            foreach (string name in CasingSetup.Names)
            {
                var profile = CasingSetup.ProfileFor(name);
                Check(profile != null && profile.mesh != null && profile.mesh.vertexCount > 50, name + " has an imported 3D spent case");
                Check(profile.mesh.bounds.size.y > .015f && profile.mesh.bounds.size.y < .08f,
                    name + " imported in metres with its long axis upright");
                Check(profile.floorImpacts.Length == 3 && profile.floorImpacts.All(c => c != null && c.length > .15f), name + " has three impact clips");
                foreach (var clip in profile.floorImpacts) Check(clips.Add(clip), clip.name + " is not shared with another casing");
                var definition = CasingSetup.WeaponFor(name);
                if (!loadout.Owns(definition)) loadout.TryCarry(definition);
                for (int i = 0; i < loadout.CarryCapacity; i++)
                    if (loadout.CarriedAt(i) == definition) loadout.Select(i, true);
                yield return .3f;
                int emitted = emitter.EmittedCount;
                int impacts = emitter.FloorImpactCount;
                int sounds = emitter.PlayedSoundCount;
                weapon.TryFire();
                yield return .15f;
                Check(emitter.EmittedCount == emitted + definition.ShellsPerShot, name + " ejects one spent case per ammunition round");
                Check(emitter.LastEmissionSocket == visuals.EjectionSocket(false), name + " uses the actual main-hand ejection port");
                if (name == "GrenadeLauncher")
                    Check(UnityEngine.Object.FindObjectsByType<WeaponProjectile>(FindObjectsSortMode.None).Length > 0,
                        "Grenade launcher launches a live projectile as it ejects its case");
                yield return 1.8f;
                Check(emitter.FloorImpactCount > impacts && emitter.PlayedSoundCount > sounds, name + " strikes the floor and plays impact audio");
                var root = GameObject.Find("Spent Casings (pooled)");
                Check(root.GetComponentsInChildren<AudioSource>().Any(a => profile.floorImpacts.Contains(a.clip)), name + " plays its own casing sound family");
            }
            foreach (string name in new[] { "Flamethrower", "TeslaCoil", "NailGun" })
            {
                var definition = CasingSetup.WeaponFor(name);
                if (!loadout.Owns(definition)) loadout.TryCarry(definition);
                for (int i = 0; i < loadout.CarryCapacity; i++)
                    if (loadout.CarriedAt(i) == definition) loadout.Select(i, true);
                int before = emitter.EmittedCount;
                weapon.TryFire(); yield return .15f;
                Check(emitter.EmittedCount == before, name + " does not eject a cartridge case");
            }
            var pistol = CasingSetup.WeaponFor("Pistol");
            GameManager.Instance.AddGold(10000);
            for (int i = 0; i < 4; i++) UpgradeManager.Instance.TryBuyNextTier(pistol, 0);
            loadout.Select(0, true); yield return .3f;
            weapon.TryFire(); yield return .6f;
            weapon.TryFire(); yield return .15f;
            Check(emitter.LastEmissionSocket == visuals.EjectionSocket(true), "Akimbo ejects the second case from the off-hand gun");
            Time.timeScale = 0;
            int pausedCount = emitter.EmittedCount;
            emitter.Queue(pistol, false); yield return .2f;
            Check(emitter.EmittedCount == pausedCount, "Pause suspends casing emission and simulation");
            Time.timeScale = 1;
            yield return .1f;
            Check(emitter.EmittedCount == pausedCount + 1, "Queued casing resumes after pause");
            for (int batch = 0; batch < 9; batch++)
            {
                for (int i = 0; i < 32; i++) emitter.Queue(pistol, false);
                yield return .05f;
            }
            var pooled = GameObject.Find("Spent Casings (pooled)");
            Check(pooled.GetComponentsInChildren<MeshFilter>(true).Length == 96 && emitter.ActiveCount <= 96,
                "Rapid-fire stress remains capped at 96 reusable meshes");
            Check(pooled.GetComponentsInChildren<AudioSource>(true).Length == 8,
                "Casing audio uses eight dedicated voices");
            // Case lifetime uses game time. Editor wall time can advance faster while
            // rendering or importing, so a fixed wall-clock wait falsely reports leaks.
            float expiresAt = Time.time + 8.5f;
            while (Time.time < expiresAt) yield return .1f;
            Check(emitter.ActiveCount == 0, "All spent cases expire and return to the pool");
            Check(errors.Count == 0, "No runtime errors during casing checks: " + string.Join("; ", errors));
        }
    }
}
