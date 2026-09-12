using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZombieShooter.EditorTools
{
    /// <summary>Repeatable integration checks in a temporary Play-mode scene. No arena edits.</summary>
    [InitializeOnLoad]
    public static class AbilityAnimationValidation
    {
        const string Pending = "ZombieShooter.AbilityValidation";
        const string PriorScene = Pending + ".Scene";
        const string Output = "ArtSource/AbilityAnimations/unity_validation.txt";
        static readonly List<string> Results = new();
        static IEnumerator checks;
        static float resumeAt;
        static Camera camera;

        static AbilityAnimationValidation()
        {
            EditorApplication.playModeStateChanged += OnMode;
        }

        [MenuItem("Tools/Zombie Shooter/Validate Ability Animations")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(PriorScene, SceneManager.GetActiveScene().path);
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnMode(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                Results.Clear();
                checks = Exercise();
                resumeAt = 0;
                EditorApplication.update += Tick;
            }
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Pending, false);
                string path = SessionState.GetString(PriorScene, "");
                if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            }
        }

        static void Tick()
        {
            if (Time.time < resumeAt) return;
            try
            {
                if (checks.MoveNext())
                {
                    resumeAt = Time.time + (checks.Current is float seconds ? seconds : .02f);
                    return;
                }
                Results.Add("PASS: all ability animation checks completed in Unity Play mode.");
                Finish(false);
            }
            catch (Exception error)
            {
                Results.Add("FAIL: " + error);
                Finish(true);
            }
        }

        static void Finish(bool failed)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllLines(Output, Results);
            if (failed) Debug.LogError("ABILITY_VALIDATION_FAILED: " + Results[Results.Count - 1]);
            else Debug.Log("ABILITY_VALIDATION_PASSED: " + Output);
            EditorApplication.isPlaying = false;
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Results.Add("PASS: " + message);
        }

        static void Call(object target, string method) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        static void Set(object target, string field, object value) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static Animator Rig(GameObject go)
        {
            var animator = go.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        static bool State(Animator animator, string name, int layer = 0)
        {
            return animator.GetCurrentAnimatorStateInfo(layer).IsName(name) ||
                (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName(name));
        }

        static GameObject Enemy(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{name}.prefab");
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            Rig(go);
            return go;
        }

        static void CheckClip(string pack, string name, float duration, GameObject model, bool looping = false)
        {
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pack))
                if (asset is AnimationClip c && c.name == name) clip = c;
            Check(clip != null && Mathf.Abs(clip.length - duration) < .025f, name + " imported duration matches gameplay");
            Check(clip.isLooping == looping, name + " loop setting");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int matched = 0;
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(Transform)) continue;
                if (string.IsNullOrEmpty(binding.path) || model.transform.Find(binding.path) != null) matched++;
                else throw new InvalidOperationException(name + " unmatched bone: " + binding.path);
            }
            Check(matched > 30, name + " bone curves bind to the existing rig");
        }

        static void Picture(GameObject subject, string name)
        {
            camera.transform.position = subject.transform.position + new Vector3(3, 2.2f, 4);
            camera.transform.LookAt(subject.transform.position + Vector3.up * .1f);
            var target = new RenderTexture(720, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(720, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 720, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes($"ArtSource/AbilityAnimations/proof/Unity_{name}.png", texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(texture);
            Object.Destroy(target);
        }

        static IEnumerator Exercise()
        {
            Time.timeScale = 1f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Validation floor";
            ground.transform.position = new Vector3(0, -.15f, 0);
            ground.transform.localScale = new Vector3(80, .3f, 80);
            camera = new GameObject("Validation camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f, .06f, .08f);
            var sun = new GameObject("Validation light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientLight = Color.gray;

            var screamer = Enemy("Screamer", new Vector3(0, 1.08f, 0));
            var inside = Enemy("Revenant", new Vector3(11.5f, 1.03f, 0));
            var outside = Enemy("Leaper", new Vector3(12.5f, .93f, 0));
            CheckClip(AbilityAnimationSetup.ZombiePack, "Scream", 1.5f, screamer.transform.Find("AbilityModel").gameObject);
            CheckClip(AbilityAnimationSetup.ZombiePack, "GetUp", 2.2f, inside.transform.Find("AbilityModel").gameObject);
            CheckClip(AbilityAnimationSetup.ZombiePack, "Leap", .65f, outside.transform.Find("AbilityModel").gameObject);
            int howls = 0;
            screamer.GetComponent<HordeAura>().Screamed += _ => howls++;
            yield return .2f;
            var howlAnimator = Rig(screamer);
            Check(State(howlAnimator, "Scream"), "Screamer enters Scream on aura activation");
            Check(inside.GetComponent<ZombieAI>().SpeedMultiplier == 1.5f &&
                  outside.GetComponent<ZombieAI>().SpeedMultiplier == 1f, "Aura still respects its 12m radius");
            yield return .45f;
            Check(howls == 1 && howlAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > .3f,
                  "0.25s aura refreshes do not restart the howl");
            Picture(screamer, "Scream");
            yield return 1f;
            Check(State(howlAnimator, "Chase"), "Scream returns to Chase");
            yield return 1.5f;
            Check(howls == 2 && State(howlAnimator, "Scream"), "Howl repeats at a readable three-second cadence");
            screamer.GetComponent<Health>().TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .05f;
            Check(State(howlAnimator, "Death"), "Death interrupts Scream");
            Check(inside.GetComponent<ZombieAI>().SpeedMultiplier == 1f, "Dead Screamer immediately releases its buff");
            screamer.SetActive(false);
            outside.SetActive(false);

            var revenantHealth = inside.GetComponent<Health>();
            var revenantAnimator = Rig(inside);
            int deaths = 0, recoveries = 0, attacks = 0;
            revenantHealth.Died += _ => deaths++;
            inside.GetComponent<Revenant>().Reviving += _ => recoveries++;
            inside.GetComponent<ZombieAI>().Attacked += () => attacks++;
            var victim = new GameObject("Recovery attack target");
            victim.transform.position = inside.transform.position + Vector3.forward;
            victim.AddComponent<Health>();
            inside.GetComponent<ZombieAI>().SetTarget(victim.transform);
            revenantHealth.TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .1f;
            Check(Mathf.Abs(revenantHealth.Current - revenantHealth.Max * .45f) < .01f && deaths == 0,
                  "First lethal hit restores 45% health without a death payout");
            Check(State(revenantAnimator, "GetUp") && recoveries == 1, "GetUp wins over the lethal hit reaction");
            revenantHealth.TakeDamage(new DamageInfo(1, Vector3.zero, Vector3.forward, 3));
            yield return .55f;
            Check(State(revenantAnimator, "GetUp") && attacks == 0, "Recovery blocks attacks and survives ordinary hit reactions");
            Picture(inside, "GetUp");
            Object.Destroy(victim);
            yield return 1.75f;
            Check(State(revenantAnimator, "Chase"), "GetUp returns to Chase after 2.2 seconds");
            revenantHealth.TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .05f;
            Check(deaths == 1 && !revenantHealth.IsAlive && State(revenantAnimator, "Death"), "Second lethal hit kills exactly once");
            revenantHealth.Heal(500);
            Check(!revenantHealth.IsAlive, "Ordinary healing cannot revive a dead object");
            inside.SetActive(false);
            inside.SetActive(true);
            Check(revenantHealth.Current == revenantHealth.Max, "Pool reuse resets health");
            revenantHealth.TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .05f;
            Check(recoveries == 2 && State(revenantAnimator, "GetUp"), "Pool reuse resets the revive allowance and animation");
            revenantHealth.TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .05f;
            Check(State(revenantAnimator, "Death"), "A second killing hit can interrupt GetUp");
            inside.SetActive(false);

            var leaper = Enemy("Leaper", new Vector3(-10, .93f, 0));
            var wall = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Barricade.prefab"),
                new Vector3(-10, 0, 2), Quaternion.identity);
            var targetGo = new GameObject("Leap target");
            targetGo.transform.position = new Vector3(-10, 1, 7);
            targetGo.AddComponent<Health>();
            leaper.GetComponent<ZombieAI>().SetTarget(targetGo.transform);
            int leapAttacks = 0;
            leaper.GetComponent<ZombieAI>().Attacked += () => leapAttacks++;
            yield return .3f;
            var leap = leaper.GetComponent<BarricadeLeaper>();
            Check(leap.IsLeaping && State(Rig(leaper), "Leap") && leaper.transform.position.y > 2f,
                  "Leap clip plays during the scripted vault arc");
            Check(leapAttacks == 0, "Leaper does not attack while airborne");
            Picture(leaper, "Leap");
            yield return .5f;
            Check(!leap.IsLeaping && State(Rig(leaper), "Chase") && leaper.transform.position.z > 3f,
                  "0.65-second Leap lands beyond the barricade and returns to Chase");
            leaper.SetActive(false);
            wall.SetActive(false);

            var player = new GameObject("Ultimate validation player");
            player.SetActive(false);
            player.transform.position = new Vector3(0, 1.1f, -10);
            var health = player.AddComponent<Health>();
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            var movement = player.AddComponent<PlayerController>();
            var weapon = player.AddComponent<Weapon>();
            Set(weapon, "definition", AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Weapons/WPN_Pistol.asset"));
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Art/Characters/MainCharacter/MainCharacter.fbx"), player.transform);
            model.transform.localPosition = Vector3.down;
            var playerRig = model.GetComponent<Animator>();
            if (playerRig == null) playerRig = model.AddComponent<Animator>();
            playerRig.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Art/Characters/MainCharacter/MainCharacter.controller");
            playerRig.applyRootMotion = false;
            playerRig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            player.AddComponent<PlayerAnimator>();
            var ultimate = player.AddComponent<UltimateAbility>();
            Set(ultimate, "weapon", weapon);
            Set(ultimate, "movement", movement);
            Set(ultimate, "health", health);
            player.SetActive(true);
            yield return .1f;
            CheckClip(AbilityAnimationSetup.SurvivorPack, "Ultimate", 2f / 3f, model, true);
            // Grant the resolved capstone stats in this temporary fixture only, then use the
            // actual public activation/end path. No purchased assets or saved progress change.
            var stats = UpgradeManager.Resolve(weapon.Definition);
            stats.UltimateKills = 1;
            Set(weapon, "stats", stats);
            weapon.enabled = false; // Hold the magazine while animation ownership is examined.
            Set(ultimate, "kills", 1);
            ultimate.TryActivate();
            yield return .15f;
            Check(ultimate.Active && movement.AimOverridden && State(playerRig, "Ultimate"), "Capstone activation selects full-body Ultimate");
            Check(playerRig.GetLayerWeight(1) == 0 && Mathf.Approximately(movement.SpinDegreesPerSecond, 540),
                  "Ultimate suppresses Aim/Fire layer while retaining the 540 degree gameplay spin");
            health.TakeDamage(new DamageInfo(1, Vector3.zero, Vector3.zero, 3));
            yield return .1f;
            Check(State(playerRig, "Ultimate"), "Damage reactions cannot erase an active Ultimate");
            Picture(player, "Ultimate");
            weapon.EndUltimate();
            yield return .2f;
            Check(!movement.AimOverridden && !State(playerRig, "Ultimate") && playerRig.GetLayerWeight(1) == 1,
                  "Ending the ultimate restores normal aim and animation layers");
            Set(ultimate, "kills", 1);
            ultimate.TryActivate();
            yield return .1f;
            health.TakeDamage(new DamageInfo(10000, Vector3.zero, Vector3.zero, 0));
            yield return .1f;
            Check(State(playerRig, "Death") && !ultimate.Active && !movement.AimOverridden,
                  "Player death interrupts Ultimate and releases spin control");
        }
    }
}
