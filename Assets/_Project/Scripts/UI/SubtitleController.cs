using Nexo.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Muestra subtítulos en la etiqueta hud-subtitles a partir de GameEvents.OnSubtitle.
    /// Cada línea se oculta sola tras un tiempo proporcional a su longitud (scheduler de UI Toolkit, sin Update).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class SubtitleController : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [Header("Duración en Pantalla")]
        [SerializeField] private float _minDuration = 2.5f;
        [SerializeField] private float _secondsPerCharacter = 0.06f;
        [SerializeField] private float _maxDuration = 8f;

        private Label _label;
        private IVisualElementScheduledItem _hideItem;

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

            Hide();
            GameEvents.OnSubtitle += HandleSubtitle;
        }

        private void OnDisable()
        {
            GameEvents.OnSubtitle -= HandleSubtitle;
            _hideItem?.Pause();
        }

        private void HandleSubtitle(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            _label.text = text;
            _label.RemoveFromClassList(HiddenClass);

            float seconds = Mathf.Clamp(_minDuration + text.Length * _secondsPerCharacter, _minDuration, _maxDuration);
            _hideItem?.Pause();
            _hideItem = _label.schedule.Execute(Hide).StartingIn((long)(seconds * 1000f));
        }

        private void Hide()
        {
            if (_label == null) return;

            _label.text = string.Empty;
            _label.AddToClassList(HiddenClass);
        }
    }
}
