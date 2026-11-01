using System.Collections;
using Nexo.Player;
using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Cierra el bucle jugable: al recibir GameEvents.OnPlayerCaught congela al jugador, deja que el HUD
    /// muestre la pantalla de captura y reinicia el piso actual con SceneLoader tras una breve pausa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCaptureHandler : MonoBehaviour
    {
        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("Tiempos")]
        [SerializeField] private float _restartDelay = 3f;

        private Coroutine _captureRoutine;

        public bool IsCapturing => _captureRoutine != null;

        private void Awake()
        {
            if (_sceneLoader == null)
            {
                Debug.LogError($"[PlayerCaptureHandler] Falta asignar _sceneLoader en {name}.", this);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;

            if (_captureRoutine != null)
            {
                StopCoroutine(_captureRoutine);
                _captureRoutine = null;
                if (_playerMovement != null) _playerMovement.SetInputLocked(false, false);
            }
        }

        private void HandlePlayerCaught()
        {
            // El infectado puede repetir el ataque durante la pausa: solo cuenta la primera captura
            if (IsCapturing || _sceneLoader == null) return;
            _captureRoutine = StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            if (_playerMovement != null) _playerMovement.SetInputLocked(true, false);
            GameEvents.RaiseNotification(new HudNotification("NEXOOS", "Señal vital interrumpida. Reiniciando protocolo del Piso 12...", false));

            yield return new WaitForSeconds(_restartDelay);

            // Esperar a que termine cualquier carga en curso antes de reiniciar
            while (!_sceneLoader.ReloadFloor()) yield return null;
            while (_sceneLoader.IsLoading) yield return null;

            if (_playerMovement != null) _playerMovement.SetInputLocked(false, false);
            _captureRoutine = null;
        }
    }
}
