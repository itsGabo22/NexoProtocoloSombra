using System.Collections;
using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Pistola M1911 de Alex: disparo hitscan desde la cámara, cargador y reserva limitados, recarga con [R].
    /// Cada disparo es un ruido de 100 que atrae a todos los infectados cercanos: es un último recurso.
    /// Las acciones de entrada se crean en código (clic izquierdo / gatillo y R / botón oeste) y funcionan
    /// por callbacks: sin Update(). La munición se recoge con AmmoPickup vía GameEvents.OnAmmoPicked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        private const string ViewmodelName = "Viewmodel_M1911";

        [Header("Referencias")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private PlayerMovement _playerMovement;
        [Tooltip("Modelo en primera persona; si se deja vacío se busca un hijo llamado Viewmodel_M1911.")]
        [SerializeField] private Transform _viewmodel;
        [SerializeField] private AudioSource _audioSource;

        [Header("Munición")]
        [SerializeField] private int _magazineSize = 7;
        [SerializeField] private int _startingMagazine = 2;
        [SerializeField] private int _startingReserve = 0;
        [SerializeField] private int _maxReserve = 35;

        [Header("Disparo")]
        [SerializeField] private float _range = 30f;
        [SerializeField] private float _fireCooldown = 0.35f;
        [SerializeField] private float _reloadTime = 1.6f;
        [SerializeField] private LayerMask _hitMask = (1 << 0) | (1 << 9) | (1 << 11); // Default | Infected | Environment
        [SerializeField, Range(0f, 100f)] private float _shotNoise = 100f;

        [Header("Apuntar (clic derecho)")]
        [SerializeField] private Vector3 _aimLocalPosition = new Vector3(0f, -0.1f, 0.16f);
        [SerializeField] private float _aimFov = 42f;
        [SerializeField] private float _aimTransitionTime = 0.15f;
        [Tooltip("Dispersión en grados al disparar desde la cadera / apuntando.")]
        [SerializeField] private float _hipSpread = 3.5f;
        [SerializeField] private float _aimSpread = 0.3f;

        [Header("Feedback")]
        [SerializeField] private AudioClip _shotClip;
        [SerializeField] private AudioClip _emptyClip;
        [SerializeField] private AudioClip _reloadClip;
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [SerializeField] private Light _muzzleLight;

        private InputAction _fireAction;
        private InputAction _reloadAction;
        private InputAction _aimAction;
        private Camera _camera;
        private float _baseFov = 60f;
        private float _aimBlend;
        private bool _isAiming;
        private Coroutine _aimRoutine;
        private int _magazine;
        private int _reserve;
        private float _nextFireTime;
        private bool _isReloading;
        private bool _isCaptured;
        private Vector3 _viewmodelRestPos;
        private Quaternion _viewmodelRestRot;
        private Coroutine _recoilRoutine;
        private WaitForSeconds _flashWait;

        public int Magazine => _magazine;
        public int Reserve => _reserve;

        private void Awake()
        {
            _magazine = Mathf.Clamp(_startingMagazine, 0, _magazineSize);
            _reserve = Mathf.Clamp(_startingReserve, 0, _maxReserve);
            _flashWait = new WaitForSeconds(0.05f);

            _fireAction = new InputAction("Fire", InputActionType.Button);
            _fireAction.AddBinding("<Mouse>/leftButton");
            _fireAction.AddBinding("<Gamepad>/rightTrigger");
            _reloadAction = new InputAction("Reload", InputActionType.Button);
            _reloadAction.AddBinding("<Keyboard>/r");
            _reloadAction.AddBinding("<Gamepad>/buttonWest");
            _aimAction = new InputAction("Aim", InputActionType.Button);
            _aimAction.AddBinding("<Mouse>/rightButton");
            _aimAction.AddBinding("<Gamepad>/leftTrigger");

            if (_cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraTransform = cam.transform;
            }
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_cameraTransform != null) _camera = _cameraTransform.GetComponent<Camera>();
            if (_camera != null) _baseFov = _camera.fieldOfView;
            if (_muzzleLight != null) _muzzleLight.enabled = false;
        }

        private void Start()
        {
            // El viewmodel lo coloca Systems_Persistent como hijo de la cámara: se resuelve una sola vez
            if (_viewmodel == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == ViewmodelName) { _viewmodel = t; break; }
                }
            }
            if (_viewmodel != null)
            {
                _viewmodelRestPos = _viewmodel.localPosition;
                _viewmodelRestRot = _viewmodel.localRotation;
            }
        }

        private void OnEnable()
        {
            _fireAction.performed += HandleFire;
            _reloadAction.performed += HandleReload;
            _aimAction.started += HandleAimStarted;
            _aimAction.canceled += HandleAimReleased;
            _aimAction.Enable();
            _fireAction.Enable();
            _reloadAction.Enable();

            GameEvents.OnAmmoPicked += HandleAmmoPicked;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            GameEvents.OnFloorChanged += HandleFloorChanged;
        }

        private void OnDisable()
        {
            _fireAction.performed -= HandleFire;
            _reloadAction.performed -= HandleReload;
            _aimAction.started -= HandleAimStarted;
            _aimAction.canceled -= HandleAimReleased;
            _aimAction.Disable();
            SetAimImmediate(false);
            _fireAction.Disable();
            _reloadAction.Disable();

            GameEvents.OnAmmoPicked -= HandleAmmoPicked;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            _isReloading = false;
        }

        private void OnDestroy()
        {
            _fireAction?.Dispose();
            _reloadAction?.Dispose();
            _aimAction?.Dispose();
        }

        private void HandlePlayerCaught()
        {
            _isCaptured = true;
            SetAimImmediate(false);
        }

        private void HandleFloorChanged(int floor) => _isCaptured = false;

        private bool CanAct()
        {
            if (_isCaptured || _isReloading) return false;
            if (_playerMovement != null && _playerMovement.IsInputLocked) return false;
            // Con el cursor libre hay una UI modal abierta (terminal, rueda, pausa)
            return Cursor.lockState == CursorLockMode.Locked;
        }

        // ---------- Disparo ----------

        private void HandleFire(InputAction.CallbackContext context)
        {
            if (!CanAct() || Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + _fireCooldown;

            if (_magazine <= 0)
            {
                PlayClip(_emptyClip, 0.8f);
                if (_reserve <= 0) GameEvents.RaiseSubtitle("(Clic. El cargador está vacío y no te quedan cartuchos)");
                return;
            }

            _magazine--;
            PlayClip(_shotClip, 1f);
            GameEvents.RaiseNoiseEmitted(transform.position, _shotNoise);
            ShowMuzzleFlash();
            if (_recoilRoutine != null) StopCoroutine(_recoilRoutine);
            _recoilRoutine = StartCoroutine(RecoilRoutine());

            if (_cameraTransform != null &&
                Physics.Raycast(_cameraTransform.position, ShotDirection(), out RaycastHit hit, _range, _hitMask, QueryTriggerInteraction.Ignore))
            {
                SmartInfectedBrain brain = hit.collider.GetComponentInParent<SmartInfectedBrain>();
                if (brain != null) brain.ApplyHit(transform.position);
            }

            if (_magazine == 0 && _reserve > 0) StartCoroutine(ReloadRoutine());
        }

        private void ShowMuzzleFlash()
        {
            if (_viewmodel != null && _muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(_muzzleFlashPrefab, _viewmodel.position + _viewmodel.forward * 0.18f + _viewmodel.up * 0.05f, _viewmodel.rotation);
                Destroy(flash, 0.15f);
            }
            if (_muzzleLight != null) StartCoroutine(MuzzleLightRoutine());
        }

        private IEnumerator MuzzleLightRoutine()
        {
            _muzzleLight.enabled = true;
            yield return _flashWait;
            _muzzleLight.enabled = false;
        }

        private IEnumerator RecoilRoutine()
        {
            if (_viewmodel == null) yield break;

            const float duration = 0.18f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                _viewmodel.localPosition = BasePosition + new Vector3(0f, 0.01f, -0.06f) * k;
                _viewmodel.localRotation = _viewmodelRestRot * Quaternion.Euler(-14f * k, 0f, 0f);
                yield return null;
            }
            _viewmodel.localPosition = BasePosition;
            _viewmodel.localRotation = _viewmodelRestRot;
            _recoilRoutine = null;
        }

        // ---------- Apuntar ----------

        /// <summary>Posición base del viewmodel según cuánto se está apuntando (0 = cadera, 1 = mira).</summary>
        private Vector3 BasePosition => Vector3.Lerp(_viewmodelRestPos, _aimLocalPosition, _aimBlend);

        private Vector3 ShotDirection()
        {
            float spread = Mathf.Lerp(_hipSpread, _aimSpread, _aimBlend);
            Vector2 offset = Random.insideUnitCircle * spread;
            return Quaternion.AngleAxis(offset.x, _cameraTransform.up) * Quaternion.AngleAxis(offset.y, _cameraTransform.right) * _cameraTransform.forward;
        }

        private void HandleAimStarted(InputAction.CallbackContext context)
        {
            if (!CanAct()) return;
            SetAiming(true);
        }

        private void HandleAimReleased(InputAction.CallbackContext context) => SetAiming(false);

        private void SetAiming(bool aiming)
        {
            if (_isAiming == aiming) return;
            _isAiming = aiming;
            if (_aimRoutine != null) StopCoroutine(_aimRoutine);
            _aimRoutine = StartCoroutine(AimRoutine(aiming ? 1f : 0f));
        }

        private void SetAimImmediate(bool aiming)
        {
            if (_aimRoutine != null) { StopCoroutine(_aimRoutine); _aimRoutine = null; }
            _isAiming = aiming;
            ApplyAim(aiming ? 1f : 0f);
        }

        private IEnumerator AimRoutine(float target)
        {
            float speed = 1f / Mathf.Max(0.01f, _aimTransitionTime);
            while (!Mathf.Approximately(_aimBlend, target))
            {
                ApplyAim(Mathf.MoveTowards(_aimBlend, target, speed * Time.deltaTime));
                yield return null;
            }
            _aimRoutine = null;
        }

        private void ApplyAim(float blend)
        {
            _aimBlend = blend;
            if (_camera != null) _camera.fieldOfView = Mathf.Lerp(_baseFov, _aimFov, blend);
            if (_viewmodel != null && _recoilRoutine == null) _viewmodel.localPosition = BasePosition;
        }

        // ---------- Recarga y munición ----------

        private void HandleReload(InputAction.CallbackContext context)
        {
            if (!CanAct() || _magazine >= _magazineSize) return;
            if (_reserve <= 0)
            {
                GameEvents.RaiseSubtitle("(No te quedan cartuchos .45)");
                return;
            }
            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            SetAiming(false);
            PlayClip(_reloadClip, 0.9f);
            if (_viewmodel != null) _viewmodel.localRotation = _viewmodelRestRot * Quaternion.Euler(25f, -20f, 0f);

            yield return new WaitForSeconds(_reloadTime);

            int needed = _magazineSize - _magazine;
            int moved = Mathf.Min(needed, _reserve);
            _magazine += moved;
            _reserve -= moved;
            if (_viewmodel != null) _viewmodel.localRotation = _viewmodelRestRot;
            _isReloading = false;
            AnnounceAmmo();
        }

        private void HandleAmmoPicked(int rounds)
        {
            _reserve = Mathf.Min(_maxReserve, _reserve + rounds);
            AnnounceAmmo();
        }

        private void AnnounceAmmo()
        {
            GameEvents.RaiseNotification(new HudNotification("M1911", $"Cargador {_magazine}/{_magazineSize} · Reserva {_reserve}", false));
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (_audioSource != null && clip != null) _audioSource.PlayOneShot(clip, volume);
        }
    }
}
