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
