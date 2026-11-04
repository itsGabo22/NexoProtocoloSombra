using System;
using System.Collections.Generic;

namespace Nexo.AI.Elena
{
    /// <summary>Orden de modelos Gemini a probar (lógica pura, sin vacíos ni duplicados).</summary>
    public static class GeminiModelOrder
    {
        /// <summary>Diálogo: el principal y luego las reservas.</summary>
        public static List<string> Dialog(string primary, string[] fallbacks)
        {
            var list = new List<string>();
            Add(list, primary);
            if (fallbacks != null) foreach (string m in fallbacks) Add(list, m);
            return list;
        }

        /// <summary>
        /// Transcripción de voz: el modelo de transcripción (por defecto gemini-2.5-flash, el que responde de forma fiable)
        /// y luego las reservas, sin vacíos ni duplicados.
        /// </summary>
        public static List<string> Speech(string speechModel, string[] fallbacks) => Dialog(speechModel, fallbacks);

        private static void Add(List<string> list, string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return;
            string m = model.Trim();
            foreach (string existing in list) if (string.Equals(existing, m, StringComparison.Ordinal)) return;
            list.Add(m);
        }
    }
}
