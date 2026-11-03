using Nexo.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Visión térmica visible en el pipeline Built-in. El Volume VP_Thermal de ThermalVisionController no tiene efecto
    /// sin URP, así que este componente muestra una capa UI Toolkit a pantalla completa (tinte cian + viñeta) en el HUD
    /// mientras FlashlightSystem (tecla F) está activo. La barra de batería sigue llegando por GameEvents.OnBatteryChanged.
    /// Sin Update: reacciona al evento StateChanged de FlashlightSystem. También actualiza el indicador
    /// ACTIVA/APAGADA del panel de batería (hud-battery-state).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ThermalOverlayController : MonoBehaviour
    {
        private const string OverlayName = "hud-thermal-overlay";
        private const string OnClass = "thermal-on";
        private const string StateLabelName = "hud-battery-state";

        [Header("Referencias (PF_Player en Systems_Persistent)")]
        [SerializeField] private FlashlightSystem _flashlight;

        private UIDocument _document;
        private VisualElement _overlay;
        private Label _stateLabel;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_flashlight == null) Debug.LogError($"[ThermalOverlayController] Falta asignar _flashlight en {name}.", this);
        }

        private void OnEnable()
        {
            if (_flashlight == null) return;
            _flashlight.StateChanged += HandleThermalStateChanged;
            HandleThermalStateChanged(_flashlight.IsOn);
        }

        private void OnDisable()
        {
            if (_flashlight != null) _flashlight.StateChanged -= HandleThermalStateChanged;
        }

        private void HandleThermalStateChanged(bool isOn)
        {
            // Se resuelve al primer uso: el UIDocument puede construir su árbol después de este OnEnable
            if (_overlay == null) _overlay = _document.rootVisualElement?.Q<VisualElement>(OverlayName);
            _overlay?.EnableInClassList(OnClass, isOn);

            // Indicador de estado junto al porcentaje de batería (texto + color, no solo color)
            if (_stateLabel == null) _stateLabel = _document.rootVisualElement?.Q<Label>(StateLabelName);
            if (_stateLabel == null) return;
            _stateLabel.text = isOn ? "ACTIVA" : "APAGADA";
            _stateLabel.EnableInClassList("text-accent", isOn);
            _stateLabel.EnableInClassList("text-muted", !isOn);
        }
    }
}
