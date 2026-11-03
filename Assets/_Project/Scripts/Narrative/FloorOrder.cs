using System;

namespace Nexo.Narrative
{
    /// <summary>
    /// Orden de pisos del juego como clase pura de C# (sin UnityEngine), probada en EditMode.
    /// Índice 0 = primer piso; TryGetNext devuelve false en el último piso (ahí termina el BioScanner).
    /// </summary>
    public sealed class FloorOrder
    {
        private readonly string[] _scenes;

        public FloorOrder(string[] scenes)
        {
            _scenes = scenes ?? Array.Empty<string>();
        }

        public int Count => _scenes.Length;
        public int CurrentIndex { get; private set; }
        public bool IsLast => _scenes.Length == 0 || CurrentIndex >= _scenes.Length - 1;
        public string CurrentScene => SceneAt(CurrentIndex);

        public string SceneAt(int index)
        {
            return index >= 0 && index < _scenes.Length ? _scenes[index] : null;
        }

        /// <summary>Índice del piso siguiente; false si ya se está en el último (o no hay pisos).</summary>
        public bool TryGetNext(out int nextIndex)
        {
            nextIndex = CurrentIndex + 1;
            if (nextIndex < _scenes.Length) return true;

            nextIndex = -1;
            return false;
        }

        /// <summary>Avanza al siguiente piso. Devuelve false (sin cambios) en el último.</summary>
        public bool Advance()
        {
            if (!TryGetNext(out int next)) return false;

            CurrentIndex = next;
            return true;
        }

        /// <summary>Vuelve al primer piso (JUGAR o volver al menú).</summary>
        public void Reset()
        {
            CurrentIndex = 0;
        }

        /// <summary>
        /// Número de piso a partir del nombre de escena ("Floor06_Labs" → 6, "Floor12_Offices" → 12); -1 si no sigue el patrón.
        /// </summary>
        public static int ParseFloorNumber(string sceneName)
        {
            const string Prefix = "Floor";
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith(Prefix, StringComparison.Ordinal)) return -1;

            int number = 0;
            int digits = 0;
            for (int i = Prefix.Length; i < sceneName.Length && char.IsDigit(sceneName[i]); i++)
            {
                number = number * 10 + (sceneName[i] - '0');
                digits++;
            }
            return digits > 0 ? number : -1;
        }
    }
}
