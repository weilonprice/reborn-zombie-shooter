using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Measures where a dense horde's frame actually goes, instead of assuming.
    /// <para>
    /// Debt 6 has been deferred since the beginning on the reasoning that steering is
    /// O(n^2) and will break first. Since that was written the horde went from four
    /// capsules to twelve skinned archetypes, every body gained nine collider hit zones on
    /// animated bones, and up to ninety-six spent cases sweep the scene - so the honest
    /// answer to "CPU steering or GPU skinning" is that nobody knows. This is the thing
    /// that turns that into a number.
    /// </para>
    /// <para>
    /// Ramps the live count and samples each step, because the question is not what one
    /// frame costs, it is which curve is quadratic. Separation is O(n^2) by construction
    /// and should show as cost per enemy RISING with the count; skinning and draw calls
    /// should stay flat per enemy. That difference is the whole finding, and it is
    /// invisible in a single measurement at one population.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class HordeProfile
    {
        const string Pending = "ZombieShooter.HordeProfile";
        const string Report = "ArtSource/Profiling/horde_profile.txt";

        /// <summary>Live enemies to hold at each step. 60 is maxAliveAtOnce today.</summary>
        static readonly int[] Steps = { 15, 30, 45, 60, 90 };

        /// <summary>Frames sampled per step, after letting the population settle.</summary>
        const int SamplesPerStep = 120;

        static IEnumerator work;
        static double resumeAt;
        static readonly List<string> lines = new();
        static readonly List<string> errors = new();

        static HordeProfile() => EditorApplication.playModeStateChanged += OnMode;

        [MenuItem("Tools/Zombie Shooter/Profile The Horde")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void OnMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                lines.Clear(); errors.Clear();
                Application.logMessageReceived += OnLog;
                work = Measure(); resumeAt = EditorApplication.timeSinceStartup + .2;
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
                if (work.MoveNext())
                {
                    resumeAt = EditorApplication.timeSinceStartup + (work.Current is float d ? d : 0f);
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
            if (error != null) lines.Add("ABORTED: " + error);
            if (errors.Count > 0) lines.Add("runtime errors: " + string.Join("; ", errors.Take(5)));
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllLines(Report, lines);
            if (error != null) Debug.LogException(error);
            else Debug.Log($"HORDE_PROFILE_WRITTEN: {Report}");
            EditorApplication.isPlaying = false;
        }

        static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
        }

        sealed class Probe : IDisposable
        {
            public readonly string Name;
            readonly ProfilerRecorder recorder;
            readonly double toMilliseconds;
            public Probe(string name, ProfilerCategory category, string marker, bool nanoseconds)
            {
                Name = name;
                recorder = ProfilerRecorder.StartNew(category, marker, 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame);
                toMilliseconds = nanoseconds ? 1e-6 : 1.0;
            }
            public bool Valid => recorder.Valid;
            public double Read() => recorder.Valid ? recorder.LastValue * toMilliseconds : double.NaN;
            public void Dispose() => recorder.Dispose();
        }

        static IEnumerator Measure()
        {
            var waves = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            if (waves == null) throw new InvalidOperationException(
                "No WaveManager in the scene. Build the arena first.");

            // The wave loop has to stop, or it spawns and kills underneath the measurement
            // and no step ever holds a steady population. Enemies still chase and attack -
            // that load is the point - but the player is topped up each frame so a death
            // does not end the run halfway through the ramp.
            waves.StopAllCoroutines();
            waves.enabled = false;
            var playerHealth = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Health>();

            // Marker probes for the paths debt 6 names, plus the two systems added since.
            var timings = new[]
            {
                new Probe("Separation (O(n^2))", ProfilerCategory.Scripts, "ZombieAI.Separation", true),
                new Probe("Barricade spherecast", ProfilerCategory.Scripts, "ZombieAI.BarricadeSense", true),
                new Probe("Casing simulate", ProfilerCategory.Scripts, "CasingEjector.Simulate", true),
                new Probe("Animator update", ProfilerCategory.Animation, "Animators.Update", true),
                new Probe("Physics step", ProfilerCategory.Physics, "Physics.Processing", true),
            };
            var counters = new[]
            {
                new Probe("Draw calls", ProfilerCategory.Render, "Draw Calls Count", false),
                new Probe("SetPass calls", ProfilerCategory.Render, "SetPass Calls Count", false),
                new Probe("Batches", ProfilerCategory.Render, "Batches Count", false),
            };

            lines.Add("HORDE PROFILE");
            lines.Add("Ramped populations, " + SamplesPerStep + " sampled frames each, median reported.");
            lines.Add("The question is which cost per enemy RISES with the count. Separation is");
            lines.Add("O(n^2) by construction; skinning and draw calls should stay flat per enemy.");
            lines.Add("");

            var unavailable = timings.Concat(counters).Where(p => !p.Valid).Select(p => p.Name).ToList();
            if (unavailable.Count > 0)
                lines.Add("unavailable on this platform: " + string.Join(", ", unavailable));

            var header = "  pop |  frame ms |    fps | " +
                         string.Join(" | ", timings.Select(t => t.Name)) + " | " +
                         string.Join(" | ", counters.Select(c => c.Name));
            lines.Add(header);
            lines.Add(new string('-', header.Length));

            var perEnemy = new List<(int pop, double frame, double separation, double animation)>();

            foreach (int target in Steps)
            {
                yield return Populate(waves, target);
                yield return .5f;   // let pooling, spawning and the flow field settle

                var frames = new List<double>(SamplesPerStep);
                var samples = timings.Concat(counters).ToDictionary(p => p.Name, _ => new List<double>());

                for (int i = 0; i < SamplesPerStep; i++)
                {
                    if (playerHealth != null && playerHealth.IsAlive) playerHealth.Heal(9999f);
                    frames.Add(Time.unscaledDeltaTime * 1000.0);
                    foreach (var probe in timings.Concat(counters))
                        samples[probe.Name].Add(probe.Read());
                    yield return 0f;
                }

                double frame = Median(frames);
                int live = ZombieAI.ActiveZombies?.Count ?? 0;
                var cells = timings.Concat(counters)
                                   .Select(p => Median(samples[p.Name]))
                                   .ToArray();

                lines.Add($"{live,5} | {frame,9:F2} | {1000.0 / Math.Max(frame, .001),6:F1} | " +
                          string.Join(" | ", cells.Select((v, i) =>
                              double.IsNaN(v) ? "n/a".PadLeft(Header(timings, counters, i).Length)
                                              : v.ToString(i < timings.Length ? "F3" : "F0")
                                                 .PadLeft(Header(timings, counters, i).Length))));

                perEnemy.Add((live, frame, cells[0], cells[3]));
            }

            lines.Add("");
            lines.Add("COST PER ENEMY - this is the part that answers debt 6");
            lines.Add("  pop | frame ms/enemy | separation ms/enemy | animator ms/enemy");
            foreach (var (pop, frame, separation, animation) in perEnemy)
            {
                if (pop <= 0) continue;
                lines.Add($"{pop,5} | {frame / pop,14:F4} | {separation / pop,19:F5} | " +
                          (double.IsNaN(animation) ? "n/a" : (animation / pop).ToString("F5")));
            }

            lines.Add("");
            lines.Add("Rising separation-per-enemy confirms the O(n^2) walk is the ceiling and a");
            lines.Add("spatial hash is the fix. Flat separation with a rising frame time means the");
            lines.Add("cost moved elsewhere while debt 6 was being blamed, and the register needs");
            lines.Add("rewriting before anyone optimises the wrong loop.");

            lines.Add("");
            lines.Add("SCENE AT THE LARGEST POPULATION");
            lines.Add($"  live enemies ............ {ZombieAI.ActiveZombies?.Count ?? 0}");
            lines.Add($"  colliders in scene ...... {UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length}");
            lines.Add($"  skinned meshes .......... {UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Length}");
            lines.Add($"  animators ............... {UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Length}");
            lines.Add($"  audio sources ........... {UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}");

            foreach (var probe in timings.Concat(counters)) probe.Dispose();
        }

        static string Header(Probe[] timings, Probe[] counters, int index) =>
            index < timings.Length ? timings[index].Name : counters[index - timings.Length].Name;

        /// <summary>
        /// Holds the live count at a target by spawning through WaveManager's own pool, so
        /// what is measured is the real prefab with its real components - not a stand-in.
        /// </summary>
        static IEnumerator Populate(WaveManager waves, int target)
        {
            var spawn = typeof(WaveManager).GetMethod("Spawn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (spawn == null) throw new InvalidOperationException(
                "WaveManager.Spawn not found - the profiler needs it to build a population.");

            int guard = 0;
            while ((ZombieAI.ActiveZombies?.Count ?? 0) < target && guard++ < 400)
            {
                spawn.Invoke(waves, new object[] { 1 });
                if (guard % 10 == 0) yield return 0f;
            }
            yield return 0f;
        }

        static double Median(List<double> values)
        {
            var clean = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (clean.Count == 0) return double.NaN;
            return clean[clean.Count / 2];
        }
    }
}
