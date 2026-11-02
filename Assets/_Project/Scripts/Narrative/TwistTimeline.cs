using System;
using System.Collections.Generic;

namespace Nexo.Narrative
{
    /// <summary>
    /// Línea de tiempo del giro final como clase pura de C# (sin dependencias de UnityEngine), probada en EditMode.
    /// Ordena los pasos por Offset de forma estable (los empates conservan el orden de entrada), calcula la duración
    /// total (el final más tardío) y, con reducir movimiento, deja la rampa de luces en duración 0 sin alterar el orden.
    /// Los Offset o Duration negativos se RECHAZAN con ArgumentOutOfRangeException (no se recortan en silencio).
    /// </summary>
    public sealed class TwistTimeline
    {
        public const float HudCorruptionOffset = 0.8f;
        public const float LightRampOffset = 2.0f;
        public const float FirstLineOffset = 3.5f;

        private readonly List<TwistStep> _steps;

        public TwistTimeline(IEnumerable<TwistStep> steps, bool reduceMotion)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            _steps = new List<TwistStep>();
            foreach (TwistStep step in steps)
            {
                if (step.Offset < 0f) throw new ArgumentOutOfRangeException(nameof(steps), $"Offset negativo en {step}.");
                if (step.Duration < 0f) throw new ArgumentOutOfRangeException(nameof(steps), $"Duración negativa en {step}.");

                TwistStep effective = reduceMotion && step.Action == TwistAction.LightRamp ? step.WithDuration(0f) : step;
                InsertStable(effective);
            }

            float total = 0f;
            foreach (TwistStep step in _steps)
            {
                if (step.End > total) total = step.End;
            }
            TotalDuration = total;
        }

        /// <summary>Pasos ordenados por Offset.</summary>
        public IReadOnlyList<TwistStep> Steps => _steps;

        /// <summary>Segundos desde el inicio hasta el final del último paso (0 si no hay pasos).</summary>
        public float TotalDuration { get; }

        /// <summary>
        /// Secuencia del juego: t=0 giro, +0.8 s corrupción del HUD, +2.0 s rampa de luces, +3.5 s líneas de Elena
        /// (una cada secondsPerLine) y créditos creditsDelay segundos después de la última línea.
        /// </summary>
        public static TwistTimeline CreateDefault(int lineCount, float secondsPerLine, float lightRampSeconds, float creditsDelay, bool reduceMotion)
        {
            lineCount = Math.Max(0, lineCount);
            secondsPerLine = Math.Max(0f, secondsPerLine);
            float lastLineEnd = FirstLineOffset + lineCount * secondsPerLine;

            var steps = new List<TwistStep>
            {
                new TwistStep(0f, TwistAction.StartTwist),
                new TwistStep(HudCorruptionOffset, TwistAction.HudCorruption),
                new TwistStep(LightRampOffset, TwistAction.LightRamp, Math.Max(0f, lightRampSeconds))
            };
            for (int i = 0; i < lineCount; i++)
            {
                steps.Add(new TwistStep(FirstLineOffset + i * secondsPerLine, TwistAction.ElenaLine, secondsPerLine, i));
            }
            steps.Add(new TwistStep(lastLineEnd + Math.Max(0f, creditsDelay), TwistAction.ShowCredits));

            return new TwistTimeline(steps, reduceMotion);
        }

        private void InsertStable(TwistStep step)
        {
            int index = _steps.Count;
            while (index > 0 && _steps[index - 1].Offset > step.Offset)
            {
                index--;
            }
            _steps.Insert(index, step);
        }
    }
}
