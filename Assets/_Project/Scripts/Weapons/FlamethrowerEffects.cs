using UnityEngine;

namespace ZombieShooter
{
    /// <summary>Continuous flame visuals from accepted fuel ticks, after the hand/muzzle pose.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FlamethrowerEffects : MonoBehaviour
    {
        Weapon weapon;
        WeaponVisuals visuals;
        Health health;
        ParticleSystem fire, smoke;
        Light glow;
        Material flameMaterial, smokeMaterial;
        Texture2D texture;
        GameObject effectsRoot;
        readonly RaycastHit[] hits = new RaycastHit[32];
        float firingUntil, emission;
        WeaponDefinition firingWeapon;
        public bool IsEmitting { get; private set; }
        public int EmittedParticles { get; private set; }
        public int LiveParticles => fire != null ? fire.particleCount : 0;

        void Awake()
        {
            weapon = GetComponent<Weapon>(); visuals = GetComponent<WeaponVisuals>(); health = GetComponent<Health>();
            effectsRoot = new GameObject("Flamethrower stream");
            texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { name = "Soft flame sprite", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[1024];
            for (int y=0;y<32;y++) for(int x=0;x<32;x++)
            {
                float radius = new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                pixels[y*32+x] = new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.6f));
            }
            texture.SetPixels(pixels); texture.Apply();
            flameMaterial = MakeMaterial("Flame", true);
            smokeMaterial = MakeMaterial("Smoke", false);
            if (flameMaterial == null || smokeMaterial == null) { enabled = false; return; }
            fire = MakeParticles("Flame jet", flameMaterial, 600);
            smoke = MakeParticles("Smoke tail", smokeMaterial, 80);
            var size = fire.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.55f,1),new Keyframe(1,.3f)));
            var colour = fire.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {new GradientColorKey(new Color(1,.9f,.45f),0),new GradientColorKey(new Color(1,.38f,.025f),.35f),new GradientColorKey(new Color(.55f,.06f,.005f),1)},
                new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.75f,.65f),new GradientAlphaKey(0,1)});
            colour.color = gradient;
            var smokeColour = smoke.colorOverLifetime; smokeColour.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] {new GradientColorKey(new Color(.13f,.12f,.11f),0),new GradientColorKey(new Color(.22f,.2f,.18f),1)},
                new[] {new GradientAlphaKey(.22f,0),new GradientAlphaKey(0,1)});
            smokeColour.color = sg;
            var lightGo = new GameObject("Nozzle glow"); lightGo.transform.SetParent(effectsRoot.transform);
            glow = lightGo.AddComponent<Light>(); glow.color = new Color(1,.35f,.04f); glow.range = 4f; glow.intensity = 0;
            glow.shadows = LightShadows.None;
        }

        Material MakeMaterial(string name, bool additive)
        {
            // A Resources reference keeps the particle shader available in player builds.
            // Guarded: new Material(null) throws, and a shader stripped from a build would
            // take the whole weapon down at Awake rather than just losing its flames.
            var shader = Resources.Load<Shader>("FlamethrowerParticles");
            if (shader == null)
            {
                Debug.LogError("FlamethrowerEffects: Resources/FlamethrowerParticles shader is " +
                               "missing, so the flamethrower will fire without visible flame.", this);
                return null;
            }

            var material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.renderQueue = 3000;
            return material;
        }

        ParticleSystem MakeParticles(string name, Material material, int capacity)
        {
            var go = new GameObject(name); go.transform.SetParent(effectsRoot.transform);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity; main.startSpeed = 0; main.startLifetime = .4f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play(); return ps;
        }

        void OnEnable() { if (weapon != null) weapon.Fired += OnFired; }
        void OnFired()
        {
            if (weapon.Definition == null || !(weapon.Definition.Delivery is ConeDelivery)) return;
            firingWeapon = weapon.Definition;
            firingUntil = Time.time + Mathf.Max(.12f,weapon.SecondsBetweenShots*1.5f);
        }
        void LateUpdate()
        {
            if (Time.timeScale <= 0) { glow.intensity = 0; return; }
            IsEmitting = weapon != null && firingWeapon != null && weapon.Definition == firingWeapon &&
                Time.time < firingUntil && !weapon.IsReloading && (health == null || health.IsAlive) &&
                (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing);
            var muzzle = visuals != null ? visuals.MainMuzzleSocket : null;
            if (!IsEmitting || muzzle == null) { emission = 0; glow.intensity = 0; return; }
            glow.transform.position = muzzle.position; glow.intensity = 2.2f + Mathf.Sin(Time.time*43f)*.45f;
            var stats = UpgradeManager.Resolve(firingWeapon);
            var cone = (ConeDelivery)firingWeapon.Delivery;
            float range = stats.Range;
            float halfAngle = stats.ConeHalfAngle >= 0 ? stats.ConeHalfAngle : cone.HalfAngle;
            emission += Time.deltaTime*450f;
            int count = Mathf.Min(64,Mathf.FloorToInt(emission)); emission -= count;
            for (int i=0;i<count;i++)
            {
                float yaw = Random.Range(-halfAngle,halfAngle);
                var direction = Quaternion.AngleAxis(yaw,Vector3.up)*transform.forward;
                float reach = Mathf.Max(.1f,range-Vector3.Distance(transform.position,muzzle.position));
                if (cone.BlockedByGeometry)
                {
                    int found = Physics.RaycastNonAlloc(muzzle.position,direction,hits,reach,~0,QueryTriggerInteraction.Ignore);
                    for(int h=0;h<found;h++)
                        if(hits[h].collider.GetComponentInParent<IDamageable>() == null)
                            reach = Mathf.Min(reach,Mathf.Max(0,hits[h].distance-.3f));
                }
                if (reach < .1f) continue;
                float speed = range/Random.Range(.32f,.46f);
                var p = new ParticleSystem.EmitParams {
                    position = muzzle.position, velocity = direction*speed,
                    startLifetime = reach/speed, startSize = Random.Range(1.2f,2f)*Mathf.Clamp(range/11f,1,1.7f),
                    startColor = Color.white, rotation = Random.Range(0,360), angularVelocity = Random.Range(-90,90) };
                fire.Emit(p,1); EmittedParticles++;
                if (i%6==0)
                {
                    p.position += direction*Mathf.Min(reach*.75f,4);
                    p.velocity = direction*1.1f+Vector3.up*.65f;
                    p.startLifetime = .4f; p.startSize = .7f; p.startColor = Color.white;
                    smoke.Emit(p,1);
                }
            }
            // Compact hot core makes ignition readable even at point-blank range.
            //
            // White, not blue. colorOverLifetime MULTIPLIES the start colour rather than
            // replacing it, so a blue core met the gradient's orange and came out a dark
            // brown speck - the opposite of hot. White lets the gradient's own yellow head
            // through. A genuinely blue pilot flame would need its own system, outside this
            // gradient.
            fire.Emit(new ParticleSystem.EmitParams {position=muzzle.position,velocity=transform.forward*2,
                startLifetime=.08f,startSize=.32f,startColor=Color.white},1);
        }
        void OnDisable()
        {
            if (weapon != null) weapon.Fired -= OnFired;
            firingUntil = 0; IsEmitting = false;
            if (fire != null) fire.Clear(); if (smoke != null) smoke.Clear(); if (glow != null) glow.intensity = 0;
        }
        void OnDestroy()
        {
            if (effectsRoot != null) Destroy(effectsRoot);
            if (flameMaterial != null) Destroy(flameMaterial); if (smokeMaterial != null) Destroy(smokeMaterial);
            if (texture != null) Destroy(texture);
        }
    }
}
