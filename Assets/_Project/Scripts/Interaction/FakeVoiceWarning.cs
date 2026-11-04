using System.Collections;
using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Elena real advierte al jugador cuando aparece una notificación falsa con su nombre (mimetismo del infectado).
    /// Si una secuencia del guion ya hizo que Elena respondiera, no repite el aviso. Con enfriamiento para no saturar.
    /// Va en la escena del piso (Piso 6): no altera el comportamiento de otros pisos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FakeVoiceWarning : MonoBehaviour
    {
        [SerializeField] private float _delay = 3.5f;
        [SerializeField] private float _cooldown = 25f;
        [SerializeField, TextArea(1, 3)] private string[] _warnings =
        {
            "¡Ese mensaje no lleva mi sello, Alex! No soy yo.",
            "Alex, esa voz no soy yo. Mira el sello BIO-ID.",
            "No le hagas caso. Si no ves mi sello, no es mía."
        };

        private float _lastWarningTime = float.NegativeInfinity;
        private float _lastElenaTime = float.NegativeInfinity;
        private int _next;
        private Coroutine _pending;

        private void OnEnable()
        {
            GameEvents.OnNotification += HandleNotification;
            GameEvents.OnElenaResponse += HandleElenaResponse;
        }

        private void OnDisable()
        {
            GameEvents.OnNotification -= HandleNotification;
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            if (_pending != null) { StopCoroutine(_pending); _pending = null; }
        }

        private void HandleElenaResponse(ElenaResponse response) => _lastElenaTime = Time.time;

        private void HandleNotification(HudNotification notification)
        {
            if (!notification.IsFake || notification.Sender != "ELENA") return;
            if (_pending != null || Time.time - _lastWarningTime < _cooldown || _warnings.Length == 0) return;
            _pending = StartCoroutine(WarnRoutine(Time.time));
        }

        private IEnumerator WarnRoutine(float fakeTime)
        {
            yield return new WaitForSeconds(_delay);
            _pending = null;

            // El guion ya respondió (StorySequence con Elena tras la voz falsa): no duplicar
            if (_lastElenaTime > fakeTime) yield break;

            _lastWarningTime = Time.time;
            string line = _warnings[_next++ % _warnings.Length];
            GameEvents.RaiseElenaResponse(new ElenaResponse { dialogue = line, emotion = "URGENT", trust_level = 60 });
            GameEvents.RaiseSubtitle("ELENA: " + line);
        }
    }
}
