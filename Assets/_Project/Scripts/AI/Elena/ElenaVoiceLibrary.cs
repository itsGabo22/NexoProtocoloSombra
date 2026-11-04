using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Índice de la voz pregrabada de Elena: texto normalizado (ElenaVoiceText) → clip. Lo genera
    /// Nexo › Elena › Generate Voice y vive en Resources/Elena/ElenaVoiceLibrary.asset para cargarse sin referencias.
    /// </summary>
    [CreateAssetMenu(fileName = "ElenaVoiceLibrary", menuName = "Nexo/AI/Elena Voice Library")]
    public sealed class ElenaVoiceLibrary : ScriptableObject
    {
        public const string ResourcePath = "Elena/ElenaVoiceLibrary";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Texto normalizado (ElenaVoiceText.Normalize).")]
            public string Key;
            [Tooltip("Texto original (referencia legible).")]
            public string Text;
            public string Emotion;
            public AudioClip Clip;
        }

        [Tooltip("Motor con que se sintetizaron las voces (créditos e informe).")]
        [SerializeField] private string _engine = "";
        [SerializeField] private List<Entry> _entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;
        private static ElenaVoiceLibrary s_instance;
        private static bool s_loaded;

        public string Engine => _engine;
        public IReadOnlyList<Entry> Entries => _entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_loaded = false;
        }

        /// <summary>Biblioteca cargada de Resources (null si aún no se generó).</summary>
        public static ElenaVoiceLibrary Instance
        {
            get
            {
                if (!s_loaded)
                {
                    s_loaded = true;
                    s_instance = Resources.Load<ElenaVoiceLibrary>(ResourcePath);
                }
                return s_instance;
            }
        }

        public bool TryGetClip(string line, out AudioClip clip)
        {
            clip = null;
            if (_lookup == null) BuildLookup();
            string key = ElenaVoiceText.Normalize(line);
            if (key.Length == 0 || !_lookup.TryGetValue(key, out Entry entry) || entry.Clip == null) return false;
            clip = entry.Clip;
            return true;
        }

        /// <summary>Duración del clip de la línea, o 0 si no tiene voz.</summary>
        public static float SecondsFor(string line)
        {
            ElenaVoiceLibrary lib = Instance;
            return lib != null && lib.TryGetClip(line, out AudioClip clip) ? clip.length : 0f;
        }

#if UNITY_EDITOR
        /// <summary>Solo el generador del Editor: reemplaza el contenido del índice.</summary>
        public void EditorSetEntries(List<Entry> entries, string engine)
        {
            _entries = entries ?? new List<Entry>();
            _engine = engine ?? "";
            _lookup = null;
        }
#endif

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, Entry>();
            foreach (Entry e in _entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.Key)) _lookup[e.Key] = e;
            }
        }

        private void OnValidate() => _lookup = null;
    }
}
