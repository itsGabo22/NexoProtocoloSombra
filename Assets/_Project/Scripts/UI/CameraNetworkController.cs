using System;
using System.Collections.Generic;
using Nexo.Core;
using Nexo.Interaction;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Nexo.UI
{
    /// <summary>
    /// Red de cámaras NexoOS de los tres pisos (CameraNetwork.uxml). Vive junto al jugador en Systems_Persistent.
    /// Al abrirse cualquier SecurityTerminal aparece el aviso [C]; con [C] se abre la red sobre la terminal.
    /// - Piso cargado: imagen EN VIVO. Reutiliza la cámara de la escena si existe (SceneCameraName); si no,
    ///   usa UNA cámara compartida que solo renderiza mientras el panel está abierto.
    /// - Otros pisos: no están cargados (los pisos se cargan de uno en uno), así que muestra el ENLACE DIFERIDO:
    ///   el último fotograma archivado de esa cámara, con ruido y la hora de la última sincronía.
    /// La RenderTexture (640x360) se crea al abrir y se libera al cerrar. Sin Update: entrada por callbacks y
    /// animaciones con VisualElement.schedule. No modifica TerminalController ni SecurityTerminal.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class CameraNetworkController : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const string ActiveTabClass = "netcam-tab--active";
        private const string SelectedItemClass = "netcam-item--selected";
        private const string BurstClass = "netcam-static--burst";

        [Header("Datos")]
        [SerializeField] private SecurityCameraNetwork _network;

        [Header("Referencias")]
        [Tooltip("Para saber si la terminal sigue abierta (la terminal bloquea la entrada del jugador).")]
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private int _sortingOrder = 40;

        [Header("Render en vivo")]
        [SerializeField] private int _renderWidth = 640;
        [SerializeField] private int _renderHeight = 360;
        [SerializeField] private float _liveFarClip = 60f;

        /// <summary>Se emite al mostrar una cámara (id). Lo usan sustos ligados a la red (CameraFeedApparition).</summary>
        public static event Action<string> FeedViewed;

        private InputAction _toggleAction;
        private InputAction _upAction;
        private InputAction _downAction;
        private InputAction _leftAction;
        private InputAction _rightAction;

        private VisualElement _root;
        private VisualElement _launcher;
        private VisualElement _overlay;
        private VisualElement _tabs;
        private ScrollView _list;
        private VisualElement _image;
        private VisualElement _static;
        private VisualElement _scanlines;
        private Label _noSignal;
        private Label _feedId;
        private Label _feedStatus;
        private Label _feedClock;
        private Label _feedFloor;
        private Label _status;
        private Button _closeButton;

        private readonly List<CameraFeedDefinition> _floorFeeds = new();
        private readonly HashSet<string> _darkRooms = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Camera> _sceneCameraCache = new();

        private bool _terminalOpen;
        private bool _networkOpen;
        private int _currentFloor = 12;
        private int _selectedFloor;
        private int _selectedIndex = -1;
        private CameraFeedDefinition _selectedFeed;

        private Camera _liveCamera;
        private RenderTexture _liveTexture;
        private Camera _reusedCamera;
        private bool _reusedWasEnabled;
        private bool _reusedTextureAssigned;

        private Texture2D _noiseTexture;
        private Color32[] _noisePixels;
        private Texture2D _scanlineTexture;
        private Texture2D _ghostTexture;

        // Loop "vivo" del enlace diferido: paneo, barra de interferencia, silueta y reloj de reproducción
        private VisualElement _rollBar;
        private VisualElement _ghost;
        private IVisualElementScheduledItem _archiveLoop;
        private float _archiveStart;
        private float _nextGhostTime;
        private float _ghostStart = -1f;
        private float _ghostFrom;
        private float _ghostTo;
        private const float GhostDuration = 2.8f;
        private IVisualElementScheduledItem _watcher;
        private IVisualElementScheduledItem _clockTicker;
        private IVisualElementScheduledItem _archiveFlicker;

        public bool IsNetworkOpen => _networkOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => FeedViewed = null;

        private void Awake()
        {
            if (_playerMovement == null) _playerMovement = GetComponentInParent<PlayerMovement>();

            _toggleAction = new InputAction("CameraNetworkToggle", InputActionType.Button);
            _toggleAction.AddBinding("<Keyboard>/c");
            _toggleAction.AddBinding("<Gamepad>/buttonNorth");
            _upAction = CreateButton("NetUp", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            _downAction = CreateButton("NetDown", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");
            _leftAction = CreateButton("NetLeft", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            _rightAction = CreateButton("NetRight", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");

            BuildTextures();
        }

        private static InputAction CreateButton(string name, string keyboard, string gamepad)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(keyboard);
            action.AddBinding(gamepad);
            return action;
        }

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) return;
            document.sortingOrder = _sortingOrder;

            _root = document.rootVisualElement.Q<VisualElement>("netcam-root");
            if (_root == null)
            {
                Debug.LogWarning("[CameraNetworkController] No se encontró 'netcam-root' en el UIDocument.", this);
                return;
            }

            _launcher = _root.Q<VisualElement>("netcam-launcher");
            _overlay = _root.Q<VisualElement>("netcam-overlay");
            _tabs = _root.Q<VisualElement>("netcam-tabs");
            _list = _root.Q<ScrollView>("netcam-list");
            _image = _root.Q<VisualElement>("netcam-image");
            _static = _root.Q<VisualElement>("netcam-static");
            _scanlines = _root.Q<VisualElement>("netcam-scanlines");
            _noSignal = _root.Q<Label>("netcam-nosignal");
            _feedId = _root.Q<Label>("netcam-feed-id");
            _feedStatus = _root.Q<Label>("netcam-feed-status");
            _feedClock = _root.Q<Label>("netcam-feed-clock");
            _feedFloor = _root.Q<Label>("netcam-feed-floor");
            _status = _root.Q<Label>("netcam-status");
            _closeButton = _root.Q<Button>("netcam-close");

            if (_static != null && _noiseTexture != null) _static.style.backgroundImage = new StyleBackground(_noiseTexture);
            if (_scanlines != null && _scanlineTexture != null) _scanlines.style.backgroundImage = new StyleBackground(_scanlineTexture);
            BuildArchiveOverlays();

            _launcher?.AddToClassList(HiddenClass);
            _overlay?.AddToClassList(HiddenClass);
            _launcher?.RegisterCallback<ClickEvent>(HandleLauncherClicked);
            if (_closeButton != null) _closeButton.clicked += CloseNetwork;

            SecurityTerminal.Opened += HandleTerminalOpened;
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnLightsChanged += HandleLightsChanged;
            GameEvents.OnPlayerCaught += HideAll;
            GameEvents.OnCinematicStarted += HandleCinematicStarted;

            _toggleAction.performed += HandleTogglePerformed;
            _upAction.performed += HandleUp;
            _downAction.performed += HandleDown;
            _leftAction.performed += HandleLeft;
            _rightAction.performed += HandleRight;
        }

        private void OnDisable()
        {
            SecurityTerminal.Opened -= HandleTerminalOpened;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnLightsChanged -= HandleLightsChanged;
            GameEvents.OnPlayerCaught -= HideAll;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;

            _toggleAction.performed -= HandleTogglePerformed;
            _upAction.performed -= HandleUp;
            _downAction.performed -= HandleDown;
            _leftAction.performed -= HandleLeft;
            _rightAction.performed -= HandleRight;

            _launcher?.UnregisterCallback<ClickEvent>(HandleLauncherClicked);
            if (_closeButton != null) _closeButton.clicked -= CloseNetwork;

            HideAll();
        }

        private void OnDestroy()
        {
            _toggleAction?.Dispose();
            _upAction?.Dispose();
            _downAction?.Dispose();
            _leftAction?.Dispose();
            _rightAction?.Dispose();
            ReleaseLiveTexture();
            if (_liveCamera != null) Destroy(_liveCamera.gameObject);
            if (_noiseTexture != null) Destroy(_noiseTexture);
            if (_scanlineTexture != null) Destroy(_scanlineTexture);
            if (_ghostTexture != null) Destroy(_ghostTexture);
        }

        // ---------- Terminal y apertura ----------

        private void HandleTerminalOpened(SecurityTerminal terminal)
        {
            if (_root == null || _network == null) return;

            _terminalOpen = true;
            _launcher?.RemoveFromClassList(HiddenClass);
            _toggleAction.Enable();

            // La terminal se cierra con Esc o con su botón: al devolver el control al jugador se cierra todo
            _watcher?.Pause();
            _watcher = _root.schedule.Execute(CheckTerminalStillOpen).Every(250);
        }

        private void CheckTerminalStillOpen()
        {
            if (_playerMovement != null && !_playerMovement.IsInputLocked) HideAll();
        }

        private void HandleLauncherClicked(ClickEvent evt) => OpenNetwork();

        private void HandleTogglePerformed(InputAction.CallbackContext context)
        {
            if (!_terminalOpen) return;
            if (_networkOpen) CloseNetwork();
            else OpenNetwork();
        }

        public void OpenNetwork()
        {
            if (_networkOpen || !_terminalOpen || _overlay == null) return;

            _networkOpen = true;
            _launcher?.AddToClassList(HiddenClass);
            _overlay.RemoveFromClassList(HiddenClass);
            _upAction.Enable();
            _downAction.Enable();
            _leftAction.Enable();
            _rightAction.Enable();

            _clockTicker = _root.schedule.Execute(UpdateClock).Every(1000);
            BuildTabs();
            SelectFloor(ContainsFloor(_currentFloor) ? _currentFloor : _network.Floors[0]);
            SetStatus($"> Enlace establecido. Piso actual: {_currentFloor}. Señal en vivo solo en el piso actual.");
        }

        public void CloseNetwork()
        {
            if (!_networkOpen) return;

            _networkOpen = false;
            StopLive();
            ReleaseLiveTexture();
            _clockTicker?.Pause();
            _archiveFlicker?.Pause();
            StopArchiveLoop();
            ClearList();
            _overlay?.AddToClassList(HiddenClass);
            _upAction.Disable();
            _downAction.Disable();
            _leftAction.Disable();
            _rightAction.Disable();

            if (_terminalOpen) _launcher?.RemoveFromClassList(HiddenClass);
        }

        /// <summary>Una cinemática cierra la red (y libera su RenderTexture) como si se cerrara la terminal.</summary>
        private void HandleCinematicStarted(string id) => HideAll();

        private void HideAll()
        {
            CloseNetwork();
            _terminalOpen = false;
            _watcher?.Pause();
            _launcher?.AddToClassList(HiddenClass);
            _toggleAction?.Disable();
        }

        // ---------- Pestañas y lista ----------

        private void BuildTabs()
        {
            if (_tabs == null) return;
            ClearTabs();

            foreach (int floor in _network.Floors)
            {
                var tab = new Button { text = $"PISO {floor}" };
                tab.AddToClassList("netcam-tab");
                if (floor == _currentFloor)
                {
                    var here = new Label("● AQUÍ");
                    here.AddToClassList("netcam-tab__here");
                    tab.Add(here);
                }
                tab.RegisterCallback<ClickEvent, int>(HandleTabClicked, floor);
                _tabs.Add(tab);
            }
        }

        private void ClearTabs()
        {
            if (_tabs == null) return;
            foreach (VisualElement child in _tabs.Children()) child.UnregisterCallback<ClickEvent, int>(HandleTabClicked);
            _tabs.Clear();
        }

        private void HandleTabClicked(ClickEvent evt, int floor) => SelectFloor(floor);

        private void SelectFloor(int floor)
        {
            _selectedFloor = floor;
            int i = 0;
            foreach (VisualElement tab in _tabs.Children())
            {
                tab.EnableInClassList(ActiveTabClass, _network.Floors[i] == floor);
                i++;
            }

            _network.GetFeeds(floor, _floorFeeds);
            BuildList();
            if (_floorFeeds.Count > 0) SelectFeed(0);
            else ShowNoSignal("PISO SIN CÁMARAS REGISTRADAS");
        }

        private void BuildList()
        {
            ClearList();
            if (_list == null) return;

            for (int i = 0; i < _floorFeeds.Count; i++)
            {
                CameraFeedDefinition feed = _floorFeeds[i];
                FeedMode mode = ModeOf(feed);

                var item = new VisualElement();
                item.AddToClassList("netcam-item");
                item.AddToClassList(mode == FeedMode.Live ? "netcam-item--live" : mode == FeedMode.Dark ? "netcam-item--dark" : mode == FeedMode.Archive ? "netcam-item--archive" : "netcam-item--offline");

                var dot = new VisualElement();
                dot.AddToClassList("netcam-item__dot");
                var texts = new VisualElement();
                texts.AddToClassList("flex-col");
                var id = new Label($"CAM {feed.Id}");
                id.AddToClassList("netcam-item__id");
                var label = new Label(feed.Label);
                label.AddToClassList("netcam-item__label");
                texts.Add(id);
                texts.Add(label);
                var state = new Label(ModeText(mode));
                state.AddToClassList("netcam-item__state");

                item.Add(dot);
                item.Add(texts);
                item.Add(state);
                item.RegisterCallback<ClickEvent, int>(HandleItemClicked, i);
                _list.Add(item);
            }
        }

        private void ClearList()
        {
            if (_list == null) return;
            foreach (VisualElement child in _list.Children()) child.UnregisterCallback<ClickEvent, int>(HandleItemClicked);
            _list.Clear();
            _selectedIndex = -1;
            _selectedFeed = null;
        }

        private void HandleItemClicked(ClickEvent evt, int index) => SelectFeed(index);

        private void HandleUp(InputAction.CallbackContext context) { if (_networkOpen && _floorFeeds.Count > 0) SelectFeed((_selectedIndex - 1 + _floorFeeds.Count) % _floorFeeds.Count); }
        private void HandleDown(InputAction.CallbackContext context) { if (_networkOpen && _floorFeeds.Count > 0) SelectFeed((_selectedIndex + 1) % _floorFeeds.Count); }
        private void HandleLeft(InputAction.CallbackContext context) => CycleFloor(-1);
        private void HandleRight(InputAction.CallbackContext context) => CycleFloor(1);

        private void CycleFloor(int step)
        {
            if (!_networkOpen) return;
            int count = _network.Floors.Count;
            int index = 0;
            for (int i = 0; i < count; i++) if (_network.Floors[i] == _selectedFloor) index = i;
            SelectFloor(_network.Floors[(index + step + count) % count]);
        }

        // ---------- Selección de cámara ----------

        private enum FeedMode { Live, Dark, Archive, Offline }

        private FeedMode ModeOf(CameraFeedDefinition feed)
        {
            if (feed.Floor == _currentFloor) return !string.IsNullOrEmpty(feed.RoomId) && _darkRooms.Contains(feed.RoomId) ? FeedMode.Dark : FeedMode.Live;
            return feed.ArchiveFrame != null ? FeedMode.Archive : FeedMode.Offline;
        }

        private static string ModeText(FeedMode mode) => mode switch
        {
            FeedMode.Live => "EN VIVO",
            FeedMode.Dark => "SIN LUZ",
            FeedMode.Archive => "DIFERIDO",
            _ => "SIN SEÑAL"
        };

        private void SelectFeed(int index)
        {
            if (index < 0 || index >= _floorFeeds.Count) return;

            _selectedIndex = index;
            _selectedFeed = _floorFeeds[index];
            int i = 0;
            foreach (VisualElement item in _list.Children()) item.EnableInClassList(SelectedItemClass, i++ == index);

            _archiveFlicker?.Pause();
            StopArchiveLoop();
            PlayStaticBurst();
            StopLive();

            CameraFeedDefinition feed = _selectedFeed;
            FeedMode mode = ModeOf(feed);
            bool current = feed.Floor == _currentFloor;

            _noSignal?.AddToClassList(HiddenClass);
            _image.RemoveFromClassList("netcam-image--dark");
            _image.RemoveFromClassList("netcam-image--archive");
            SetOsd(_feedId, $"CAM {feed.Id} · {feed.Label}", null);
            SetOsd(_feedFloor, current ? $"PISO {feed.Floor} · ACTUAL" : $"PISO {feed.Floor} · REMOTO", null);

            switch (mode)
            {
                case FeedMode.Live:
                case FeedMode.Dark:
                    _image.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(StartLive(feed)));
                    if (mode == FeedMode.Dark) _image.AddToClassList("netcam-image--dark");
                    SetOsd(_feedStatus, mode == FeedMode.Dark ? "● IR · SALA SIN LUZ" : "● EN VIVO", mode == FeedMode.Dark ? "netcam-osd--dark" : "netcam-osd--live");
                    SetStatus(mode == FeedMode.Dark ? $"> {feed.Label}: iluminación caída. Imagen infrarroja de baja calidad." : $"> {feed.Label}: señal en vivo.");
                    break;
                case FeedMode.Archive:
                    _image.style.backgroundImage = new StyleBackground(feed.ArchiveFrame);
                    _image.AddToClassList("netcam-image--archive");
                    SetOsd(_feedStatus, "◌ ENLACE DIFERIDO · REPRODUCCIÓN", "netcam-osd--archive");
                    SetStatus($"> Piso {feed.Floor} fuera de alcance: reproduciendo la grabación desde las {DisplayTimestamp(feed)}.");
                    _archiveFlicker = _root.schedule.Execute(FlickerArchive).Every(160);
                    StartArchiveLoop();
                    break;
                default:
                    ShowNoSignal($"> CAM {feed.Id}: sin señal ni archivo disponible.");
                    break;
            }

            UpdateClock();
            FeedViewed?.Invoke(feed.Id);
        }

        private void ShowNoSignal(string status)
        {
            StopLive();
            _image.style.backgroundImage = StyleKeyword.None;
            _noSignal?.RemoveFromClassList(HiddenClass);
            SetOsd(_feedStatus, "✖ SIN SEÑAL", "netcam-osd--dark");
            SetStatus(status);
        }

        private void UpdateClock()
        {
            if (_feedClock == null || _selectedFeed == null) return;
            _feedClock.text = _selectedFeed.Floor == _currentFloor
                ? DateTime.Now.ToString("HH:mm:ss") + "  REC"
                : "▶ " + PlaybackTime(_selectedFeed);
        }

        /// <summary>Hora de la grabación: parte de la última sincronía y avanza en un bucle de 45 s.</summary>
        private string PlaybackTime(CameraFeedDefinition feed)
        {
            if (!TimeSpan.TryParse(feed.ArchiveTimestamp, out TimeSpan start)) return DisplayTimestamp(feed);
            float elapsed = (Time.unscaledTime - _archiveStart) % 45f;
            return (start + TimeSpan.FromSeconds(Mathf.Floor(elapsed))).ToString(@"hh\:mm\:ss");
        }

        // ---------- Loop "vivo" del enlace diferido ----------

        private void BuildArchiveOverlays()
        {
            VisualElement screen = _root.Q<VisualElement>("netcam-screen");
            if (screen == null || _rollBar != null) return;

            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = Length.Percent(9f);
            _ghost.style.height = Length.Percent(42f);
            _ghost.style.top = Length.Percent(44f);
            _ghost.style.backgroundImage = new StyleBackground(_ghostTexture);
            _ghost.style.display = DisplayStyle.None;

            _rollBar = new VisualElement { pickingMode = PickingMode.Ignore };
            _rollBar.style.position = Position.Absolute;
            _rollBar.style.left = 0;
            _rollBar.style.right = 0;
            _rollBar.style.height = Length.Percent(10f);
            _rollBar.style.backgroundColor = new Color(1f, 1f, 1f, 0.07f);
            _rollBar.style.display = DisplayStyle.None;

            // Encima de la imagen y por debajo del ruido y del OSD
            int index = screen.IndexOf(_image) + 1;
            screen.Insert(index, _ghost);
            screen.Insert(index + 1, _rollBar);
        }

        private void StartArchiveLoop()
        {
            if (_rollBar == null) return;
            _archiveStart = Time.unscaledTime;
            _nextGhostTime = _archiveStart + Random.Range(3f, 7f);
            _ghostStart = -1f;
            _rollBar.style.display = DisplayStyle.Flex;
            _archiveLoop = _root.schedule.Execute(TickArchiveLoop).Every(40);
        }

        private void StopArchiveLoop()
        {
            _archiveLoop?.Pause();
            _archiveLoop = null;
            if (_rollBar != null) _rollBar.style.display = DisplayStyle.None;
            if (_ghost != null) _ghost.style.display = DisplayStyle.None;
            if (_image != null)
            {
                _image.style.scale = StyleKeyword.Null;
                _image.style.translate = StyleKeyword.Null;
            }
        }

        private void TickArchiveLoop()
        {
            float t = Time.unscaledTime - _archiveStart;

            // Paneo y zoom muy lentos: la cámara "respira" como una PTZ real
            _image.style.scale = new Scale(new Vector3(1.07f, 1.07f, 1f));
            _image.style.translate = new Translate(Length.Percent(Mathf.Sin(t * 0.13f) * 1.6f), Length.Percent(Mathf.Cos(t * 0.09f) * 1.1f));

            // Barra de interferencia que recorre la imagen cada 6 s
            _rollBar.style.top = Length.Percent(((t / 6f) % 1f) * 110f - 10f);

            // Silueta que cruza el encuadre de vez en cuando
            float now = Time.unscaledTime;
            if (_ghostStart < 0f && now >= _nextGhostTime)
            {
                _ghostStart = now;
                bool leftToRight = Random.value < 0.5f;
                _ghostFrom = leftToRight ? 12f : 78f;
                _ghostTo = leftToRight ? 78f : 12f;
                _ghost.style.display = DisplayStyle.Flex;
            }
            if (_ghostStart >= 0f)
            {
                float k = (now - _ghostStart) / GhostDuration;
                if (k >= 1f)
                {
                    _ghostStart = -1f;
                    _ghost.style.display = DisplayStyle.None;
                    _nextGhostTime = now + Random.Range(9f, 16f);
                }
                else
                {
                    _ghost.style.left = Length.Percent(Mathf.Lerp(_ghostFrom, _ghostTo, k));
                    _ghost.style.opacity = Mathf.Sin(k * Mathf.PI) * 0.6f;
                }
            }
        }

        private static string DisplayTimestamp(CameraFeedDefinition feed) => string.IsNullOrEmpty(feed.ArchiveTimestamp) ? "--:--:--" : feed.ArchiveTimestamp;

        // ---------- Render en vivo ----------

        private RenderTexture StartLive(CameraFeedDefinition feed)
        {
            Camera sceneCamera = FindSceneCamera(feed.SceneCameraName);
            if (sceneCamera != null)
            {
                // Reutiliza la cámara existente del piso (p. ej. Cam_F12_01 con su RT_Cam_F12_01)
                _reusedCamera = sceneCamera;
                _reusedWasEnabled = sceneCamera.enabled;
                if (sceneCamera.targetTexture == null)
                {
                    sceneCamera.targetTexture = EnsureLiveTexture();
                    _reusedTextureAssigned = true;
                }
                sceneCamera.enabled = true;
                return sceneCamera.targetTexture;
            }

            EnsureLiveCamera();
            _liveCamera.transform.SetPositionAndRotation(feed.Position, Quaternion.Euler(feed.EulerAngles));
            _liveCamera.fieldOfView = feed.FieldOfView;
            _liveCamera.targetTexture = EnsureLiveTexture();
            _liveCamera.enabled = true;
            return _liveTexture;
        }

        private void StopLive()
        {
            if (_liveCamera != null) _liveCamera.enabled = false;

            if (_reusedCamera != null)
            {
                if (_reusedTextureAssigned) _reusedCamera.targetTexture = null;
                _reusedCamera.enabled = _reusedWasEnabled;
                _reusedCamera = null;
                _reusedTextureAssigned = false;
            }
        }

        private void EnsureLiveCamera()
        {
            if (_liveCamera != null) return;

            var go = new GameObject("NetCam_Live");
            go.transform.SetParent(transform, false);
            _liveCamera = go.AddComponent<Camera>();
            _liveCamera.enabled = false;
            _liveCamera.nearClipPlane = 0.05f;
            _liveCamera.farClipPlane = _liveFarClip;
            _liveCamera.depth = -10f;
            _liveCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            _liveCamera.allowHDR = false;
            _liveCamera.allowMSAA = false;
        }

        private RenderTexture EnsureLiveTexture()
        {
            if (_liveTexture != null) return _liveTexture;

            _liveTexture = new RenderTexture(_renderWidth, _renderHeight, 16, RenderTextureFormat.Default)
            {
                name = "RT_NetCam_Live",
                antiAliasing = 1
            };
            _liveTexture.Create();
            return _liveTexture;
        }

        private void ReleaseLiveTexture()
        {
            if (_liveTexture == null) return;
            if (_liveCamera != null) _liveCamera.targetTexture = null;
            _liveTexture.Release();
            Destroy(_liveTexture);
            _liveTexture = null;
        }

        private Camera FindSceneCamera(string cameraName)
        {
            if (string.IsNullOrEmpty(cameraName) || FloorMetadata.Active == null) return null;
            if (_sceneCameraCache.TryGetValue(cameraName, out Camera cached) && cached != null) return cached;

            // Búsqueda única por carga de piso en la escena activa (no en rutas calientes)
            foreach (GameObject root in FloorMetadata.Active.gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.name != cameraName) continue;
                    _sceneCameraCache[cameraName] = camera;
                    return camera;
                }
            }
            return null;
        }

        // ---------- Eventos del juego ----------

        private void HandleFloorChanged(int floor)
        {
            _currentFloor = floor;
            _darkRooms.Clear();
            _sceneCameraCache.Clear();
            HideAll();
        }

        private void HandleLightsChanged(string roomId, bool isOn)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            if (isOn) _darkRooms.Remove(roomId);
            else _darkRooms.Add(roomId);

            if (_networkOpen && _selectedFeed != null && string.Equals(_selectedFeed.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                int index = _selectedIndex;
                BuildList();
                SelectFeed(index);
            }
        }

        // ---------- Efectos ----------

        private void PlayStaticBurst()
        {
            if (_static == null) return;
            RegenerateNoise();
            // La opacidad inline del parpadeo de archivo taparía la de la clase
            _static.style.opacity = StyleKeyword.Null;
            _static.AddToClassList(BurstClass);
            // Al quitar la clase, la transición de opacidad de la USS desvanece el ruido
            _static.schedule.Execute(EndStaticBurst).StartingIn(90);
        }

        private void EndStaticBurst() => _static?.RemoveFromClassList(BurstClass);

        private void FlickerArchive()
        {
            if (_static == null) return;
            RegenerateNoise();
            _static.style.opacity = Random.Range(0.06f, 0.2f);
        }

        private void BuildTextures()
        {
            const int width = 192, height = 108;
            _noiseTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "NetCam_Noise" };
            _noisePixels = new Color32[width * height];
            RegenerateNoise();

            _scanlineTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "NetCam_Scanlines" };
            _scanlineTexture.SetPixels32(new[] { new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 150), new Color32(0, 0, 0, 40) });
            _scanlineTexture.Apply();
            _ghostTexture = BuildGhostTexture();
        }

        /// <summary>Silueta humana borrosa (cabeza, hombros y torso) generada en código: sin assets externos.</summary>
        private static Texture2D BuildGhostTexture()
        {
            const int w = 48, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "NetCam_Ghost", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x - w * 0.5f) / (w * 0.5f);
                    float v = y / (float)h; // 0 abajo, 1 arriba
                    float headDist = new Vector2(u * 1.6f, (v - 0.88f) * 4.2f).magnitude;
                    float bodyHalf = v > 0.78f ? 0f : Mathf.Lerp(0.55f, 0.95f, Mathf.Clamp01((v - 0.1f) / 0.6f));
                    float alpha = Mathf.Max(Mathf.Clamp01((1f - headDist) * 3f), Mathf.Clamp01((bodyHalf - Mathf.Abs(u)) * 4f));
                    alpha *= Mathf.Clamp01(v * 6f); // pies difuminados
                    pixels[y * w + x] = new Color32(6, 8, 10, (byte)(alpha * 235f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void RegenerateNoise()
        {
            if (_noiseTexture == null) return;
            for (int i = 0; i < _noisePixels.Length; i++)
            {
                byte v = (byte)Random.Range(0, 256);
                _noisePixels[i] = new Color32(v, v, v, 255);
            }
            _noiseTexture.SetPixels32(_noisePixels);
            _noiseTexture.Apply(false);
        }

        private bool ContainsFloor(int floor)
        {
            foreach (int f in _network.Floors) if (f == floor) return true;
            return false;
        }

        private static void SetOsd(Label label, string text, string modifier)
        {
            if (label == null) return;
            label.text = text;
            label.RemoveFromClassList("netcam-osd--live");
            label.RemoveFromClassList("netcam-osd--archive");
            label.RemoveFromClassList("netcam-osd--dark");
            if (!string.IsNullOrEmpty(modifier)) label.AddToClassList(modifier);
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }
    }
}
