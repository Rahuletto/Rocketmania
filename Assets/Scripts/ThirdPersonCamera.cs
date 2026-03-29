using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0.2f, 0.96f, 0f);

    [Header("Framing")]
    [SerializeField] private float shoulderOffset = 1f;

    [Header("Aim")]
    [SerializeField] private float aimDistance = 3.55f;
    [SerializeField] private float aimShoulderOffset = 0.62f;
    [SerializeField] private float aimBlendSpeed = 6f;
    [SerializeField] [Range(0.85f, 1f)] private float aimFovMultiplier = 0.985f;

    [Header("Orbit")]
    [SerializeField] private float distance = 4.25f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 55f;

    [Header("Look")]
    [SerializeField] [Range(0.02f, 0.4f)] private float lookSensitivity = 0.12f;
    [SerializeField] private bool invertY;
    [SerializeField] private bool rotatePlayerYawWithCamera = true;

    [Header("Collision")]
    [SerializeField] private bool avoidClipping = true;
    [SerializeField] private float sphereRadius = 0.25f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float clipSmooth = 12f;

    [Header("Shake")]
    [SerializeField] private float shakeFrequency = 18f;

    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private bool lockCursorOnPlay;

    private Transform cameraTransform;
    private bool didApplyCursorLock;
    private float yaw;
    private float pitch;
    private float currentDistance;
    private float aimBlend;
    private bool aimHeld;
    private float defaultFieldOfView;
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeMagnitude;
    private Vector3 shakeOffset;
    private float shakeNoiseSeed;

    internal float LookSensitivity
    {
        get => lookSensitivity;
        set => lookSensitivity = Mathf.Clamp(value, 0.02f, 0.4f);
    }

    internal Ray GetAimRay()
    {
        if (cam == null)
            return default;
        return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private void Awake()
    {
        if (target == null)
        {
            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
                target = motor.transform;
        }

        if (cam == null)
            cam = GetComponentInChildren<Camera>();
        if (cam == null)
            cam = Camera.main;

        cameraTransform = cam != null ? cam.transform : transform;
        if (cam != null)
            defaultFieldOfView = cam.fieldOfView;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pitch = 5f;
        }
        else
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f)
                pitch -= 360f;
        }

        currentDistance = distance;
        shakeNoiseSeed = Random.value * 1000f;
    }

    private void Start()
    {
        if (!lockCursorOnPlay)
            return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        didApplyCursorLock = true;
    }

    private void OnDestroy()
    {
        if (!didApplyCursorLock)
            return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal void SetAiming(bool aiming)
    {
        aimHeld = aiming;
    }

    internal void OnLook(Vector2 lookDelta)
    {
        if (target == null)
            return;

        float invert = invertY ? -1f : 1f;
        yaw += lookDelta.x * lookSensitivity;
        pitch -= lookDelta.y * lookSensitivity * invert;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (rotatePlayerYawWithCamera)
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (target == null || cam == null)
            return;

        UpdateShake();

        float targetBlend = aimHeld ? 1f : 0f;
        aimBlend = Mathf.MoveTowards(aimBlend, targetBlend, Time.deltaTime * aimBlendSpeed);

        float effectiveDistance = Mathf.Lerp(distance, aimDistance, aimBlend);
        float effectiveShoulder = Mathf.Lerp(shoulderOffset, aimShoulderOffset, aimBlend);

        float aimFov = defaultFieldOfView * aimFovMultiplier;
        cam.fieldOfView = Mathf.Lerp(defaultFieldOfView, aimFov, aimBlend);

        Vector3 pivot = target.position + target.TransformVector(pivotOffset);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredOffset = orbit * new Vector3(0f, 0f, -effectiveDistance);

        float d = effectiveDistance;
        if (avoidClipping &&
            Physics.SphereCast(pivot, sphereRadius, desiredOffset.normalized, out RaycastHit hit,
                effectiveDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            bool hitSelf = hit.collider != null && target != null &&
                (hit.collider.transform == target || hit.collider.transform.IsChildOf(target));
            if (!hitSelf)
                d = Mathf.Max(sphereRadius * 2f, hit.distance - sphereRadius);
        }

        currentDistance = Mathf.Lerp(currentDistance, d, Time.deltaTime * clipSmooth);

        Vector3 lateral = orbit * new Vector3(effectiveShoulder, 0f, 0f);
        Vector3 basePos = pivot + orbit * new Vector3(0f, 0f, -currentDistance) + lateral;
        cameraTransform.position = basePos + shakeOffset;
        cameraTransform.rotation = orbit;
    }

    private void UpdateShake()
    {
        if (shakeTimeRemaining <= 0f)
        {
            shakeOffset = Vector3.zero;
            return;
        }

        shakeTimeRemaining -= Time.deltaTime;
        float t = shakeDuration > 0f ? Mathf.Clamp01(shakeTimeRemaining / shakeDuration) : 0f;
        float envelope = t * t;
        float time = Time.time * shakeFrequency;

        float nx = (Mathf.PerlinNoise(shakeNoiseSeed, time) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(shakeNoiseSeed + 17f, time) - 0.5f) * 2f;
        float nz = (Mathf.PerlinNoise(shakeNoiseSeed + 31f, time) - 0.5f) * 2f;
        shakeOffset = new Vector3(nx, ny, nz) * (shakeMagnitude * envelope);
    }

    internal void Shake(float magnitude, float duration)
    {
        if (duration <= 0f)
            return;
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
        shakeDuration = duration;
        shakeTimeRemaining = duration;
    }

    internal void ShakeImpulse(float magnitude)
    {
        Shake(magnitude, 0.08f);
    }
}
