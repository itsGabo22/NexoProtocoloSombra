using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Proveedor de contingencia que entrega respuestas pre-cacheadas por piso e intención.
    /// Garantiza que la experiencia narrativa y la interacción con puertas nunca se detengan si no hay internet.
    /// </summary>
    public sealed class ElenaFallbackProvider
    {
        [Serializable]
        internal sealed class Entry
        {
            public int floor;
            public string key;
            public ElenaResponse response;
        }

        [Serializable]
        internal sealed class Library
        {
            public Entry[] entries;
        }

        private readonly List<Entry> _entries = new();

        private static readonly ElenaResponse Emergency = new ElenaResponse
        {
            dialogue = "Alex, la señal está fallando... sigue caminando, yo te aviso, ¿sí?",
            emotion = "scared",
            ui_action = "none",
            target_id = string.Empty,
            trust_level = 50
        };

        public ElenaFallbackProvider(IEnumerable<TextAsset> files)
        {
            if (files == null) return;
            foreach (TextAsset file in files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.text)) continue;
                try
                {
                    Library library = JsonUtility.FromJson<Library>(file.text);
                    if (library?.entries != null)
                    {
                        _entries.AddRange(library.entries);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ElenaFallbackProvider] Error parseando {file.name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Busca por piso e intención (SAFE_PATH, OPEN_DOOR, FEAR); si no encuentra, usa GENERIC y luego respuesta de emergencia.
        /// </summary>
        public ElenaResponse Get(int floor, string key)
        {
            ElenaResponse generic = null;
            foreach (Entry entry in _entries)
            {
                if (entry.floor != floor) continue;
                if (string.Equals(entry.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.response;
                }
                if (string.Equals(entry.key, "GENERIC", StringComparison.OrdinalIgnoreCase))
                {
                    generic = entry.response;
                }
            }
            return generic ?? Emergency;
        }
    }
}
