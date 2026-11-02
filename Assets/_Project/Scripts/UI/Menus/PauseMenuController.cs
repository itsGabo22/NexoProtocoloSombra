using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Pausa: Esc / Start (NexoControls/Player/Pause) abre y cierra la pantalla durante el juego.
    /// Al abrir congela el tiempo (Time.timeScale = 0); MenuFlowController bloquea al jugador y oculta el HUD.
    /// Ítems: CONTINUAR, OPCIONES, REINICIAR PISO, MENÚ PRINCIPAL.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour, IMenuScreen, IMenuNavigationHandler
    {
        private const int Continue = 0;
        private const int Options = 1;
        private const int Restart = 2;
        private const int MainMenu = 3;

        private static readonly string[] ItemNames =
        {
            "pause-item-continue", "pause-item-options", "pause-item-restart", "pause-item-main-menu"
        };

        // La spec solo da la pista de CONTINUAR; el resto sigue el mismo tono
        private static readonly string[] Hints =
        {
            "Vuelve a la partida.",
            "Ajusta video y accesibilidad.",
            "Reinicia el piso desde el principio.",
            "Abandona la partida y vuelve al menú principal."
        };

        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private MenuFlowController _menuFlow;
        [Tooltip("Opcional: si la terminal está abierta, Esc la cierra a ella y no abre la pausa.")]
        [SerializeField] private TerminalController _terminal;
        [Tooltip("Opcional: no se pausa durante una captura (el piso se está reiniciando).")]
        [SerializeField] private PlayerCaptureHandler _captureHandler;

        [Header("Entrada (NexoControls/Player/Pause)")]
        // En el Editor, Esc libera el cursor bloqueado y puede no llegar al juego: por eso Pause también responde a P.
        [SerializeField] private InputActionReference _pauseAction;

        private readonly VisualElement[] _items = new VisualElement[ItemNames.Length];
        private MenuNavigator _navigator;
        private Label _hint;
        private int _lastToggleFrame = -1;

        public bool IsPaused => _menuFlow != null && _menuFlow.Current == ScreenId.Pause;

        private void Awake()
        {
            _navigator = new MenuNavigator(this);
            if (_menuFlow == null) Debug.LogError($"[PauseMenuController] Falta asignar _menuFlow en {name}.", this);
            if (_pauseAction == null) Debug.LogError($"[PauseMenuController] Falta asignar _pauseAction (NexoControls/Player/Pause) en {name}.", this);
        }

        private void OnEnable()
        {
            MenuEvents.ResumeRequested += HandleResumeRequested;
            if (_pauseAction != null)
            {
                _pauseAction.action.performed += HandlePausePerformed;
                _pauseAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            MenuEvents.ResumeRequested -= HandleResumeRequested;
            if (_pauseAction != null) _pauseAction.action.performed -= HandlePausePerformed;

            // No dejar el juego congelado si este componente se desactiva en pausa
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            // La misma pulsación de Esc también llega como NavigationCancelEvent: un solo cambio por frame
            if (_menuFlow == null || Time.frameCount == _lastToggleFrame) return;

            if (_menuFlow.Current == ScreenId.None) TryPause();
            else if (_menuFlow.Current == ScreenId.Pause) Resume();
            // En Opciones o Créditos, Esc lo gestiona la propia pantalla (volver)
        }

        private void TryPause()
        {
            // Current == None solo ocurre en juego (tras JUGAR). No se exige FloorMetadata: Floor12_Offices aún no lo tiene.
            if (_terminal != null && _terminal.IsOpen) return;                 // Esc / P cierran la terminal
            if (_captureHandler != null && _captureHandler.IsCapturing) return; // el piso se está reiniciando

            _lastToggleFrame = Time.frameCount;
            Time.timeScale = 0f;
            _menuFlow.ShowScreen(ScreenId.Pause);
        }

        private void Resume()
        {
            if (!IsPaused || Time.frameCount == _lastToggleFrame) return;

            _lastToggleFrame = Time.frameCount;
            Time.timeScale = 1f;
            _menuFlow.ShowScreen(ScreenId.None);
        }

        private void HandleResumeRequested()
        {
            Resume();
        }

        // ---------- Pantalla de pausa ----------

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _hint = root.Q<Label>("pause-hint");

            Label location = root.Q<Label>("pause-location");
            FloorMetadata floor = FloorMetadata.Active;
            if (location != null && floor != null)
            {
                // El UXML trae "PISO 12 · OFICINAS"; para otros pisos solo se conoce el número
                string prefix = $"PISO {floor.FloorNumber}";
                if (!location.text.StartsWith(prefix)) location.text = prefix;
            }

            for (int i = 0; i < ItemNames.Length; i++)
            {
                _items[i] = root.Q<VisualElement>(ItemNames[i]);
            }

            _navigator.Attach(root, _items, Continue);
        }

        public void Unbind()
        {
            _navigator.Detach();
            _hint = null;
        }

        public void OnItemFocused(int index)
        {
            if (_hint != null) _hint.text = Hints[index];
        }

        public void OnItemSubmitted(int index)
        {
            switch (index)
            {
                case Continue:
                    MenuEvents.RaiseResumeRequested();
                    break;
                case Options:
                    _menuFlow.OpenOptions(ScreenId.OptionsVideo, ScreenId.Pause);
                    break;
                case Restart:
                    MenuEvents.RaiseRestartFloorRequested();
                    break;
                case MainMenu:
                    MenuEvents.RaiseReturnToMainMenuRequested();
                    break;
            }
        }

        public void OnValueStep(int index, int direction)
        {
        }

        public void OnTabStep(int direction)
        {
        }

        public void OnCancel()
        {
            Resume();
        }
    }
}
