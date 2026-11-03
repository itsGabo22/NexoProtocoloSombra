using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Caja o puñado de cartuchos .45 que el jugador recoge con [E]. Notifica la cantidad por GameEvents
    /// (PlayerWeapon la suma a la reserva) y se oculta. Se reactiva al recargar el piso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmoPickup : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] private int _rounds = 6;
        [SerializeField] private string _label = "cartuchos .45";

        public string Prompt => $"[E] Recoger {_label} (x{_rounds})";

        public void Interact()
        {
            GameEvents.RaiseAmmoPicked(_rounds);
            gameObject.SetActive(false);
        }
    }
}
