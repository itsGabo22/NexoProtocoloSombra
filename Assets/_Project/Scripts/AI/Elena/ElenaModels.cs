using System;
using UnityEngine;

namespace Nexo.AI.Elena
{
    public enum ElenaEmotion
    {
        Calm,
        Happy,
        Scared,
        Urgent,
        Sad,
        Whisper
    }

    public enum ElenaUiAction
    {
        None,
        HighlightSafeRoute,
        PingEnemyRadar,
        UnlockDoor,
        CutLights,
        DimFlashlight,
        GlitchHud,
        FakeNotification
    }

    [Serializable]
    public sealed class ElenaResponse
    {
        public string dialogue = string.Empty;
        public string emotion = "calm";
        public string ui_action = "none";
        public string ui_parameter = string.Empty;
        public int trust_level = 50;

        public ElenaEmotion ParsedEmotion
        {
            get
            {
                if (string.IsNullOrWhiteSpace(emotion)) return ElenaEmotion.Calm;
                string clean = emotion.Trim().Replace("_", "");
                if (Enum.TryParse<ElenaEmotion>(clean, true, out ElenaEmotion result))
                {
                    return result;
                }
                return ElenaEmotion.Calm;
            }
        }

        public ElenaUiAction ParsedAction
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ui_action)) return ElenaUiAction.None;
                string clean = ui_action.Trim().Replace("_", "");
                if (Enum.TryParse<ElenaUiAction>(clean, true, out ElenaUiAction result))
                {
                    return result;
                }
                return ElenaUiAction.None;
            }
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(dialogue))
            {
                error = "El campo 'dialogue' no puede estar vacío.";
                return false;
            }

            if (dialogue.Length > 220)
            {
                dialogue = dialogue.Substring(0, 220);
            }

            trust_level = Mathf.Clamp(trust_level, 0, 100);

            // Validar que emotion sea conocida si fue especificada
            if (!string.IsNullOrWhiteSpace(emotion))
            {
                string cleanEmotion = emotion.Trim().Replace("_", "");
                if (!Enum.TryParse<ElenaEmotion>(cleanEmotion, true, out _))
                {
                    emotion = "calm";
                }
            }

            // Validar que ui_action sea conocida si fue especificada
            if (!string.IsNullOrWhiteSpace(ui_action))
            {
                string cleanAction = ui_action.Trim().Replace("_", "");
                if (!Enum.TryParse<ElenaUiAction>(cleanAction, true, out _))
                {
                    error = $"Acción de interfaz desconocida: {ui_action}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ElenaRequestContext
    {
        public int floor = 12;
        public string player_room = "F12_Office_Entrance";
        public float player_noise = 0f;
        public float player_battery = 1f;
        public int infected_nearby = 0;
        public int anomalies_detected = 0;
        public string[] available_doors = Array.Empty<string>();
        public int trust_level = 50;
        public string question_selected = string.Empty;
    }

    [Serializable]
    public sealed class ChatTurn
    {
        public string role = "user";
        public string text = string.Empty;

        public ChatTurn() { }

        public ChatTurn(string role, string text)
        {
            this.role = role;
            this.text = text;
        }
    }

    public static class ElenaJson
    {
        /// <summary>
        /// Extrae y deserializa un ElenaResponse de una cadena JSON, soportando bloques Markdown ```json.
        /// </summary>
        public static bool TryParse(string rawJson, out ElenaResponse response, out string parseError)
        {
            response = null;
            parseError = string.Empty;

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                parseError = "Respuesta vacía.";
                return false;
            }

            try
            {
                string text = rawJson.Trim();

                // Limpiar markdown si existe
                if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(7);
                }
                else if (text.StartsWith("```"))
                {
                    text = text.Substring(3);
                }

                if (text.EndsWith("```"))
                {
                    text = text.Substring(0, text.Length - 3);
                }

                text = text.Trim();

                // Extraer el objeto JSON delimitado por llaves {}
                int firstBrace = text.IndexOf('{');
                int lastBrace = text.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    text = text.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                response = JsonUtility.FromJson<ElenaResponse>(text);
                if (response == null)
                {
                    parseError = "JsonUtility retornó nulo.";
                    return false;
                }

                return response.IsValid(out parseError);
            }
            catch (Exception ex)
            {
                parseError = ex.Message;
                return false;
            }
        }
    }
}
