using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Controla la linterna y el modo de visión térmica de las gafas NexoOS.
    /// Consume batería continuamente cuando está encendida y notifica al HUD.
    /// Se desactiva y bloquea durante el giro argumental.
    /// </summary>
    public sealed class FlashlightSystem : MonoBehaviour
    {
        [Header("Referencias de Iluminación")]
        [SerializeField] private Light _spotlight;

        [Header("Configuración de Batería (0 - 1)")]
        [SerializeField] private float _drainRate = 0.035f;
        [SerializeField] private float _rechargeRate = 0.02f;

        private float _battery = 1.0f;
        private bool _isOn = false;
        private bool _isLocked = false;

        public bool IsOn => _isOn;
        public float Battery => _battery;

        private void Start()
        {
            if (_spotlight != null)
            {
                _spotlight.enabled = _isOn;
            }
            GameEvents.RaiseBatteryChanged(_battery);
        }

        private void OnEnable()
        {
            GameEvents.OnPlotTwistTriggered += HandlePlotTwistTriggered;
        }

        private void OnDisable()
        {
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwistTriggered;
        }

        private void HandlePlotTwistTriggered()
        {
            _isLocked = true;
            _isOn = false;
            if (_spotlight != null)
            {
                _spotlight.enabled = false;
            }
            _battery = 0f;
            GameEvents.RaiseBatteryChanged(_battery);
        }

        // JUSTIFICACIÓN UPDATE: Muestreo de entrada interactiva de alternancia (F) y consumo de batería en tiempo real.
        private void Update()
        {
            if (_isLocked) return;

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                ToggleFlashlight();
            }

            if (_isOn)
            {
                _battery = Mathf.Max(0f, _battery - _drainRate * Time.deltaTime);
                if (_battery <= 0f)
                {
                    _isOn = false;
                    if (_spotlight != null) _spotlight.enabled = false;
                }
                GameEvents.RaiseBatteryChanged(_battery);
            }
            else if (_battery < 1.0f)
            {
                _battery = Mathf.Min(1.0f, _battery + _rechargeRate * Time.deltaTime);
                GameEvents.RaiseBatteryChanged(_battery);
            }
        }

        public void ToggleFlashlight()
        {
            if (_isLocked) return;
            if (!_isOn && _battery <= 0.05f) return;

            _isOn = !_isOn;
            if (_spotlight != null)
            {
                _spotlight.enabled = _isOn;
            }
        }
    }
}
