using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 12f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.1f; // how far below the player to check for ground
    public LayerMask groundMask;           // assign this to your “Ground” layer

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // Make sure stepOffset isn’t huge. E.g. 0.3–0.5m in the Inspector.
        controller.stepOffset = 0.3f;
    }

    void Update()
    {
        // 1. Check if we’re grounded by casting a small sphere just below the controller
        isGrounded = Physics.CheckSphere(
            transform.position + Vector3.down * (controller.height / 2 - groundCheckDistance),
            groundCheckDistance,
            groundMask
        );

        // 2. If grounded and falling, reset vertical velocity
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // small negative to keep us “snug” to the ground
        }

        // 3. Read input (XZ plane) and move horizontally
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // 4. Apply gravity every frame
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // Optional: visualize ground-check sphere in Scene view
    void OnDrawGizmosSelected()
    {
        if (controller == null) return;
        Vector3 sphereCenter = transform.position + Vector3.down * (controller.height / 2 - groundCheckDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphereCenter, groundCheckDistance);
    }

    // In PlayerMovement.cs
    public void ResetVerticalVelocity()
    {
        velocity = Vector3.zero;
    }

}
