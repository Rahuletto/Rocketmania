using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputAsset;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;
    [SerializeField] private RocketLauncher rocketLauncher;

    private PlayerMotor motor;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    public InputAction ReloadAction => playerMap != null ? playerMap.FindAction("Reload") : null;

    void Awake()
    {
        motor = GetComponent<PlayerMotor>();

        if (thirdPersonCamera == null)
            thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>();
        if (rocketLauncher == null)
            rocketLauncher = GetComponentInChildren<RocketLauncher>();

        if (inputAsset == null)
        {
            Debug.LogError("InputHandler: Assign the Input Actions asset (e.g. InputSystem_Actions).");
            return;
        }

        playerMap = inputAsset.FindActionMap("Player");
        moveAction = playerMap.FindAction("Move");
        lookAction = playerMap.FindAction("Look");
        jumpAction = playerMap.FindAction("Jump");
        attackAction = playerMap.FindAction("Attack");
        sprintAction = playerMap.FindAction("Sprint");
        crouchAction = playerMap.FindAction("Crouch");

        jumpAction.performed += OnJumpPerformed;
        attackAction.performed += OnAttackPerformed;
        crouchAction.performed += OnCrouchPerformed;
        crouchAction.canceled += OnCrouchCanceled;
    }

    void OnDestroy()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
            attackAction.performed -= OnAttackPerformed;
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }
    }

    void OnEnable()
    {
        playerMap?.Enable();
    }

    void OnDisable()
    {
        playerMap?.Disable();
    }

    void Update()
    {
        bool aim = false;
        if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
            aim = true;
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            aim = true;

        if (motor != null)
        {
            motor.SetAiming(aim);
            motor.SetSprinting(sprintAction != null && sprintAction.IsPressed());
        }

        if (thirdPersonCamera == null)
            return;

        thirdPersonCamera.SetAiming(aim);

        Vector2 look = lookAction != null ? lookAction.ReadValue<Vector2>() : default;
        if (look.sqrMagnitude < 1e-6f && Mouse.current != null)
            look = Mouse.current.delta.ReadValue();

        thirdPersonCamera.OnLook(look);
    }

    void FixedUpdate()
    {
        if (motor != null && moveAction != null)
            motor.Move(moveAction.ReadValue<Vector2>());
    }

    private void OnJumpPerformed(InputAction.CallbackContext _) => motor.Jump();

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || rocketLauncher == null)
            return;
        rocketLauncher.TryFire();
    }

    private void OnCrouchPerformed(InputAction.CallbackContext _) => motor.Crouch();
    private void OnCrouchCanceled(InputAction.CallbackContext _) => motor.Uncrouch();
}
