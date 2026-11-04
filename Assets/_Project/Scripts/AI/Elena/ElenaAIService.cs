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
            "Abre la puerta de las escaleras.",
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
            GameEvents.OnPlayerSpoke += HandlePlayerSpoke;
            GameEvents.RaiseQuickQuestionsChanged(IntentTexts);
        }

        private void OnDisable()
        {
            GameEvents.OnQuickQuestionSelected -= HandleQuickQuestionSelected;
            GameEvents.OnPlayerSpoke -= HandlePlayerSpoke;
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

        /// <summary>
        /// Pregunta libre dicha por voz (ElenaVoiceInput). La intención se deduce por palabras clave para el fallback;
        /// Gemini recibe además el texto literal en player_text.
        /// </summary>
        private async void HandlePlayerSpoke(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                await AskAsync(InferIntent(text), text);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _isBusy = false;
                GameEvents.RaiseElenaThinking(false);
            }
        }

        private static string InferIntent(string text)
        {
            string t = text.ToLowerInvariant();
            if (t.Contains("puerta") || t.Contains("abre") || t.Contains("abrir") || t.Contains("desbloquea")) return "OPEN_DOOR";
            if (t.Contains("ruta") || t.Contains("camino") || t.Contains("salida") || t.Contains("despejado") || t.Contains("pasillo") || t.Contains("donde")) return "SAFE_PATH";
            if (t.Contains("miedo") || t.Contains("ayuda") || t.Contains("auxilio") || t.Contains("asustado")) return "FEAR";
            return "GENERIC";
        }

        public async Awaitable AskAsync(string intentKey, string questionText)
        {
            if (_isBusy) return;

            _isBusy = true;
            GameEvents.RaiseElenaThinking(true);

            ElenaRequestContext context = _stateTracker != null
                ? _stateTracker.CaptureContext(intentKey, questionText)
                : new ElenaRequestContext { floor = 12, player_intent = intentKey, player_text = questionText };

            ElenaResponse remote = null;
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            bool forceFallback = _config != null && _config.ForceFallback;

            if (_remoteProvider != null && !forceFallback && isOnline)
            {
                try
                {
                    remote = await _remoteProvider.RequestAsync(context, _history);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ElenaAIService] Solicitud remota fallida: {ex.Message}");
                }
            }

            // Cadena Gemini → respaldo JSON → emergencia: nunca queda sin respuesta
            ElenaResponse response = ElenaReplyResolver.Pick(remote, remote != null ? (_remoteProvider as GeminiProvider)?.LastModel : null,
                () => _fallbackProvider?.Get(context.floor, intentKey), out bool usedFallback);
            Debug.Log($"[ElenaAIService] Respuesta de Elena desde: {response.source}");
            // Cambio de Gabriel (2026-11-04): con Gemini en vivo, [2] debe abrir la puerta del guion igual que el respaldo (sin bloqueos)
            if (!usedFallback && _fallbackProvider != null && ElenaReplyResolver.KeepScriptedUnlock(response, _fallbackProvider.Get(context.floor, intentKey)))
                Debug.Log($"[ElenaAIService] Orden del guion conservada: {response.ui_action} {response.target_id}");

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
