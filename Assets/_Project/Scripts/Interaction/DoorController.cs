using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Controlador de puertas de seguridad del complejo.
    /// Responde a la tecla de interacción [E] si está desbloqueada y a órdenes remotas de Elena (UnlockDoor).
    /// </summary>
    public sealed class DoorController : MonoBehaviour, IInteractable
    {
        [Header("Identificación de Puerta")]
        [SerializeField] private string _doorId = "F12_Door_Stairs";
        [SerializeField] private bool _isLocked = true;
        [SerializeField] private bool _isOpen = false;

        [Header("Animación / Transform de Hoja de Puerta")]
        [SerializeField] private Transform _doorHinge;
        [SerializeField] private Vector3 _openOffset = new Vector3(0f, 0f, 1.4f);
        [SerializeField] private float _transitionSpeed = 5.0f;

        private Vector3 _closedPosition;
        private Vector3 _targetPosition;

        public string DoorId => _doorId;
        public bool IsLocked => _isLocked;
        public bool IsOpen => _isOpen;

        public string Prompt
        {
            get
            {
                if (_isLocked) return "[E] Puerta bloqueada por protocolo de seguridad";
                return _isOpen ? "[E] Cerrar puerta" : "[E] Abrir puerta";
            }
        }

        private void Awake()
        {
            if (_doorHinge != null)
            {
                _closedPosition = _doorHinge.localPosition;
                _targetPosition = _isOpen ? _closedPosition + _openOffset : _closedPosition;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnUiAction += HandleUiAction;
        }

        private void OnDisable()
        {
            GameEvents.OnUiAction -= HandleUiAction;
        }

        private void HandleUiAction(ElenaUiAction action, string targetId)
        {
            if (action == ElenaUiAction.UnlockDoor && string.Equals(targetId, _doorId, System.StringComparison.OrdinalIgnoreCase))
            {
                UnlockAndOpen();
            }
        }

        public void Interact()
        {
            if (_isLocked)
            {
                GameEvents.RaiseNotification(new HudNotification("NEXOOS", $"Acceso denegado: {_doorId} requiere desbloqueo de Elena.", false));
                return;
            }

            ToggleDoor();
        }

        public void UnlockAndOpen()
        {
            _isLocked = false;
            _isOpen = true;
            if (_doorHinge != null)
            {
                _targetPosition = _closedPosition + _openOffset;
            }
            GameEvents.RaiseNotification(new HudNotification("ELENA", $"Puerta {_doorId} desbloqueada remotamente.", false));
        }

        public void ToggleDoor()
        {
            _isOpen = !_isOpen;
            if (_doorHinge != null)
            {
                _targetPosition = _isOpen ? _closedPosition + _openOffset : _closedPosition;
            }
        }

        // JUSTIFICACIÓN UPDATE: Interpolación suave de traslación física/visual de la hoja de puerta.
        private void Update()
        {
            if (_doorHinge != null && (_doorHinge.localPosition - _targetPosition).sqrMagnitude > 0.0001f)
            {
                _doorHinge.localPosition = Vector3.Lerp(_doorHinge.localPosition, _targetPosition, Time.deltaTime * _transitionSpeed);
            }
        }
    }
}
