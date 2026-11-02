using Nexo.Accessibility;
using Nexo.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Flujo de pantallas NexoOS sobre un único UIDocument: sustituye su visualTreeAsset por la pantalla pedida,
    /// aplica el tema (theme-nexo / theme-hive) y las clases de accesibilidad a la raíz y enlaza su controlador.
    /// Mientras hay un menú abierto oculta el HUD y bloquea los controles del jugador (PlayerMovement.SetInputLocked).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class MenuFlowController : MonoBehaviour
    {
        private const string ThemeNexo = "theme-nexo";
        private const string ThemeHive = "theme-hive";

        [Header("Pantallas (Assets/_Project/UI/Layouts/Menus)")]
        [SerializeField] private VisualTreeAsset _bootAwakeningLayout;
        [SerializeField] private VisualTreeAsset _bootBiometricScanLayout;
        [Tooltip("Fuera de alcance por ahora: no hay controlador, el arranque la salta.")]
        [SerializeField] private VisualTreeAsset _opticalCalibrationLayout;
        [SerializeField] private VisualTreeAsset _mainMenuLayout;
        [SerializeField] private VisualTreeAsset _optionsVideoLayout;
        [SerializeField] private VisualTreeAsset _optionsAccessibilityLayout;
        [SerializeField] private VisualTreeAsset _pauseMenuLayout;
        [SerializeField] private VisualTreeAsset _floorRestartedLayout;
        [SerializeField] private VisualTreeAsset _creditsLayout;

        [Header("Arranque")]
        [Tooltip("Muestra Despertar → Escaneo biométrico → Menú al iniciar. Desactivar para probar un piso directamente.")]
        [SerializeField] private bool _playBootOnStart = true;

        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private PlayerMovement _playerMovement;
        [Tooltip("UIDocument de PF_HUD: se oculta mientras hay un menú abierto.")]
        [SerializeField] private UIDocument _hudDocument;
        [Tooltip("PauseMenuController de este mismo GameObject (controla la pantalla de pausa y la tecla Esc).")]
        [SerializeField] private PauseMenuController _pauseMenu;

        private UIDocument _document;
        private VisualElement _screenRoot;
        private IMenuScreen _activeScreen;

        private BootSequenceController _boot;
        private MainMenuController _mainMenu;
        private OptionsController _options;
        private FloorRestartedController _floorRestarted;
        private CreditsController _credits;

        /// <summary>Pantalla visible (None = en juego, sin menú).</summary>
        public ScreenId Current { get; private set; } = ScreenId.None;

        /// <summary>Pantalla a la que vuelve Opciones al cancelar (MainMenu o Pause).</summary>
        public ScreenId OptionsReturnScreen { get; private set; } = ScreenId.MainMenu;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _boot = new BootSequenceController();
            _mainMenu = new MainMenuController();
            _options = new OptionsController();
            _floorRestarted = new FloorRestartedController();
            _credits = new CreditsController();

            if (_playerMovement == null) Debug.LogWarning($"[MenuFlowController] Falta asignar _playerMovement en {name}: los menús no bloquearán al jugador.", this);
            if (_pauseMenu == null) Debug.LogWarning($"[MenuFlowController] Falta asignar _pauseMenu en {name}: la pantalla de pausa no tendrá controlador.", this);
        }

        private void OnEnable()
        {
            AccessibilitySettings.Changed += HandleAccessibilityChanged;
            MenuEvents.ShowFloorRestartedRequested += HandleShowFloorRestartedRequested;
        }

        private void OnDisable()
        {
            AccessibilitySettings.Changed -= HandleAccessibilityChanged;
            MenuEvents.ShowFloorRestartedRequested -= HandleShowFloorRestartedRequested;

            _activeScreen?.Unbind();
            _activeScreen = null;
            _screenRoot = null;
        }

        private void Start()
        {
            ShowScreen(_playBootOnStart ? ScreenId.BootAwakening : ScreenId.None);
        }

        /// <summary>
        /// Muestra una pantalla (o ninguna con ScreenId.None) y enlaza su controlador.
        /// </summary>
        public void ShowScreen(ScreenId screen)
        {
            if (_document == null) return;

            _activeScreen?.Unbind();
            _activeScreen = null;
            _screenRoot = null;

            VisualTreeAsset layout = LayoutFor(screen);
            if (screen != ScreenId.None && layout == null)
            {
                Debug.LogWarning($"[MenuFlowController] Falta asignar el VisualTreeAsset de {screen} en {name}.", this);
                screen = ScreenId.None;
            }

            // Sustituir el asset reconstruye rootVisualElement; los controladores vuelven a consultar sus elementos en Bind
            _document.visualTreeAsset = layout;
            Current = screen;

            bool menuOpen = screen != ScreenId.None;
            SetHudVisible(!menuOpen);
            if (_playerMovement != null) _playerMovement.SetInputLocked(menuOpen, menuOpen);
            if (!menuOpen) return;

            VisualElement documentRoot = _document.rootVisualElement;
            _screenRoot = documentRoot != null ? documentRoot.Q<VisualElement>(RootNameFor(screen)) : null;
            if (_screenRoot == null)
            {
                Debug.LogWarning($"[MenuFlowController] No se encontró '{RootNameFor(screen)}' en la pantalla {screen}.", this);
                return;
            }

            bool hive = screen == ScreenId.FloorRestarted || screen == ScreenId.Credits;
            _screenRoot.EnableInClassList(ThemeHive, hive);
            _screenRoot.EnableInClassList(ThemeNexo, !hive);
            ApplyAccessibility(AccessibilitySettings.Current);

            _activeScreen = ControllerFor(screen);
            _activeScreen?.Bind(this, _screenRoot, screen);
        }

        /// <summary>
        /// Abre Opciones en la pestaña indicada (OptionsVideo u OptionsAccessibility) y recuerda a dónde volver.
        /// </summary>
        public void OpenOptions(ScreenId tab, ScreenId returnTo)
        {
            OptionsReturnScreen = returnTo;
            ShowScreen(tab == ScreenId.OptionsAccessibility ? ScreenId.OptionsAccessibility : ScreenId.OptionsVideo);
        }

        private void HandleShowFloorRestartedRequested()
        {
            ShowScreen(ScreenId.FloorRestarted);
        }

        private void HandleAccessibilityChanged(AccessibilityOptions options)
        {
            ApplyAccessibility(options);
        }

        /// <summary>
        /// Mismas clases que AccessibilityApplier aplica al HUD, sobre la raíz de la pantalla (junto a la clase de tema,
        /// que es lo que piden los selectores compuestos .theme-nexo.a11y-hc / .theme-hive.a11y-hc).
        /// </summary>
        private void ApplyAccessibility(AccessibilityOptions o)
        {
            if (_screenRoot == null) return;

            _screenRoot.EnableInClassList("a11y-hc", o.HighContrast);
            _screenRoot.EnableInClassList("reduce-motion", o.ReduceMotion);
            _screenRoot.EnableInClassList("ui-scale-lg", o.Scale == UiScale.Large);
            _screenRoot.EnableInClassList("ui-scale-xl", o.Scale == UiScale.ExtraLarge);
            _screenRoot.EnableInClassList("cb-deutan", o.Colorblind == ColorblindMode.Deutan);
            _screenRoot.EnableInClassList("cb-protan", o.Colorblind == ColorblindMode.Protan);
            _screenRoot.EnableInClassList("cb-tritan", o.Colorblind == ColorblindMode.Tritan);
        }

        private void SetHudVisible(bool visible)
        {
            if (_hudDocument == null || _hudDocument.rootVisualElement == null) return;
            _hudDocument.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualTreeAsset LayoutFor(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.BootAwakening: return _bootAwakeningLayout;
                case ScreenId.BootBiometricScan: return _bootBiometricScanLayout;
                case ScreenId.OpticalCalibration: return _opticalCalibrationLayout;
                case ScreenId.MainMenu: return _mainMenuLayout;
                case ScreenId.OptionsVideo: return _optionsVideoLayout;
                case ScreenId.OptionsAccessibility: return _optionsAccessibilityLayout;
                case ScreenId.Pause: return _pauseMenuLayout;
                case ScreenId.FloorRestarted: return _floorRestartedLayout;
                case ScreenId.Credits: return _creditsLayout;
                default: return null;
            }
        }

        private static string RootNameFor(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.BootAwakening: return "boot-root";
                case ScreenId.BootBiometricScan: return "scan-root";
                case ScreenId.OpticalCalibration: return "calib-root";
                case ScreenId.MainMenu: return "mm-root";
                case ScreenId.OptionsVideo: return "optv-root";
                case ScreenId.OptionsAccessibility: return "opta-root";
                case ScreenId.Pause: return "pause-root";
                case ScreenId.FloorRestarted: return "fr-root";
                case ScreenId.Credits: return "cr-root";
                default: return string.Empty;
            }
        }

        private IMenuScreen ControllerFor(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.BootAwakening:
                case ScreenId.BootBiometricScan:
                    return _boot;
                case ScreenId.MainMenu: return _mainMenu;
                case ScreenId.OptionsVideo:
                case ScreenId.OptionsAccessibility:
                    return _options;
                case ScreenId.Pause: return _pauseMenu;
                case ScreenId.FloorRestarted: return _floorRestarted;
                case ScreenId.Credits: return _credits;
                default: return null;
            }
        }
    }
}
