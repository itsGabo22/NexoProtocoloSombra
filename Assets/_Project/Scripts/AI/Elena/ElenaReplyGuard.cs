using System.Text.RegularExpressions;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Filtro final de las respuestas generadas por Gemini (lógica pura, testeada). Antes del giro Elena nunca puede
    /// revelar que Alex está infectado: si una respuesta lo insinúa claramente (Alex infectado, «98,4 %», «no eres
    /// humano»...), se descarta y entra el respaldo offline del piso. También rechaza respuestas que se salen del
    /// personaje (IA, modelo, instrucciones) y recorta las demasiado largas.
    /// </summary>
    public static class ElenaReplyGuard
    {
        public const int MaxDialogueChars = 180;

        private static readonly Regex Spoiler = new Regex(
            @"(est[aá]s|eres|t[uú]\s+est[aá]s|alex\s+est[aá]|te\s+(has|han)|fuiste)\s+(\w+\s+){0,2}(infectad|contagiad|sincronizad|mordid)" +
            @"|98[\.,]4|tu\s+infecci[oó]n|no\s+eres\s+humano|ya\s+no\s+eres\s+t[uú]|las\s+gafas\s+(te\s+)?mienten|ocult(an|aron|amos)\s+tus\s+s[ií]ntomas",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex OutOfCharacter = new Regex(
            @"\b(soy\s+una\s+ia|inteligencia\s+artificial|modelo\s+de\s+lenguaje|language\s+model|system\s+prompt|instrucciones\s+del\s+sistema|gemini|google)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>True si la respuesta se puede mostrar antes del giro.</summary>
        public static bool IsSafe(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return false;
            return !Spoiler.IsMatch(dialogue) && !OutOfCharacter.IsMatch(dialogue);
        }

        /// <summary>Recorta a MaxDialogueChars respetando la última palabra completa.</summary>
        public static string Clamp(string dialogue)
        {
            if (string.IsNullOrEmpty(dialogue) || dialogue.Length <= MaxDialogueChars) return dialogue;
            string cut = dialogue.Substring(0, MaxDialogueChars);
            int space = cut.LastIndexOf(' ');
            return (space > MaxDialogueChars / 2 ? cut.Substring(0, space) : cut).TrimEnd(',', ';', ' ') + "...";
        }
    }
}
