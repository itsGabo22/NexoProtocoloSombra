using System.Collections;
using Nexo.Core;
using Nexo.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Atajos para presentaciones (desactivados por defecto: EnableDemoShortcuts = false).
    /// [F5] siguiente piso · [F6] vida completa · [F7] munición llena · [F8] ir directo al BioScanner del Piso 1.
    /// No tocan la narrativa ni el HUD: solo usan las APIs públicas de FloorProgression, PlayerHealth y PlayerWeapon.
    /// Por callbacks de entrada: sin Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoShortcuts : MonoBehaviour
    {
        [Tooltip("Actívalo solo para demostraciones. En la versión normal debe quedar en false.")]
        public bool EnableDemoShortcuts = false;

        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerWeapon _weapon;
        [Tooltip("Distancia delante del BioScanner donde aparece el jugador con [F8].")]
        [SerializeField] private float _scannerStandOff = 1.6f;

        private InputAction _nextFloor;
        private InputAction _heal;
        private InputAction _ammo;
        private InputAction _scanner;
        private Coroutine _jumpRoutine;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_weapon == null) _weapon = GetComponent<PlayerWeapon>();
            _nextFloor = new InputAction("DemoNextFloor", InputActionType.Button, "<Keyboard>/f5");
            _heal = new InputAction("DemoHeal", InputActionType.Button, "<Keyboard>/f6");
            _ammo = new InputAction("DemoAmmo", InputActionType.Button, "<Keyboard>/f7");
            _scanner = new InputAction("DemoScanner", InputActionType.Button, "<Keyboard>/f8");
        }

        private void OnEnable()
        {
            if (!EnableDemoShortcuts) return;

            _nextFloor.performed += HandleNextFloor;
            _heal.performed += HandleHeal;
            _ammo.performed += HandleAmmo;
            _scanner.performed += HandleScanner;
            _nextFloor.Enable();
            _heal.Enable();
            _ammo.Enable();
            _scanner.Enable();
        }

        private void OnDisable()
        {
            _nextFloor.performed -= HandleNextFloor;
            _heal.performed -= HandleHeal;
            _ammo.performed -= HandleAmmo;
            _scanner.performed -= HandleScanner;
            _nextFloor.Disable();
            _heal.Disable();
            _ammo.Disable();
            _scanner.Disable();
            if (_jumpRoutine != null) { StopCoroutine(_jumpRoutine); _jumpRoutine = null; }
        }

        private void OnDestroy()
        {
            _nextFloor?.Dispose();
            _heal?.Dispose();
            _ammo?.Dispose();
            _scanner?.Dispose();
        }

        private void HandleNextFloor(InputAction.CallbackContext context)
        {
            FloorProgression progression = FloorProgression.Active;
            if (progression != null && !progression.IsBusy && !progression.IsLastFloor) progression.LoadNext();
        }

        private void HandleHeal(InputAction.CallbackContext context)
        {
            if (_health != null) _health.ResetFull();
        }

        private void HandleAmmo(InputAction.CallbackContext context)
        {
            // SetAmmo recorta a la capacidad del cargador y a la reserva máxima
            if (_weapon != null) _weapon.SetAmmo(int.MaxValue, int.MaxValue);
        }

        private void HandleScanner(InputAction.CallbackContext context)
        {
            if (_jumpRoutine == null) _jumpRoutine = StartCoroutine(JumpToScannerRoutine());
        }

        private IEnumerator JumpToScannerRoutine()
        {
            FloorProgression progression = FloorProgression.Active;
            while (progression != null && !progression.IsLastFloor)
            {
                while (progression.IsBusy) yield return null;
                if (!progression.LoadNext()) break;
                yield return null;
                while (progression.IsBusy) yield return null;
            }

            BioScanner scanner = FindScannerInActiveFloor();
            if (scanner != null)
            {
                Vector3 front = scanner.transform.forward;
                front.y = 0f;
                if (front.sqrMagnitude < 0.01f) front = Vector3.back;
                Vector3 target = scanner.transform.position + front.normalized * _scannerStandOff;
                target.y = FloorMetadata.Active != null && FloorMetadata.Active.PlayerSpawn != null ? FloorMetadata.Active.PlayerSpawn.position.y : target.y - 1.1f;

                var controller = GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                transform.SetPositionAndRotation(target, Quaternion.LookRotation(-front.normalized));
                if (controller != null) controller.enabled = true;
            }
            _jumpRoutine = null;
        }

        private static BioScanner FindScannerInActiveFloor()
        {
            if (FloorMetadata.Active == null) return null;
            foreach (GameObject root in FloorMetadata.Active.gameObject.scene.GetRootGameObjects())
            {
                BioScanner scanner = root.GetComponentInChildren<BioScanner>(true);
                if (scanner != null) return scanner;
            }
            return null;
        }
    }
}
