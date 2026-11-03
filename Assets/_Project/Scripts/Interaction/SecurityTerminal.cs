using System;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Terminal de seguridad física del piso. Al interactuar con [E] solicita abrir la UI (TerminalController),
    /// enciende sus cámaras de techo (deshabilitadas por defecto) y expone las puertas que puede operar.
    /// </summary>
    public sealed class SecurityTerminal : MonoBehaviour, IInteractable
    {
        [Header("Identificación")]
        [SerializeField] private string _terminalId = "F12_Terminal_Security";

        [Header("Cámaras de Seguridad (Target Texture = RT_Cam_F12_xx)")]
        [SerializeField] private Camera[] _feedCameras;
        [SerializeField] private string[] _feedLabels = { "CAM 01 · CUBÍCULOS" };

        [Header("Puertas Controlables")]
        [SerializeField] private DoorController[] _doors;

        /// <summary>Se emite al usar una terminal; TerminalController (en Systems_Persistent) la abre.</summary>
        public static event Action<SecurityTerminal> Opened;

        public string TerminalId => _terminalId;
        public int FeedCount => _feedCameras != null ? _feedCameras.Length : 0;
        public int DoorCount => _doors != null ? _doors.Length : 0;
        public string Prompt => "[E] Usar terminal de seguridad";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Opened = null;
        }

        private void Awake()
        {
            // Las cámaras solo renderizan mientras la terminal está abierta (presupuesto de 60 FPS)
            SetFeedsActive(false);
        }

        public void Interact()
        {
            Opened?.Invoke(this);
        }

        public void SetFeedsActive(bool active)
        {
            if (_feedCameras == null) return;

            foreach (Camera feedCamera in _feedCameras)
            {
                if (feedCamera != null) feedCamera.enabled = active;
            }
        }

        public RenderTexture GetFeedTexture(int index)
        {
            if (_feedCameras == null || index < 0 || index >= _feedCameras.Length || _feedCameras[index] == null) return null;
            return _feedCameras[index].targetTexture;
        }

        public string GetFeedLabel(int index)
        {
            if (_feedLabels != null && index >= 0 && index < _feedLabels.Length && !string.IsNullOrEmpty(_feedLabels[index]))
            {
                return _feedLabels[index];
            }
            return $"CAM {index + 1:00}";
        }

        public DoorController GetDoor(int index)
        {
            if (_doors == null || index < 0 || index >= _doors.Length) return null;
            return _doors[index];
        }

        /// <summary>
        /// Abre o cierra la puerta indicada. Las puertas bloqueadas por protocolo requieren a Elena.
        /// </summary>
        public bool TryToggleDoor(int index)
        {
            DoorController door = GetDoor(index);
            if (door == null) return false;

            if (door.IsLocked)
            {
                GameEvents.RaiseNotification(new HudNotification("NEXOOS", $"{door.DoorId}: bloqueo de protocolo activo. Solicita apertura a Elena.", false));
                return false;
            }

            door.ToggleDoor();
            return true;
        }
    }
}
