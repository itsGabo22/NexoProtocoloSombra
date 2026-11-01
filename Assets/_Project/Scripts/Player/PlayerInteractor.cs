using System.Collections;
using Nexo.Interaction;
using Nexo.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Detecta objetos IInteractable frente a la cámara del jugador mediante un raycast periódico (cada 0.1s).
    /// Actualiza el prompt diegético del HUD y ejecuta Interact() al recibir la acción Interact del Input System.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Configuración de Interacción")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _interactDistance = 2.5f;
        [SerializeField] private LayerMask _interactableMask = ~0;
        [SerializeField] private float _scanInterval = 0.1f;

        [Header("Entrada (NexoControls/Player/Interact)")]
        [SerializeField] private InputActionReference _interactAction;

        [Header("Referencia al HUD")]
        [SerializeField] private HUDController _hudController;

        private IInteractable _currentInteractable;
        private Coroutine _scanRoutine;
        private WaitForSeconds _scanWait;

        private void Awake()
        {
            _scanWait = new WaitForSeconds(_scanInterval);

            if (_cameraTransform == null)
            {
                Debug.LogError($"[PlayerInteractor] Falta asignar _cameraTransform en {name}.", this);
            }
            if (_interactAction == null)
            {
                Debug.LogError($"[PlayerInteractor] Falta asignar _interactAction (NexoControls/Player/Interact) en {name}.", this);
            }
            if (_hudController == null)
            {
                Debug.LogWarning($"[PlayerInteractor] Sin HUDController asignado en {name}: no se mostrará el prompt [E].", this);
            }
        }

        private void OnEnable()
        {
            if (_interactAction != null)
            {
                _interactAction.action.performed += HandleInteractPerformed;
                _interactAction.action.Enable();
            }

            if (_cameraTransform != null)
            {
                _scanRoutine = StartCoroutine(ScanRoutine());
            }
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.action.performed -= HandleInteractPerformed;
            }

            if (_scanRoutine != null)
            {
                StopCoroutine(_scanRoutine);
                _scanRoutine = null;
            }

            ClearCurrentInteractable();
        }

        /// <summary>
        /// Barrido periódico del raycast de interacción (sustituye al sondeo por frame).
        /// </summary>
        private IEnumerator ScanRoutine()
        {
            while (true)
            {
                Scan();
                yield return _scanWait;
            }
        }

        private void Scan()
        {
            Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableMask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    SetCurrentInteractable(interactable);
                    return;
                }

                IInteractable parentInteractable = hit.collider.GetComponentInParent<IInteractable>();
                if (parentInteractable != null)
                {
                    SetCurrentInteractable(parentInteractable);
                    return;
                }
            }

            ClearCurrentInteractable();
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context)
        {
            if (_currentInteractable == null) return;

            _currentInteractable.Interact();

            // Refrescar el prompt de inmediato (p. ej. "Abrir" -> "Cerrar")
            if (_hudController != null)
            {
                _hudController.SetInteractPrompt(_currentInteractable.Prompt);
            }
        }

        private void SetCurrentInteractable(IInteractable interactable)
        {
            _currentInteractable = interactable;
            if (_hudController != null)
            {
                _hudController.SetInteractPrompt(interactable.Prompt);
            }
        }

        private void ClearCurrentInteractable()
        {
            if (_currentInteractable == null) return;

            _currentInteractable = null;
            if (_hudController != null)
            {
                _hudController.SetInteractPrompt(string.Empty);
            }
        }
    }
}
