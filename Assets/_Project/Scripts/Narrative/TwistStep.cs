namespace Nexo.Narrative
{
    /// <summary>
    /// Paso de la línea de tiempo del giro: empieza en Offset (segundos desde el inicio) y dura Duration.
    /// Tipo puro de C# (sin UnityEngine) para poder probarlo en EditMode.
    /// </summary>
    public readonly struct TwistStep
    {
        public TwistStep(float offset, TwistAction action, float duration = 0f, int lineIndex = -1)
        {
            Offset = offset;
            Action = action;
            Duration = duration;
            LineIndex = lineIndex;
        }

        public float Offset { get; }
        public TwistAction Action { get; }
        public float Duration { get; }

        /// <summary>Índice de la línea de Elena (solo para TwistAction.ElenaLine; -1 en el resto).</summary>
        public int LineIndex { get; }

        public float End => Offset + Duration;

        public TwistStep WithDuration(float duration)
        {
            return new TwistStep(Offset, Action, duration, LineIndex);
        }

        public override string ToString()
        {
            return $"{Offset:0.##}s {Action}{(LineIndex >= 0 ? "#" + LineIndex : string.Empty)} ({Duration:0.##}s)";
        }
    }
}
