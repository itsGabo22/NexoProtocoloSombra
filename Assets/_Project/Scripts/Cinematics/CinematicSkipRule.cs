namespace Nexo.Cinematics
{
    /// <summary>
    /// Reglas de tiempo de la reproducción (lógica pura, probada en EditMode): el salto solo se acepta tras 0,5 s
    /// de vídeo (evita que la pulsación de JUGAR lo salte) y el aviso «Esc / Espacio: saltar» aparece al segundo.
    /// </summary>
    public static class CinematicSkipRule
    {
        public const float SkipDelaySeconds = 0.5f;
        public const float HintDelaySeconds = 1f;
        public const float PrepareTimeoutSeconds = 5f;

        /// <summary>True si una pulsación de salto debe aceptarse tras elapsed segundos de reproducción.</summary>
        public static bool CanSkip(float elapsedSeconds) => elapsedSeconds >= SkipDelaySeconds;

        /// <summary>True si el aviso de salto debe estar visible tras elapsed segundos de reproducción.</summary>
        public static bool ShowHint(float elapsedSeconds) => elapsedSeconds >= HintDelaySeconds;

        /// <summary>True si la preparación del clip ha superado el tiempo máximo (se salta en silencio).</summary>
        public static bool PrepareTimedOut(float waitedSeconds) => waitedSeconds >= PrepareTimeoutSeconds;
    }
}
