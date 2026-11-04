namespace Nexo.AI.Elena
{
    /// <summary>
    /// thinkingConfig de la API REST de Gemini (lógica pura, testeada). Medido el 2026-11-04 con la clave del proyecto:
    /// - gemini-3.8-flash: «minimal» → 400 (no soportado); «low» es el nivel más bajo válido.
    /// - gemini-3.x: thinkingBudget 0 → 400; se usa thinkingLevel.
    /// - gemini-2.5-flash: thinkingBudget 0 desactiva el razonamiento.
    /// Sin esta opción gemini-3.8-flash gasta ~256 tokens pensando y tarda ~4,6 s en una respuesta de 8 tokens.
    /// </summary>
    public static class GeminiThinking
    {
        /// <summary>Fragmento JSON «"thinkingConfig":{...}» para el modelo, o cadena vacía si no se envía.</summary>
        public static string ConfigFragment(string model, string level)
        {
            if (string.IsNullOrWhiteSpace(model)) return string.Empty;
            string m = model.Trim().ToLowerInvariant();
            if (m.Contains("tts") || m.Contains("image")) return string.Empty;
            if (m.StartsWith("gemini-2.5-flash") || m.StartsWith("gemini-2.0")) return "\"thinkingConfig\":{\"thinkingBudget\":0}";
            if (string.IsNullOrWhiteSpace(level)) return string.Empty;
            return "\"thinkingConfig\":{\"thinkingLevel\":\"" + level.Trim().ToLowerInvariant() + "\"}";
        }

        /// <summary>Inserta el fragmento al principio de «"generationConfig":{» en un JSON de JsonUtility (sin espacios).</summary>
        public static string Inject(string json, string fragment)
        {
            const string anchor = "\"generationConfig\":{";
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fragment)) return json;
            int i = json.IndexOf(anchor, System.StringComparison.Ordinal);
            if (i < 0) return json;
            int at = i + anchor.Length;
            bool empty = at < json.Length && json[at] == '}';
            return json.Substring(0, at) + fragment + (empty ? "" : ",") + json.Substring(at);
        }

        /// <summary>Errores que justifican probar el modelo de reserva (sin cuota, saturado, retirado o caído).</summary>
        public static bool ShouldTryNextModel(long httpCode, bool timedOut)
        {
            return timedOut || httpCode == 0 || httpCode == 404 || httpCode == 429 || httpCode >= 500;
        }
    }
}
