using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    public float speed = 5f;
    private bool isGrounded;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;
    [SerializeField] float aimSpeedMultiplier = 0.52f;
    [SerializeField] float knockbackDecay = 10f;
    bool aiming;
    Vector3 knockbackVelocity;
    private float standingHeight;
    private Vector3 standingCenter;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;
    }

    public void Move(Vector2 input)
    {
        isGrounded = controller.isGrounded;

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        float aimMul = aiming ? aimSpeedMultiplier : 1f;
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
        if (controller.isGrounded)
            playerVelocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
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
