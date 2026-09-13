using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Cosmetic spent cases. Bounded reusable meshes, swept surface contacts and a separate
    /// audio pool: no rigid bodies in the horde and no stealing gunshot/impact voices.
    /// Runs after WeaponVisuals has placed the animated ejection sockets.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CasingEjector : MonoBehaviour
    {
        [SerializeField] WeaponDefinition[] weapons;
        [SerializeField] CasingDefinition[] profiles;
        [SerializeField, Range(16, 256)] int capacity = 96;
        [SerializeField] float lifetime = 8f;
        [SerializeField, Range(1, 16)] int audioVoices = 8;
        readonly Dictionary<WeaponDefinition, CasingDefinition> byWeapon = new();
        readonly Queue<Pending> pending = new(16);
        readonly RaycastHit[] hits = new RaycastHit[16];
        Case[] pool;
        AudioSource[] voices;
        Transform poolRoot;
        WeaponVisuals visuals;
        int nextCase, nextVoice, lastVariant = -1;
        float nextSound;
        public int EmittedCount { get; private set; }
        public int FloorImpactCount { get; private set; }
        public int PlayedSoundCount { get; private set; }
        public int ActiveCount { get; private set; }
        public Transform LastEmissionSocket { get; private set; }

        struct Pending { public CasingDefinition profile; public Transform socket; public bool offHand; }
        sealed class Case
        {
            public Transform transform;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public CasingDefinition profile;
            public Vector3 velocity, spin;
            public float age;
            public int contacts;
            public bool active, settled;
        }

        void Awake()
        {
            visuals = GetComponent<WeaponVisuals>();
            for (int i = 0; weapons != null && profiles != null && i < Mathf.Min(weapons.Length, profiles.Length); i++)
                if (weapons[i] != null && profiles[i] != null) byWeapon[weapons[i]] = profiles[i];
            poolRoot = new GameObject("Spent Casings (pooled)").transform;
            pool = new Case[Mathf.Clamp(capacity, 16, 256)];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Case " + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(poolRoot, false);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                pool[i] = new Case { transform = go.transform, filter = go.GetComponent<MeshFilter>(), renderer = renderer };
                go.SetActive(false);
            }
            voices = new AudioSource[Mathf.Clamp(audioVoices, 1, 16)];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject("Casing impact " + i);
                go.transform.SetParent(poolRoot, false);
                var voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 3f;
                voice.maxDistance = 20f;
                voice.dopplerLevel = 0;
                voice.priority = 180;
                voices[i] = voice;
            }
        }

        public void Queue(WeaponDefinition weapon, bool offHand)
        {
            if (!isActiveAndEnabled || weapon == null || weapon.ShellsPerShot == 0 ||
                !byWeapon.TryGetValue(weapon, out var profile)) return;
            var socket = visuals != null ? visuals.EjectionSocket(offHand) : null;
            if (socket == null) return;
            for (int i = 0; i < weapon.ShellsPerShot && pending.Count < 16; i++)
                pending.Enqueue(new Pending { profile = profile, socket = socket, offHand = offHand });
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            while (pending.Count > 0) Emit(pending.Dequeue());
            // Substeps keep bouncing stable during a slow frame or kill slow-motion.
            float elapsed = Mathf.Min(Time.deltaTime, .1f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed / .02f));
            ActiveCount = 0;
            foreach (var casing in pool)
            {
                if (!casing.active) continue;
                casing.age += Time.deltaTime;
                if (casing.age >= lifetime)
                {
                    casing.active = false;
                    casing.transform.gameObject.SetActive(false);
                    continue;
                }
                ActiveCount++;
                if (!casing.settled)
                    for (int step = 0; step < steps && !casing.settled; step++) Advance(casing, elapsed / steps);
                float fade = Mathf.Clamp01((lifetime - casing.age) / .6f);
                casing.transform.localScale = Vector3.one * (casing.profile.displayScale * fade);
            }
        }

        void Emit(Pending shot)
        {
            if (shot.socket == null || shot.profile.mesh == null) return;
            var casing = pool[nextCase];
            nextCase = (nextCase + 1) % pool.Length;
            casing.profile = shot.profile;
            casing.filter.sharedMesh = shot.profile.mesh;
            casing.renderer.sharedMaterials = shot.profile.materials;
            casing.transform.SetPositionAndRotation(shot.socket.position, Random.rotation);
            casing.transform.localScale = Vector3.one * shot.profile.displayScale;
            var sideways = transform.right * (shot.offHand ? -1f : 1f);
            casing.velocity = sideways * Random.Range(1.4f, 2.4f) + Vector3.up * Random.Range(1.4f, 2.3f)
                              - transform.forward * Random.Range(.15f, .55f);
            casing.spin = Random.onUnitSphere * Random.Range(400f, 850f);
            casing.age = 0;
            casing.contacts = 0;
            casing.active = true;
            casing.settled = false;
            casing.transform.gameObject.SetActive(true);
            LastEmissionSocket = shot.socket;
            EmittedCount++;
        }

        void Advance(Case casing, float dt)
        {
            casing.velocity += Vector3.down * (9.81f * dt);
            Vector3 travel = casing.velocity * dt;
            float length = travel.magnitude;
            if (length < .00001f) return;
            int count = Physics.SphereCastNonAlloc(casing.transform.position, casing.profile.CollisionRadius,
                travel / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
            int closest = -1;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.GetComponentInParent<IDamageable>() != null) continue;
                if (hits[i].distance >= nearest) continue;
                closest = i; nearest = hits[i].distance;
            }
            casing.transform.Rotate(casing.spin * dt, Space.World);
            if (closest < 0) { casing.transform.position += travel; return; }
            var hit = hits[closest];
            float strike = Mathf.Max(0, -Vector3.Dot(casing.velocity, hit.normal));
            casing.transform.position = hit.point + hit.normal * (casing.profile.CollisionRadius + .002f);
            casing.velocity = Vector3.Reflect(casing.velocity, hit.normal) * casing.profile.bounce;
            casing.spin *= .52f;
            casing.contacts++;
            if (hit.normal.y > .45f)
            {
                FloorImpactCount++;
                if (strike > .55f && casing.contacts <= 3) PlayImpact(casing.profile, hit.point, strike, casing.contacts);
                if (casing.velocity.magnitude < .65f || casing.contacts >= 4)
                {
                    casing.settled = true;
                    casing.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal)
                        * Quaternion.Euler(90f, casing.transform.eulerAngles.y, 0f);
                }
            }
        }

        void PlayImpact(CasingDefinition profile, Vector3 point, float speed, int bounce)
        {
            var clips = profile.floorImpacts;
            if (clips == null || clips.Length == 0 || Time.unscaledTime < nextSound) return;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastVariant) index = (index + 1) % clips.Length;
            lastVariant = index;
            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.transform.position = point;
            source.clip = clips[index];
            source.pitch = Random.Range(.94f, 1.06f);
            source.volume = profile.impactVolume * Mathf.Clamp01(speed / 4f) / Mathf.Sqrt(bounce);
            source.Play();
            nextSound = Time.unscaledTime + .018f;
            PlayedSoundCount++;
        }

        void OnDisable()
        {
            pending.Clear();
            if (voices != null) foreach (var voice in voices) if (voice != null) voice.Stop();
            if (pool != null) foreach (var casing in pool)
            { casing.active = false; if (casing.transform != null) casing.transform.gameObject.SetActive(false); }
        }
        void OnDestroy() { if (poolRoot != null) Destroy(poolRoot.gameObject); }
    }
}
