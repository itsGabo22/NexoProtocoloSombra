using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nexo.AI.Elena
{
    public interface IElenaProvider
    {
        Awaitable<ElenaResponse> RequestAsync(ElenaRequestContext context, List<ChatTurn> history);
    }

    /// <summary>
    /// Cliente HTTP con Gemini Flash (2.5) vía UnityWebRequest.
    /// Envía el System Prompt, historial y contexto diegético, exigiendo respuesta JSON con timeout estricto.
    /// </summary>
    public sealed class GeminiProvider : IElenaProvider
    {
        [Serializable]
        private sealed class RequestPart
        {
            public string text;
        }

        [Serializable]
        private sealed class RequestContent
        {
            public string role;
            public RequestPart[] parts;
        }

        [Serializable]
        private sealed class SystemInstruction
        {
            public RequestPart[] parts;
        }

        [Serializable]
        private sealed class GenerationConfig
        {
            public string responseMimeType = "application/json";
            public float temperature = 0.7f;
        }

        [Serializable]
        private sealed class RequestBody
        {
            public SystemInstruction systemInstruction;
            public RequestContent[] contents;
            public GenerationConfig generationConfig;
        }

        [Serializable]
        private sealed class CandidatePart
        {
            public string text;
        }

        [Serializable]
        private sealed class CandidateContent
        {
            public CandidatePart[] parts;
        }

        [Serializable]
        private sealed class Candidate
        {
            public CandidateContent content;
        }

        [Serializable]
        private sealed class GeminiResponse
        {
            public Candidate[] candidates;
        }

        private readonly ElenaConfig _config;
        private readonly string _apiKey;

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

            var contentsList = new List<RequestContent>();

            // Añadir turnos históricos
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

            // Construir turno actual del usuario enriquecido con el contexto diegético
            string contextJson = JsonUtility.ToJson(context);
            string promptText = $"CONTEXTO_JSON: {contextJson}\nPREGUNTA_JUGADOR: {context.player_text}";

            contentsList.Add(new RequestContent
            {
                role = "user",
                parts = new[] { new RequestPart { text = promptText } }
            });

            var body = new RequestBody
            {
                contents = contentsList.ToArray(),
                generationConfig = new GenerationConfig
                {
                    responseMimeType = "application/json",
                    temperature = _config != null ? _config.Temperature : 0.7f
                }
            };

            if (_config != null && _config.SystemPrompt != null)
            {
                body.systemInstruction = new SystemInstruction
                {
                    parts = new[] { new RequestPart { text = _config.SystemPrompt.text } }
                };
            }

            string jsonBody = JsonUtility.ToJson(body);
            string url = _config != null ? _config.BuildEndpointUrl() : "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", _apiKey);
            request.timeout = (int)(_config != null ? _config.TimeoutSeconds : 6f);

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GeminiProvider] HTTP {request.responseCode}: {request.error}");
                return null;
            }

            string responseJson = request.downloadHandler.text;
            GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseJson);

            string extractedText = null;
            if (geminiResponse?.candidates != null && geminiResponse.candidates.Length > 0)
            {
                var candidateParts = geminiResponse.candidates[0]?.content?.parts;
                if (candidateParts != null && candidateParts.Length > 0)
                {
                    extractedText = candidateParts[0].text;
                }
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                Debug.LogWarning("[GeminiProvider] No se encontró texto de respuesta en el candidato de Gemini.");
                return null;
            }

            if (ElenaJson.TryParse(extractedText, out ElenaResponse elenaResponse, out string parseError))
            {
                return elenaResponse;
            }

            Debug.LogWarning($"[GeminiProvider] Error parseando JSON de Elena: {parseError}");
            return null;
        }
    }
}
