using System;
using System.Collections.Generic;
using Nexo.Core;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Servicio orquestador de IA generativa de Elena.
    /// Responde a preguntas rápidas del HUD consultando a Gemini Flash (2.5) con fallback local automático.
    /// Actualiza el historial de conversación, notifica al HUD y enruta órdenes a la UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElenaAIService : MonoBehaviour
    {
        private static readonly string[] IntentKeys = { "SAFE_PATH", "OPEN_DOOR", "FEAR" };
        private static readonly string[] IntentTexts =
        {
            "¿Está despejado el pasillo?",
            "Abre la puerta norte.",
            "Tengo miedo, ¿qué tienes en la mano?"
        };

        [Header("Configuración y Secretos")]
        [SerializeField] private ElenaConfig _config;
        [SerializeField] private ElenaSecrets _secrets;
        [SerializeField] private GameStateTracker _stateTracker;

        private IElenaProvider _remoteProvider;
        private ElenaFallbackProvider _fallbackProvider;
        private readonly List<ChatTurn> _history = new();
        private bool _isBusy = false;
        private bool _fallbackNoticeShown = false;

        private void Awake()
        {
            if (_config != null && _config.FallbackFiles != null)
            {
                _fallbackProvider = new ElenaFallbackProvider(_config.FallbackFiles);
            }

            ElenaSecrets secretsToUse = _secrets != null ? _secrets : (_config != null ? _config.Secrets : null);
            if (secretsToUse != null && secretsToUse.HasApiKey)
            {
                _remoteProvider = new GeminiProvider(_config, secretsToUse.ApiKey);
            }
            else
            {
                Debug.Log("[ElenaAIService] Sin API key configurada: operando en modo contingencia (fallback local).");
            }
        }

        private void OnEnable()
        {
            GameEvents.OnQuickQuestionSelected += HandleQuickQuestionSelected;
            GameEvents.RaiseQuickQuestionsChanged(IntentTexts);
        }

        private void OnDisable()
        {
            GameEvents.OnQuickQuestionSelected -= HandleQuickQuestionSelected;
        }

        private async void HandleQuickQuestionSelected(int index)
        {
            if (index < 0 || index >= IntentKeys.Length) return;

            string key = IntentKeys[index];
            string questionText = IntentTexts[index];

            try
            {
                await AskAsync(key, questionText);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _isBusy = false;
                GameEvents.RaiseElenaThinking(false);
            }
        }

        public async Awaitable AskAsync(string intentKey, string questionText)
        {
            if (_isBusy) return;

            _isBusy = true;
            GameEvents.RaiseElenaThinking(true);

            ElenaRequestContext context = _stateTracker != null
                ? _stateTracker.CaptureContext(questionText)
                : new ElenaRequestContext { floor = 12, question_selected = questionText };

            ElenaResponse response = null;
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            bool forceFallback = _config != null && _config.ForceFallback;

            if (_remoteProvider != null && !forceFallback && isOnline)
            {
                try
                {
                    response = await _remoteProvider.RequestAsync(context, _history);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ElenaAIService] Solicitud remota fallida: {ex.Message}");
                }
            }

            bool usedFallback = false;
            if (response == null || !response.IsValid(out _))
            {
                usedFallback = true;
                if (_fallbackProvider != null)
                {
                    response = _fallbackProvider.Get(context.floor, intentKey);
                }
                else
                {
                    response = new ElenaResponse
                    {
                        dialogue = "Alex... sigue caminando hacia la esclusa, por favor.",
                        emotion = "scared",
                        ui_action = "none",
                        trust_level = 50
                    };
                }
            }

            RememberTurn(questionText, response.dialogue);

            GameEvents.RaiseElenaThinking(false);
            GameEvents.RaiseElenaResponse(response);
            ElenaUiCommandRouter.Dispatch(response, context);

            if (usedFallback && !_fallbackNoticeShown)
            {
                _fallbackNoticeShown = true;
                GameEvents.RaiseNotification(new HudNotification("NEXOOS", "Enlace con Elena inestable: canal de contingencia activo.", false));
            }

            _isBusy = false;
        }

        private void RememberTurn(string userText, string modelText)
        {
            _history.Add(new ChatTurn("user", userText));
            _history.Add(new ChatTurn("model", modelText));

            int maxTurns = _config != null ? _config.MaxHistoryTurns * 2 : 12;
            while (_history.Count > maxTurns)
            {
                _history.RemoveAt(0);
            }
        }
    }
}
