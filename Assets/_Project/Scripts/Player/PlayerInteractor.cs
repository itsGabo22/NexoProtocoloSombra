using Nexo.Interaction;
using Nexo.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Realiza un raycast continuo desde la cámara del jugador para detectar objetos que implementen IInteractable.
    /// Actualiza el prompt diegético en el HUD y desencadena Interact() al pulsar [E].
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Configuración de Interacción")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private float _interactDistance = 2.5f;
        [SerializeField] private LayerMask _interactableMask = ~0;

        [Header("Referencia al HUD")]
        [SerializeField] private HUDController _hudController;

        private IInteractable _currentInteractable;

        private void Awake()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }
            if (_hudController == null)
            {
                _hudController = FindAnyObjectByType<HUDController>();
            }
        }

        // JUSTIFICACIÓN UPDATE: Detección por raycast en el cono visual del jugador y lectura de tecla E.
        private void Update()
        {
            if (_playerCamera == null) return;

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableMask))
            {
                if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    SetCurrentInteractable(interactable);
                }
                else if (hit.collider.GetComponentInParent<IInteractable>() is { } parentInteractable)
                {
                    SetCurrentInteractable(parentInteractable);
                }
                else
                {
                    ClearCurrentInteractable();
                }
            }
            else
            {
                ClearCurrentInteractable();
            }

            if (_currentInteractable != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                _currentInteractable.Interact();
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
            if (_currentInteractable != null)
            {
                _currentInteractable = null;
                if (_hudController != null)
                {
                    _hudController.SetInteractPrompt(string.Empty);
                }
            }
        }
    }
}
