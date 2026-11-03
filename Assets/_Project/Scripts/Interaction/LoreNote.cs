using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Nota o documento legible del piso. Al interactuar muestra su texto como notificación del HUD
    /// (remitente = autor de la nota). Aporta narrativa ambiental sin bloquear al jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoreNote : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _author = "NOTA";
        [SerializeField, TextArea(2, 5)] private string _body = "...";
        [Tooltip("Reacción opcional (p. ej. Elena comenta la nota).")]
        [SerializeField] private StorySequence _followUp;

        private bool _read;

        public string Prompt => _read ? "[E] Releer nota" : "[E] Leer nota";

        public void Interact()
        {
            _read = true;
            GameEvents.RaiseNotification(new HudNotification(_author, _body, false));
            if (_followUp != null) _followUp.Play();
        }
    }
}
