using Nexo.Core;
using Nexo.Player;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Cuchillo en el suelo (botín del patrullero del Piso 12). [E] para recogerlo: queda equipado en [X]
    /// y el HUD lo muestra en el panel del arma. Brilla un poco para verse en la penumbra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnifePickup : MonoBehaviour, IInteractable
    {
        public string Prompt => "[E] Recoger cuchillo";

        public void Interact()
        {
            PlayerKnife knife = PlayerKnife.Current;
            if (knife == null) return;
            if (knife.HasKnife)
            {
                GameEvents.RaiseSubtitle("(Ya llevas un cuchillo)");
                return;
            }
            knife.SetHasKnife(true);
            GameEvents.RaiseNotification(new HudNotification("NEXOOS", "Cuchillo recogido: [X] para atacar (por la espalda, sin que te vea: silencioso).", false));
            gameObject.SetActive(false);
        }
    }
}
