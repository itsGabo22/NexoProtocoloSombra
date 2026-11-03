using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.UI
{
    /// <summary>
    /// Sustituye en el momento de mostrar los ids internos (F12_Door_Stairs, F01_BioScanner…) por nombres en español
    /// (lógica pura, sin UnityEngine, probada en EditMode). Se aplica a todo texto visible del HUD sea cual sea su origen
    /// (código, escenas, JSON de Elena, Gemini); nunca modifica los datos ni los eventos originales.
    /// </summary>
    public static class HudTextSanitizer
    {
        // Id interno: «F12_…» / «F6_…» o cualquier «…_Door_…». Un «Puerta »/«puerta » delante se trata aparte
        private static readonly Regex IdPattern = new Regex(
            @"(?<noun>\b[Pp]uerta[ \t]+)?(?<id>\b(?:F\d{1,2}_\w+|\w+_Door_\w+)\b)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Devuelve el texto con los ids sustituidos. «Puerta F12_Door_Stairs desbloqueada» → «Puerta de las escaleras desbloqueada»;
        /// «Acceso denegado: F12_Door_Stairs requiere…» → «Acceso denegado: la puerta de las escaleras requiere…».
        /// Puerta desconocida → «una puerta»; otro id desconocido → «ese lugar». Sin ids, devuelve el mismo texto.
        /// </summary>
        public static string Humanize(string text)
        {
            // Atajo sin asignaciones: todo id interno contiene «_»
            if (string.IsNullOrEmpty(text) || text.IndexOf('_') < 0) return text;

            MatchCollection matches = IdPattern.Matches(text);
            if (matches.Count == 0) return text;

            var builder = new StringBuilder(text.Length + 32);
            int cursor = 0;
            foreach (Match match in matches)
            {
                builder.Append(text, cursor, match.Index - cursor);
                builder.Append(Replacement(text, match));
                cursor = match.Index + match.Length;
            }
            builder.Append(text, cursor, text.Length - cursor);
            return builder.ToString();
        }

        private static string Replacement(string text, Match match)
        {
            string id = match.Groups["id"].Value;
            Group noun = match.Groups["noun"];

            if (noun.Success && DoorDisplayNames.IsDoorId(id))
            {
                // «Puerta F12_Door_Stairs» → «Puerta de las escaleras»; puerta desconocida → solo «Puerta»
                string nounWord = noun.Value.TrimEnd();
                string qualifier = DoorDisplayNames.DoorQualifier(id);
                return qualifier.Length > 0 ? nounWord + " " + qualifier : nounWord;
            }

            string name = DoorDisplayNames.Destination(id);
            if (IsSentenceStart(text, match.Groups["id"].Index)) name = Capitalize(name);
            return noun.Success ? noun.Value + name : name;
        }

        /// <summary>True si antes del índice solo hay espacios/aperturas («¡¿"») desde el inicio o desde un final de frase.</summary>
        private static bool IsSentenceStart(string text, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '¡' || c == '¿' || c == '"' || c == '«' || c == '(') continue;
                return c == '.' || c == '!' || c == '?' || c == '\n';
            }
            return true;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
