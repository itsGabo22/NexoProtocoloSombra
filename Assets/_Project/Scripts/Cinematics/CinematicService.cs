using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Nexo.Accessibility;
using Nexo.Core;
using Nexo.Narrative;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
#if NEXO_VIDEO
using UnityEngine.Video;
#endif
using Cursor = UnityEngine.Cursor;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Reproductor de cinemáticas a pantalla completa (intro, transición de piso, final). Se crea solo al arrancar
    /// (RuntimeInitializeOnLoadMethod, DontDestroyOnLoad): no hay que añadirlo a ninguna escena.
    /// API: CinematicService.Play(id, onFinished). El clip es StreamingAssets/Cinematics/&lt;id&gt;.mp4; si no existe se
    /// registra una vez y onFinished se llama al instante: el juego nunca se bloquea ni lanza por un clip ausente o roto.
    ///
    /// Render: VideoPlayer → RenderTexture → VisualElement de un UIDocument propio (PS_Nexo, sortingOrder 1000).
    /// Se descarta CameraNearPlane porque en Built-in los paneles de UI Toolkit se dibujan después de las cámaras:
    /// HUD y menús quedarían encima del vídeo. Con la RT el orden es seguro, el 16:9 se respeta con
    /// background-size: contain sobre fondo negro, y el aviso de salto y los subtítulos viven en el mismo panel.
    ///
    /// Durante el clip se reutiliza la pausa del juego (Time.timeScale = 0, como el menú de pausa): IA, NavMesh,
    /// forcejeo, micrófono y física se congelan. El VideoPlayer usa tiempo no escalado y su AudioSource ignora
    /// AudioListener.pause, que silencia el resto del audio. Además se bloquea al jugador y se oculta el HUD
    /// (GameEvents.OnCinematicStarted). Al terminar, saltar o fallar se restaura exactamente el estado previo.
    ///
    /// Requiere el módulo integrado Video (com.unity.modules.video en Packages/manifest.json): Nexo.asmdef define
    /// NEXO_VIDEO solo si está. Sin él el juego compila igual y cada Play() continúa al instante (se registra una vez).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinematicService : MonoBehaviour
    {
        private const string SkipHintText = "Esc / Espacio: saltar";
        private const float FirstFrameTimeoutSeconds = 1f;
        private const float EndSafetyMarginSeconds = 2f;

        private static CinematicService _instance;
        private static int _lastFinishedFrame = -10;
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();

        private CinematicSettings _settings;
#if NEXO_VIDEO
        private VideoPlayer _video;
#endif
        private AudioSource _audio;
        private RenderTexture _renderTexture;
        private UIDocument _document;
        private VisualElement _overlay;
        private VisualElement _videoElement;
        private Label _caption;
        private Label _hint;
        private InputAction _skipAction;

        private bool _playing;
        private string _currentId;
        private Action _onFinished;
        private Coroutine _routine;
        private bool _overlayVisible;
#if !NEXO_VIDEO
#pragma warning disable CS0414 // sin módulo Video la rutina que lo lee no se compila
#endif
        private bool _skipRequested;
#if !NEXO_VIDEO
#pragma warning restore CS0414
#endif
        private bool _ended;
        private bool _errored;
        private float _elapsed;
        private CinematicCaptionTrack _captions = CinematicCaptionTrack.Empty;
        private int _captionIndex = -1;

        // Estado previo (se restaura tal cual)
        private bool _frozen;
        private float _savedTimeScale = 1f;
        private bool _savedAudioPause;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;
        private PlayerMovement _player;
        private PlayerMovement _lockedPlayer;
        private bool _savedInputLocked;

        /// <summary>True mientras se prepara o reproduce una cinemática.</summary>
        public static bool IsPlaying => _instance != null && _instance._playing;

        /// <summary>Id de la cinemática en curso (vacío si no hay ninguna).</summary>
        public static string CurrentId => IsPlaying ? _instance._currentId : string.Empty;

        /// <summary>Segundos de reproducción visibles del clip en curso (0 si aún no se ve).</summary>
        public static float Elapsed => IsPlaying ? _instance._elapsed : 0f;

        /// <summary>True si el clip en curso ya se ve en pantalla (preparado y con primer fotograma).</summary>
        public static bool IsShowingVideo => IsPlaying && _instance._overlayVisible;

        /// <summary>
        /// True durante la cinemática y el fotograma siguiente: la tecla que la salta (Esc) no debe abrir la pausa.
        /// </summary>
        public static bool BlocksGameplayInput => IsPlaying || Time.frameCount - _lastFinishedFrame <= 1;

        /// <summary>RenderTexture del clip en curso (null si no hay ninguno). Para comprobaciones automáticas.</summary>
        public static RenderTexture CurrentTexture => IsPlaying ? _instance._renderTexture : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _lastFinishedFrame = -10;
            MissingLogged.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("[CinematicService]");
            DontDestroyOnLoad(go);
            go.AddComponent<CinematicService>();
        }

        /// <summary>True si existe StreamingAssets/Cinematics/&lt;id&gt;.mp4.</summary>
        public static bool HasClip(string id)
        {
            string path = CinematicPaths.VideoPath(Application.streamingAssetsPath, id);
            return path.Length > 0 && File.Exists(path);
        }

        /// <summary>
        /// Reproduce la cinemática id y llama a onFinished al acabar, al saltarla o si falla. Si el clip no existe,
        /// si ya hay otra en curso o si el servicio no está activo, llama a onFinished al instante.
        /// </summary>
        public static void Play(string id, Action onFinished)
        {
            if (_instance == null)
            {
                InvokeSafely(onFinished);
                return;
            }
            _instance.PlayInternal(id, onFinished);
        }

        /// <summary>Pide saltar la cinemática en curso. Devuelve false si no se acepta (aún no van 0,5 s de vídeo).</summary>
        public static bool RequestSkip() => _instance != null && _instance.TrySkip();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.ignoreListenerPause = true; // el resto del audio se pausa con AudioListener.pause

#if NEXO_VIDEO
            _video = gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.source = VideoSource.Url;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; // sigue con Time.timeScale = 0
            _video.skipOnDrop = true;
            _video.waitForFirstFrame = true;
            _video.audioOutputMode = VideoAudioOutputMode.AudioSource;
#endif

            _skipAction = new InputAction("CinematicSkip", InputActionType.Button);
            _skipAction.AddBinding("<Keyboard>/escape");
            _skipAction.AddBinding("<Keyboard>/space");
            _skipAction.AddBinding("<Keyboard>/enter");
            _skipAction.AddBinding("<Keyboard>/numpadEnter");
            _skipAction.AddBinding("<Mouse>/leftButton");

            CreateDocument();
        }

        private void OnEnable()
        {
            if (_instance != this) return;

            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnSubtitle += HandleSubtitle;
#if NEXO_VIDEO
            _video.loopPointReached += HandleLoopPointReached;
            _video.errorReceived += HandleErrorReceived;
#endif
            _skipAction.performed += HandleSkipPerformed;
        }

        private void OnDisable()
        {
            if (_instance != this) return;

            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnSubtitle -= HandleSubtitle;
#if NEXO_VIDEO
            _video.loopPointReached -= HandleLoopPointReached;
            _video.errorReceived -= HandleErrorReceived;
#endif
            _skipAction.performed -= HandleSkipPerformed;

            // Nunca dejar el juego congelado
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_playing) Finish();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            _skipAction?.Dispose();
            ReleaseRenderTexture();
            _instance = null;
        }

        // ---------- Reproducción ----------

        private void PlayInternal(string id, Action onFinished)
        {
            if (_playing)
            {
                Debug.LogWarning($"[CinematicService] Se pidió '{id}' mientras suena '{_currentId}': se omite.", this);
                InvokeSafely(onFinished);
                return;
            }

            string path = CinematicPaths.VideoPath(Application.streamingAssetsPath, id);
            if (path.Length == 0 || !File.Exists(path))
            {
                if (MissingLogged.Add(id ?? string.Empty))
                {
                    Debug.Log($"[CinematicService] No hay clip para '{id}' ({(path.Length > 0 ? path : "id no válido")}): se continúa sin cinemática.", this);
                }
                InvokeSafely(onFinished);
                return;
            }

#if !NEXO_VIDEO
            if (MissingLogged.Add("module:" + id))
            {
                Debug.Log($"[CinematicService] '{id}' existe, pero el módulo Video no está activo en Packages/manifest.json: se continúa sin cinemática.", this);
            }
            InvokeSafely(onFinished);
            return;
#else
            _playing = true;
            _currentId = id;
            _onFinished = onFinished;
            _skipRequested = false;
            _ended = false;
            _errored = false;
            _elapsed = 0f;
            _routine = StartCoroutine(PlaybackRoutine(path));
#endif
        }

#if NEXO_VIDEO

        private IEnumerator PlaybackRoutine(string path)
        {
            // Primero se cierran las UI modales (terminal, red de cámaras, forcejeo) y se oculta el HUD; luego se
            // guarda el estado ya «en juego» y se congela
            GameEvents.RaiseCinematicStarted(_currentId);
            Freeze();
            LoadCaptions();

            _video.url = path;
            _video.controlledAudioTrackCount = 1;
            _video.EnableAudioTrack(0, true);
            _video.SetTargetAudioSource(0, _audio);
            _audio.volume = _settings != null ? _settings.Volume : 1f;
            _video.Prepare();

            float waited = 0f;
            while (!_video.isPrepared && !_errored)
            {
                if (CinematicSkipRule.PrepareTimedOut(waited))
                {
                    Debug.LogWarning($"[CinematicService] '{_currentId}' no se preparó en {CinematicSkipRule.PrepareTimeoutSeconds:0} s: se salta.", this);
                    break;
                }
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            if (!_video.isPrepared || _errored)
            {
                Finish();
                yield break;
            }

            EnsureRenderTexture((int)_video.width, (int)_video.height);
            _video.targetTexture = _renderTexture;
            _video.Play();

            // Se espera el primer fotograma para no enseñar una textura vacía
            float firstFrameWait = 0f;
            while (_video.frame < 0 && !_errored && !_ended && firstFrameWait < FirstFrameTimeoutSeconds)
            {
                yield return null;
                firstFrameWait += Time.unscaledDeltaTime;
            }

            ShowOverlay();
            _skipAction.Enable();

            double length = _video.length;
            bool hintShown = false;
            while (!_ended && !_errored && !_skipRequested)
            {
                if (!hintShown && CinematicSkipRule.ShowHint(_elapsed))
                {
                    hintShown = true;
                    ShowHint();
                }
                UpdateCaptions(_video.time);

                // Red de seguridad: un clip que no avisa de su final no puede bloquear el juego
                if (length > 0d && _elapsed > length + EndSafetyMarginSeconds) break;

                yield return null;
                _elapsed += Time.unscaledDeltaTime;
            }

            // Salto: se termina un fotograma después de la pulsación (Espacio no salta ni dispara al devolver el control)
            Finish();
        }
#endif

        private void Finish()
        {
            if (!_playing) return;

            _skipAction.Disable();
#if NEXO_VIDEO
            if (_video != null)
            {
                _video.Stop();
                _video.targetTexture = null;
            }
#endif
            HideOverlay();
            ReleaseRenderTexture();
            if (_captions.Count > 0) NarrativeSubtitles.Clear();
            _captions = CinematicCaptionTrack.Empty;
            _captionIndex = -1;

            Restore();

            string id = _currentId;
            Action callback = _onFinished;
            _onFinished = null;
            _currentId = null;
            _playing = false;
            _overlayVisible = false;
            _routine = null;
            _lastFinishedFrame = Time.frameCount;

            GameEvents.RaiseCinematicFinished(id);
            InvokeSafely(callback);
        }

        private bool TrySkip()
        {
            if (!_playing || !_overlayVisible || !CinematicSkipRule.CanSkip(_elapsed)) return false;
            _skipRequested = true;
            return true;
        }

        private static void InvokeSafely(Action callback)
        {
            if (callback == null) return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ---------- Congelar / restaurar ----------

        private void Freeze()
        {
            _savedTimeScale = Time.timeScale;
            _savedAudioPause = AudioListener.pause;
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;

            _lockedPlayer = _player;
            if (_lockedPlayer != null)
            {
                _savedInputLocked = _lockedPlayer.IsInputLocked;
                _lockedPlayer.SetInputLocked(true, false);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _frozen = true;
        }

        private void Restore()
        {
            if (!_frozen) return;
            _frozen = false;

            Time.timeScale = _savedTimeScale;
            AudioListener.pause = _savedAudioPause;
            if (_lockedPlayer != null) _lockedPlayer.SetInputLocked(_savedInputLocked, _savedCursorVisible);
            _lockedPlayer = null;
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
        }

        // ---------- Eventos ----------

        private void HandlePlayerSpawned(Transform player)
        {
            _player = player != null ? player.GetComponent<PlayerMovement>() : null;
        }

#if NEXO_VIDEO
        private void HandleLoopPointReached(VideoPlayer source)
        {
            _ended = true;
        }

        private void HandleErrorReceived(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[CinematicService] Error de vídeo en '{_currentId}': {message}. Se continúa sin cinemática.", this);
            _errored = true;
        }
#endif

        private void HandleSkipPerformed(InputAction.CallbackContext context)
        {
            TrySkip();
        }

        private void HandleSubtitle(string text)
        {
            // Solo se muestran sobre el vídeo los subtítulos de la propia cinemática
            if (!_playing || _caption == null || _captions.Count == 0) return;

            _caption.text = text ?? string.Empty;
            _caption.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ---------- Subtítulos ----------

        private void LoadCaptions()
        {
            _captions = CinematicCaptionTrack.Empty;
            _captionIndex = -1;

            string captionsPath = CinematicPaths.CaptionsPath(Application.streamingAssetsPath, _currentId);
            if (captionsPath.Length == 0 || !File.Exists(captionsPath)) return;

            try
            {
                _captions = CinematicCaptionTrack.Parse(File.ReadAllText(captionsPath));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[CinematicService] No se pudieron leer los subtítulos de '{_currentId}': {e.Message}", this);
            }
        }

        private void UpdateCaptions(double time)
        {
            if (_captions.Count == 0) return;

            int index = _captions.IndexAt(time);
            if (index == _captionIndex) return;

            _captionIndex = index;
            if (index < 0)
            {
                NarrativeSubtitles.Clear();
                return;
            }
            CinematicCaption line = _captions.Lines[index];
            NarrativeSubtitles.Say(line.speaker, line.text);
        }

        // ---------- Capa de UI Toolkit ----------

        private void CreateDocument()
        {
            _settings = Resources.Load<CinematicSettings>(CinematicSettings.ResourcePath);
            PanelSettings panel = _settings != null ? _settings.PanelSettings : null;
            if (panel == null)
            {
                Debug.LogWarning($"[CinematicService] Falta Resources/{CinematicSettings.ResourcePath} con PS_Nexo: se usa un PanelSettings propio.", this);
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
            }

            var child = new GameObject("CinematicOverlay");
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            _document = child.AddComponent<UIDocument>();
            _document.panelSettings = panel;
            _document.sortingOrder = _settings != null ? _settings.SortingOrder : 1000f;
            child.SetActive(true);

            BuildOverlay();
        }

        private void BuildOverlay()
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            root.pickingMode = PickingMode.Ignore;

            _overlay = new VisualElement { name = "cinematic-overlay" };
            Stretch(_overlay);
            _overlay.style.backgroundColor = Color.black;
            _overlay.style.display = DisplayStyle.None;
            _overlay.pickingMode = PickingMode.Position; // bloquea clics a lo que haya debajo
            root.Add(_overlay);

            _videoElement = new VisualElement { name = "cinematic-video" };
            Stretch(_videoElement);
            _videoElement.pickingMode = PickingMode.Ignore;
            // Letterbox: el clip conserva su proporción (16:9) sobre el fondo negro
            _videoElement.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            _videoElement.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            _videoElement.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            _videoElement.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            _overlay.Add(_videoElement);

            _caption = new Label { name = "cinematic-caption" };
            _caption.pickingMode = PickingMode.Ignore;
            _caption.style.position = Position.Absolute;
            _caption.style.left = Length.Percent(12f);
            _caption.style.right = Length.Percent(12f);
            _caption.style.bottom = Length.Percent(7f);
            _caption.style.fontSize = 30f;
            _caption.style.color = Color.white;
            _caption.style.whiteSpace = WhiteSpace.Normal;
            _caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            _caption.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _caption.style.paddingLeft = 16f;
            _caption.style.paddingRight = 16f;
            _caption.style.paddingTop = 6f;
            _caption.style.paddingBottom = 6f;
            _caption.style.display = DisplayStyle.None;
            _overlay.Add(_caption);

            _hint = new Label(SkipHintText) { name = "cinematic-skip-hint" };
            _hint.pickingMode = PickingMode.Ignore;
            _hint.style.position = Position.Absolute;
            _hint.style.right = 48f;
            _hint.style.bottom = 32f;
            _hint.style.fontSize = 20f;
            _hint.style.color = new Color(1f, 1f, 1f, 0.75f);
            _hint.style.opacity = 0f;
            _overlay.Add(_hint);
        }

        private static void Stretch(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
        }

        private void ShowOverlay()
        {
            if (_overlay == null) BuildOverlay();
            if (_overlay == null) return;

            if (_renderTexture != null)
            {
                _videoElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_renderTexture));
            }

            // Aviso invisible al empezar; aparece con fundido (instantáneo con reducir movimiento)
            bool reduceMotion = AccessibilitySettings.Current.ReduceMotion;
            _hint.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            _hint.style.transitionDuration = new List<TimeValue> { new TimeValue(reduceMotion ? 0f : 400f, TimeUnit.Millisecond) };
            _hint.style.opacity = 0f;
            _caption.text = string.Empty;
            _caption.style.display = DisplayStyle.None;

            _overlay.style.display = DisplayStyle.Flex;
            _overlayVisible = true;
        }

        private void ShowHint()
        {
            if (_hint != null) _hint.style.opacity = 1f;
        }

        private void HideOverlay()
        {
            if (_overlay == null) return;

            _overlay.style.display = DisplayStyle.None;
            if (_videoElement != null) _videoElement.style.backgroundImage = StyleKeyword.None;
            if (_hint != null) _hint.style.opacity = 0f;
            if (_caption != null)
            {
                _caption.text = string.Empty;
                _caption.style.display = DisplayStyle.None;
            }
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }

            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;

            ReleaseRenderTexture();
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_Cinematic"
            };
            _renderTexture.Create();
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null) return;

#if NEXO_VIDEO
            if (_video != null && _video.targetTexture == _renderTexture) _video.targetTexture = null;
#endif
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
