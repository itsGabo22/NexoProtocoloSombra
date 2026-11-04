namespace Nexo.UI
{
    /// <summary>
    /// Prioridad de un subtítulo (lógica pura): los avisos de daño y agarre interrumpen la línea actual,
    /// el resto espera su turno en la cola.
    /// </summary>
    public static class SubtitlePriority
    {
        public const int Normal = 0;
        public const int Urgent = 2;

        private static readonly string[] UrgentPrefixes =
        {
            "(Te golpean", "(Las heridas", "(Caes", "(¡Te agarra", "(¡Algo te agarra", "(Lo empujas", "(Te suelta"
        };

        public static int Classify(string text)
        {
            if (string.IsNullOrEmpty(text)) return Normal;
            foreach (string prefix in UrgentPrefixes)
            {
                if (text.StartsWith(prefix, System.StringComparison.Ordinal)) return Urgent;
            }
            return Normal;
        }
    }
}
