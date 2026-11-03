using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Transcribe la voz del jugador con Gemini (entrada de audio WAV). Se usa cuando el PC no tiene el
    /// reconocimiento de voz de Windows. Reutiliza modelo, endpoint, timeout y API key de ElenaConfig.
    /// </summary>
    public static class GeminiSpeechTranscriber
    {
        private const string Prompt = "Transcribe exactamente lo que dice la persona en este audio, en el idioma en que habla. " +
                                      "Responde solo con el texto transcrito, sin comillas ni comentarios. Si no se entiende nada, responde vacío.";

        [Serializable] private sealed class Part { public string text; }
        [Serializable] private sealed class Content { public Part[] parts; }
        [Serializable] private sealed class Candidate { public Content content; }
        [Serializable] private sealed class Response { public Candidate[] candidates; }

        /// <summary>Devuelve el texto reconocido o null si no hay API key, falla la red o no se entendió nada.</summary>
        public static async Awaitable<string> TranscribeAsync(ElenaConfig config, float[] samples, int sampleRate)
        {
            if (config == null || config.Secrets == null || !config.Secrets.HasApiKey || samples == null || samples.Length == 0) return null;

            string audio = Convert.ToBase64String(EncodeWav(samples, sampleRate));
            string body = "{\"contents\":[{\"parts\":[{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + audio +
                          "\"}},{\"text\":\"" + Prompt + "\"}]}]}";

            string url = string.Format(config.EndpointTemplate, config.ModelName);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", config.Secrets.ApiKey);
            request.timeout = Mathf.CeilToInt(config.TimeoutSeconds * 2f);

            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GeminiSpeechTranscriber] Error de transcripción: {request.error}");
                return null;
            }

            Response response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
            Part[] parts = response?.candidates != null && response.candidates.Length > 0 ? response.candidates[0]?.content?.parts : null;
            string text = parts != null && parts.Length > 0 ? parts[0].text : null;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static byte[] EncodeWav(float[] samples, int sampleRate)
        {
            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataBytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
            foreach (float s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
