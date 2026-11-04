using System;
using System.Collections.Generic;
using System.Text;
using Nexo.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Nexo.AI.Elena
{
    public interface IElenaProvider
    {
        Awaitable<ElenaResponse> RequestAsync(ElenaRequestContext context, List<ChatTurn> history);
    }

    /// <summary>
    /// Cliente HTTP con Gemini vía UnityWebRequest (modelo de diálogo de ElenaConfig, por defecto gemini-3.8-flash).
    /// Envía el System Prompt, historial y contexto diegético, exige JSON y pide el razonamiento mínimo (thinkingLevel
    /// «low»: sin él 3.8-flash tarda ~4,6 s). Si el modelo responde 503/429/404 o se agota el tiempo, prueba los modelos de
    /// reserva dentro del MISMO plazo total (TimeoutSeconds). Cualquier fallo devuelve null y ElenaAIService usa el respaldo
    /// offline: el jugador nunca ve un error. Las respuestas pasan por ElenaReplyGuard (sin spoilers del giro ni salir del
    /// personaje) y HudTextSanitizer (sin ids internos de puertas).
    /// </summary>
    public sealed class GeminiProvider : IElenaProvider
    {
        [Serializable] private sealed class RequestPart { public string text; }
        [Serializable] private sealed class RequestContent { public string role; public RequestPart[] parts; }
        [Serializable] private sealed class SystemInstruction { public RequestPart[] parts; }
        [Serializable] private sealed class GenerationConfig { public string responseMimeType = "application/json"; public float temperature = 0.7f; }
        [Serializable] private sealed class RequestBody { public SystemInstruction systemInstruction; public RequestContent[] contents; public GenerationConfig generationConfig; }
        [Serializable] private sealed class CandidatePart { public string text; public bool thought; }
        [Serializable] private sealed class CandidateContent { public CandidatePart[] parts; }
        [Serializable] private sealed class Candidate { public CandidateContent content; }
        [Serializable] private sealed class GeminiResponse { public Candidate[] candidates; }

        private const float MinAttemptSeconds = 1.2f;

        private readonly ElenaConfig _config;
        private readonly string _apiKey;

        /// <summary>Modelo que respondió la última vez y su latencia (diagnóstico; nunca incluye la clave).</summary>
        public string LastModel { get; private set; } = string.Empty;
        public float LastLatency { get; private set; }

        public GeminiProvider(ElenaConfig config, string apiKey)
        {
            _config = config;
            _apiKey = apiKey;
        }

        public async Awaitable<ElenaResponse> RequestAsync(ElenaRequestContext context, List<ChatTurn> history)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Debug.LogWarning("[GeminiProvider] ApiKey vacía o nula. Abortando solicitud remota.");
                return null;
            }

            byte[] baseBody = null;
            float budget = _config != null ? Mathf.Max(1f, _config.TimeoutSeconds) : 5f;
            float start = Time.realtimeSinceStartup;

            var models = new List<string>(Models());
            for (int i = 0; i < models.Count; i++)
            {
                string model = models[i];
                float remaining = budget - (Time.realtimeSinceStartup - start);
                if (remaining < MinAttemptSeconds) break;
                // Con reservas pendientes, el intento no se come todo el plazo (deja sitio al siguiente modelo)
                float attempt = i < models.Count - 1 ? Mathf.Min(remaining, budget * 0.6f) : remaining;

                string json = BuildBody(context, history);
                json = GeminiThinking.Inject(json, GeminiThinking.ConfigFragment(model, _config != null ? _config.ThinkingLevel : "low"));
                baseBody = Encoding.UTF8.GetBytes(json);

                (ElenaResponse response, long code, bool timedOut) = await SendAsync(model, baseBody, attempt);
                if (response != null)
                {
                    LastModel = model;
                    LastLatency = Time.realtimeSinceStartup - start;
                    Debug.Log($"[GeminiProvider] Respuesta de {model} en {LastLatency:0.00} s");
                    return response;
                }
                if (!GeminiThinking.ShouldTryNextModel(code, timedOut)) break;
            }
            return null;
        }

        private IEnumerable<string> Models()
        {
            yield return _config != null ? _config.ModelName : ElenaConfig.DefaultDialogModel;
            if (_config == null) yield break;
            foreach (string m in _config.FallbackModelNames)
                if (!string.IsNullOrWhiteSpace(m) && m.Trim() != _config.ModelName) yield return m.Trim();
        }

        private string BuildBody(ElenaRequestContext context, List<ChatTurn> history)
        {
            var contentsList = new List<RequestContent>();
            if (history != null)
            {
                foreach (ChatTurn turn in history)
                {
                    contentsList.Add(new RequestContent
                    {
                        role = turn.role == "user" ? "user" : "model",
                        parts = new[] { new RequestPart { text = turn.text } }
                    });
                }
            }

            // Turno actual enriquecido con el contexto diegético
            string contextJson = JsonUtility.ToJson(context);
            contentsList.Add(new RequestContent
            {
                role = "user",
                parts = new[] { new RequestPart { text = $"CONTEXTO_JSON: {contextJson}\nPREGUNTA_JUGADOR: {context.player_text}" } }
            });

            var body = new RequestBody
            {
                contents = contentsList.ToArray(),
                generationConfig = new GenerationConfig { responseMimeType = "application/json", temperature = _config != null ? _config.Temperature : 0.7f }
            };
            if (_config != null && _config.SystemPrompt != null)
            {
                body.systemInstruction = new SystemInstruction { parts = new[] { new RequestPart { text = _config.SystemPrompt.text } } };
            }
            return JsonUtility.ToJson(body);
        }

        private async Awaitable<(ElenaResponse, long, bool)> SendAsync(string model, byte[] body, float seconds)
        {
            string url = _config != null ? _config.BuildEndpointUrl(model) : string.Format(ElenaConfig.DefaultEndpoint, model);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", _apiKey);
            request.timeout = Mathf.Max(1, Mathf.FloorToInt(seconds));

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                bool timedOut = request.responseCode == 0;
                // Solo código y tipo de error en el log (el cuerpo puede repetir datos de la petición)
                Debug.LogWarning($"[GeminiProvider] {model}: HTTP {request.responseCode} ({request.result}){(timedOut ? " sin respuesta a tiempo" : "")}");
                return (null, request.responseCode, timedOut);
            }

            string extractedText = ExtractText(request.downloadHandler.text);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                Debug.LogWarning($"[GeminiProvider] {model}: respuesta sin texto.");
                return (null, request.responseCode, false);
            }

            if (!ElenaJson.TryParse(extractedText, out ElenaResponse elenaResponse, out string parseError))
            {
                Debug.LogWarning($"[GeminiProvider] {model}: JSON de Elena no válido ({parseError}).");
                return (null, request.responseCode, false);
            }

            // Nunca ids internos ni spoilers del giro: si el texto no es seguro, entra el respaldo offline
            elenaResponse.dialogue = ElenaReplyGuard.Clamp(HudTextSanitizer.Humanize(elenaResponse.dialogue));
            if (!ElenaReplyGuard.IsSafe(elenaResponse.dialogue))
            {
                Debug.LogWarning($"[GeminiProvider] {model}: respuesta descartada por el filtro de personaje/spoiler.");
                return (null, request.responseCode, false);
            }
            return (elenaResponse, request.responseCode, false);
        }

        /// <summary>Texto de la respuesta (sin las partes de razonamiento, si las hubiera).</summary>
        private static string ExtractText(string json)
        {
            GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(json);
            CandidatePart[] parts = response?.candidates != null && response.candidates.Length > 0 ? response.candidates[0]?.content?.parts : null;
            if (parts == null) return null;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i] != null && !parts[i].thought && !string.IsNullOrWhiteSpace(parts[i].text)) return parts[i].text;
            }
            return null;
        }
    }
}
