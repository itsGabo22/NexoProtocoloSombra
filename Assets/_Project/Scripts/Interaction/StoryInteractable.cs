using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Objeto de la historia que el jugador activa con [E] (grabadora, archivos...). Reproduce una StorySequence
    /// la primera vez; después solo muestra el prompt de repetido.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "[E] Examinar";
        [SerializeField] private string _usedPrompt = "";
        [SerializeField] private StorySequence _sequence;

        public string Prompt => _sequence != null && _sequence.HasStarted ? _usedPrompt : _prompt;

        public void Interact()
        {
            if (_sequence != null) _sequence.Play();
        }
    }
}
