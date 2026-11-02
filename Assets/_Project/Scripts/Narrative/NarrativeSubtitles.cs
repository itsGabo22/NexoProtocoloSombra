using Nexo.Core;

namespace Nexo.Narrative
{
    /// <summary>
    /// Subtítulos narrativos a través de la API existente GameEvents.RaiseSubtitle(string).
    /// SubtitleController solo recibe texto (calcula él la duración), así que el hablante va dentro: "ELENA: línea".
    /// </summary>
    public static class NarrativeSubtitles
    {
        public const string ElenaSpeaker = "ELENA";

        public static string Format(string speaker, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;
            return string.IsNullOrWhiteSpace(speaker) ? line.Trim() : $"{speaker.Trim()}: {line.Trim()}";
        }

        /// <summary>Muestra una línea con hablante; las líneas vacías se ignoran.</summary>
        public static void Say(string speaker, string line)
        {
            string text = Format(speaker, line);
            if (text.Length > 0) GameEvents.RaiseSubtitle(text);
        }

        /// <summary>Oculta el subtítulo actual (SubtitleController oculta la etiqueta con texto vacío).</summary>
        public static void Clear()
        {
            GameEvents.RaiseSubtitle(string.Empty);
        }
    }
}
