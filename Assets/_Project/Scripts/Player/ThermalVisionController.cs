using System.Collections;
using System.Collections.Generic;
using Nexo.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nexo.Player
{
    /// <summary>
    /// Mezcla el Volume VP_Thermal (hijo de PF_Player) cuando la visión térmica está activa.
    /// Si el infectado (o Elena) corta las luces, enciende la visión térmica automáticamente
    /// y la apaga al restaurarse la energía si fue activada por el apagón.
    /// </summary>
    [RequireComponent(typeof(FlashlightSystem))]
    public sealed class ThermalVisionController : MonoBehaviour
    {
        [Header("Volume de Post-Proceso (perfil VP_Thermal)")]
        [SerializeField] private Volume _thermalVolume;
        [SerializeField] private float _blendDuration = 0.25f;

        [Header("Apagones")]
        [SerializeField] private bool _autoEnableOnBlackout = true;
        [Tooltip("Opcional: si se asigna, solo reacciona a apagones del cuarto actual del jugador.")]
        [SerializeField] private GameStateTracker _stateTracker;

        private readonly HashSet<string> _darkRooms = new();
        private FlashlightSystem _flashlight;
        private Coroutine _blendRoutine;
        private bool _autoEnabled;
        private bool _changingFromBlackout;

        private void Awake()
        {
            _flashlight = GetComponent<FlashlightSystem>();

            if (_thermalVolume == null)
            {
                Debug.LogError($"[ThermalVisionController] Falta asignar _thermalVolume (VP_Thermal) en {name}.", this);
                return;
            }
            _thermalVolume.weight = 0f;
        }

        private void OnEnable()
        {
            _flashlight.StateChanged += HandleThermalStateChanged;
            GameEvents.OnLightsChanged += HandleLightsChanged;
            GameEvents.OnFloorChanged += HandleFloorChanged;
        }

        private void OnDisable()
        {
            _flashlight.StateChanged -= HandleThermalStateChanged;
            GameEvents.OnLightsChanged -= HandleLightsChanged;
            GameEvents.OnFloorChanged -= HandleFloorChanged;

            if (_blendRoutine != null)
            {
                StopCoroutine(_blendRoutine);
                _blendRoutine = null;
            }
            if (_thermalVolume != null) _thermalVolume.weight = _flashlight.IsOn ? 1f : 0f;
        }

        private void HandleThermalStateChanged(bool isOn)
        {
            // Si el jugador alterna manualmente, deja de considerarse un encendido automático
            if (!_changingFromBlackout) _autoEnabled = false;

            BlendTo(isOn ? 1f : 0f);
        }

        private void HandleLightsChanged(string roomId, bool isOn)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            if (_stateTracker != null && !string.Equals(roomId, _stateTracker.CurrentRoom, System.StringComparison.OrdinalIgnoreCase)) return;

            if (isOn) _darkRooms.Remove(roomId);
            else _darkRooms.Add(roomId);

            if (!_autoEnableOnBlackout) return;

            if (!isOn && !_flashlight.IsOn && _flashlight.CanTurnOn)
            {
                SetThermalFromBlackout(true);
                _autoEnabled = _flashlight.IsOn;
            }
            else if (isOn && _darkRooms.Count == 0 && _autoEnabled && _flashlight.IsOn)
            {
                SetThermalFromBlackout(false);
                _autoEnabled = false;
            }
        }

        private void HandleFloorChanged(int floor)
        {
            // Al cargar o reiniciar un piso todas sus luces arrancan encendidas
            _darkRooms.Clear();
            if (_autoEnabled && _flashlight.IsOn) SetThermalFromBlackout(false);
            _autoEnabled = false;
        }

        private void SetThermalFromBlackout(bool on)
        {
            _changingFromBlackout = true;
            _flashlight.SetOn(on);
            _changingFromBlackout = false;
        }

        private void BlendTo(float target)
        {
            if (_thermalVolume == null) return;

            if (_blendRoutine != null) StopCoroutine(_blendRoutine);
            if (!isActiveAndEnabled || _blendDuration <= 0f)
            {
                _thermalVolume.weight = target;
                return;
            }
            _blendRoutine = StartCoroutine(BlendRoutine(target));
        }

        /// <summary>
        /// Transición corta del peso del Volume; termina al alcanzar el objetivo.
        /// </summary>
        private IEnumerator BlendRoutine(float target)
        {
            float start = _thermalVolume.weight;
            float elapsed = 0f;

            while (elapsed < _blendDuration)
            {
                elapsed += Time.deltaTime;
                _thermalVolume.weight = Mathf.Lerp(start, target, elapsed / _blendDuration);
                yield return null;
            }

            _thermalVolume.weight = target;
            _blendRoutine = null;
        }
    }
}
