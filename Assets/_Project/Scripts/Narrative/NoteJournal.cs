using System.Collections.Generic;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// Diario de notas recogidas en la partida (lógica pura + estado de sesión). Cada nota entra una sola vez
    /// (id estable: piso + autor + texto); el diario [J] las lista en orden de lectura para releerlas.
    /// Se vacía al empezar una partida nueva (JUGAR) y sobrevive a reinicios de piso y cambios de escena.
    /// </summary>
    public static class NoteJournal
    {
        public struct Note
        {
            public string Id;
            public int Floor;
            public string Author;
            public string Body;
        }

        private static readonly List<Note> s_notes = new List<Note>();

        public static IReadOnlyList<Note> Notes => s_notes;
        public static int Count => s_notes.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => s_notes.Clear();

        public static string MakeId(int floor, string author, string body)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in (author ?? "") + "|" + (body ?? "")) { h ^= c; h *= 16777619; }
                return $"F{floor:00}-{h:x8}";
            }
        }

        /// <summary>Añade la nota si es nueva. Devuelve true si se añadió.</summary>
        public static bool Add(int floor, string author, string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            string id = MakeId(floor, author, body);
            foreach (Note n in s_notes) if (n.Id == id) return false;
            s_notes.Add(new Note { Id = id, Floor = floor, Author = string.IsNullOrWhiteSpace(author) ? "NOTA" : author.Trim(), Body = body.Trim() });
            return true;
        }

        public static bool Contains(int floor, string author, string body)
        {
            string id = MakeId(floor, author, body);
            foreach (Note n in s_notes) if (n.Id == id) return true;
            return false;
        }
    }
}
