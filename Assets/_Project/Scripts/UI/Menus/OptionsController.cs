using Nexo.Accessibility;
using Nexo.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Opciones con dos pestañas (Q/E alternan; AUDIO y CONTROLES no existen todavía):
    /// ACCESIBILIDAD es funcional (AccessibilitySettings + AccessibilityPrefs, vista previa en vivo);
    /// VIDEO aplica «Resolución» y «Modo de pantalla» (Screen) y «Volumen general» (AudioPrefs); el resto solo cambia el valor
    /// mostrado. Esc vuelve a la pantalla de origen (menú o pausa).
    /// </summary>
    public sealed class OptionsController : IMenuScreen, IMenuNavigationHandler
    {
        private const string OnClass = "is-on";
        private const string On = "ACTIVADO";
        private const string Off = "DESACTIVADO";
        private const int SliderStep = 10;

        private enum RowKind { Choice, Toggle, Slider }

        private sealed class Row
        {
            public readonly string Name;
            public readonly RowKind Kind;
            public readonly string[] Choices;
            public int Value;
            public VisualElement Element;

            public Row(string name, RowKind kind, string[] choices, int value)
            {
                Name = name;
                Kind = kind;
                Choices = choices;
                Value = value;
            }
        }

        // Pestaña VIDEO: valores solo visuales durante la sesión (TODO apply: Screen.SetResolution, QualitySettings, etc.)
        private readonly Row[] _videoRows =
        {
            new Row("optv-row-resolution", RowKind.Choice, new[] { "1920 × 1080", "1600 × 900", "1280 × 720" }, 0),
            new Row("optv-row-display-mode", RowKind.Choice, new[] { "Pantalla completa", "Ventana sin bordes", "Ventana" }, 0),
            new Row("optv-row-quality", RowKind.Choice, new[] { "Baja", "Media", "Alta" }, 2),
            new Row("optv-row-shadows", RowKind.Toggle, null, 1),
            new Row("optv-row-antialiasing", RowKind.Choice, new[] { "Desactivado", "FXAA", "SMAA", "TAA" }, 3),
            new Row("optv-row-brightness", RowKind.Slider, null, 60),
            new Row("optv-row-vsync", RowKind.Toggle, null, 0),
            new Row("optv-row-master-volume", RowKind.Slider, null, AudioPrefs.DefaultMasterVolume)
        };

        // Filas de VIDEO que se aplican: resolución y modo de pantalla (Screen), volumen general (AudioPrefs)
        private const int ResolutionRow = 0;
        private const int DisplayModeRow = 1;
        private const int MasterVolumeRow = 7;
        private static readonly Vector2Int[] Resolutions = { new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1280, 720) };
        private static readonly FullScreenMode[] DisplayModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

        // Pestaña ACCESIBILIDAD: el orden coincide con las filas de Options_Accessibility.uxml
        private const int HighContrastRow = 0;
        private const int UiScaleRow = 1;
        private const int ColorblindRow = 2;
        private const int ReduceMotionRow = 3;
        private const int SubtitlesRow = 4;
        private const int SubtitleSizeRow = 5;
        private const int ReadingSpeedRow = 6;

        private readonly Row[] _accessibilityRows =
        {
            new Row("opta-row-high-contrast", RowKind.Toggle, null, 0),
            new Row("opta-row-ui-scale", RowKind.Choice, new[] { "100%", "125%", "150%" }, 0),
            new Row("opta-row-colorblind", RowKind.Choice, new[] { "Ninguno", "Deuteranopia", "Protanopia", "Tritanopia" }, 0),
            new Row("opta-row-reduce-motion", RowKind.Toggle, null, 0),
            new Row("opta-row-subtitles", RowKind.Toggle, null, 1),
            new Row("opta-row-subtitle-size", RowKind.Choice, new[] { "Pequeño", "Mediano", "Grande" }, 1),
            new Row("opta-row-reading-speed", RowKind.Choice, new[] { "Normal", "Lento" }, 0)
        };

        private readonly MenuNavigator _navigator;
        private MenuFlowController _flow;
        private ScreenId _screen;
        private Row[] _rows;
        private VisualElement _videoTab;
        private VisualElement _accessibilityTab;
        private int _videoIndex = 2;          // CALIDAD DE IMAGEN, la fila enfocada en la spec
        private int _accessibilityIndex = 1;  // ESCALA DE LA INTERFAZ

        public OptionsController()
        {
            _navigator = new MenuNavigator(this);
        }

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _flow = flow;
            _screen = screen;
            bool accessibility = screen == ScreenId.OptionsAccessibility;
            _rows = accessibility ? _accessibilityRows : _videoRows;
            if (accessibility) LoadAccessibilityValues();
            else
            {
                _videoRows[MasterVolumeRow].Value = AudioPrefs.MasterVolume;
                SyncDisplayRows();
            }

            var elements = new VisualElement[_rows.Length];
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].Element = root.Q<VisualElement>(_rows[i].Name);
                elements[i] = _rows[i].Element;
                Refresh(_rows[i]);
            }

            // Pestañas clicables (además de Q/E)
            _videoTab = root.Q<VisualElement>("tabbar-tab-video");
            _accessibilityTab = root.Q<VisualElement>("tabbar-tab-accessibility");
            _videoTab?.RegisterCallback<ClickEvent>(HandleVideoTabClicked);
            _accessibilityTab?.RegisterCallback<ClickEvent>(HandleAccessibilityTabClicked);

            _navigator.Attach(root, elements, accessibility ? _accessibilityIndex : _videoIndex);
        }

        public void Unbind()
        {
            _navigator.Detach();
            _videoTab?.UnregisterCallback<ClickEvent>(HandleVideoTabClicked);
            _accessibilityTab?.UnregisterCallback<ClickEvent>(HandleAccessibilityTabClicked);
            _videoTab = null;
            _accessibilityTab = null;

            if (_rows != null)
            {
                foreach (Row row in _rows) row.Element = null;
            }
            _rows = null;
        }

        public void OnItemFocused(int index)
        {
            if (_screen == ScreenId.OptionsAccessibility) _accessibilityIndex = index;
            else _videoIndex = index;
        }

        public void OnItemSubmitted(int index)
        {
            Row row = RowAt(index);
            if (row == null || row.Kind == RowKind.Slider) return;
            ChangeValue(row, 1);
        }

        public void OnValueStep(int index, int direction)
        {
            Row row = RowAt(index);
            if (row != null) ChangeValue(row, direction);
        }

        public void OnTabStep(int direction)
        {
            // Solo dos pestañas: Q y E alternan entre VIDEO y ACCESIBILIDAD
            _flow.ShowScreen(_screen == ScreenId.OptionsVideo ? ScreenId.OptionsAccessibility : ScreenId.OptionsVideo);
        }

        public void OnCancel()
        {
            _flow.ShowScreen(_flow.OptionsReturnScreen);
        }

        private Row RowAt(int index)
        {
            return _rows != null && index >= 0 && index < _rows.Length ? _rows[index] : null;
        }

        private void ChangeValue(Row row, int direction)
        {
            switch (row.Kind)
            {
                case RowKind.Choice:
                    int count = row.Choices.Length;
                    row.Value = (row.Value + direction + count) % count;
                    break;
                case RowKind.Toggle:
                    row.Value = row.Value == 1 ? 0 : 1;
                    break;
                case RowKind.Slider:
                    row.Value = Mathf.Clamp(row.Value + direction * SliderStep, 0, 100);
                    break;
            }

            Refresh(row);

            if (_screen == ScreenId.OptionsAccessibility)
            {
                SaveAccessibilityValues();
            }
            else if (row == _videoRows[MasterVolumeRow])
            {
                AudioPrefs.SetMasterVolume(row.Value);
            }
            else if (row == _videoRows[ResolutionRow] || row == _videoRows[DisplayModeRow])
            {
                ApplyDisplay();
            }
            // TODO apply: calidad, sombras, AA, brillo y VSync siguen siendo solo visuales.
        }

        /// <summary>Refleja en las filas el modo y la resolución reales (Alt+Intro también cambia el modo).</summary>
        private void SyncDisplayRows()
        {
            int mode = System.Array.IndexOf(DisplayModes, Screen.fullScreenMode);
            if (mode == -1) mode = Screen.fullScreenMode == FullScreenMode.MaximizedWindow ? 2 : 1;
            _videoRows[DisplayModeRow].Value = mode;
            for (int i = 0; i < Resolutions.Length; i++)
            {
                if (Screen.width == Resolutions[i].x && Screen.height == Resolutions[i].y) _videoRows[ResolutionRow].Value = i;
            }
        }

        private void ApplyDisplay()
        {
            // En el Editor la ventana Game no cambia; en el build se aplica al instante y Unity lo recuerda
            Vector2Int size = Resolutions[_videoRows[ResolutionRow].Value];
            Screen.SetResolution(size.x, size.y, DisplayModes[_videoRows[DisplayModeRow].Value]);
        }

        private static void Refresh(Row row)
        {
            if (row.Element == null) return;

            switch (row.Kind)
            {
                case RowKind.Choice:
                    Label value = row.Element.Q<Label>("setting-row-value");
                    if (value != null) value.text = row.Choices[row.Value];
                    break;
                case RowKind.Toggle:
                    bool on = row.Value == 1;
                    row.Element.EnableInClassList(OnClass, on);
                    Label state = row.Element.Q<Label>("setting-row-state");
                    if (state != null) state.text = on ? On : Off;
                    break;
                case RowKind.Slider:
                    Label number = row.Element.Q<Label>("setting-row-value");
                    if (number != null) number.text = row.Value.ToString();
                    VisualElement fill = row.Element.Q<VisualElement>("setting-row-fill");
                    if (fill != null) fill.style.width = Length.Percent(row.Value);
                    break;
            }
        }

        private void LoadAccessibilityValues()
        {
            AccessibilityOptions o = AccessibilitySettings.Current;
            _accessibilityRows[HighContrastRow].Value = o.HighContrast ? 1 : 0;
            _accessibilityRows[UiScaleRow].Value = (int)o.Scale;
            _accessibilityRows[ColorblindRow].Value = (int)o.Colorblind;
            _accessibilityRows[ReduceMotionRow].Value = o.ReduceMotion ? 1 : 0;
            _accessibilityRows[SubtitlesRow].Value = o.Subtitles ? 1 : 0;
            _accessibilityRows[SubtitleSizeRow].Value = AccessibilityPrefs.SubtitleSize;
            _accessibilityRows[ReadingSpeedRow].Value = MessageTiming.ReadingSpeed;
        }

        /// <summary>
        /// Guarda y aplica al instante: AccessibilitySettings.Changed actualiza el HUD y la raíz de esta pantalla,
        /// así las tarjetas de ejemplo (real y falsa) reflejan contraste, escala y daltonismo en vivo.
        /// </summary>
        private void SaveAccessibilityValues()
        {
            var options = new AccessibilityOptions
            {
                HighContrast = _accessibilityRows[HighContrastRow].Value == 1,
                Scale = (UiScale)_accessibilityRows[UiScaleRow].Value,
                Colorblind = (ColorblindMode)_accessibilityRows[ColorblindRow].Value,
                ReduceMotion = _accessibilityRows[ReduceMotionRow].Value == 1,
                Subtitles = _accessibilityRows[SubtitlesRow].Value == 1
            };

            // TODO apply: el tamaño de subtítulos se guarda pero SubtitleController aún no lo usa
            AccessibilityPrefs.Save(options, _accessibilityRows[SubtitleSizeRow].Value, _accessibilityRows[ReadingSpeedRow].Value);
        }

        private void HandleVideoTabClicked(ClickEvent evt)
        {
            if (_screen != ScreenId.OptionsVideo) _flow.ShowScreen(ScreenId.OptionsVideo);
        }

        private void HandleAccessibilityTabClicked(ClickEvent evt)
        {
            if (_screen != ScreenId.OptionsAccessibility) _flow.ShowScreen(ScreenId.OptionsAccessibility);
        }
    }
}
