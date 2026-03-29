using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    public float speed = 5f;
    private bool isGrounded;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;
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
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * speed * Time.deltaTime);
        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        if(isGrounded && playerVelocity.y < 0f)
        {
            playerVelocity.y = -2f;
        }
    }

    public void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }
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
}
