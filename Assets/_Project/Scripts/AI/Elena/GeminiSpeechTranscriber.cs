using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Transcribe la voz del jugador con Gemini (entrada de audio WAV). Se usa cuando el PC no tiene el
    /// reconocimiento de voz de Windows. Modelo de transcripción, endpoint, razonamiento mínimo y clave de ElenaConfig;
    /// si el modelo está saturado (503/429/404) prueba los de reserva dentro del mismo plazo.
    /// </summary>
    public static class GeminiSpeechTranscriber
    {
        private const string Prompt = "Transcribe exactamente lo que dice la persona en este audio. El idioma es español (es-ES / es-MX / es-CO). " +
                                      "Responde solo con el texto transcrito, sin comillas ni comentarios. Si no se entiende nada, responde vacío.";

        [Serializable] private sealed class Part { public string text; public bool thought; }
        [Serializable] private sealed class Content { public Part[] parts; }
        [Serializable] private sealed class Candidate { public Content content; }
        [Serializable] private sealed class Response { public Candidate[] candidates; }

        /// <summary>Cuerpo JSON de la petición (audio en base64 + instrucción + razonamiento mínimo del modelo).</summary>
        public static string BuildBody(string model, string thinkingLevel, string base64Wav)
        {
            string body = "{\"contents\":[{\"parts\":[{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + base64Wav +
                          "\"}},{\"text\":\"" + Prompt + "\"}]}],\"generationConfig\":{\"temperature\":0}}";
            return GeminiThinking.Inject(body, GeminiThinking.ConfigFragment(model, thinkingLevel));
        }

        /// <summary>Devuelve el texto reconocido o null si no hay API key, falla la red o no se entendió nada.</summary>
        public static async Awaitable<string> TranscribeAsync(ElenaConfig config, float[] samples, int sampleRate)
        {
            if (config == null || config.Secrets == null || !config.Secrets.HasApiKey || samples == null || samples.Length == 0) return null;

            string audio = Convert.ToBase64String(VoicePcm.EncodeWav(samples, sampleRate));
            // Primero el modelo de transcripción (por defecto gemini-2.5-flash, fiable en ~1-2 s) y luego las reservas
            List<string> models = GeminiModelOrder.Speech(config.SpeechToTextModel, config.FallbackModelNames);

            // Subir el audio tarda más que un texto: el plazo total es el de Elena + 2 s
            float budget = config.TimeoutSeconds + 2f;
            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < models.Count; i++)
            {
                float remaining = budget - (Time.realtimeSinceStartup - start);
                if (remaining < 1.5f) break;
                // El audio tarda algo más en subir que un texto: +1,5 s sobre el plazo por modelo
                float attempt = i < models.Count - 1 ? Mathf.Min(remaining, config.PerModelTimeoutSeconds + 1.5f) : remaining;

                using var request = new UnityWebRequest(config.BuildEndpointUrl(models[i]), UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(BuildBody(models[i], config.ThinkingLevel, audio)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-goog-api-key", config.Secrets.ApiKey);
                request.timeout = Mathf.Max(1, Mathf.FloorToInt(attempt));

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[GeminiSpeechTranscriber] {models[i]}: HTTP {request.responseCode} ({request.result})");
                    if (GeminiThinking.ShouldTryNextModel(request.responseCode, request.responseCode == 0)) continue;
                    return null;
                }

                Response response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                Part[] parts = response?.candidates != null && response.candidates.Length > 0 ? response.candidates[0]?.content?.parts : null;
                if (parts == null) return null;
                for (int p = parts.Length - 1; p >= 0; p--)
                {
                    if (parts[p] != null && !parts[p].thought && !string.IsNullOrWhiteSpace(parts[p].text)) return parts[p].text.Trim();
                }
                return null;
            }
            return null;
        }
    }
}
