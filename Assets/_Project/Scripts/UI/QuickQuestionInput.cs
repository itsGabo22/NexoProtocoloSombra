using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.UI
{
    /// <summary>
    /// Traduce los atajos [1], [2] y [3] (NexoControls/UI/QuickQuestion1..3) a preguntas rápidas para Elena.
    /// Emite GameEvents.RaiseQuickQuestionSelected igual que los botones radio-quick-1..3 del HUD.
    /// </summary>
    public sealed class QuickQuestionInput : MonoBehaviour
    {
        [Header("Acciones (NexoControls/UI)")]
        [SerializeField] private InputActionReference _quickQuestion1;
        [SerializeField] private InputActionReference _quickQuestion2;
        [SerializeField] private InputActionReference _quickQuestion3;

        private bool _isLocked;

        private void OnEnable()
        {
            Bind(_quickQuestion1, HandleQuickQuestion1);
            Bind(_quickQuestion2, HandleQuickQuestion2);
            Bind(_quickQuestion3, HandleQuickQuestion3);
            GameEvents.OnPlotTwistTriggered += HandlePlotTwistTriggered;
        }

        private void OnDisable()
        {
            Unbind(_quickQuestion1, HandleQuickQuestion1);
            Unbind(_quickQuestion2, HandleQuickQuestion2);
            Unbind(_quickQuestion3, HandleQuickQuestion3);
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwistTriggered;
        }

        private static void Bind(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
        {
            if (reference == null) return;
            reference.action.performed += handler;
            reference.action.Enable();
        }

        private static void Unbind(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
        {
            if (reference == null) return;
            reference.action.performed -= handler;
        }

        private void HandleQuickQuestion1(InputAction.CallbackContext context) => Select(0);
        private void HandleQuickQuestion2(InputAction.CallbackContext context) => Select(1);
        private void HandleQuickQuestion3(InputAction.CallbackContext context) => Select(2);

        private void HandlePlotTwistTriggered()
        {
            _isLocked = true;
        }

        private void Select(int index)
        {
            if (_isLocked) return;
            GameEvents.RaiseQuickQuestionSelected(index);
        }
    }
}
