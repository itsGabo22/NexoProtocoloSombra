using Nexo.AI.Elena;
using Nexo.Core;
using Nexo.Interaction;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// UI de la terminal de seguridad (SecurityTerminal.uxml). Vive en PF_HUD (Systems_Persistent).
    /// Se abre al recibir SecurityTerminal.Opened, muestra las RenderTexture de las cámaras y opera las puertas.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class TerminalController : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("Entrada (NexoControls/Player/Pause)")]
        [SerializeField] private InputActionReference _closeAction;

        private VisualElement _root;
        private VisualElement _feeds;
        private VisualElement _doors;
        private Label _title;
        private Label _status;
        private Button _closeButton;

        private SecurityTerminal _terminal;
        private bool _isCaptured;

        public bool IsOpen => _terminal != null;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            _root = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.Q<VisualElement>("terminal-root")
                : null;

            if (_root == null)
            {
                Debug.LogWarning("[TerminalController] No se encontró 'terminal-root' en el UIDocument.", this);
                return;
            }

            _feeds = _root.Q<VisualElement>("terminal-feeds");
            _doors = _root.Q<VisualElement>("terminal-doors");
            _title = _root.Q<Label>("terminal-title");
            _status = _root.Q<Label>("terminal-status");
            _closeButton = _root.Q<Button>("terminal-close");

            _root.AddToClassList(HiddenClass);

            SecurityTerminal.Opened += Open;
            GameEvents.OnUiAction += HandleUiAction;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            GameEvents.OnFloorChanged += HandleFloorChanged;
            if (_closeButton != null) _closeButton.clicked += Close;
            if (_closeAction != null)
            {
                _closeAction.action.performed += HandleClosePerformed;
                _closeAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            SecurityTerminal.Opened -= Open;
            GameEvents.OnUiAction -= HandleUiAction;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            if (_closeButton != null) _closeButton.clicked -= Close;
            if (_closeAction != null) _closeAction.action.performed -= HandleClosePerformed;

            Close();
        }

        /// <summary>
        /// Abre la UI para la terminal indicada y enciende sus cámaras.
        /// </summary>
        public void Open(SecurityTerminal terminal)
        {
            if (_root == null || terminal == null || IsOpen || _isCaptured) return;

            _terminal = terminal;
            _terminal.SetFeedsActive(true);

            if (_title != null) _title.text = terminal.TerminalId.Replace('_', ' ').ToUpperInvariant();
            SetStatus(string.Empty);

            BuildFeeds();
            BuildDoors();

            _root.RemoveFromClassList(HiddenClass);
            if (_playerMovement != null) _playerMovement.SetInputLocked(true, true);
        }

        public void Close() => Close(true);

        /// <summary>
        /// Cierra la terminal. Con restoreInput = false no devuelve el control al jugador (captura en curso).
        /// </summary>
        private void Close(bool restoreInput)
        {
            if (!IsOpen) return;

            _terminal.SetFeedsActive(false);
            _terminal = null;

            ClearDoors();
            _feeds?.Clear();
            _root.AddToClassList(HiddenClass);

            if (restoreInput && _playerMovement != null) _playerMovement.SetInputLocked(false, false);
        }

        private void HandlePlayerCaught()
        {
            // PlayerCaptureHandler mantiene congelado al jugador hasta el reinicio del piso
            _isCaptured = true;
            Close(false);
        }

        private void HandleFloorChanged(int floor)
        {
            _isCaptured = false;
        }

        private void HandleClosePerformed(InputAction.CallbackContext context)
        {
            Close();
        }

        private void HandleUiAction(ElenaUiAction action, string targetId)
        {
            // Elena puede desbloquear una puerta mientras la terminal está abierta
            if (IsOpen && action == ElenaUiAction.UnlockDoor) BuildDoors();
        }

        private void BuildFeeds()
        {
            if (_feeds == null) return;
            _feeds.Clear();

            for (int i = 0; i < _terminal.FeedCount; i++)
            {
                var feed = new VisualElement();
                feed.AddToClassList("terminal-feed");

                var image = new VisualElement();
                image.AddToClassList("terminal-feed__image");
                RenderTexture texture = _terminal.GetFeedTexture(i);
                if (texture != null)
                {
                    image.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                }

                var label = new Label(_terminal.GetFeedLabel(i));
                label.AddToClassList("terminal-feed__label");

                feed.Add(image);
                feed.Add(label);
                _feeds.Add(feed);
            }
        }

        private void BuildDoors()
        {
            if (_doors == null) return;
            ClearDoors();

            for (int i = 0; i < _terminal.DoorCount; i++)
            {
                DoorController door = _terminal.GetDoor(i);
                if (door == null) continue;

                var row = new VisualElement();
                row.AddToClassList("terminal-door");
                row.EnableInClassList("terminal-door--open", door.IsOpen);
                row.EnableInClassList("terminal-door--locked", door.IsLocked);

                var info = new VisualElement();
                info.AddToClassList("flex-col");

                var idLabel = new Label(door.DoorId);
                idLabel.AddToClassList("text-sm");
                idLabel.AddToClassList("text-main");

                var stateLabel = new Label(door.IsLocked ? "BLOQUEADA · PROTOCOLO" : (door.IsOpen ? "ABIERTA" : "CERRADA"));
                stateLabel.AddToClassList("terminal-door__state");

                info.Add(idLabel);
                info.Add(stateLabel);

                var button = new Button { text = door.IsOpen ? "CERRAR" : "ABRIR" };
                button.AddToClassList("btn-quick");
                button.AddToClassList("terminal-door__button");
                button.SetEnabled(!door.IsLocked);
                button.RegisterCallback<ClickEvent, int>(HandleDoorClicked, i);

                row.Add(info);
                row.Add(button);
                _doors.Add(row);
            }
        }

        private void ClearDoors()
        {
            if (_doors == null) return;

            foreach (VisualElement row in _doors.Children())
            {
                Button button = row.Q<Button>();
                button?.UnregisterCallback<ClickEvent, int>(HandleDoorClicked);
            }
            _doors.Clear();
        }

        private void HandleDoorClicked(ClickEvent evt, int doorIndex)
        {
            if (!IsOpen) return;

            DoorController door = _terminal.GetDoor(doorIndex);
            if (_terminal.TryToggleDoor(doorIndex) && door != null)
            {
                SetStatus($"> {door.DoorId}: {(door.IsOpen ? "APERTURA" : "CIERRE")} REMOTO EJECUTADO");
            }
            BuildDoors();
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }
    }
}
