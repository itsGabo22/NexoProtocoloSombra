using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Escribe el piso actual en un Label de otro UIDocument (pantalla de captura del HUD, título de la terminal)
    /// sin tocar sus controladores. Lee el piso de MenuFlowController y se actualiza con CurrentFloorChanged.
    /// La plantilla admite {0} = número de piso y {1} = nombre de la zona.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorLabelBinder : MonoBehaviour
    {
        [SerializeField] private MenuFlowController _menuFlow;
        [SerializeField] private UIDocument _document;
        [Tooltip("name del Label en el UXML (p. ej. hud-capture-floor, terminal-title).")]
        [SerializeField] private string _labelName = "hud-capture-floor";
        [SerializeField] private string _template = "Reiniciando protocolo del Piso {0}...";

        private void OnEnable()
        {
            if (_menuFlow == null || _document == null)
            {
                Debug.LogWarning($"[FloorLabelBinder] Faltan referencias en {name}: no se actualizará '{_labelName}'.", this);
                return;
            }

            _menuFlow.CurrentFloorChanged += HandleCurrentFloorChanged;
            Apply(_menuFlow.CurrentFloor);
        }

        private void Start()
        {
            // El UIDocument puede construir su árbol después de este OnEnable
            if (_menuFlow != null) Apply(_menuFlow.CurrentFloor);
        }

        private void OnDisable()
        {
            if (_menuFlow != null) _menuFlow.CurrentFloorChanged -= HandleCurrentFloorChanged;
        }

        private void HandleCurrentFloorChanged(int floor)
        {
            Apply(floor);
        }

        private void Apply(int floor)
        {
            if (_document == null || _document.rootVisualElement == null) return;

            Label label = _document.rootVisualElement.Q<Label>(_labelName);
            if (label != null) label.text = FloorLabels.Format(_template, floor);
        }
    }
}
