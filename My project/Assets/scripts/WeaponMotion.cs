using UnityEngine;

public class WeaponMotion : MonoBehaviour
{
    [Header("Referencias")]
    public FPSController playerController;

    [Header("Inercia de Ratón (Weapon Sway)")]
    public float swayAmount = 0.03f;
    public float maxSwayAmount = 0.06f;
    public float swaySmooth = 8f;

    [Header("Rotación de Inercia (Tilt)")]
    public float tiltAmount = 4f;
    public float maxTiltAmount = 8f;
    public float strafeTilt = 3f;

    [Header("Balanceo al Caminar (Weapon Bob)")]
    public float walkBobSpeed = 10f;
    public float walkBobAmount = 0.025f;
    public float crouchBobSpeed = 6f;
    public float crouchBobAmount = 0.012f;

    [Header("Postura al Agacharse")]
    public Vector3 crouchWeaponOffset = new Vector3(-0.03f, -0.02f, 0f);
    public Vector3 crouchWeaponRotation = new Vector3(0f, 0f, 12f);

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float bobTimer = 0f;

    void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        float moveX = Mathf.Clamp(-mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float moveY = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);
        Vector3 swayPos = new Vector3(moveX, moveY, 0);

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
