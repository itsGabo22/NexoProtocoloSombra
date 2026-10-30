using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador legacy de inercia de arma adaptado de Carlos.
/// Migrado al New Input System para compatibilidad total con Unity 6 LTS.
/// </summary>
public class WeaponMotion : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] public FPSController playerController;

    [Header("Inercia de Ratón (Weapon Sway)")]
    [SerializeField] public float swayAmount = 0.03f;
    [SerializeField] public float maxSwayAmount = 0.06f;
    [SerializeField] public float swaySmooth = 8f;

    [Header("Rotación de Inercia (Tilt)")]
    [SerializeField] public float tiltAmount = 4f;
    [SerializeField] public float maxTiltAmount = 8f;
    [SerializeField] public float strafeTilt = 3f;

    [Header("Balanceo al Caminar (Weapon Bob)")]
    [SerializeField] public float walkBobSpeed = 10f;
    [SerializeField] public float walkBobAmount = 0.025f;
    [SerializeField] public float crouchBobSpeed = 6f;
    [SerializeField] public float crouchBobAmount = 0.012f;

    [Header("Postura al Agacharse")]
    [SerializeField] public Vector3 crouchWeaponOffset = new Vector3(-0.03f, -0.02f, 0f);
    [SerializeField] public Vector3 crouchWeaponRotation = new Vector3(0f, 0f, 12f);

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float bobTimer = 0f;

    private void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    // JUSTIFICACIÓN UPDATE: Suavizado continuo de transform de arma (sway y bobbing visual).
    private void Update()
    {
        Vector2 mouseDelta = Vector2.zero;
        if (Mouse.current != null)
        {
            mouseDelta = Mouse.current.delta.ReadValue() * 0.05f;
        }

        float mouseX = mouseDelta.x;
        float mouseY = mouseDelta.y;

        float moveX = Mathf.Clamp(-mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float moveY = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);
        Vector3 swayPos = new Vector3(moveX, moveY, 0f);

        Vector3 bobPos = Vector3.zero;
        if (playerController != null && playerController.IsMoving)
        {
            float speed = playerController.IsCrouching ? crouchBobSpeed : walkBobSpeed;
            float amount = playerController.IsCrouching ? crouchBobAmount : walkBobAmount;

            bobTimer += Time.deltaTime * speed;
            bobPos.x = Mathf.Cos(bobTimer * 0.5f) * amount;
            bobPos.y = Mathf.Sin(bobTimer) * amount;
        }
        else
        {
            bobTimer += Time.deltaTime * 1.5f;
            bobPos.y = Mathf.Sin(bobTimer) * 0.004f;
        }

        Vector3 crouchOffset = (playerController != null && playerController.IsCrouching) ? crouchWeaponOffset : Vector3.zero;

        Vector3 finalPosition = initialPosition + swayPos + bobPos + crouchOffset;
        transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition, Time.deltaTime * swaySmooth);

        float tiltX = Mathf.Clamp(mouseY * tiltAmount, -maxTiltAmount, maxTiltAmount);
        float tiltY = Mathf.Clamp(-mouseX * tiltAmount, -maxTiltAmount, maxTiltAmount);
        float strafeZ = playerController != null ? -playerController.MoveInput.x * strafeTilt : 0f;

        Vector3 crouchRot = (playerController != null && playerController.IsCrouching) ? crouchWeaponRotation : Vector3.zero;

        Quaternion targetRotation = initialRotation * Quaternion.Euler(
            tiltX + crouchRot.x,
            tiltY + crouchRot.y,
            strafeZ + crouchRot.z
        );

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * swaySmooth);
    }
}
