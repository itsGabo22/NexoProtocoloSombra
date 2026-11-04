namespace Nexo.AI.Elena
{
    /// <summary>
    /// Indicación de estilo para la síntesis de voz de Elena según su emoción (lógica pura, testeada).
    /// Se escribe en inglés y en forma corta («Say softly, like a young girl: …»): con indicaciones largas o en
    /// español el modelo TTS llega a leerlas en voz alta, y con susurros largos alarga el clip con pausas.
    /// </summary>
    public static class ElenaVoiceStyle
    {
        public static string Prefix(string emotion)
        {
            switch ((emotion ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "URGENT": return "Say urgently, like a frightened young girl";
                case "SCARED": return "Say nervously, like a frightened young girl";
                case "SAD": return "Say sadly, like a young girl about to cry";
                case "WHISPER": return "Whisper quickly, like a frightened young girl";
                case "HAPPY": return "Say with relief, like a young girl";
                default: return "Say softly and gently, like a young girl";
            }
        }

        /// <summary>Texto completo que se envía al modelo TTS.</summary>
        public static string Prompt(string emotion, string line) => $"{Prefix(emotion)}: {line}";

        /// <summary>
        /// True si una transcripción del clip contiene la indicación (el modelo la leyó en voz alta) y hay que regenerarlo.
        /// </summary>
        public static bool TranscriptLeaksStyle(string transcript)
        {
            if (string.IsNullOrEmpty(transcript)) return false;
            string t = transcript.ToLowerInvariant();
            return t.Contains("young girl") || t.Contains("like a") || t.Contains("say softly") || t.Contains("whisper") ||
                   t.Contains("niña pequeña") || t.Contains("con voz");
        }
    }
}
