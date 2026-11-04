using System;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Línea de subtítulo de una cinemática (formato de &lt;id&gt;.captions.json: start y end en segundos de vídeo).
    /// Campos públicos en minúscula para que JsonUtility lea el JSON tal cual.
    /// </summary>
    [Serializable]
    public sealed class CinematicCaption
    {
        public float start;
        public float end;
        public string speaker;
        public string text;

        /// <summary>True si la línea tiene texto y un intervalo válido.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(text) && end > start && start >= 0f;

        /// <summary>True si el instante (segundos de vídeo) cae dentro de [start, end).</summary>
        public bool Contains(double time) => time >= start && time < end;
    }
}
