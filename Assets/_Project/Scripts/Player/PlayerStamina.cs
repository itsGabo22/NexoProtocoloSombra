using System;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Gestiona la resistencia (stamina) del jugador para el sprint continuo.
    /// Recuperación automática tras un retardo sin sondeo innecesario.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Header("Configuración de Stamina")]
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _drainRate = 25f;
        [SerializeField] private float _recoveryRate = 18f;
        [SerializeField] private float _recoveryDelay = 1.2f;

        private PlayerMovement _movement;
        private float _currentStamina;
        private float _lastDrainTime;

        public float CurrentStamina => _currentStamina;
        public float NormalizedStamina => Mathf.Clamp01(_currentStamina / _maxStamina);
        public bool HasStamina => _currentStamina > 5f;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _currentStamina = _maxStamina;
        }

        // JUSTIFICACIÓN UPDATE: Consumo y regeneración continua vinculada al estado de sprint del jugador.
        private void Update()
        {
            if (_movement != null && _movement.IsSprinting && _movement.IsMoving)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - _drainRate * Time.deltaTime);
                _lastDrainTime = Time.time;
            }
            else if (Time.time - _lastDrainTime >= _recoveryDelay && _currentStamina < _maxStamina)
            {
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _recoveryRate * Time.deltaTime);
            }
        }
    }
}
