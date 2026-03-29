using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    private PlayerMotor motor;
    private InputSystem_Actions inputActions;
    private InputSystem_Actions.PlayerActions player;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        player = inputActions.Player;
        motor = GetComponent<PlayerMotor>();

        if (thirdPersonCamera == null)
            thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>();

        player.Jump.performed += OnJumpPerformed;
        player.Crouch.performed += OnCrouchPerformed;
        player.Crouch.canceled += OnCrouchCanceled;
    }

    void OnDestroy()
    {
        player.Jump.performed -= OnJumpPerformed;
        player.Crouch.performed -= OnCrouchPerformed;
        player.Crouch.canceled -= OnCrouchCanceled;
        inputActions.Dispose();
    }

    void OnEnable()
    {
        player.Enable();
    }

    void OnDisable()
    {
        player.Disable();
    }

    void Update()
    {
        if (thirdPersonCamera == null) return;

        bool aim = false;
        if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
            aim = true;
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            aim = true;
        thirdPersonCamera.SetAiming(aim);

        Vector2 look = player.Look.ReadValue<Vector2>();
        if (look.sqrMagnitude < 1e-6f && Mouse.current != null)
            look = Mouse.current.delta.ReadValue();

        thirdPersonCamera.OnLook(look);
    }

    void FixedUpdate()
    {
        if (motor != null)
            motor.Move(player.Move.ReadValue<Vector2>());
    }

    private void OnJumpPerformed(InputAction.CallbackContext _) => motor.Jump();
    private void OnCrouchPerformed(InputAction.CallbackContext _) => motor.Crouch();
    private void OnCrouchCanceled(InputAction.CallbackContext _) => motor.Uncrouch();
}
