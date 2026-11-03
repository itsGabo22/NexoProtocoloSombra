using Nexo.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Visión térmica visible en el pipeline Built-in. El Volume VP_Thermal de ThermalVisionController no tiene efecto
    /// sin URP, así que este componente muestra una capa UI Toolkit a pantalla completa (tinte cian + viñeta) en el HUD
    /// mientras FlashlightSystem (tecla F) está activo. La barra de batería sigue llegando por GameEvents.OnBatteryChanged.
    /// Sin Update: reacciona al evento StateChanged de FlashlightSystem.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ThermalOverlayController : MonoBehaviour
    {
        private const string OverlayName = "hud-thermal-overlay";
        private const string OnClass = "thermal-on";

        [Header("Referencias (PF_Player en Systems_Persistent)")]
        [SerializeField] private FlashlightSystem _flashlight;

        private UIDocument _document;
        private VisualElement _overlay;

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
        }
    }
}
