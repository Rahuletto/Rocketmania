using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    RocketLauncher rocketLauncher;
    private Vector3 playerVelocity;
    public float speed = 5f;
    private bool isGrounded;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;
    [SerializeField] float aimSpeedMultiplier = 0.52f;
    [SerializeField] float sprintSpeedMultiplier = 1.65f;
    [Header("Stamina (sprint)")]
    [SerializeField] float maxStaminaSeconds = 8f;
    [SerializeField] float staminaRegenPerSecond = 3f;
    [SerializeField] float exhaustedSpeedMultiplier = 0.75f;
    [SerializeField] float reloadJumpHeightMultiplier = 0.5f;
    [SerializeField] float knockbackDecay = 10f;
    bool aiming;
    bool sprinting;
    bool exhausted;
    float stamina;
    Vector2 lastMoveInput;
    Vector3 knockbackVelocity;
    private float standingHeight;
    private Vector3 standingCenter;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        rocketLauncher = GetComponentInChildren<RocketLauncher>(true);
        standingHeight = controller.height;
        standingCenter = controller.center;
        stamina = maxStaminaSeconds;
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;
    }

    public void Move(Vector2 input)
    {
        isGrounded = controller.isGrounded;
        lastMoveInput = input;
        float dt = Time.deltaTime;

        UpdateStamina(input, dt);

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        float aimMul = aiming ? aimSpeedMultiplier : 1f;
        if (exhausted)
            aimMul *= exhaustedSpeedMultiplier;

        if (IsSprintingBoostActive)
            aimMul *= sprintSpeedMultiplier;

        if (rocketLauncher != null)
            aimMul *= rocketLauncher.MovementSpeedMultiplier;

        Vector3 kb = new Vector3(knockbackVelocity.x, 0f, knockbackVelocity.z) * Time.deltaTime;
        controller.Move(move * speed * aimMul * Time.deltaTime + kb);

        float kd = Mathf.Exp(-knockbackDecay * Time.deltaTime);
        knockbackVelocity.x *= kd;
        knockbackVelocity.z *= kd;

        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        if (isGrounded && playerVelocity.y < 0f)
            playerVelocity.y = -2f;
    }

    public void Jump()
    {
        if (!controller.isGrounded)
            return;
        float h = jumpHeight;
        if (rocketLauncher != null && rocketLauncher.IsFullReloading)
            h *= reloadJumpHeightMultiplier;
        playerVelocity.y = Mathf.Sqrt(2f * h * -gravity);
    }

    public void Crouch()
    {
        float h = standingHeight * 0.5f;
        controller.height = h;
        controller.center = new Vector3(standingCenter.x, h * 0.5f, standingCenter.z);
    }

    public void Uncrouch()
    {
        controller.height = standingHeight;
        controller.center = standingCenter;
    }

    public void SetAiming(bool value) => aiming = value;

    public void SetSprinting(bool value) => sprinting = value;

    public bool IsSprinting => sprinting;

    public bool IsSprintingBoostActive =>
        sprinting
        && !aiming
        && lastMoveInput.sqrMagnitude > 0.01f
        && stamina > 0f
        && !exhausted
        && !(rocketLauncher != null && rocketLauncher.IsFullReloading);

    public float StaminaNormalized => maxStaminaSeconds > 1e-6f ? stamina / maxStaminaSeconds : 0f;

    public bool IsExhausted => exhausted;

    void UpdateStamina(Vector2 input, float dt)
    {
        if (maxStaminaSeconds <= 0f)
            return;

        if (exhausted)
        {
            stamina += staminaRegenPerSecond * dt;
            if (stamina >= maxStaminaSeconds)
            {
                stamina = maxStaminaSeconds;
                exhausted = false;
            }
            return;
        }

        bool draining =
            sprinting
            && !aiming
            && input.sqrMagnitude > 0.01f
            && stamina > 0f
            && !(rocketLauncher != null && rocketLauncher.IsFullReloading);
        if (draining)
        {
            stamina -= dt;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
            }
        }
        else
        {
            stamina += staminaRegenPerSecond * dt;
            if (stamina > maxStaminaSeconds)
                stamina = maxStaminaSeconds;
        }
    }

    public void AddKnockback(Vector3 horizontalVelocityChange)
    {
        knockbackVelocity += new Vector3(horizontalVelocityChange.x, 0f, horizontalVelocityChange.z);
    }

    public void ApplyBlastImpulse(Vector3 deltaV)
    {
        knockbackVelocity += new Vector3(deltaV.x, 0f, deltaV.z);
        playerVelocity.y += deltaV.y;
    }
}
