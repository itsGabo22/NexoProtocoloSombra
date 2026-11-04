using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Síntesis de voz con Gemini TTS (REST, responseModalities AUDIO). En juego solo se usa para respuestas dinámicas
    /// de Elena que no tienen clip pregrabado, con timeout corto; el Editor la usa para pregenerar todas las líneas fijas.
    /// La clave viaja en la cabecera x-goog-api-key y nunca se escribe en el log.
    /// </summary>
    public static class GeminiTtsClient
    {
        [Serializable] private sealed class Part { public string text; }
        [Serializable] private sealed class Content { public Part[] parts; }
        [Serializable] private sealed class PrebuiltVoiceConfig { public string voiceName; }
        [Serializable] private sealed class VoiceConfig { public PrebuiltVoiceConfig prebuiltVoiceConfig; }
        [Serializable] private sealed class SpeechConfig { public VoiceConfig voiceConfig; }
        [Serializable] private sealed class GenerationConfig { public string[] responseModalities; public SpeechConfig speechConfig; }
        [Serializable] private sealed class Body { public Content[] contents; public GenerationConfig generationConfig; }

        [Serializable] private sealed class InlineData { public string mimeType; public string data; }
        [Serializable] private sealed class ResponsePart { public InlineData inlineData; }
        [Serializable] private sealed class ResponseContent { public ResponsePart[] parts; }
        [Serializable] private sealed class Candidate { public ResponseContent content; }
        [Serializable] private sealed class Response { public Candidate[] candidates; }

        /// <summary>Cuerpo JSON de la petición TTS (estilo por emoción + línea).</summary>
        public static string BuildBody(string emotion, string line, string voice) => BuildBodyRaw(ElenaVoiceStyle.Prompt(emotion, line), voice);

        /// <summary>Cuerpo JSON con el texto tal cual (sin indicación de estilo).</summary>
        public static string BuildBodyRaw(string text, string voice)
        {
            var body = new Body
            {
                contents = new[] { new Content { parts = new[] { new Part { text = text } } } },
                generationConfig = new GenerationConfig
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new SpeechConfig { voiceConfig = new VoiceConfig { prebuiltVoiceConfig = new PrebuiltVoiceConfig { voiceName = voice } } }
                }
            };
            return JsonUtility.ToJson(body);
        }

        /// <summary>Extrae el audio (bytes y tipo MIME) de la respuesta JSON; false si no hay audio.</summary>
        public static bool TryParseAudio(string json, out byte[] audio, out string mimeType)
        {
            audio = null;
            mimeType = null;
            if (string.IsNullOrEmpty(json)) return false;
            Response response;
            try { response = JsonUtility.FromJson<Response>(json); }
            catch (ArgumentException) { return false; }
            ResponsePart[] parts = response?.candidates != null && response.candidates.Length > 0 ? response.candidates[0]?.content?.parts : null;
            if (parts == null) return false;
            foreach (ResponsePart part in parts)
            {
                if (part?.inlineData == null || string.IsNullOrEmpty(part.inlineData.data)) continue;
                try { audio = Convert.FromBase64String(part.inlineData.data); }
                catch (FormatException) { return false; }
                mimeType = part.inlineData.mimeType;
                return true;
            }
            return false;
        }

        /// <summary>Clip de voz para una respuesta dinámica, o null si no hay clave, falla la red o no llega a tiempo.</summary>
        public static async Awaitable<AudioClip> SynthesizeAsync(ElenaConfig config, string line, string emotion)
        {
            if (config == null || config.Secrets == null || !config.Secrets.HasApiKey || string.IsNullOrWhiteSpace(line)) return null;

            string url = config.BuildEndpointUrl(config.TtsModel);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(BuildBody(emotion, line, config.TtsVoice)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", config.Secrets.ApiKey);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(config.TtsTimeoutSeconds));

            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"[GeminiTtsClient] Voz dinámica no disponible (HTTP {request.responseCode}); solo subtítulo.");
                return null;
            }

            if (!TryParseAudio(request.downloadHandler.text, out byte[] audio, out string mime) ||
                !VoicePcm.TryDecode(audio, mime, out float[] samples, out int rate)) return null;

            samples = VoicePcm.CompressSilence(samples, rate);
            if (samples.Length == 0) return null;
            VoicePcm.Normalize(samples);
            AudioClip clip = AudioClip.Create("elena_dynamic", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
