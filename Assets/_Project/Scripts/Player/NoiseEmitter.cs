using Nexo.Core;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Calcula y emite el nivel acústico producido por el jugador según su postura y velocidad.
    /// Publica datos para el espectrograma del HUD y alertas sonoras espaciales para la IA de los infectados.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class NoiseEmitter : MonoBehaviour
    {
        [Header("Niveles de Ruido por Postura (0 - 100)")]
        [SerializeField] private float _idleNoise = 0f;
        [SerializeField] private float _crouchNoise = 15f;
        [SerializeField] private float _walkNoise = 45f;
        [SerializeField] private float _sprintNoise = 90f;

        [Header("Frecuencia de Emisión para IA")]
        [SerializeField] private float _aiEmissionInterval = 0.2f;

        private PlayerMovement _movement;
        private float _currentNoise;
        private float _lastEmissionTime;

        public float CurrentNoise => _currentNoise;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
        }

        // JUSTIFICACIÓN UPDATE: Muestreo del ruido producido por la velocidad y postura del jugador (autorizado en rules.md).
        private void Update()
        {
            if (_movement == null) return;

            float targetNoise = _idleNoise;

            if (_movement.IsMoving)
            {
                if (_movement.IsCrouching)
                {
                    targetNoise = _crouchNoise;
                }
                else if (_movement.IsSprinting)
                {
                    targetNoise = _sprintNoise;
                }
                else
                {
                    targetNoise = _walkNoise;
                }
            }

            _currentNoise = Mathf.MoveTowards(_currentNoise, targetNoise, Time.deltaTime * 180f);
            GameEvents.RaiseNoiseChanged(_currentNoise);

            if (_currentNoise > 10f && Time.time - _lastEmissionTime >= _aiEmissionInterval)
            {
                _lastEmissionTime = Time.time;
                GameEvents.RaiseNoiseEmitted(transform.position, _currentNoise);
            }
        }
    }
}
