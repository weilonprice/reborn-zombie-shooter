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
    public static class CombatRegression
    {
        const string Pending = "ZombieShooter.CombatRegression";
        static IEnumerator checks;
        static double resumeAt;
        static readonly List<string> results = new();
        static readonly List<string> errors = new();
        static CombatRegression() => EditorApplication.playModeStateChanged += OnMode;

        [MenuItem("Tools/Zombie Shooter/Validate Combat Regression")]
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
            File.WriteAllLines("ArtSource/Casings/combat_regression.txt", results);
            if (error != null) Debug.LogException(error);
            else Debug.Log("COMBAT_REGRESSION_PASSED: " + results.Count + " checks.");
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Zombie.prefab");
            var enemy = UnityEngine.Object.Instantiate(prefab, new Vector3(0,1,5), Quaternion.Euler(0,180,0));
            enemy.GetComponent<ZombieAI>().enabled = false;
            yield return .4f;
            var hp = enemy.GetComponent<Health>();
            var fields = new SerializedObject(weapon);
            var origin = (Transform)fields.FindProperty("rayOrigin").objectReferenceValue;
            var muzzle = (Transform)fields.FindProperty("muzzle").objectReferenceValue;
            results.Add("Origin " + origin.position + " direction " + muzzle.forward + " socket " + visuals.EjectionSocket(false)?.position);
            Physics.SyncTransforms();
            foreach(var hit in Physics.RaycastAll(origin.position,muzzle.forward,10,~0,QueryTriggerInteraction.Collide))
                results.Add("Ray hit " + hit.collider.name + " at " + hit.point + " parent health " + hit.collider.GetComponentInParent<Health>()?.name);
            foreach(var col in enemy.GetComponentsInChildren<Collider>())
                results.Add("Zone " + col.name + " bounds " + col.bounds + " enabled " + col.enabled);
            float before = hp.Current; int cases = emitter.EmittedCount;
            weapon.TryFire(); yield return .1f;
            results.Add("Health before " + before + " after " + hp.Current + " cases " + cases + " -> " + emitter.EmittedCount);
            yield return 2f;
            results.Add("Floor contacts " + emitter.FloorImpactCount + " sounds " + emitter.PlayedSoundCount);
            Check(hp.Current < before, "Centre shot damages real animated zombie");
            Check(emitter.EmittedCount > cases, "Real weapon fire emits a case");
            Check(emitter.FloorImpactCount > 0 && emitter.PlayedSoundCount > 0, "Case lands with sound");
            Check(errors.Count == 0, "No runtime errors: " + string.Join("; ",errors));
        }
    }
}
