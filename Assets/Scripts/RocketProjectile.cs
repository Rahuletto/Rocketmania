using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class RocketProjectile : MonoBehaviour
{
    [SerializeField] GameObject impactFxPrefab;
    [SerializeField] float impactFxScale = 1f;
    [SerializeField] float impactFxDestroyAfterSeconds;
    [FormerlySerializedAs("impactSphereRadius")]
    [SerializeField] float fallbackBlastRadius = 0.35f;
    [FormerlySerializedAs("impactSphereLifetime")]
    [SerializeField] float fallbackBlastCleanupTime = 2.2f;

    [SerializeField] float blastRadius = 3.2f;
    [SerializeField] float blastImpulse = 16f;
    [SerializeField] float blastUpwardBoost = 0.55f;
    [SerializeField] float blastSurfaceNormalWeight = 0.5f;
    [SerializeField] bool ignoreOwnerInBlast;

    [Header("Physics blast — Rigidbodies")]
    [SerializeField] float rigidbodyBlastImpulse = 18f;
    [SerializeField] bool affectKinematicRigidbodies;

    [Header("Radial shockwave visual")]
    [SerializeField] bool spawnRadialShockwaveVisual = true;
    [SerializeField] float shockwaveVisualDuration = 0.38f;
    [SerializeField] Color shockwaveColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] float shockwaveStartAlpha = 0.5f;
    [SerializeField] float shockwaveBloomIntensity = 5f;
    [SerializeField] bool shockwaveSpawnLight = true;
    [SerializeField] Light shockwaveLightReference;
    [SerializeField] float shockwaveLightIntensityFallback = 1400f;

    Rigidbody rb;
    Collider col;
    bool hitSomething;
    PlayerMotor ownerMotor;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.mass = 0.35f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(Vector3 worldVelocity, Collider[] ownerColliders, PlayerMotor owner)
    {
        ownerMotor = owner;
        if (ownerColliders != null)
        {
            foreach (Collider c in ownerColliders)
            {
                if (c != null && col != null)
                    Physics.IgnoreCollision(col, c, true);
            }
        }

        rb.linearVelocity = worldVelocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hitSomething)
            return;
        hitSomething = true;

        ContactPoint contact = collision.GetContact(0);
        Vector3 pos = contact.point;
        Vector3 normal = contact.normal;

        ApplyBlastToPlayers(pos, normal);
        if (rigidbodyBlastImpulse > 0f)
            ApplyBlastToRigidbodies(pos, normal);
        if (spawnRadialShockwaveVisual)
            SpawnRadialShockwaveVisual(pos);

        SpawnImpact(pos, normal);

        Destroy(gameObject);
    }

    Vector3 BlastDirection(Vector3 radialUnit, Vector3 impactNormal)
    {
        Vector3 n = impactNormal.sqrMagnitude > 1e-6f ? impactNormal.normalized : Vector3.up;
        Vector3 v = radialUnit + Vector3.up * blastUpwardBoost + n * blastSurfaceNormalWeight;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
    }

    void ApplyBlastToPlayers(Vector3 blastCenter, Vector3 impactNormal)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            blastCenter,
            blastRadius,
            ~0,
            QueryTriggerInteraction.Ignore);

        var seen = new HashSet<PlayerMotor>();
        foreach (Collider c in overlaps)
        {
            PlayerMotor motor = c.GetComponentInParent<PlayerMotor>();
            if (motor == null || !seen.Add(motor))
                continue;
            if (ignoreOwnerInBlast && ownerMotor != null && motor == ownerMotor)
                continue;

            Vector3 toTarget = motor.transform.position - blastCenter;
            float dist = toTarget.magnitude;
            Vector3 radial = dist > 0.01f ? toTarget / dist : Vector3.up;
            Vector3 dir = BlastDirection(radial, impactNormal);
            float falloff = 1f - Mathf.Clamp01(dist / blastRadius);
            Vector3 deltaV = dir * (blastImpulse * falloff);
            motor.ApplyBlastImpulse(deltaV);
        }
    }

    void ApplyBlastToRigidbodies(Vector3 blastCenter, Vector3 impactNormal)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            blastCenter,
            blastRadius,
            ~0,
            QueryTriggerInteraction.Ignore);

        var seen = new HashSet<Rigidbody>();
        foreach (Collider c in overlaps)
        {
            if (c.GetComponentInParent<PlayerMotor>() != null)
                continue;

            Rigidbody body = c.attachedRigidbody;
            if (body == null || !seen.Add(body))
                continue;
            if (body == rb)
                continue;
            if (body.isKinematic && !affectKinematicRigidbodies)
                continue;

            if (ignoreOwnerInBlast && ownerMotor != null &&
                (body.transform == ownerMotor.transform || body.transform.IsChildOf(ownerMotor.transform)))
                continue;

            Vector3 toTarget = body.worldCenterOfMass - blastCenter;
            float dist = toTarget.magnitude;
            Vector3 radial = dist > 0.01f ? toTarget / dist : Vector3.up;
            Vector3 dir = BlastDirection(radial, impactNormal);
            float falloff = 1f - Mathf.Clamp01(dist / blastRadius);
            body.AddForce(dir * (rigidbodyBlastImpulse * falloff), ForceMode.Impulse);
        }
    }

    void SpawnRadialShockwaveVisual(Vector3 center)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "RadialShockwave";
        go.transform.position = center;
        go.transform.localScale = Vector3.one * 0.12f;
        Destroy(go.GetComponent<Collider>());

        Shader s = Shader.Find("HDRP/Unlit");
        if (s == null)
            s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null)
            s = Shader.Find("Unlit/Color");
        if (s == null)
            s = Shader.Find("Sprites/Default");

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        if (r != null && s != null)
        {
            Material mat = new Material(s);
            SetupShockwaveTransparentMaterial(mat);
            r.material = mat;
        }

        bool shaderIsHdrp = s != null && s.name.IndexOf("HDRP", System.StringComparison.OrdinalIgnoreCase) >= 0;
        RocketImpactShockwave wave = go.AddComponent<RocketImpactShockwave>();
        wave.Init(
            blastRadius,
            shockwaveVisualDuration,
            shockwaveColor,
            shockwaveStartAlpha,
            shockwaveBloomIntensity,
            shaderIsHdrp,
            shockwaveSpawnLight,
            shockwaveLightReference,
            shockwaveLightIntensityFallback);
    }

    static void SetupShockwaveTransparentMaterial(Material mat)
    {
        if (mat == null || mat.shader == null)
            return;

        bool hdrp = mat.shader.name.IndexOf("HDRP", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (hdrp)
        {
            HDMaterial.SetSurfaceType(mat, true);
            HDMaterial.ValidateMaterial(mat);
        }
        else if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);

        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    void SpawnImpact(Vector3 position, Vector3 normal)
    {
        if (impactFxPrefab != null)
        {
            Quaternion rot = normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;
            GameObject fx = Instantiate(impactFxPrefab, position, rot);
            if (impactFxScale != 1f)
                fx.transform.localScale = fx.transform.localScale * impactFxScale;
            PlayImpactFx(fx);
            ScheduleImpactFxCleanup(fx);
            return;
        }

        SpawnFallbackBlastParticles(position, normal);
    }

    void ScheduleImpactFxCleanup(GameObject fxRoot)
    {
        if (fxRoot == null)
            return;

        float delay = impactFxDestroyAfterSeconds > 0f
            ? impactFxDestroyAfterSeconds
            : EstimateImpactFxLifetime(fxRoot);
        Destroy(fxRoot, delay);
    }

    static float EstimateImpactFxLifetime(GameObject fxRoot)
    {
        float maxEnd = 0f;
        bool any = false;
        foreach (ParticleSystem ps in fxRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            any = true;
            var main = ps.main;
            float life = main.startLifetime.constantMax;

            if (main.loop)
            {
                float loopCap = Mathf.Min(Mathf.Max(life, main.duration) + 1f, 8f);
                maxEnd = Mathf.Max(maxEnd, loopCap);
                continue;
            }

            float emitWindow = main.duration;
            maxEnd = Mathf.Max(maxEnd, emitWindow + life);
        }

        if (!any)
            return 2f;

        return Mathf.Clamp(maxEnd + 0.4f, 0.6f, 25f);
    }

    static void PlayImpactFx(GameObject fxRoot)
    {
        if (fxRoot == null)
            return;

        foreach (ParticleSystem ps in fxRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void SpawnFallbackBlastParticles(Vector3 position, Vector3 normal)
    {
        Quaternion rot = normal.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        GameObject root = new GameObject("RocketImpactBlast");
        root.transform.SetPositionAndRotation(position, rot);

        float s = Mathf.Max(0.05f, fallbackBlastRadius * impactFxScale);
        AddFireBurst(root.transform, s);
        AddSmokePuff(root.transform, s);

        Destroy(root, fallbackBlastCleanupTime);
    }

    static void AddFireBurst(Transform parent, float radius)
    {
        GameObject go = new GameObject("FireBurst");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f * radius / 0.35f, 14f * radius / 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * radius / 0.35f, 0.28f * radius / 0.35f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;

        var grad = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.7f),
            new Color(1f, 0.35f, 0.05f));
        main.startColor = grad;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)52, (short)68, 1, 0.02f)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.45f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.12f, 1f),
            new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.22f),
                new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 0.65f),
                new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    static void AddSmokePuff(Transform parent, float radius)
    {
        GameObject go = new GameObject("SmokePuff");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * radius / 0.35f, 3.5f * radius / 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f * radius / 0.35f, 0.65f * radius / 0.35f);
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;
        main.startColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)18, (short)26, 1, 0.04f)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.55f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.4f, 1f),
            new Keyframe(1f, 1.15f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.45f, 0.45f), 0f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0.25f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }
}

public class RocketImpactShockwave : MonoBehaviour
{
    float duration;
    float elapsed;
    float startDiameter;
    float endDiameter;
    Color baseRgb;
    float alphaStart;
    float bloomIntensity;
    bool hdrpShader;
    Material mat;
    Light fxLight;
    float lightIntensityStart;

    public void Init(
        float blastRadius,
        float dur,
        Color color,
        float startAlpha,
        float bloomEmissiveIntensity,
        bool shaderIsHdrp,
        bool spawnLight,
        Light lightReference,
        float lightIntensityFallback)
    {
        duration = Mathf.Max(0.05f, dur);
        startDiameter = 0.15f;
        endDiameter = Mathf.Max(0.2f, blastRadius * 2f);
        baseRgb = color;
        baseRgb.a = 1f;
        alphaStart = Mathf.Clamp01(startAlpha);
        bloomIntensity = Mathf.Max(0f, bloomEmissiveIntensity);
        hdrpShader = shaderIsHdrp;
        Renderer r = GetComponent<Renderer>();
        if (r != null)
            mat = r.material;

        if (spawnLight)
        {
            Light L = gameObject.AddComponent<Light>();
            L.type = LightType.Point;
            L.shadows = LightShadows.None;
            float range = Mathf.Max(endDiameter * 0.55f, blastRadius * 1.25f);

            if (lightReference != null)
            {
                L.color = lightReference.color;
                L.intensity = lightReference.intensity;
                L.useColorTemperature = lightReference.useColorTemperature;
                L.colorTemperature = lightReference.colorTemperature;
                L.lightUnit = lightReference.lightUnit;
                if (lightReference.type == LightType.Point || lightReference.type == LightType.Spot)
                    L.range = Mathf.Max(lightReference.range, range);
                else
                    L.range = range;
                if (lightReference.type == LightType.Spot)
                {
                    L.type = LightType.Spot;
                    L.spotAngle = lightReference.spotAngle;
                    L.innerSpotAngle = lightReference.innerSpotAngle;
                }
            }
            else
            {
                L.color = new Color(baseRgb.r, baseRgb.g, baseRgb.b, 1f);
                L.range = range;
                L.intensity = lightIntensityFallback;
            }

            lightIntensityStart = L.intensity;
            fxLight = L;
        }

        ApplyAtProgress(0f);
    }

    void ApplyAtProgress(float u)
    {
        u = Mathf.Clamp01(u);
        float diameter = Mathf.Lerp(startDiameter, endDiameter, u);
        transform.localScale = Vector3.one * diameter;

        float fade = Mathf.Lerp(alphaStart, 0f, u);
        if (fxLight != null)
            fxLight.intensity = lightIntensityStart * fade;

        if (mat == null)
            return;

        Color c = baseRgb;
        c.a = fade;

        if (mat.HasProperty("_UnlitColor"))
            mat.SetColor("_UnlitColor", c);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        mat.color = c;

        if (hdrpShader)
        {
            if (bloomIntensity > 0f && fade > 0f)
            {
                Color em = new Color(baseRgb.r, baseRgb.g, baseRgb.b, 1f);
                em *= bloomIntensity * fade * 0.4f;
                HDMaterial.SetEmissiveColor(mat, em);
            }
            else
                HDMaterial.SetEmissiveColor(mat, Color.black);
            return;
        }

        if (bloomIntensity > 0f && fade > 0f)
        {
            Color emissive = new Color(baseRgb.r, baseRgb.g, baseRgb.b, 1f);
            emissive *= bloomIntensity * fade * 0.4f;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissive);
        }
        else if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", Color.black);
    }

    void LateUpdate()
    {
        elapsed += Time.deltaTime;
        float u = Mathf.Clamp01(elapsed / duration);
        ApplyAtProgress(u);

        if (u >= 1f)
        {
            ApplyAtProgress(1f);
            Destroy(gameObject);
        }
    }
}
