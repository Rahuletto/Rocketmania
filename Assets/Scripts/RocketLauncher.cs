using UnityEngine;

[DefaultExecutionOrder(10)]
public class RocketLauncher : MonoBehaviour
{
    [SerializeField] RocketProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [SerializeField] float muzzleClearance = 0.45f;
    [SerializeField] float projectileSpeed = 52f;
    [SerializeField] float fireCooldown = 0.45f;
    [SerializeField] float maxAimDistance = 400f;
    [SerializeField] LayerMask aimHitMask = ~0;

    [Header("Recoil")]
    [SerializeField] float recoilPitch = 1.8f;
    [SerializeField] float recoilYaw = 0.55f;
    [SerializeField] float aimRecoilMultiplier = 0.32f;
    [SerializeField] float shakeImpulseHip = 0.035f;
    [SerializeField] float shakeImpulseAim = 0.014f;
    [SerializeField] float shootPushback = 8.5f;
    [SerializeField] float shootPushbackVerticalBlend = 0.4f;

    [Header("Aim pitch")]
    [SerializeField] float aimPitchFollowSmoothing = 14f;
    [SerializeField] float pitchAngleSign = 1f;
    [SerializeField] AimPitchAxis aimPitchAxis = AimPitchAxis.ParentLocalX;

    enum AimPitchAxis
    {
        ParentLocalX,
        ParentLocalY,
        ParentLocalZ
    }

    ThirdPersonCamera thirdPersonCamera;
    PlayerMotor ownerMotor;
    float nextFireTime;
    Quaternion baseLocalRotation;

    void Awake()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>();

        ownerMotor = GetComponentInParent<PlayerMotor>();
        if (muzzle == null)
        {
            Transform head = transform.Find("Head");
            muzzle = head != null ? head : transform;
        }

        baseLocalRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        if (thirdPersonCamera == null)
            return;

        float aimT = thirdPersonCamera.AimBlendSmoothed;
        float pitchDeg = thirdPersonCamera.AimPitchDegrees * pitchAngleSign;
        Quaternion pitchInParent = aimPitchAxis switch
        {
            AimPitchAxis.ParentLocalX => Quaternion.Euler(pitchDeg, 0f, 0f),
            AimPitchAxis.ParentLocalY => Quaternion.Euler(0f, pitchDeg, 0f),
            AimPitchAxis.ParentLocalZ => Quaternion.Euler(0f, 0f, pitchDeg),
            _ => Quaternion.Euler(pitchDeg, 0f, 0f)
        };
        Quaternion withPitch = pitchInParent * baseLocalRotation;
        Quaternion targetRot = Quaternion.Slerp(baseLocalRotation, withPitch, aimT);
        float k = 1f - Mathf.Exp(-aimPitchFollowSmoothing * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, k);
    }

    public void TryFire()
    {
        if (thirdPersonCamera == null || muzzle == null)
            return;
        if (Time.time < nextFireTime)
            return;

        Ray aimRay = thirdPersonCamera.GetAimRay();
        Vector3 aimPoint = GetCrosshairWorldPoint(aimRay);
        Vector3 muzzlePos = muzzle.position;
        Vector3 dir = aimPoint - muzzlePos;
        if (dir.sqrMagnitude < 1e-6f)
            dir = aimRay.direction;
        dir.Normalize();

        Vector3 spawnPos = muzzlePos + dir * muzzleClearance;
        Quaternion rot = Quaternion.LookRotation(dir);

        RocketProjectile proj = CreateProjectile(spawnPos, rot);
        Collider[] ownerColliders = ownerMotor != null
            ? ownerMotor.GetComponentsInChildren<Collider>()
            : System.Array.Empty<Collider>();

        proj.Launch(dir * projectileSpeed, ownerColliders, ownerMotor);

        nextFireTime = Time.time + fireCooldown;

        bool aiming = thirdPersonCamera.IsAiming;
        float recoilMul = aiming ? aimRecoilMultiplier : 1f;
        float yawKick = Random.Range(-recoilYaw, recoilYaw) * recoilMul;

        thirdPersonCamera.ApplyRecoil(recoilPitch * recoilMul, yawKick);
        float shake = aiming ? shakeImpulseAim : shakeImpulseHip;
        thirdPersonCamera.ShakeImpulse(shake);

        if (ownerMotor != null && shootPushback > 0f)
            ApplyShooterKnockback(dir, recoilMul);
    }

    void ApplyShooterKnockback(Vector3 shotDir, float aimStabilityMul)
    {
        float strength = shootPushback * aimStabilityMul;
        Vector3 raw = -shotDir * strength;
        float vy = raw.y * shootPushbackVerticalBlend;
        if (vy < 0f)
            vy = 0f;

        Vector3 impulse = new Vector3(raw.x, vy, raw.z);
        if (new Vector3(impulse.x, 0f, impulse.z).sqrMagnitude < 0.04f)
        {
            Vector3 flatBack = -ownerMotor.transform.forward * strength;
            impulse.x = flatBack.x;
            impulse.z = flatBack.z;
        }

        ownerMotor.ApplyBlastImpulse(impulse);
    }

    RocketProjectile CreateProjectile(Vector3 position, Quaternion rotation)
    {
        if (projectilePrefab != null)
        {
            RocketProjectile p = Instantiate(projectilePrefab, position, rotation);
            return p;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "RocketProjectile";
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = Vector3.one * 0.14f;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody>();

        RocketProjectile rp = go.GetComponent<RocketProjectile>();
        if (rp == null)
            rp = go.AddComponent<RocketProjectile>();

        return rp;
    }

    Vector3 GetCrosshairWorldPoint(Ray aimRay)
    {
        Transform ownerRoot = ownerMotor != null ? ownerMotor.transform : null;

        RaycastHit[] hits = Physics.RaycastAll(
            aimRay.origin,
            aimRay.direction,
            maxAimDistance,
            aimHitMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (ownerRoot != null &&
                (h.collider.transform == ownerRoot || h.collider.transform.IsChildOf(ownerRoot)))
                continue;

            return h.point;
        }

        return aimRay.origin + aimRay.direction * maxAimDistance;
    }
}
