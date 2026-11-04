using System.Collections;
using Nexo.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Nexo.Player
{
    /// <summary>
    /// Red de seguridad contra caídas del mundo y puntos de control del piso.
    /// - El inicio de cada piso es un punto de control; los CheckpointTrigger del piso lo actualizan.
    /// - Cada segundo guarda la última posición segura (en el suelo, sobre el NavMesh, sin UI ni escondite).
    /// - Si el jugador cae por debajo del piso (umbral de altura), funde a negro, lo devuelve al punto más reciente,
    ///   le resta un punto de vida y avisa «Punto de control restaurado». Si era su último punto, la caída entra en el
    ///   flujo normal de captura (PlayerCaptureHandler reinicia el piso desde su inicio).
    /// Sin Update: una corrutina comprueba cada 0,25 s.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCheckpoints : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerHealth _health;

        [Header("Caída")]
        [Tooltip("Metros por debajo del punto de aparición del piso a partir de los que se considera que el jugador cayó del mundo.")]
        [SerializeField] private float _fallDepth = 6f;
        [SerializeField] private float _checkInterval = 0.25f;

        [Header("Posición segura")]
        [SerializeField] private float _safeSampleInterval = 1f;
        [Tooltip("Antigüedad mínima de una posición segura para usarla (evita reaparecer en el mismo borde).")]
        [SerializeField] private float _safeMinAge = 2.5f;

        [Header("Fundido")]
        [SerializeField] private float _fadeOutSeconds = 0.35f;
        [SerializeField] private float _holdSeconds = 0.3f;
        [SerializeField] private float _fadeInSeconds = 0.6f;

        private readonly CheckpointLog _log = new CheckpointLog();
        private Coroutine _monitor;
        private bool _respawning;
        private bool _caught;
        private float _floorY;
        private float _nextSafeSample;
        private FloorMetadata _floor;

        /// <summary>Número de caídas corregidas en esta sesión (comprobaciones del Editor).</summary>
        public int FallRespawns { get; private set; }
        public bool IsRespawning => _respawning;
        public CheckpointLog Log => _log;

        private void Awake()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnCheckpointReached += HandleCheckpointReached;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            if (_monitor == null) _monitor = StartCoroutine(MonitorRoutine());
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnCheckpointReached -= HandleCheckpointReached;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            if (_monitor != null) { StopCoroutine(_monitor); _monitor = null; }
            if (_respawning)
            {
                _respawning = false;
                GameEvents.RaiseScreenFade(0f, 0f);
                if (_movement != null && !_caught) _movement.SetInputLocked(false, false);
            }
        }

        private void HandlePlayerSpawned(Transform player)
        {
            if (player != transform) return;
            _floor = FloorMetadata.Active;
            _floorY = transform.position.y;
            _caught = false;
            _log.Reset(transform.position, transform.eulerAngles.y, Time.time);
            _nextSafeSample = Time.time + _safeSampleInterval;
        }

        private void HandleCheckpointReached(Vector3 position, float yaw, string label)
        {
            _log.SetCheckpoint(position, yaw, Time.time, label);
        }

        private void HandlePlayerCaught() => _caught = true;

        private IEnumerator MonitorRoutine()
        {
            var wait = new WaitForSeconds(_checkInterval);
            while (true)
            {
                yield return wait;
                // Entre la descarga de un piso y su publicación el jugador puede caer en el vacío: no cuenta
                if (_respawning || _caught || _floor == null || FloorMetadata.Active != _floor) continue;

                if (CheckpointLog.IsFallen(transform.position.y, _floorY, _fallDepth))
                {
                    StartCoroutine(RespawnRoutine());
                    continue;
                }

                if (Time.time >= _nextSafeSample)
                {
                    _nextSafeSample = Time.time + _safeSampleInterval;
                    TrySampleSafeGround();
                }
            }
        }

        private void TrySampleSafeGround()
        {
            if (_movement == null || !_movement.IsGrounded || _movement.IsInputLocked || PlayerHiding.IsHidden) return;
            Vector3 p = transform.position;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) return;
            if (Mathf.Abs(hit.position.y - p.y) > 0.4f) return;
            _log.AddSafeGround(p, transform.eulerAngles.y, Time.time);
        }

        private IEnumerator RespawnRoutine()
        {
            _respawning = true;
            if (_movement != null) _movement.SetInputLocked(true, false);
            GameEvents.RaiseScreenFade(1f, _fadeOutSeconds);
            yield return new WaitForSecondsRealtime(_fadeOutSeconds);

            if (_log.TryChoose(Time.time, _safeMinAge, out Vector3 position, out float yaw) && _movement != null)
            {
                _movement.TeleportTo(position + Vector3.up * 0.05f, yaw);
            }
            FallRespawns++;

            bool lastLife = _health != null && _health.Points <= 1;
            if (_health != null) _health.LosePoint();
            yield return new WaitForSecondsRealtime(_holdSeconds);

            GameEvents.RaiseScreenFade(0f, _fadeInSeconds);
            if (!lastLife && !_caught)
            {
                GameEvents.RaiseNotification(new HudNotification("NEXOOS", "Punto de control restaurado.", false));
                if (_movement != null) _movement.SetInputLocked(false, false);
            }
            // Si era el último punto de vida, PlayerCaptureHandler ya bloqueó al jugador y reinicia el piso
            _respawning = false;
        }
    }
}
