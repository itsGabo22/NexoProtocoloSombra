using System;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Controlador oficial en primera persona de Alex.
    /// Desacopla la rotación y posición de la cámara del rig de Mixamo para prevenir cinetosis y oscilaciones,
    /// escala el hueso de la cabeza para evitar oclusión visual y sincroniza parámetros con el Animator.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("Referencias de Cámara y Rig")]
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Transform _headBoneToHide;
        [SerializeField] private Animator _characterAnimator;

        [Header("Parámetros de Movimiento")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 7.0f;
        [SerializeField] private float _crouchSpeed = 2.2f;
        [SerializeField] private float _gravity = -19.62f;

        [Header("Sensibilidad y Límites de Cámara")]
        [SerializeField] private float _mouseSensitivity = 2.0f;
        [SerializeField] private float _maxLookAngle = 85.0f;

        [Header("Transiciones de Postura")]
        [SerializeField] private float _standingHeight = 1.8f;
        [SerializeField] private float _crouchingHeight = 1.0f;
        [SerializeField] private float _standingCamY = 1.6f;
        [SerializeField] private float _crouchingCamY = 0.85f;
        [SerializeField] private float _crouchTransitionSpeed = 10.0f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _pitch = 0f;
        private bool _isCrouching = false;
        private bool _isFrozen = false;
        private bool _inputLocked = false;

        public Vector2 MoveInput { get; private set; }
        public bool IsMoving => _controller != null && _controller.isGrounded && MoveInput.sqrMagnitude > 0.01f;
        public bool IsCrouching => _isCrouching;
        public bool IsSprinting { get; private set; }

        /// <summary>
        /// Se emite cuando el jugador empieza (true) o deja (false) de esprintar en movimiento.
        /// </summary>
        public event Action<bool> SprintStateChanged;

        private bool _wasSprintingMoving;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // Si no se asignó la cabeza manualmente, buscarla en la jerarquía del modelo Mixamo
            if (_headBoneToHide == null)
            {
                Transform[] transforms = GetComponentsInChildren<Transform>();
                foreach (Transform t in transforms)
                {
                    if (t.name.Contains("Head") || t.name.Contains("head"))
                    {
                        _headBoneToHide = t;
                        break;
                    }
                }
            }

            // Desacoplar la cabeza en primera persona para evitar clipping sin romper el rig de los brazos
            if (_headBoneToHide != null)
            {
                _headBoneToHide.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Bloquea la entrada de movimiento/cámara y libera el cursor mientras hay UI modal abierta
        /// (terminal de seguridad, rueda de órdenes). La gravedad sigue aplicándose.
        /// </summary>
        public void SetInputLocked(bool locked, bool showCursor)
        {
            _inputLocked = locked;
            Cursor.lockState = locked && showCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked && showCursor;
        }

        private void OnEnable()
        {
            GameEvents.OnPlotTwistTriggered += HandlePlotTwistTriggered;
        }

        private void OnDisable()
        {
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwistTriggered;
        }

        private void HandlePlotTwistTriggered()
        {
            _isFrozen = true;
            MoveInput = Vector2.zero;
            IsSprinting = false;
            NotifySprintState();
            if (_characterAnimator != null)
            {
                _characterAnimator.SetFloat(SpeedHash, 0f);
                _characterAnimator.SetBool(IsMovingHash, false);
            }
        }

        // JUSTIFICACIÓN UPDATE: Muestreo de entrada continua del jugador y traslación por CharacterController.
        private void Update()
        {
            if (_isFrozen) return;

            HandleLook();
            HandleCrouch();
            HandleMovement();
            UpdateAnimator();
            NotifySprintState();
        }

        private void NotifySprintState()
        {
            bool sprintingMoving = IsSprinting && IsMoving;
            if (sprintingMoving == _wasSprintingMoving) return;

            _wasSprintingMoving = sprintingMoving;
            SprintStateChanged?.Invoke(sprintingMoving);
        }

        private void HandleLook()
        {
            if (_inputLocked || Mouse.current == null) return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * (_mouseSensitivity * 0.08f);

            _pitch -= mouseDelta.y;
            _pitch = Mathf.Clamp(_pitch, -_maxLookAngle, _maxLookAngle);

            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            transform.Rotate(Vector3.up * mouseDelta.x);
        }

        private void HandleCrouch()
        {
            if (!_inputLocked && Keyboard.current != null && (Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame))
            {
                if (_isCrouching)
                {
                    if (!Physics.Raycast(transform.position, Vector3.up, _standingHeight + 0.1f))
                    {
                        _isCrouching = false;
                    }
                }
                else
                {
                    _isCrouching = true;
                }
            }

            float targetHeight = _isCrouching ? _crouchingHeight : _standingHeight;
            float targetCamY = _isCrouching ? _crouchingCamY : _standingCamY;

            if (_controller != null)
            {
                _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * _crouchTransitionSpeed);
                _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
            }

            if (_cameraPivot != null)
            {
                Vector3 camPos = _cameraPivot.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * _crouchTransitionSpeed);
                _cameraPivot.localPosition = camPos;
            }
        }

        private void HandleMovement()
        {
            if (_controller == null) return;

            if (_controller.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            Vector2 rawInput = Vector2.zero;
            bool sprintPressed = false;

            if (!_inputLocked && Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) rawInput.y += 1f;
                if (Keyboard.current.sKey.isPressed) rawInput.y -= 1f;
                if (Keyboard.current.aKey.isPressed) rawInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed) rawInput.x += 1f;
                sprintPressed = Keyboard.current.leftShiftKey.isPressed;
            }

            MoveInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;
            IsSprinting = sprintPressed && MoveInput.y > 0.1f && !_isCrouching;

            float currentSpeed = _isCrouching ? _crouchSpeed : (IsSprinting ? _sprintSpeed : _walkSpeed);
            Vector3 moveDirection = transform.right * MoveInput.x + transform.forward * MoveInput.y;

            _controller.Move(moveDirection * currentSpeed * Time.deltaTime);

            _velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_characterAnimator == null) return;

            float speedMagnitude = MoveInput.magnitude;
            if (IsSprinting) speedMagnitude *= 1.5f;

            _characterAnimator.SetFloat(SpeedHash, speedMagnitude);
            _characterAnimator.SetBool(IsMovingHash, IsMoving);
            _characterAnimator.SetBool(IsCrouchingHash, _isCrouching);
        }
    }
}
