using Nexo.Interaction;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// Salida por escaleras de un piso. Va en un collider PROPIO junto a la puerta (no en el objeto de la puerta:
    /// PlayerInteractor toma el primer IInteractable que encuentra). Si la puerta F&lt;piso&gt;_Door_Stairs sigue
    /// bloqueada reutiliza su aviso (DoorController.Interact → "Acceso denegado"); si no, baja al siguiente piso
    /// con FloorProgression.LoadNext. En el último piso no hace nada: el giro final lo dispara el BioScanner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairsExit : MonoBehaviour, IInteractable
    {
        [Tooltip("Puerta de escaleras del piso (DoorId F12_Door_Stairs, F06_Door_Stairs...).")]
        [SerializeField] private DoorController _stairsDoor;
        [SerializeField] private string _promptText = "Bajar por las escaleras";

        public string Prompt
        {
            get
            {
                if (_stairsDoor != null && _stairsDoor.IsLocked) return _stairsDoor.Prompt;

                FloorProgression progression = FloorProgression.Active;
                if (progression == null || progression.IsLastFloor || progression.IsBusy) return string.Empty;
                return $"[E] {_promptText}";
            }
        }

        public void Interact()
        {
            if (_stairsDoor != null && _stairsDoor.IsLocked)
            {
                // Aviso existente de la puerta bloqueada
                _stairsDoor.Interact();
                return;
            }

            FloorProgression progression = FloorProgression.Active;
            if (progression == null)
            {
                Debug.LogWarning("[StairsExit] No hay FloorProgression activo (debe estar en Systems_Persistent).", this);
                return;
            }

            progression.LoadNext();
        }
    }
}
