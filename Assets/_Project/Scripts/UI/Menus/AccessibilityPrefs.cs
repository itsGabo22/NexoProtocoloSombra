using Nexo.Accessibility;
using UnityEngine;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Persistencia de las opciones de accesibilidad con PlayerPrefs (AccessibilitySettings no persiste nada).
    /// Al arrancar el juego carga lo guardado y lo aplica con AccessibilitySettings.Apply, que avisa al HUD
    /// (AccessibilityApplier) y a los menús (MenuFlowController).
    /// </summary>
    public static class AccessibilityPrefs
    {
        private const string ScaleKey = "nexo.a11y.scale";
        private const string ColorblindKey = "nexo.a11y.colorblind";
        private const string HighContrastKey = "nexo.a11y.highContrast";
        private const string ReduceMotionKey = "nexo.a11y.reduceMotion";
        private const string SubtitlesKey = "nexo.a11y.subtitles";
        private const string SubtitleSizeKey = "nexo.a11y.subtitleSize";

        /// <summary>Tamaño de subtítulos: 0 pequeño, 1 mediano, 2 grande (sin efecto en el HUD todavía).</summary>
        public static int SubtitleSize { get; private set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySavedOnStartup()
        {
            Load();
        }

        /// <summary>
        /// Lee las opciones guardadas (o los valores por defecto de AccessibilitySettings) y las aplica.
        /// </summary>
        public static void Load()
        {
            AccessibilityOptions defaults = AccessibilitySettings.Current;
            var options = new AccessibilityOptions
            {
                Scale = (UiScale)Mathf.Clamp(PlayerPrefs.GetInt(ScaleKey, (int)defaults.Scale), 0, 2),
                Colorblind = (ColorblindMode)Mathf.Clamp(PlayerPrefs.GetInt(ColorblindKey, (int)defaults.Colorblind), 0, 3),
                HighContrast = PlayerPrefs.GetInt(HighContrastKey, defaults.HighContrast ? 1 : 0) == 1,
                ReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, defaults.ReduceMotion ? 1 : 0) == 1,
                Subtitles = PlayerPrefs.GetInt(SubtitlesKey, defaults.Subtitles ? 1 : 0) == 1
            };
            SubtitleSize = Mathf.Clamp(PlayerPrefs.GetInt(SubtitleSizeKey, SubtitleSize), 0, 2);

            AccessibilitySettings.Apply(options);
        }

        /// <summary>
        /// Guarda y aplica las opciones (aplicación inmediata en HUD y menús).
        /// </summary>
        public static void Save(AccessibilityOptions options, int subtitleSize)
        {
            SubtitleSize = Mathf.Clamp(subtitleSize, 0, 2);

            PlayerPrefs.SetInt(ScaleKey, (int)options.Scale);
            PlayerPrefs.SetInt(ColorblindKey, (int)options.Colorblind);
            PlayerPrefs.SetInt(HighContrastKey, options.HighContrast ? 1 : 0);
            PlayerPrefs.SetInt(ReduceMotionKey, options.ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(SubtitlesKey, options.Subtitles ? 1 : 0);
            PlayerPrefs.SetInt(SubtitleSizeKey, SubtitleSize);
            PlayerPrefs.Save();

            AccessibilitySettings.Apply(options);
        }
    }
}
