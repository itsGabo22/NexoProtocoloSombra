using Nexo.Core;
using Nexo.Narrative;
using Nexo.UI;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Nota o documento legible del piso: un papel visible en el mundo (se resalta al mirarlo). Al interactuar se oye
    /// el papel y se lee a pantalla completa (NoteReader); queda guardada en el diario [J]. Si no hay lector (escena
    /// suelta sin HUD), se muestra como notificación, como antes. Una sola vía: nunca notificación y papel a la vez.
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
            int floor = FloorMetadata.Active != null ? FloorMetadata.Active.FloorNumber : 0;
            NoteJournal.Add(floor, _author, _body);
            if (NoteReader.Current != null) NarrativeEvents.RaiseNoteRead(_author, _body);
            else GameEvents.RaiseNotification(new HudNotification(_author, _body, false));
            if (_followUp != null) _followUp.Play();
        }
    }
}
