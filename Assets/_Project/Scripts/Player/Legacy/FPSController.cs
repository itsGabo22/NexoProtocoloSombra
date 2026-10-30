using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador legacy adaptado de Carlos para preservar compatibilidad en Floor12_Offices.unity.
/// Utiliza el New Input System y desacopla la cámara de la animación de la cabeza.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public Transform cameraHolder;
    [SerializeField] private Transform headBoneToHide;

    [Header("Movimiento")]
    [SerializeField] public float walkSpeed = 4.5f;
    [SerializeField] public float crouchSpeed = 2.2f;
    [SerializeField] public float gravity = -19.62f;

    [Header("Cámara (Mouse Look)")]
    [SerializeField] public float mouseSensitivity = 2.0f;
    [SerializeField] public float maxLookAngle = 85f;

    [Header("Agacharse (Crouch)")]
    [SerializeField] public float standingHeight = 1.8f;
    [SerializeField] public float crouchingHeight = 1.0f;
    [SerializeField] public float standingCamY = 1.6f;
    [SerializeField] public float crouchingCamY = 0.85f;
    [SerializeField] public float crouchTransitionSpeed = 10f;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isCrouching = false;

    public Vector2 MoveInput { get; private set; }
    public bool IsMoving => controller != null && controller.isGrounded && MoveInput.sqrMagnitude > 0.01f;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Si no se asignó manualmente el hueso de la cabeza, buscarlo en la jerarquía de Mixamo
        if (headBoneToHide == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform t in allChildren)
            {
                if (t.name.Contains("Head") || t.name.Contains("head"))
                {
                    headBoneToHide = t;
                    break;
                }
            }
        }

        // Desacoplar visualmente la cabeza en primera persona para evitar clipping y sacudidas
        if (headBoneToHide != null)
        {
            headBoneToHide.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // JUSTIFICACIÓN UPDATE: Muestreo de entrada interactiva y traslación por CharacterController.
    private void Update()
    {
        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * (mouseSensitivity * 0.08f);

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseDelta.x);
    }

    private void HandleCrouch()
    {
        if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame))
        {
            if (isCrouching)
            {
                if (!Physics.Raycast(transform.position, Vector3.up, standingHeight + 0.1f))
                {
                    isCrouching = false;
                }
            }
            else
            {
                isCrouching = true;
            }
        }

        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCamY = isCrouching ? crouchingCamY : standingCamY;

        if (controller != null)
        {
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            cameraHolder.localPosition = camPos;
        }
    }

    private void HandleMovement()
    {
        if (controller == null) return;

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 rawInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) rawInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) rawInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) rawInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) rawInput.x += 1f;
        }

        MoveInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;
        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        Vector3 move = transform.right * MoveInput.x + transform.forward * MoveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
