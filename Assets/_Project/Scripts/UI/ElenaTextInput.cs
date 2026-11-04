using Nexo.Cinematics;
using Nexo.Core;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// [T] abre una línea de texto para escribirle a Elena (alternativa al micrófono [V]): Intro envía, Esc cierra.
    /// El texto llega a ElenaAIService por GameEvents.RaisePlayerSpoke (Gemini o el respaldo offline del piso).
    /// Mientras está abierto el jugador queda bloqueado con el cursor visible y UiTextEntry silencia las teclas rápidas.
    /// No se abre con otra UI modal, en cinemáticas, en el giro final ni con el jugador bloqueado (escondite, forcejeo).
    /// Construido por código sobre el UIDocument del HUD; sin Update (acciones de entrada y eventos de UI Toolkit).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class ElenaTextInput : MonoBehaviour
    {
        public const int MaxChars = 140;

        private InputAction _openAction;
        private VisualElement _panel;
        private TextField _field;
        private PlayerMovement _player;
        private bool _open;

        public bool IsOpen => _open;

        private void Awake()
        {
            _openAction = new InputAction("TypeToElena", InputActionType.Button, "<Keyboard>/t");
        }

        private void OnEnable()
        {
            BuildPanel();
            _openAction.performed += HandleOpenPerformed;
            _openAction.Enable();
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnCinematicStarted += HandleCinematicStarted;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            GameEvents.OnPlotTwistTriggered += HandlePlotTwist;
        }

        private void OnDisable()
        {
            _openAction.performed -= HandleOpenPerformed;
            _openAction.Disable();
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwist;
            if (_field != null)
            {
                _field.UnregisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            }
            if (_open) Close();
        }

        private void OnDestroy() => _openAction?.Dispose();

        private void BuildPanel()
        {
            var document = GetComponent<UIDocument>();
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || _panel != null) return;

            _panel = new VisualElement { name = "hud-elena-text", pickingMode = PickingMode.Position };
            _panel.AddToClassList("hud-elena-text");
            _panel.style.position = Position.Absolute;
            _panel.style.left = Length.Percent(22);
            _panel.style.right = Length.Percent(22);
            _panel.style.bottom = 150;
            _panel.style.paddingLeft = 14; _panel.style.paddingRight = 14; _panel.style.paddingTop = 10; _panel.style.paddingBottom = 10;
            _panel.style.backgroundColor = new Color(0.02f, 0.06f, 0.1f, 0.92f);
            _panel.style.borderTopWidth = 1; _panel.style.borderBottomWidth = 1; _panel.style.borderLeftWidth = 1; _panel.style.borderRightWidth = 1;
            Color edge = new Color(0.35f, 0.78f, 1f, 0.9f);
            _panel.style.borderTopColor = edge; _panel.style.borderBottomColor = edge; _panel.style.borderLeftColor = edge; _panel.style.borderRightColor = edge;
            _panel.style.display = DisplayStyle.None;

            var title = new Label("CANAL DE TEXTO · ELENA   (Intro: enviar · Esc: cerrar)");
            title.style.color = edge;
            title.style.fontSize = 14;
            title.style.marginBottom = 6;
            _panel.Add(title);

            _field = new TextField { name = "hud-elena-text-field", maxLength = MaxChars, isDelayed = false };
            _field.style.fontSize = 18;
            _field.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            _panel.Add(_field);

            root.Add(_panel);
        }

        private void HandlePlayerSpawned(Transform player)
        {
            _player = player != null ? player.GetComponent<PlayerMovement>() : null;
        }

        private void HandleOpenPerformed(InputAction.CallbackContext context)
        {
            if (_open || !CanOpen()) return;
            Open();
        }

        private bool CanOpen()
        {
            if (_panel == null || _player == null) return false;
            if (_player.IsInputLocked || PlayerHiding.IsHidden) return false;            // terminal, forcejeo, escondite, captura
            if (UnityEngine.Cursor.lockState != CursorLockMode.Locked) return false;               // menú o modal con cursor
            if (CinematicService.BlocksGameplayInput) return false;
            if (Nexo.Narrative.NarrativeEvents.TwistHasPlayed) return false;
            return Time.timeScale > 0f;
        }

        private void Open()
        {
            _open = true;
            UiTextEntry.SetActive(true);
            _player.SetInputLocked(true, true);
            _field.SetValueWithoutNotify(string.Empty);
            _panel.style.display = DisplayStyle.Flex;
            _panel.BringToFront();
            // La misma pulsación de [T] no debe escribirse: el foco llega un instante después
            _field.schedule.Execute(FocusField).StartingIn(30);
        }

        private void FocusField()
        {
            if (!_open) return;
            _field.SetValueWithoutNotify(string.Empty);
            _field.Focus();
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (!_open) return;
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                Submit();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
        }

        /// <summary>Envía el texto escrito (también lo usan las comprobaciones del Editor).</summary>
        public void Submit()
        {
            string text = ElenaTypedText.Clean(_field != null ? _field.value : string.Empty);
            Close();
            if (text.Length == 0) return;
            GameEvents.RaiseSubtitle("TÚ: " + text);
            GameEvents.RaisePlayerSpoke(text);
        }

        /// <summary>Abre el cuadro y escribe el texto (comprobaciones del Editor).</summary>
        public bool DebugOpenWith(string text)
        {
            if (_open || !CanOpen()) return false;
            Open();
            _field.SetValueWithoutNotify(text);
            return true;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _field?.Blur();
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            UiTextEntry.SetActive(false);
            if (_player != null) _player.SetInputLocked(false, false);
        }

        private void HandleCinematicStarted(string id) => Close();
        private void HandlePlayerCaught() => Close();
        private void HandlePlotTwist() => Close();
    }
}
