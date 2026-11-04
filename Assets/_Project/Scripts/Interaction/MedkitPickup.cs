using Nexo.Core;
using Nexo.Player;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Botiquín (caja blanca con cruz roja). Se usa con [E] o pasando por encima: cura +1 vida hasta el máximo,
    /// avisa «Botiquín: +1 vida» y desaparece. Con la vida al máximo no se gasta (al pasar por encima ni se avisa).
    /// Igual que las cajas de munición, vuelve a aparecer al recargar el piso. La vida del jugador (que vive en
    /// Systems_Persistent) se obtiene de PlayerHealth.Current.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MedkitPickup : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] private int _healPoints = 1;
        [SerializeField] private AudioClip _pickupClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        private int _playerLayer = -1;

        public string Prompt => $"[E] Usar botiquín (+{_healPoints} vida)";

        private void Awake() => _playerLayer = LayerMask.NameToLayer("Player");

        public void Interact() => Use(true);

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != _playerLayer) return;
            Use(false);
        }

        private void Use(bool explicitUse)
        {
            PlayerHealth health = PlayerHealth.Current;
            if (health == null) return;
            if (!health.TryHeal(_healPoints))
            {
                if (explicitUse) GameEvents.RaiseSubtitle("(Ya estás sano. Dejas el botiquín para más tarde)");
                return;
            }

            GameEvents.RaiseNotification(new HudNotification("NEXOOS", $"Botiquín: +{_healPoints} vida", false));
            if (_pickupClip != null) AudioSource.PlayClipAtPoint(_pickupClip, transform.position, _volume);
            gameObject.SetActive(false);
        }
    }
}
