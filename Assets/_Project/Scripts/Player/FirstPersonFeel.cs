using Nexo.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Nexo.Player
{
    /// <summary>
    /// Sensación en primera persona de Alex sin modificar PlayerWeapon ni PlayerMovement:
    /// - Oculta el cuerpo Mixamo a la cámara (solo proyecta sombra): ya no se ve el torso detrás del arma.
    /// - Coloca el arma (Viewmodel_M1911) dentro de un pivote hijo de la cámara, de modo que el retroceso de cámara
    ///   mueve arma y mira a la vez y el disparo (rayo desde la cámara) siempre coincide con el centro de la pantalla.
    /// - Retroceso de cámara por disparo (detectado porque baja el cargador) con recuperación progresiva:
    ///   disparar rápido acumula desvío; apuntar lo reduce. Así se premia disparar con calma.
    /// - Balanceo al caminar (la mira oscila: menos precisión en movimiento), inercia del arma al girar,
    ///   sacudida al recibir un golpe y temblor de forcejeo (StruggleQTE).
    /// Animación visual por corrutina (sin Update): solo escribe la rotación local de la cámara y el pivote del arma.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class FirstPersonFeel : MonoBehaviour
    {
        private const string ViewmodelName = "Viewmodel_M1911";
        private const string PivotName = "WeaponSwayPivot";

        [Header("Referencias")]
        [SerializeField] private Transform _camera;
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private StruggleQTE _struggle;
        [Tooltip("Raíz del modelo Mixamo del jugador (sus SkinnedMeshRenderer pasan a ShadowsOnly).")]
        [SerializeField] private Transform _bodyRoot;

        [Header("Posición del arma (sobre la del viewmodel)")]
        [Tooltip("Desplazamiento del pivote del arma respecto a la cámara: acerca el arma al cuerpo.")]
        [SerializeField] private Vector3 _weaponOffset = new Vector3(-0.015f, -0.01f, 0.02f);

        [Header("Retroceso de cámara")]
        [SerializeField] private float _hipKick = 2.6f;
        [SerializeField] private float _aimKick = 1.5f;
        [SerializeField] private float _kickYaw = 0.7f;
        [Tooltip("Segundos para recuperar la mira tras un disparo.")]
        [SerializeField] private float _kickRecovery = 0.32f;
        [SerializeField] private float _maxKick = 9f;

        [Header("Balanceo al moverse")]
        [SerializeField] private float _bobFrequency = 1.9f;
        [SerializeField] private float _bobPitch = 0.55f;
        [SerializeField] private float _bobYaw = 0.35f;
        [SerializeField] private float _weaponBob = 0.012f;

        [Header("Inercia del arma")]
        [SerializeField] private float _swayAmount = 0.0018f;
        [SerializeField] private float _swayMax = 0.035f;
        [SerializeField] private float _swaySmooth = 10f;

        [Header("Golpes y forcejeo")]
        [SerializeField] private float _hitJolt = 6f;
        [SerializeField] private float _struggleShake = 2.2f;

        private Transform _pivot;
        private Vector3 _pivotRest;
        private Vector2 _kick;
        private Vector2 _kickVelocity;
        private float _bobPhase;
        private float _bobWeight;
        private Vector3 _sway;
        private float _jolt;
        private Vector2 _joltDirection;
        private int _lastMagazine = -1;
        private Coroutine _routine;

        private void Awake()
        {
            if (_camera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) _camera = cam.transform;
            }
            if (_weapon == null) _weapon = GetComponent<PlayerWeapon>();
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_struggle == null) _struggle = GetComponent<StruggleQTE>();

            HideBodyFromCamera();
            // Debe ocurrir antes de PlayerWeapon.Start, que guarda la posición de reposo del viewmodel
            BuildWeaponPivot();
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerAttacked += HandlePlayerAttacked;
            _routine = StartCoroutine(FeelRoutine());
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerAttacked -= HandlePlayerAttacked;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_camera != null) _camera.localRotation = Quaternion.identity;
            if (_pivot != null) _pivot.localPosition = _pivotRest;
        }

        private void HideBodyFromCamera()
        {
            Transform root = _bodyRoot;
            if (root == null)
            {
                Animator animator = GetComponentInChildren<Animator>();
                if (animator != null) root = animator.transform;
            }
            if (root == null) return;

            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        private void BuildWeaponPivot()
        {
            if (_camera == null) return;

            Transform viewmodel = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ViewmodelName) { viewmodel = t; break; }
            }
            if (viewmodel == null) return;

            _pivot = new GameObject(PivotName).transform;
            _pivot.SetParent(_camera, false);
            _pivotRest = _weaponOffset;
            _pivot.localPosition = _pivotRest;

            // CameraHolder y la cámara comparten el mismo espacio local: se conservan posición y rotación
            Vector3 localPosition = viewmodel.localPosition;
            Quaternion localRotation = viewmodel.localRotation;
            viewmodel.SetParent(_pivot, false);
            viewmodel.localPosition = localPosition;
            viewmodel.localRotation = localRotation;
        }

        private void HandlePlayerAttacked(Transform attacker)
        {
            _jolt = 1f;
            Vector3 local = attacker != null ? transform.InverseTransformDirection(attacker.position - transform.position) : Vector3.forward;
            _joltDirection = new Vector2(Mathf.Sign(local.x) * 0.6f, 1f);
        }

        private IEnumerator FeelRoutine()
        {
            while (true)
            {
                float dt = Time.deltaTime;
                DetectShot();
                Animate(dt);
                yield return null;
            }
        }

        private void DetectShot()
        {
            if (_weapon == null) return;

            int magazine = _weapon.Magazine;
            // El cargador solo baja al disparar (recargar lo sube)
            if (_lastMagazine >= 0 && magazine < _lastMagazine)
            {
                bool aiming = IsAiming();
                float pitch = aiming ? _aimKick : _hipKick;
                _kick.x = Mathf.Min(_maxKick, _kick.x + pitch * Random.Range(0.85f, 1.15f));
                _kick.y += Random.Range(-_kickYaw, _kickYaw) * (aiming ? 0.5f : 1f);
            }
            _lastMagazine = magazine;
        }

        private void Animate(float dt)
        {
            if (_camera == null) return;

            // Recuperación progresiva de la mira
            _kick = Vector2.SmoothDamp(_kick, Vector2.zero, ref _kickVelocity, _kickRecovery);

            // Balanceo: solo al moverse; agachado o apuntando es mucho más estable
            bool moving = _movement != null && _movement.IsMoving && !_movement.IsInputLocked;
            float intensity = !moving ? 0f : _movement.IsSprinting ? 1.6f : _movement.IsCrouching ? 0.35f : 1f;
            if (IsAiming()) intensity *= 0.3f;
            _bobWeight = Mathf.MoveTowards(_bobWeight, intensity, dt * 4f);
            float speedFactor = _movement != null && _movement.IsSprinting ? 1.45f : 1f;
            _bobPhase += dt * _bobFrequency * speedFactor * Mathf.PI * 2f * (moving ? 1f : 0.5f);

            float bobPitch = Mathf.Sin(_bobPhase * 2f) * _bobPitch * _bobWeight;
            float bobYaw = Mathf.Sin(_bobPhase) * _bobYaw * _bobWeight;

            // Sacudidas: golpe recibido y forcejeo
            _jolt = Mathf.MoveTowards(_jolt, 0f, dt * 3f);
            float joltPitch = _jolt * _jolt * _hitJolt * _joltDirection.y;
            float joltRoll = _jolt * _jolt * _hitJolt * _joltDirection.x;
            float shake = _struggle != null && _struggle.IsActive ? _struggleShake : 0f;
            float t = Time.time * 13f;
            float shakePitch = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake;
            float shakeYaw = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake;

            _camera.localRotation = Quaternion.Euler(-_kick.x + bobPitch - joltPitch + shakePitch, _kick.y + bobYaw + shakeYaw, joltRoll);

            if (_pivot == null) return;

            // Inercia del arma: se retrasa respecto al giro del ratón
            Vector2 look = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked ? Mouse.current.delta.ReadValue() : Vector2.zero;
            float swayScale = IsAiming() ? 0.25f : 1f;
            Vector3 targetSway = new Vector3(
                Mathf.Clamp(-look.x * _swayAmount, -_swayMax, _swayMax),
                Mathf.Clamp(-look.y * _swayAmount, -_swayMax, _swayMax), 0f) * swayScale;
            _sway = Vector3.Lerp(_sway, targetSway, 1f - Mathf.Exp(-_swaySmooth * dt));

            Vector3 weaponBob = new Vector3(Mathf.Sin(_bobPhase) * _weaponBob, -Mathf.Abs(Mathf.Cos(_bobPhase)) * _weaponBob, 0f) * _bobWeight;
            _pivot.localPosition = _pivotRest + _sway + weaponBob + new Vector3(0f, 0f, -_kick.x * 0.004f);
        }

        private bool IsAiming()
        {
            // PlayerWeapon no expone el estado de apuntado: se deduce del clic derecho/gatillo izquierdo y del cursor
            if (Cursor.lockState != CursorLockMode.Locked) return false;
            bool mouse = Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool pad = Gamepad.current != null && Gamepad.current.leftTrigger.isPressed;
            return mouse || pad;
        }
    }
}
