using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10)]
public class RocketLauncher : MonoBehaviour
{
    [SerializeField] RocketProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [SerializeField] float muzzleClearance = 0.45f;
    [SerializeField] float projectileSpeed = 52f;
    [SerializeField] float maxAimDistance = 400f;
    [SerializeField] LayerMask aimHitMask = ~0;

    [Header("Ammo")]
    [SerializeField] int maxAmmo = 5;
    [SerializeField] float loadingDuration = 1f;
    [SerializeField] float fullReloadDuration = 6f;
    [SerializeField] float loadingMoveSpeedMultiplier = 0.9f;
    [SerializeField] float reloadMoveSpeedMultiplier = 0.75f;

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
    [SerializeField] bool followCameraPitchOnlyWhileAiming = false;

    [Header("Wall avoidance")]
    [SerializeField] float wallProbeInset = 0.12f;
    [SerializeField] float wallProbeDistance = 0.95f;
    [SerializeField] float maxPullback = 0.45f;
    [SerializeField] float pullbackSmoothing = 18f;
    [SerializeField] LayerMask wallObstacleMask = ~0;

    ThirdPersonCamera thirdPersonCamera;
    PlayerMotor ownerMotor;
    int currentAmmo;
    float loadingEndTime = -1f;
    float reloadEndTime = -1f;
    Quaternion baseLocalRotation;
    Vector3 baseLocalPosition;
    float pullbackSmoothed;
    InputAction reloadActionBound;

    internal int CurrentAmmo => currentAmmo;
    internal int MaxAmmo => maxAmmo;

    internal bool IsLoadingCycle =>
        !IsFullReloading && loadingEndTime > 0f && Time.time < loadingEndTime;

    internal bool IsFullReloading => reloadEndTime > 0f && Time.time < reloadEndTime;

    internal float LoadingProgress01
    {
        get
        {
            if (loadingDuration <= 1e-6f || !IsLoadingCycle)
                return 1f;
            float rem = loadingEndTime - Time.time;
            return 1f - Mathf.Clamp01(rem / loadingDuration);
        }
    }

    internal float FullReloadProgress01
    {
        get
        {
            if (fullReloadDuration <= 1e-6f || !IsFullReloading)
                return 1f;
            float started = reloadEndTime - fullReloadDuration;
            float elapsed = Time.time - started;
            return Mathf.Clamp01(elapsed / fullReloadDuration);
        }
    }

    internal float MovementSpeedMultiplier
    {
        get
        {
            if (IsFullReloading)
                return reloadMoveSpeedMultiplier;
            if (IsLoadingCycle)
                return loadingMoveSpeedMultiplier;
            return 1f;
        }
    }

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
        baseLocalPosition = transform.localPosition;
    }

    void OnEnable()
    {
        var ih = GetComponentInParent<InputHandler>();
        reloadActionBound = ih != null ? ih.ReloadAction : null;
        if (reloadActionBound != null)
            reloadActionBound.performed += OnReloadPerformed;
    }

    void OnDisable()
    {
        if (reloadActionBound != null)
        {
            reloadActionBound.performed -= OnReloadPerformed;
            reloadActionBound = null;
        }
    }

    void Start()
    {
        currentAmmo = maxAmmo;
    }

    void OnReloadPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
            TryReload();
    }

    public void TryReload()
    {
        if (currentAmmo >= maxAmmo)
            return;
        if (IsFullReloading)
            return;

        loadingEndTime = -1f;
        reloadEndTime = Time.time + fullReloadDuration;
    }

    void LateUpdate()
    {
        if (reloadEndTime > 0f && Time.time >= reloadEndTime)
        {
            currentAmmo = maxAmmo;
            reloadEndTime = -1f;
        }

        if (thirdPersonCamera == null)
            return;

        Transform parent = transform.parent;
        Transform cam = thirdPersonCamera.AimCameraTransform;
        if (parent == null || cam == null)
            return;

        float pitchBlend = followCameraPitchOnlyWhileAiming ? thirdPersonCamera.AimBlendSmoothed : 1f;
        float pitchDeg = thirdPersonCamera.AimPitchDegrees * pitchAngleSign * pitchBlend;

        Quaternion baseWorld = parent.rotation * baseLocalRotation;
        Vector3 pitchAxis = cam.right;
        if (pitchAxis.sqrMagnitude < 1e-8f)
            return;
        pitchAxis.Normalize();

        if (Mathf.Abs(Vector3.Dot(pitchAxis, Vector3.up)) > 0.98f)
            pitchAxis = Vector3.Cross(cam.forward, Vector3.up).normalized;

        Quaternion pitchWorld = Quaternion.AngleAxis(pitchDeg, pitchAxis);
        Quaternion targetWorld = pitchWorld * baseWorld;

        float k = 1f - Mathf.Exp(-aimPitchFollowSmoothing * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetWorld, k);

        ApplyWallPullback(parent);
    }

    void ApplyWallPullback(Transform parent)
    {
        if (ownerMotor == null)
            return;

        Vector3 fwd = muzzle != null ? muzzle.forward : transform.forward;
        Vector3 origin = (muzzle != null ? muzzle.position : transform.position) - fwd * wallProbeInset;

        float pullbackTarget = 0f;
        RaycastHit[] hits = Physics.RaycastAll(origin, fwd, wallProbeDistance, wallObstacleMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (IsOwnerCollider(h.collider))
                    continue;
                float t = 1f - Mathf.Clamp01(h.distance / wallProbeDistance);
                pullbackTarget = t * maxPullback;
                break;
            }
        }

        float kp = 1f - Mathf.Exp(-pullbackSmoothing * Time.deltaTime);
        pullbackSmoothed = Mathf.Lerp(pullbackSmoothed, pullbackTarget, kp);

        Vector3 worldOffset = -fwd * pullbackSmoothed;
        transform.localPosition = baseLocalPosition + parent.InverseTransformVector(worldOffset);
    }

    bool IsOwnerCollider(Collider c)
    {
        if (c == null || ownerMotor == null)
            return false;
        Transform t = c.transform;
        return t == ownerMotor.transform || t.IsChildOf(ownerMotor.transform);
    }

    public void TryFire()
    {
        if (thirdPersonCamera == null || muzzle == null)
            return;
        if (IsFullReloading)
            return;
        if (IsLoadingCycle)
            return;
        if (currentAmmo <= 0)
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

        currentAmmo--;
        if (currentAmmo <= 0)
        {
            loadingEndTime = -1f;
            reloadEndTime = Time.time + fullReloadDuration;
        }
        else
            loadingEndTime = Time.time + loadingDuration;

        bool aiming = thirdPersonCamera.IsAiming;
        float recoilMul = aiming ? aimRecoilMultiplier : 1f;
        if (ownerMotor != null && ownerMotor.IsSprintingBoostActive)
            recoilMul *= 2f;

        float yawKick = Random.Range(-recoilYaw, recoilYaw) * recoilMul;

        thirdPersonCamera.ApplyRecoil(recoilPitch * recoilMul, yawKick);
        float shake = aiming ? shakeImpulseAim : shakeImpulseHip;
        if (ownerMotor != null && ownerMotor.IsSprintingBoostActive)
            shake *= 2f;
        thirdPersonCamera.ShakeImpulse(shake);

        if (ownerMotor != null && shootPushback > 0f)
            ApplyShooterKnockback(dir, aiming ? aimRecoilMultiplier : 1f);
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
