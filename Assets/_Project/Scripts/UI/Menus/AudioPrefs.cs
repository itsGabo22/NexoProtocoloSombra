using UnityEngine;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Volumen general del juego persistido con PlayerPrefs (como AccessibilityPrefs).
    /// El proyecto no tiene AudioMixer con parámetros expuestos, así que se aplica con AudioListener.volume,
    /// que afecta a todo el audio (efectos, ambiente y voces) sin tocar escenas.
    /// </summary>
    public static class AudioPrefs
    {
        private const string MasterVolumeKey = "nexo.audio.masterVolume";

        /// <summary>Volumen por defecto en porcentaje.</summary>
        public const int DefaultMasterVolume = 100;

        /// <summary>Volumen general actual en porcentaje (0-100).</summary>
        public static int MasterVolume { get; private set; } = DefaultMasterVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySavedOnStartup()
        {
            MasterVolume = ClampPercent(PlayerPrefs.GetInt(MasterVolumeKey, DefaultMasterVolume));
            AudioListener.volume = ToListenerVolume(MasterVolume);
        }

        /// <summary>Guarda y aplica al instante el volumen general.</summary>
        public static void SetMasterVolume(int percent)
        {
            MasterVolume = ClampPercent(percent);
            PlayerPrefs.SetInt(MasterVolumeKey, MasterVolume);
            PlayerPrefs.Save();
            AudioListener.volume = ToListenerVolume(MasterVolume);
        }

        /// <summary>Convierte un porcentaje (0-100) al rango 0-1 de AudioListener.volume.</summary>
        public static float ToListenerVolume(int percent)
        {
            return ClampPercent(percent) / 100f;
        }

        /// <summary>Limita un porcentaje al rango 0-100.</summary>
        public static int ClampPercent(int percent)
        {
            if (percent < 0) return 0;
            return percent > 100 ? 100 : percent;
        }
    }
}
