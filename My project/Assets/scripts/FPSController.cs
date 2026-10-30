using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform cameraHolder;

    [Header("Movimiento")]
    public float walkSpeed = 4.5f;
    public float crouchSpeed = 2.2f;
    public float gravity = -19.62f;

    [Header("Cámara (Mouse Look)")]
    public float mouseSensitivity = 2.0f;
    public float maxLookAngle = 85f;

    [Header("Agacharse (Crouch)")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    public float standingHeight = 1.8f;
    public float crouchingHeight = 1.0f;
    public float standingCamY = 1.6f;
    public float crouchingCamY = 0.85f;
    public float crouchTransitionSpeed = 10f;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isCrouching = false;

    public Vector2 MoveInput { get; private set; }
    public bool IsMoving => controller.isGrounded && MoveInput.sqrMagnitude > 0.1f;
    public bool IsCrouching => isCrouching;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(crouchKey))
        {
            if (isCrouching)
            {
                if (!Physics.Raycast(transform.position, Vector3.up, standingHeight + 0.1f))
                    isCrouching = false;
            }
            else
            {
                isCrouching = true;
            }
        }

        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCamY = isCrouching ? crouchingCamY : standingCamY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            cameraHolder.localPosition = camPos;
        }
    }

    void HandleMovement()
    {
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        Vector3 move = transform.right * MoveInput.x + transform.forward * MoveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}