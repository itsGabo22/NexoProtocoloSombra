using Nexo.Core;
using Nexo.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// [Tab] (o Select en mando) vuelve a mostrar el objetivo actual del piso, para que el jugador sepa
    /// siempre cómo avanzar. Por callback de entrada: sin Update().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveReminder : MonoBehaviour
    {
        private InputAction _action;

        private void Awake()
        {
            _action = new InputAction("ShowObjective", InputActionType.Button);
            _action.AddBinding("<Keyboard>/tab");
            _action.AddBinding("<Gamepad>/select");
        }

        private void OnEnable()
        {
            _action.performed += HandleShowObjective;
            _action.Enable();
        }

        private void OnDisable()
        {
            _action.performed -= HandleShowObjective;
            _action.Disable();
        }

        private void OnDestroy() => _action?.Dispose();

        private void HandleShowObjective(InputAction.CallbackContext context)
        {
            string objective = StorySequence.CurrentObjective;
            GameEvents.RaiseNotification(new HudNotification("OBJETIVO",
                string.IsNullOrEmpty(objective) ? "Explora el piso y escucha a Elena." : objective, false));
        }
    }
}
