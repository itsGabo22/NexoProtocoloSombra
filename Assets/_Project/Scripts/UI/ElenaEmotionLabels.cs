using Nexo.AI.Elena;

namespace Nexo.UI
{
    /// <summary>
    /// Etiquetas en español de la emoción de Elena para el panel de radio del HUD (lógica pura, probada en EditMode).
    /// Evita mostrar los nombres del enum en inglés (CALM, WHISPER…).
    /// </summary>
    public static class ElenaEmotionLabels
    {
        /// <summary>Texto en mayúsculas para la etiqueta radio-emotion.</summary>
        public static string ToSpanish(ElenaEmotion emotion)
        {
            switch (emotion)
            {
                case ElenaEmotion.Happy: return "ALEGRE";
                case ElenaEmotion.Scared: return "ASUSTADA";
                case ElenaEmotion.Urgent: return "URGENTE";
                case ElenaEmotion.Sad: return "TRISTE";
                case ElenaEmotion.Whisper: return "SUSURRO";
                default: return "CALMA";
            }
        }
    }
}
