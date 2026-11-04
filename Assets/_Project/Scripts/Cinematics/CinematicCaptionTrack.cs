using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Subtítulos de una cinemática (lógica pura, probada en EditMode). Lee un array JSON de
    /// {start, end, speaker, text}, descarta líneas inválidas, las ordena por start y devuelve la activa en un instante.
    /// Un JSON roto no lanza: devuelve una pista vacía (la cinemática se ve sin subtítulos).
    /// </summary>
    public sealed class CinematicCaptionTrack
    {
        [Serializable]
        private sealed class Wrapper
        {
            public CinematicCaption[] items;
        }

        private readonly List<CinematicCaption> _lines;

        public static readonly CinematicCaptionTrack Empty = new CinematicCaptionTrack(null);

        public CinematicCaptionTrack(IEnumerable<CinematicCaption> lines)
        {
            _lines = new List<CinematicCaption>();
            if (lines != null)
            {
                foreach (CinematicCaption line in lines)
                {
                    if (line != null && line.IsValid) _lines.Add(line);
                }
            }
            _lines.Sort(CompareByStart);
        }

        public int Count => _lines.Count;

        public IReadOnlyList<CinematicCaption> Lines => _lines;

        /// <summary>Parsea el contenido de &lt;id&gt;.captions.json (array en la raíz). Nunca lanza.</summary>
        public static CinematicCaptionTrack Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Empty;

            string trimmed = json.Trim();
            if (!trimmed.StartsWith("[")) return Empty;

            try
            {
                // JsonUtility no lee arrays en la raíz: se envuelve en un objeto
                Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"items\":" + trimmed + "}");
                return wrapper != null && wrapper.items != null ? new CinematicCaptionTrack(wrapper.items) : Empty;
            }
            catch (Exception)
            {
                // JsonUtility lanza ArgumentException con JSON roto; cualquier fallo deja la cinemática sin subtítulos
                return Empty;
            }
        }

        /// <summary>Índice de la línea activa en el instante (segundos de vídeo) o -1 si no hay ninguna.</summary>
        public int IndexAt(double time)
        {
            // Con solapes gana la que empezó más tarde
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i].Contains(time)) return i;
            }
            return -1;
        }

        private static int CompareByStart(CinematicCaption a, CinematicCaption b) => a.start.CompareTo(b.start);
    }
}
