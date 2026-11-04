using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Muestra subtítulos en la etiqueta hud-subtitles a partir de GameEvents.OnSubtitle.
    /// Las líneas hacen cola (SubtitleQueue): cada una dura MessageTiming (según su longitud, la velocidad de lectura y,
    /// si Elena la dice con voz, lo que dura el clip) y solo cede antes de tiempo, nunca por debajo de su mínimo, si hay
    /// otras esperando. Los avisos de daño/agarre entran al instante. Un texto vacío limpia todo.
    /// Tiempo real (los subtítulos siguen con el juego en pausa) y scheduler de UI Toolkit, sin Update.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class SubtitleController : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        private readonly SubtitleQueue _queue = new SubtitleQueue();
        private Label _label;
        private IVisualElementScheduledItem _tickItem;

        /// <summary>Línea visible ahora (comprobaciones del Editor).</summary>
        public string CurrentText => _queue.HasCurrent ? _queue.Current.Text : string.Empty;
        public int PendingCount => _queue.PendingCount;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            _label = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.Q<Label>("hud-subtitles")
                : null;

            if (_label == null)
            {
                Debug.LogWarning("[SubtitleController] No se encontró la etiqueta 'hud-subtitles' en el UIDocument.", this);
                return;
            }

            _queue.Clear();
            Hide();
            GameEvents.OnSubtitle += HandleSubtitle;
        }

        private void OnDisable()
        {
            GameEvents.OnSubtitle -= HandleSubtitle;
            _tickItem?.Pause();
        }

        private void HandleSubtitle(string text)
        {
            if (_label == null) return;
            if (string.IsNullOrEmpty(text))
            {
                _queue.Clear();
                Hide();
                return;
            }

            float voice = ElenaVoiceText.IsElenaSubtitle(text) ? ElenaVoiceLibrary.SecondsFor(text) : 0f;
            var entry = new SubtitleQueue.Entry
            {
                Text = text,
                Duration = MessageTiming.Seconds(text.Length, voice, MessageTiming.ReadingSpeed),
                Minimum = MessageTiming.MinimumSeconds(voice, MessageTiming.ReadingSpeed),
                Priority = SubtitlePriority.Classify(text)
            };

            if (_queue.Push(entry, Time.realtimeSinceStartup)) ShowCurrent();
            ScheduleTick();
        }

        private void Tick()
        {
            if (_queue.Tick(Time.realtimeSinceStartup))
            {
                if (_queue.HasCurrent) ShowCurrent();
                else Hide();
            }
            ScheduleTick();
        }

        private void ScheduleTick()
        {
            _tickItem?.Pause();
            if (!_queue.HasCurrent) return;
            float wait = Mathf.Max(0.05f, _queue.Deadline() - Time.realtimeSinceStartup);
            _tickItem = _label.schedule.Execute(Tick).StartingIn((long)(wait * 1000f));
        }

        private void ShowCurrent()
        {
            // Ids internos traducidos solo en pantalla (el texto del evento no se modifica)
            _label.text = HudTextSanitizer.Humanize(_queue.Current.Text);
            _label.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            if (_label == null) return;
            _label.text = string.Empty;
            _label.AddToClassList(HiddenClass);
        }
    }
}
