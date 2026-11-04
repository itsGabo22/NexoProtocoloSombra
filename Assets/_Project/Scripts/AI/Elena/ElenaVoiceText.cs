using System.Globalization;
using System.Text;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Normaliza una línea de Elena para buscar su clip de voz (lógica pura, testeada): quita el prefijo
    /// «ELENA:», tildes, signos y mayúsculas, y colapsa espacios. Así «ELENA: ¡Corre, Alex!» y «corre alex» coinciden.
    /// </summary>
    public static class ElenaVoiceText
    {
        public const string SpeakerPrefix = "ELENA:";

        /// <summary>True si el subtítulo lo dice Elena (no la voz falsa «¿ELENA?:»).</summary>
        public static bool IsElenaSubtitle(string subtitle)
        {
            return subtitle != null && subtitle.StartsWith(SpeakerPrefix, System.StringComparison.Ordinal);
        }

        /// <summary>Texto hablado sin el prefijo del hablante.</summary>
        public static string StripSpeaker(string subtitle)
        {
            if (string.IsNullOrEmpty(subtitle)) return string.Empty;
            return IsElenaSubtitle(subtitle) ? subtitle.Substring(SpeakerPrefix.Length).Trim() : subtitle.Trim();
        }

        /// <summary>
        /// Fracción de las palabras de <paramref name="expected"/> que aparecen en <paramref name="heard"/> (0-1).
        /// Verifica que un trozo de audio corresponde a su línea.
        /// </summary>
        public static float WordOverlap(string expected, string heard)
        {
            string[] want = Normalize(expected).Split(' ');
            var got = new System.Collections.Generic.HashSet<string>(Normalize(heard).Split(' '));
            int total = 0, hits = 0;
            foreach (string w in want)
            {
                if (w.Length == 0) continue;
                total++;
                if (got.Contains(w)) hits++;
            }
            return total == 0 ? 0f : hits / (float)total;
        }

        public static string Normalize(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;
            string decomposed = StripSpeaker(line).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            bool space = false;
            foreach (char c in decomposed)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c))
                {
                    if (space && sb.Length > 0) sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(c));
                    space = false;
                }
                else
                {
                    space = true;
                }
            }
            return sb.ToString();
        }
    }
}
