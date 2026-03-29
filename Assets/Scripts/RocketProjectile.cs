using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class RocketProjectile : MonoBehaviour
{
    [SerializeField] GameObject impactFxPrefab;
    [SerializeField] float impactSphereRadius = 0.35f;
    [SerializeField] float impactSphereLifetime = 2f;

    [SerializeField] float blastRadius = 3.2f;
    [SerializeField] float blastImpulse = 16f;
    [SerializeField] float blastUpwardBoost = 0.55f;
    [SerializeField] bool ignoreOwnerInBlast;

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

        ApplyBlastToPlayers(pos);

        SpawnImpact(pos, normal);

        Destroy(gameObject);
    }

    void ApplyBlastToPlayers(Vector3 blastCenter)
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
            Vector3 dir = (radial + Vector3.up * blastUpwardBoost).normalized;
            float falloff = 1f - Mathf.Clamp01(dist / blastRadius);
            Vector3 deltaV = dir * (blastImpulse * falloff);
            motor.ApplyBlastImpulse(deltaV);
        }
    }

    void SpawnImpact(Vector3 position, Vector3 normal)
    {
        if (impactFxPrefab != null)
        {
            Quaternion rot = normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;
            Instantiate(impactFxPrefab, position, rot);
            return;
        }

        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.name = "RocketImpact";
        fx.transform.position = position + normal * (impactSphereRadius * 0.5f);
        fx.transform.localScale = Vector3.one * (impactSphereRadius * 2f);
        Destroy(fx.GetComponent<Collider>());
        Renderer r = fx.GetComponent<Renderer>();
        if (r != null)
        {
            Shader s = Shader.Find("HDRP/Unlit");
            if (s == null)
                s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null)
                s = Shader.Find("Unlit/Color");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            Material mat = new Material(s);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_UnlitColor"))
                mat.SetColor("_UnlitColor", Color.white);
            if (mat.HasProperty("_BaseColorMap"))
                mat.SetTexture("_BaseColorMap", null);
            mat.color = Color.white;
            r.material = mat;
        }

        Destroy(fx, impactSphereLifetime);
    }
}
