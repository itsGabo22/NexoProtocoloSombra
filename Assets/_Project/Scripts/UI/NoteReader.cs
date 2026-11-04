using Nexo.Cinematics;
using Nexo.Core;
using Nexo.Narrative;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Lectura de notas como papel físico y diario [J]. Al leer una nota (NarrativeEvents.NoteRead) se oye el papel, la hoja
    /// sube desde abajo (animación corta de «coger») y ocupa la pantalla con letra mecanografiada (IBM Plex Mono) sobre
    /// fondo de papel; el texto largo se desplaza. [E] o [Esc] la cierran. Mientras se lee, el jugador queda quieto (como
    /// en la terminal) y el mundo sigue. Cada nota entra en NoteJournal; [J] lista las recogidas para releerlas.
    /// Construido por código sobre el UIDocument del HUD; sin Update (acciones de entrada y scheduler de UI Toolkit).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class NoteReader : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.91f, 0.87f, 0.76f, 1f);
        private static readonly Color Ink = new Color(0.12f, 0.11f, 0.1f, 1f);

        [SerializeField] private Font _font;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _paperClip;

        private InputAction _closeAction;
        private InputAction _journalAction;
        private VisualElement _overlay;
        private VisualElement _sheet;
        private Label _author;
        private Label _body;
        private ScrollView _scroll;
        private VisualElement _journal;
        private ScrollView _journalList;
        private PlayerMovement _player;
        private bool _lockedByUs;
        private int _openedFrame;

        public static NoteReader Current { get; private set; }
        public bool IsReading { get; private set; }
        public bool IsJournalOpen { get; private set; }
        public string CurrentAuthor => _author != null ? _author.text : string.Empty;

        private void Awake()
        {
            _closeAction = new InputAction("NoteClose", InputActionType.Button);
            _closeAction.AddBinding("<Keyboard>/e");
            _closeAction.AddBinding("<Keyboard>/escape");
            _closeAction.AddBinding("<Gamepad>/buttonEast");
            _journalAction = new InputAction("NoteJournal", InputActionType.Button, "<Keyboard>/j");
        }

        private void OnEnable()
        {
            Current = this;
            Build();
            _closeAction.performed += HandleClosePerformed;
            _journalAction.performed += HandleJournalPerformed;
            _journalAction.Enable();
            NarrativeEvents.NoteRead += HandleNoteRead;
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnCinematicStarted += HandleForceClose;
            GameEvents.OnPlayerCaught += HandleForceCloseNoArg;
            GameEvents.OnPlayerAttacked += HandlePlayerAttacked;
            GameEvents.OnPlotTwistTriggered += HandleForceCloseNoArg;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            _closeAction.performed -= HandleClosePerformed;
            _journalAction.performed -= HandleJournalPerformed;
            _closeAction.Disable();
            _journalAction.Disable();
            NarrativeEvents.NoteRead -= HandleNoteRead;
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnCinematicStarted -= HandleForceClose;
            GameEvents.OnPlayerCaught -= HandleForceCloseNoArg;
            GameEvents.OnPlayerAttacked -= HandlePlayerAttacked;
            GameEvents.OnPlotTwistTriggered -= HandleForceCloseNoArg;
            CloseAll();
        }

        private void OnDestroy()
        {
            _closeAction?.Dispose();
            _journalAction?.Dispose();
        }

        // ---------- Construcción ----------

        private void Build()
        {
            var document = GetComponent<UIDocument>();
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null || _overlay != null) return;

            _overlay = new VisualElement { name = "hud-note-overlay", pickingMode = PickingMode.Position };
            Stretch(_overlay);
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;

            _sheet = new VisualElement { name = "hud-note-sheet" };
            _sheet.style.width = Length.Percent(46);
            _sheet.style.maxHeight = Length.Percent(82);
            _sheet.style.backgroundColor = Paper;
            _sheet.style.paddingLeft = 40; _sheet.style.paddingRight = 40; _sheet.style.paddingTop = 34; _sheet.style.paddingBottom = 26;
            _sheet.style.rotate = new Rotate(new Angle(-1.2f));
            _sheet.style.borderBottomWidth = 3; _sheet.style.borderBottomColor = new Color(0.6f, 0.55f, 0.45f, 1f);
            _sheet.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("translate"), new StylePropertyName("opacity") };
            _sheet.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0.28f, TimeUnit.Second) };

            _author = new Label { name = "hud-note-author" };
            ApplyFont(_author, 15, new Color(0.4f, 0.12f, 0.1f, 1f));
            _author.style.marginBottom = 14;
            _author.style.unityFontStyleAndWeight = FontStyle.Bold;
            _sheet.Add(_author);

            _scroll = new ScrollView(ScrollViewMode.Vertical) { name = "hud-note-scroll" };
            _scroll.style.flexShrink = 1;
            _body = new Label { name = "hud-note-body" };
            ApplyFont(_body, 19, Ink);
            _body.style.whiteSpace = WhiteSpace.Normal;
            _body.style.unityParagraphSpacing = 10;
            _scroll.Add(_body);
            _sheet.Add(_scroll);

            var hint = new Label("[E] / [Esc] cerrar   ·   [J] diario de notas");
            ApplyFont(hint, 12, new Color(0.35f, 0.32f, 0.28f, 1f));
            hint.style.marginTop = 18;
            hint.style.unityTextAlign = TextAnchor.MiddleRight;
            _sheet.Add(hint);
            _overlay.Add(_sheet);

            // Diario
            _journal = new VisualElement { name = "hud-note-journal", pickingMode = PickingMode.Position };
            Stretch(_journal);
            _journal.style.backgroundColor = new Color(0.01f, 0.03f, 0.05f, 0.9f);
            _journal.style.alignItems = Align.Center;
            _journal.style.justifyContent = Justify.Center;
            _journal.style.display = DisplayStyle.None;
            var panel = new VisualElement();
            panel.style.width = Length.Percent(44);
            panel.style.maxHeight = Length.Percent(80);
            panel.style.backgroundColor = Paper;
            panel.style.paddingLeft = 28; panel.style.paddingRight = 28; panel.style.paddingTop = 24; panel.style.paddingBottom = 20;
            var title = new Label("DIARIO · NOTAS RECOGIDAS");
            ApplyFont(title, 18, Ink);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            panel.Add(title);
            _journalList = new ScrollView(ScrollViewMode.Vertical);
            _journalList.style.flexShrink = 1;
            panel.Add(_journalList);
            var close = new Label("[J] / [Esc] cerrar");
            ApplyFont(close, 12, new Color(0.35f, 0.32f, 0.28f, 1f));
            close.style.marginTop = 12;
            close.style.unityTextAlign = TextAnchor.MiddleRight;
            panel.Add(close);
            _journal.Add(panel);

            root.Add(_journal);
            root.Add(_overlay);
        }

        private static void Stretch(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        private void ApplyFont(Label label, int size, Color color)
        {
            label.style.fontSize = size;
            label.style.color = color;
            if (_font != null) label.style.unityFontDefinition = FontDefinition.FromFont(_font);
        }

        // ---------- Lectura ----------

        private void HandlePlayerSpawned(Transform player) => _player = player != null ? player.GetComponent<PlayerMovement>() : null;

        private void HandleNoteRead(string author, string body) => Open(author, body);

        /// <summary>Muestra una nota como papel a pantalla completa.</summary>
        public void Open(string author, string body)
        {
            if (_overlay == null || string.IsNullOrWhiteSpace(body)) return;
            if (CinematicService.BlocksGameplayInput || NarrativeEvents.TwistHasPlayed) return;
            if (IsJournalOpen) HideJournal(false);

            _author.text = string.IsNullOrWhiteSpace(author) ? "NOTA" : author.Trim().ToUpperInvariant();
            _body.text = HudTextSanitizer.Humanize(body.Trim());
            _scroll.scrollOffset = Vector2.zero;
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
            // «Coger» la hoja: sube desde abajo con un leve giro
            _sheet.style.translate = new Translate(0, Length.Percent(60));
            _sheet.style.opacity = 0f;
            _sheet.schedule.Execute(RaiseSheet).StartingIn(16);

            if (_audio != null && _paperClip != null) _audio.PlayOneShot(_paperClip, 0.9f);
            IsReading = true;
            _openedFrame = Time.frameCount;
            // Mientras se lee, E/Esc/F/1-3/Tab no llegan al juego (y cerrar con Esc no abre la pausa)
            UiTextEntry.SetActive(true);
            LockPlayer();
            _closeAction.Enable();
        }

        private void RaiseSheet()
        {
            _sheet.style.translate = new Translate(0, 0);
            _sheet.style.opacity = 1f;
        }

        public void Close()
        {
            if (!IsReading) return;
            IsReading = false;
            _overlay.style.display = DisplayStyle.None;
            UiTextEntry.SetActive(false);
            if (!IsJournalOpen) { _closeAction.Disable(); UnlockPlayer(); }
        }

        private void HandleClosePerformed(InputAction.CallbackContext context)
        {
            // La misma [E] que abrió la nota no la cierra
            if (Time.frameCount - _openedFrame <= 1) return;
            if (IsReading) { Close(); return; }
            if (IsJournalOpen) HideJournal(true);
        }

        // ---------- Diario ----------

        private void HandleJournalPerformed(InputAction.CallbackContext context)
        {
            if (UiTextEntry.IsActive && !IsJournalOpen && !IsReading) return;
            if (IsJournalOpen) { HideJournal(true); return; }
            if (IsReading) Close();
            ShowJournal();
        }

        public void ShowJournal()
        {
            if (_journal == null || CinematicService.BlocksGameplayInput || NarrativeEvents.TwistHasPlayed) return;
            if (!IsReading && (_player == null || (_player.IsInputLocked && !_lockedByUs) || UnityEngine.Cursor.lockState != CursorLockMode.Locked)) return;
            _journalList.Clear();
            if (NoteJournal.Count == 0)
            {
                var empty = new Label("Aún no has recogido ninguna nota.");
                ApplyFont(empty, 15, Ink);
                _journalList.Add(empty);
            }
            foreach (NoteJournal.Note note in NoteJournal.Notes)
            {
                NoteJournal.Note captured = note;
                var row = new Button { text = $"PISO {note.Floor:00} · {note.Author}" };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.backgroundColor = new Color(0.82f, 0.78f, 0.67f, 1f);
                row.style.color = Ink;
                row.style.marginBottom = 6;
                if (_font != null) row.style.unityFontDefinition = FontDefinition.FromFont(_font);
                row.userData = captured;
                row.RegisterCallback<ClickEvent>(HandleJournalRowClicked);
                _journalList.Add(row);
            }
            _journal.style.display = DisplayStyle.Flex;
            _journal.BringToFront();
            IsJournalOpen = true;
            _openedFrame = Time.frameCount;
            LockPlayer();
            _closeAction.Enable();
            // El diario usa el cursor para elegir una nota y bloquea las teclas rápidas
            UiTextEntry.SetActive(true);
        }

        /// <summary>Cierra el diario (como [J]); también lo usan las comprobaciones del Editor.</summary>
        public void CloseJournal()
        {
            if (IsJournalOpen) HideJournal(true);
        }

        private void HandleJournalRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement row && row.userData is NoteJournal.Note note) Open(note.Author, note.Body);
        }

        private void HideJournal(bool unlock)
        {
            IsJournalOpen = false;
            if (_journal != null) _journal.style.display = DisplayStyle.None;
            UiTextEntry.SetActive(false);
            if (unlock && !IsReading) { _closeAction.Disable(); UnlockPlayer(); }
        }

        private void LockPlayer()
        {
            if (_player == null || _lockedByUs) return;
            if (_player.IsInputLocked) return; // otra UI ya lo bloquea (no se le devuelve el control al cerrar)
            _player.SetInputLocked(true, true);
            _lockedByUs = true;
        }

        private void UnlockPlayer()
        {
            if (!_lockedByUs) return;
            _lockedByUs = false;
            if (_player != null) _player.SetInputLocked(false, false);
        }

        private void CloseAll()
        {
            if (IsReading) { IsReading = false; UiTextEntry.SetActive(false); if (_overlay != null) _overlay.style.display = DisplayStyle.None; }
            if (IsJournalOpen) HideJournal(false);
            _closeAction.Disable();
            UnlockPlayer();
        }

        private void HandleForceClose(string id) => CloseAll();
        private void HandleForceCloseNoArg() => CloseAll();
        private void HandlePlayerAttacked(Transform attacker) => CloseAll();
    }
}
