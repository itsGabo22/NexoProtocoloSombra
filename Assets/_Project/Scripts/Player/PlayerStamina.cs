using System.Collections;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Gestiona la resistencia (stamina) del jugador para el sprint continuo.
    /// Una corrutina solo vive mientras se gasta o se regenera stamina y se detiene al llegar al 100%.
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
        private bool _isDraining;
        private Coroutine _staminaRoutine;

        public float CurrentStamina => _currentStamina;
        public float NormalizedStamina => Mathf.Clamp01(_currentStamina / _maxStamina);
        public bool HasStamina => _currentStamina > 5f;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _currentStamina = _maxStamina;
        }

        private void OnEnable()
        {
            _movement.SprintStateChanged += HandleSprintStateChanged;
        }

        private void OnDisable()
        {
            _movement.SprintStateChanged -= HandleSprintStateChanged;
            _isDraining = false;

            if (_staminaRoutine != null)
            {
                StopCoroutine(_staminaRoutine);
                _staminaRoutine = null;
            }
        }

        private void HandleSprintStateChanged(bool isSprinting)
        {
            _isDraining = isSprinting;
            _lastDrainTime = Time.time;

            if (_staminaRoutine == null && (_isDraining || _currentStamina < _maxStamina))
            {
                _staminaRoutine = StartCoroutine(StaminaRoutine());
            }
        }

        /// <summary>
        /// Consume stamina mientras se esprinta y, tras el retardo, la regenera hasta el máximo.
        /// </summary>
        private IEnumerator StaminaRoutine()
        {
            while (true)
            {
                if (_isDraining)
                {
                    _currentStamina = Mathf.Max(0f, _currentStamina - _drainRate * Time.deltaTime);
                    _lastDrainTime = Time.time;
                }
                else if (Time.time - _lastDrainTime >= _recoveryDelay)
                {
                    _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _recoveryRate * Time.deltaTime);
                    if (_currentStamina >= _maxStamina) break;
                }

                yield return null;
            }

            _staminaRoutine = null;
        }
    }
}
