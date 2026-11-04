using UnityEngine;

namespace Nexo.UI
{
    /// <summary>
    /// Tiempo en pantalla de subtítulos, líneas de Elena y notificaciones de NexoOS (lógica pura, testeada).
    /// duración = clamp(1,4 s + 0,055 s × caracteres, 3,5 s, 11 s) × velocidad de lectura; si suena un clip de voz,
    /// al menos la duración del clip + 0,8 s.
    /// </summary>
    public static class MessageTiming
    {
        public const float BaseSeconds = 1.4f;
        public const float SecondsPerCharacter = 0.055f;
        public const float MinSeconds = 3.5f;
        public const float MaxSeconds = 11f;
        public const float VoiceTail = 0.8f;
        /// <summary>Multiplicador de la opción «Velocidad de lectura: Lento».</summary>
        public const float SlowFactor = 1.4f;

        /// <summary>Velocidad de lectura actual (0 = Normal, 1 = Lento); la fija la pantalla de Opciones.</summary>
        public static int ReadingSpeed { get; set; }

        public static float ReadingFactor(int readingSpeed) => readingSpeed == 1 ? SlowFactor : 1f;

        /// <summary>Segundos que una línea de <paramref name="characters"/> caracteres debe permanecer visible.</summary>
        public static float Seconds(int characters, float voiceSeconds, int readingSpeed)
        {
            float factor = ReadingFactor(readingSpeed);
            float reading = Mathf.Clamp(BaseSeconds + SecondsPerCharacter * Mathf.Max(0, characters), MinSeconds, MaxSeconds) * factor;
            float voice = voiceSeconds > 0f ? voiceSeconds + VoiceTail : 0f;
            return Mathf.Max(reading, voice);
        }

        /// <summary>Tiempo mínimo antes de dar paso a la siguiente línea de la cola (nunca menos que la voz).</summary>
        public static float MinimumSeconds(float voiceSeconds, int readingSpeed)
        {
            float min = MinSeconds * ReadingFactor(readingSpeed);
            return voiceSeconds > 0f ? Mathf.Max(min, voiceSeconds + VoiceTail) : min;
        }

        public static float Seconds(string text, float voiceSeconds = 0f) => Seconds(text != null ? text.Length : 0, voiceSeconds, ReadingSpeed);
    }
}
