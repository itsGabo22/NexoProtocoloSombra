using System.Collections;
using Nexo.Core;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Interaction
{
    /// <summary>
    /// Escondite (taquilla): [E] para entrar y [E] para salir. Dentro el jugador no se mueve ni dispara, la cámara
    /// queda en el punto interior (puede mirar alrededor con límites) y se oye su respiración. Los infectados que no le vieron entrar no pueden verle
    /// (PlayerHiding); si le vieron, lo sacan a la fuerza. Abrir/cerrar la puerta hace algo de ruido.
    /// Sin Update: entrada por callback y una corrutina corta de transición.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _insidePoint;
        [SerializeField] private Transform _exitPoint;
        [Tooltip("Respiración del jugador dentro del escondite (2D, en bucle).")]
        [SerializeField] private AudioSource _breathing;
        [SerializeField] private AudioSource _doorSource;
        [SerializeField] private AudioClip _doorClip;
        [SerializeField, Range(0f, 100f)] private float _doorNoise = 25f;
        [SerializeField] private float _minHideTime = 0.6f;
        [Tooltip("Giro máximo de la mirada dentro del escondite respecto a la dirección de entrada (grados).")]
        [SerializeField] private float _lookYawLimit = LookLimits.HidingYaw;
        [SerializeField] private float _lookPitchLimit = LookLimits.HidingPitch;

        private InputAction _exitAction;
        private PlayerMovement _player;
        private CharacterController _controller;
        private bool _occupied;
        private float _enterTime;

        public string Prompt => _occupied ? "[E] Salir del escondite" : "[E] Esconderse";

        private void Awake()
        {
            _exitAction = new InputAction("HidingExit", InputActionType.Button, "<Keyboard>/e");
            _exitAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            _exitAction.performed += HandleExitPerformed;
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            GameEvents.OnPlayerAttacked += HandlePlayerAttacked;
            GameEvents.OnCinematicStarted += HandleCinematicStarted;   // cinematics-ready
            GameEvents.OnCinematicFinished += HandleCinematicFinished; // cinematics-ready
        }

        private void OnDisable()
        {
            _exitAction.performed -= HandleExitPerformed;
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            GameEvents.OnPlayerAttacked -= HandlePlayerAttacked;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;   // cinematics-ready
            GameEvents.OnCinematicFinished -= HandleCinematicFinished; // cinematics-ready
            // Descarga del piso con el jugador dentro: se libera el estado y el control
            if (_occupied) Leave(true);
        }

        private void OnDestroy() => _exitAction?.Dispose();

        public void Interact()
        {
            if (_occupied || PlayerHiding.IsHidden || _player == null) return;

            _occupied = true;
            _enterTime = Time.time;
            PlayerHiding.SetHidden(true);
            _player.SetInputLocked(true, false);
            Teleport(_insidePoint != null ? _insidePoint : transform);
            // Dentro se puede mirar alrededor (±70° / ±35°) sin moverse; [E] para salir
            _player.BeginLimitedLook(_lookYawLimit, _lookPitchLimit);
            PlayDoor();
            if (_breathing != null) _breathing.Play();
            GameEvents.RaiseSubtitle("(Contienes la respiración dentro de la taquilla)");
            StartCoroutine(EnableExitNextFrame());
        }

        /// <summary>El jugador vive en Systems_Persistent: FloorMetadata lo anuncia al cargar el piso.</summary>
        private void HandlePlayerSpawned(Transform player)
        {
            _player = player != null ? player.GetComponent<PlayerMovement>() : null;
            _controller = player != null ? player.GetComponent<CharacterController>() : null;
        }

        private IEnumerator EnableExitNextFrame()
        {
            // Evita que la misma pulsación de [E] que hizo entrar haga salir
            yield return null;
            if (_occupied) _exitAction.Enable();
        }

        private void HandleExitPerformed(InputAction.CallbackContext context)
        {
            if (_occupied && Time.time - _enterTime >= _minHideTime) Leave(true);
        }

        private void HandlePlayerAttacked(Transform attacker)
        {
            // Un infectado que le vio entrar lo saca del escondite
            if (!_occupied) return;
            Leave(false);
            if (isActiveAndEnabled) StartCoroutine(RestoreInputUnlessStruggling());
        }

        private IEnumerator RestoreInputUnlessStruggling()
        {
            // PlayerHealth decide en este mismo evento si empieza un forcejeo (que gestiona su propio bloqueo)
            yield return null;
            if (_player == null) yield break;
            StruggleQTE struggle = _player.GetComponent<StruggleQTE>();
            if (struggle == null || !struggle.IsActive) _player.SetInputLocked(false, false);
        }

        private void HandlePlayerCaught()
        {
            if (_occupied) Leave(false);
        }

        // cinematics-ready: durante el clip [E] no saca al jugador del escondite (devolvería el control bajo el vídeo)
        private void HandleCinematicStarted(string id) => _exitAction.Disable();

        private void HandleCinematicFinished(string id)
        {
            if (_occupied) _exitAction.Enable();
        }

        private void Leave(bool restoreInput)
        {
            _occupied = false;
            _exitAction.Disable();
            PlayerHiding.SetHidden(false);
            if (_player != null) _player.EndLimitedLook();
            if (_breathing != null) _breathing.Stop();
            Teleport(_exitPoint != null ? _exitPoint : transform);
            PlayDoor();
            if (_player != null && restoreInput) _player.SetInputLocked(false, false);
        }

        private void Teleport(Transform target)
        {
            if (_player == null || target == null) return;
            bool wasEnabled = _controller != null && _controller.enabled;
            if (wasEnabled) _controller.enabled = false;
            _player.transform.SetPositionAndRotation(target.position, Quaternion.Euler(0f, target.eulerAngles.y, 0f));
            if (wasEnabled) _controller.enabled = true;
        }

        private void PlayDoor()
        {
            if (_doorSource != null && _doorClip != null) _doorSource.PlayOneShot(_doorClip, 0.7f);
            if (_doorNoise > 0f) GameEvents.RaiseNoiseEmitted(transform.position, _doorNoise);
        }
    }
}
