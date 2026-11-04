using System;
using System.Collections.Generic;
using Nexo.Accessibility;
using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Nexo.UI
{
    /// <summary>
    /// Controlador del HUD diegético NexoOS v4.2 construido con UI Toolkit.
    /// Cero Update(): arquitectura totalmente reactiva por eventos y scheduling de UIElements.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class HUDController : MonoBehaviour
    {
        private const string ThemeNormal = "theme-nexo";
        private const string ThemeHive = "theme-hive";
        private const int BarCount = 24;
        private const long NotificationMs = 5500;
        private const float GlitchThreshold = 0.05f;
        private const string PortraitResource = "Elena/portrait";
        private const long PickupVisibleMs = 1500;
        private const long WeaponPulseMs = 300;
        private const long HitFlashMs = 250;
        private const long LowHealthPulseMs = 1000;
        private const long HintFadeMs = 400;
        private const long NotificationPulseMs = 180;

        /// <summary>Tarjeta de notificación visible y sus temporizadores (la decisión la toma NotificationStack).</summary>
        private sealed class NotificationCard
        {
            public VisualElement Element;
            public IVisualElementScheduledItem Dismiss;
            public IVisualElementScheduledItem PulseEnd;
            /// <summary>Vida de la tarjeta según su texto (MessageTiming).</summary>
            public long LifetimeMs;
        }

        private static readonly string[] RootDebugLines =
        {
            "> SUJETO: ALEX",
            "> INFECCIÓN NEURO-ACÚSTICA: 98.4% (FASE TERMINAL)",
            "> TIEMPO DE INFECCIÓN: 00:14:22",
            "> SUPRESIÓN DE DOLOR: ACTIVA",
            "> REALIDAD AUMENTADA CORRECTIVA: OCULTANDO VENAS NEGRAS",
            "> PROTOCOLO ACTIVO: ENGAÑO SENSORIAL PARA ESCOLTA DE VIP (ELENA)",
            "> ESTADO DEL PROTOCOLO: MISIÓN CUMPLIDA"
        };

        [SerializeField] private bool _reduceMotion;

        [Header("Órdenes Tácticas de Elena")]
        [Tooltip("DoorId por defecto de HIGHLIGHT_SAFE_ROUTE cuando Elena no indica target_id (se muestra traducido con DoorDisplayNames).")]
        [SerializeField] private string _safeRouteDefaultTarget = "F12_Door_Stairs";
        [SerializeField] private float _radarRangeMeters = 18f;

        private VisualElement _root;
        private bool _hiddenByCinematic;
        private StyleEnum<DisplayStyle> _displayBeforeCinematic;
        private VisualElement _spectrogram;
        private VisualElement _notifications;
        private VisualElement _glitch;
        private VisualElement _debug;
        private VisualElement _capture;
        private VisualElement _crosshair;
        private VisualElement _fade;
        private IVisualElementScheduledItem _fadeStep;
        private float _fadeFrom;
        private float _fadeTo;
        private float _fadeStart;
        private float _fadeSeconds;
        private IVisualElementScheduledItem _hitMarkerEnd;
        private VisualElement _portrait;
        private VisualElement _trustFill;
        private VisualElement _batteryFill;

        private Label _vitalsValue;
        private Label _vitalsStatus;
        private Label _bpm;
        private Label _dialogue;
        private Label _emotionLabel;
        private Label _noiseValue;
        private Label _batteryValue;
        private Label _debugText;
        private Label _typing;
        private Label _seal;
        private Label _interactPrompt;
        private Label _vitalsInfection;

        // Panel de arma
        private VisualElement _weaponPanel;
        private VisualElement _weaponCells;
        private Label _weaponAmmo;
        private Label _weaponStatus;
        private Label _weaponPickup;
        private readonly List<VisualElement> _cells = new();
        private string _weaponStatusClass = string.Empty;
        private int _magazine;
        private int _reserve;
        private int _magazineSize;
        private bool _reloading;

        // Feedback de daño y ayuda de controles
        private VisualElement _damage;
        private Label _controlsHint;
        private int _lastHealthPercent = VitalsFeedback.Unknown;
        private bool _lowHealth;
        private bool _hudCorrupted;

        private readonly List<VisualElement> _bars = new();
        private readonly float[] _barLevels = new float[BarCount];
        private readonly Button[] _quick = new Button[3];
        private readonly Action[] _quickHandlers = new Action[3];
        private readonly NotificationStack _notificationStack = new NotificationStack(CurrentTime);
        private readonly Dictionary<int, NotificationCard> _cards = new();

        private IVisualElementScheduledItem _typewriter;
        private IVisualElementScheduledItem _spectro;
        private IVisualElementScheduledItem _pulse;
        private IVisualElementScheduledItem _glitchPulse;
        private IVisualElementScheduledItem _pickupHide;
        private IVisualElementScheduledItem _weaponPulseEnd;
        private IVisualElementScheduledItem _hitEnd;
        private IVisualElementScheduledItem _lowPulse;
        private IVisualElementScheduledItem _hintHide;
        private string _emotionClass;
        private float _noise;
        private int _infectedNearby;

        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        private void HandleAccessibilityChanged(AccessibilityOptions options)
        {
            SetReduceMotion(options.ReduceMotion);
            // Con reducir movimiento el pulso de salud baja pasa a ser una viñeta fija
            RefreshLowHealth();
        }

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc == null || uiDoc.rootVisualElement == null) return;

            _root = uiDoc.rootVisualElement.Q<VisualElement>("hud-root");
            if (_root == null) return;

            BindElements();
            BuildSpectrogram();

            GameEvents.OnNoiseChanged += HandleNoise;
            GameEvents.OnBatteryChanged += HandleBattery;
            GameEvents.OnVitalsChanged += HandleVitals;
            GameEvents.OnNotification += ShowNotification;
            GameEvents.OnElenaResponse += HandleElenaResponse;
            GameEvents.OnElenaThinking += HandleElenaThinking;
            GameEvents.OnQuickQuestionsChanged += HandleQuickLabels;
            GameEvents.OnHudCorruptionRequested += PlayCorruptionSequence;
            GameEvents.OnPlayerCaught += ShowCaptureScreen;
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnUiAction += HandleUiAction;
            GameEvents.OnInfectedNearbyChanged += HandleInfectedNearbyChanged;
            GameEvents.OnAmmoChanged += HandleAmmoChanged;
            GameEvents.OnWeaponReloadChanged += HandleWeaponReloadChanged;
            GameEvents.OnAmmoPicked += HandleAmmoPicked;
            GameEvents.OnCinematicStarted += HandleCinematicStarted;
            GameEvents.OnCinematicFinished += HandleCinematicFinished;
            GameEvents.OnScreenFade += HandleScreenFade;
            GameEvents.OnHitConfirmed += HandleHitConfirmed;
            AccessibilitySettings.Changed += HandleAccessibilityChanged;
            HandleAccessibilityChanged(AccessibilitySettings.Current);

            for (int i = 0; i < _quick.Length; i++)
            {
                int index = i;
                _quickHandlers[i] = () => GameEvents.RaiseQuickQuestionSelected(index);
                if (_quick[i] != null) _quick[i].clicked += _quickHandlers[i];
            }

            _spectro = _root.schedule.Execute(TickSpectrogram).Every(80);
            if (_portrait != null)
            {
                _pulse = _portrait.schedule.Execute(PulsePortrait).Every(700);
            }
        }

        private void OnDisable()
        {
            GameEvents.OnNoiseChanged -= HandleNoise;
            GameEvents.OnBatteryChanged -= HandleBattery;
            GameEvents.OnVitalsChanged -= HandleVitals;
            GameEvents.OnNotification -= ShowNotification;
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            GameEvents.OnElenaThinking -= HandleElenaThinking;
            GameEvents.OnQuickQuestionsChanged -= HandleQuickLabels;
            GameEvents.OnHudCorruptionRequested -= PlayCorruptionSequence;
            GameEvents.OnPlayerCaught -= ShowCaptureScreen;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnUiAction -= HandleUiAction;
            GameEvents.OnInfectedNearbyChanged -= HandleInfectedNearbyChanged;
            GameEvents.OnAmmoChanged -= HandleAmmoChanged;
            GameEvents.OnWeaponReloadChanged -= HandleWeaponReloadChanged;
            GameEvents.OnAmmoPicked -= HandleAmmoPicked;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;
            GameEvents.OnCinematicFinished -= HandleCinematicFinished;
            GameEvents.OnScreenFade -= HandleScreenFade;
            GameEvents.OnHitConfirmed -= HandleHitConfirmed;
            AccessibilitySettings.Changed -= HandleAccessibilityChanged;
            _fadeStep?.Pause();
            _hitMarkerEnd?.Pause();
            if (_fade != null) _fade.style.opacity = 0f;
            if (_hiddenByCinematic) HandleCinematicFinished(string.Empty);

            for (int i = 0; i < _quick.Length; i++)
            {
                if (_quick[i] != null && _quickHandlers[i] != null)
                {
                    _quick[i].clicked -= _quickHandlers[i];
                }
            }

            _spectro?.Pause();
            _typewriter?.Pause();
            _pulse?.Pause();
            _glitchPulse?.Pause();
            _glitch?.RemoveFromClassList("hud-glitch-pulse");
            _pickupHide?.Pause();
            _weaponPulseEnd?.Pause();
            _hitEnd?.Pause();
            _lowPulse?.Pause();
            _hintHide?.Pause();
            ClearNotifications();
        }

        /// <summary>Reloj de la pila de notificaciones (tiempo real: no depende de Time.timeScale).</summary>
        private static double CurrentTime() => Time.realtimeSinceStartupAsDouble;

        private void PulsePortrait()
        {
            if (_portrait != null)
            {
                _portrait.ToggleInClassList("pulse-on");
            }
        }

        private void BindElements()
        {
            _vitalsValue = _root.Q<Label>("hud-vitals-value");
            _vitalsStatus = _root.Q<Label>("hud-vitals-status");
            _bpm = _root.Q<Label>("hud-bpm");
            _dialogue = _root.Q<Label>("radio-dialogue");
            _emotionLabel = _root.Q<Label>("radio-emotion");
            _typing = _root.Q<Label>("radio-typing");
            _seal = _root.Q<Label>("radio-seal");
            _noiseValue = _root.Q<Label>("hud-noise-value");
            _batteryValue = _root.Q<Label>("hud-battery-value");
            _debugText = _root.Q<Label>("hud-debug-text");
            _interactPrompt = _root.Q<Label>("hud-interact");
            _vitalsInfection = _root.Q<Label>("hud-vitals-infection");
            _weaponPanel = _root.Q<VisualElement>("hud-weapon");
            _weaponCells = _root.Q<VisualElement>("hud-weapon-cells");
            _weaponAmmo = _root.Q<Label>("hud-weapon-ammo");
            _weaponStatus = _root.Q<Label>("hud-weapon-status");
            _weaponPickup = _root.Q<Label>("hud-weapon-pickup");
            _damage = _root.Q<VisualElement>("hud-damage");
            _controlsHint = _root.Q<Label>("hud-controls-hint");

            _portrait = _root.Q<VisualElement>("radio-elena-portrait");
            _trustFill = _root.Q<VisualElement>("radio-trust-fill");
            _batteryFill = _root.Q<VisualElement>("hud-battery-fill");
            _spectrogram = _root.Q<VisualElement>("hud-spectrogram");
            _notifications = _root.Q<VisualElement>("hud-notifications");
            _glitch = _root.Q<VisualElement>("hud-glitch");
            _debug = _root.Q<VisualElement>("hud-debug");
            _capture = _root.Q<VisualElement>("hud-capture");
            _crosshair = _root.Q<VisualElement>("hud-crosshair");
            BuildFadeOverlay();

            for (int i = 0; i < _quick.Length; i++)
            {
                _quick[i] = _root.Q<Button>($"radio-quick-{i + 1}");
            }

            ApplyPortrait();
        }

        /// <summary>
        /// Retrato de Elena opcional (Assets/_Project/Resources/Elena/portrait.png). Si no existe se mantiene el círculo
        /// vacío; Resources.Load devuelve null sin registrar nada en consola.
        /// </summary>
        private void ApplyPortrait()
        {
            if (_portrait == null) return;
            Texture2D portrait = Resources.Load<Texture2D>(PortraitResource);
            if (portrait == null) return;

            _portrait.style.backgroundImage = new StyleBackground(portrait);
            _portrait.AddToClassList("radio-portrait--image");
        }

        private void BuildSpectrogram()
        {
            if (_spectrogram == null) return;
            _spectrogram.Clear();
            _bars.Clear();

            for (int i = 0; i < BarCount; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("spectrogram-bar");
                _spectrogram.Add(bar);
                _bars.Add(bar);
            }
        }

        private void HandleNoise(float value)
        {
            _noise = Mathf.Clamp(value, 0f, 100f);
            if (_noiseValue != null) _noiseValue.text = Mathf.RoundToInt(_noise).ToString();
            if (_spectrogram != null) _spectrogram.EnableInClassList("noise-high", _noise >= 66f);
        }

        private void TickSpectrogram()
        {
            for (int i = 0; i < _bars.Count; i++)
            {
                float target = Mathf.Max(0.04f, (_noise / 100f) * Random.Range(0.4f, 1f));
                _barLevels[i] = Mathf.Lerp(_barLevels[i], target, 0.5f);
                _bars[i].style.height = Length.Percent(_barLevels[i] * 100f);
            }
        }

        private void HandleBattery(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (_batteryFill != null)
            {
                _batteryFill.style.width = Length.Percent(normalized * 100f);
                _batteryFill.EnableInClassList("low", normalized < 0.2f);
            }
            if (_batteryValue != null)
            {
                _batteryValue.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }
        }

        private void HandleVitals(VitalsData data)
        {
            // Tras el giro el HUD ya dice la verdad («98.4% INFECTADO»): no se vuelve a pisar
            if (_hudCorrupted) return;

            if (_vitalsValue != null) _vitalsValue.text = data.ValueText;
            if (_vitalsStatus != null) _vitalsStatus.text = data.StatusText;
            if (_bpm != null) _bpm.text = $"♥ {data.HeartRate} BPM";

            if (!VitalsFeedback.TryParsePercent(data.ValueText, out int percent)) return;
            if (VitalsFeedback.IsDamage(_lastHealthPercent, percent)) FlashDamage();
            _lastHealthPercent = percent;
            _lowHealth = VitalsFeedback.IsLowHealth(percent);
            RefreshLowHealth();
        }

        // ---------- Feedback de daño ----------

        /// <summary>Destello rojo breve en los bordes al recibir un golpe; se omite con reducir movimiento.</summary>
        private void FlashDamage()
        {
            if (_reduceMotion || _damage == null) return;

            _hitEnd?.Pause();
            _damage.AddToClassList("damage-vignette--hit");
            _hitEnd = _damage.schedule.Execute(EndDamageFlash).StartingIn(HitFlashMs);
        }

        private void EndDamageFlash()
        {
            _damage?.RemoveFromClassList("damage-vignette--hit");
        }

        /// <summary>Viñeta de salud baja: pulso lento, o fija si está activo reducir movimiento.</summary>
        private void RefreshLowHealth()
        {
            if (_damage == null) return;

            bool active = _lowHealth && !_hudCorrupted;
            _damage.EnableInClassList("damage-vignette--low", active);

            if (active && !_reduceMotion)
            {
                if (_lowPulse == null) _lowPulse = _damage.schedule.Execute(ToggleLowHealthPulse).Every(LowHealthPulseMs);
                else _lowPulse.Resume();
                return;
            }

            _lowPulse?.Pause();
            _damage.RemoveFromClassList("damage-vignette--low-peak");
        }

        private void ToggleLowHealthPulse()
        {
            _damage?.ToggleInClassList("damage-vignette--low-peak");
        }

        // ---------- Panel de arma ----------

        private void HandleAmmoChanged(int magazine, int reserve, int magazineSize)
        {
            _magazine = magazine;
            _reserve = reserve;
            _magazineSize = magazineSize;
            RefreshWeapon();
        }

        private void HandleWeaponReloadChanged(bool isReloading)
        {
            _reloading = isReloading;
            RefreshWeapon();
        }

        private void RefreshWeapon()
        {
            if (_weaponPanel == null) return;
            _weaponPanel.RemoveFromClassList("hidden");

            AmmoStatus status = AmmoDisplayState.Resolve(_magazine, _reserve, _magazineSize, _reloading);
            if (_weaponAmmo != null) _weaponAmmo.text = AmmoDisplayState.Counter(_magazine, _reserve, _magazineSize);
            if (_weaponStatus != null) _weaponStatus.text = AmmoDisplayState.StatusText(status);

            if (_weaponStatusClass.Length > 0) _weaponPanel.RemoveFromClassList(_weaponStatusClass);
            _weaponStatusClass = AmmoDisplayState.StatusClass(status);
            if (_weaponStatusClass.Length > 0) _weaponPanel.AddToClassList(_weaponStatusClass);

            RefreshCells();
        }

        private void RefreshCells()
        {
            if (_weaponCells == null) return;

            int count = AmmoDisplayState.CellCount(_magazineSize);
            if (_cells.Count != count)
            {
                _weaponCells.Clear();
                _cells.Clear();
                for (int i = 0; i < count; i++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("hud-weapon-cell");
                    _weaponCells.Add(cell);
                    _cells.Add(cell);
                }
            }

            int filled = AmmoDisplayState.FilledCells(_magazine, _magazineSize);
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].EnableInClassList("hud-weapon-cell--filled", i < filled);
            }
        }

        /// <summary>«+N» junto al nombre del arma y un pulso breve del panel (sin pulso con reducir movimiento).</summary>
        /// <summary>Durante una cinemática se oculta todo el HUD; al terminar recupera el display que tenía.</summary>
        /// <summary>Capa negra a pantalla completa para el fundido de reaparición (creada por código, no bloquea el ratón).</summary>
        private void BuildFadeOverlay()
        {
            if (_fade != null) return;
            _fade = new VisualElement { name = "hud-fade", pickingMode = PickingMode.Ignore };
            _fade.style.position = Position.Absolute;
            _fade.style.left = 0; _fade.style.right = 0; _fade.style.top = 0; _fade.style.bottom = 0;
            _fade.style.backgroundColor = Color.black;
            _fade.style.opacity = 0f;
            _root.Add(_fade);
        }

        private void HandleScreenFade(float alpha, float seconds)
        {
            if (_fade == null) return;
            _fade.BringToFront();
            _fadeStep?.Pause();
            _fadeFrom = _fade.resolvedStyle.opacity;
            _fadeTo = Mathf.Clamp01(alpha);
            _fadeStart = Time.unscaledTime;
            _fadeSeconds = Mathf.Max(0f, seconds);
            if (_fadeSeconds <= 0f || _reduceMotion) { _fade.style.opacity = _fadeTo; return; }
            _fadeStep = _fade.schedule.Execute(StepFade).Every(16).Until(FadeDone);
        }

        private void StepFade()
        {
            float k = Mathf.Clamp01((Time.unscaledTime - _fadeStart) / _fadeSeconds);
            _fade.style.opacity = Mathf.Lerp(_fadeFrom, _fadeTo, k);
        }

        private bool FadeDone()
        {
            bool done = Time.unscaledTime - _fadeStart >= _fadeSeconds;
            if (done) _fade.style.opacity = _fadeTo;
            return done;
        }

        /// <summary>Marca de impacto: la mira crece un instante (más si el disparo mata).</summary>
        private void HandleHitConfirmed(bool killed)
        {
            if (_crosshair == null) return;
            float scale = killed ? 1.9f : 1.45f;
            _crosshair.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _hitMarkerEnd?.Pause();
            _hitMarkerEnd = _crosshair.schedule.Execute(EndHitMarker).StartingIn(killed ? 180 : 110);
        }

        private void EndHitMarker()
        {
            if (_crosshair != null) _crosshair.style.scale = StyleKeyword.Null;
        }

        private void HandleCinematicStarted(string id)
        {
            if (_root == null || _hiddenByCinematic) return;

            _displayBeforeCinematic = _root.style.display;
            _hiddenByCinematic = true;
            _root.style.display = DisplayStyle.None;
        }

        private void HandleCinematicFinished(string id)
        {
            if (_root == null || !_hiddenByCinematic) return;

            _hiddenByCinematic = false;
            _root.style.display = _displayBeforeCinematic;
        }

        private void HandleAmmoPicked(int rounds)
        {
            string text = AmmoDisplayState.PickupText(rounds);
            if (_weaponPickup == null || text.Length == 0) return;

            _weaponPickup.text = text;
            _weaponPickup.AddToClassList("hud-weapon__pickup--visible");
            _pickupHide?.Pause();
            _pickupHide = _weaponPickup.schedule.Execute(HidePickup).StartingIn(PickupVisibleMs);

            if (_reduceMotion || _weaponPanel == null) return;
            _weaponPulseEnd?.Pause();
            _weaponPanel.AddToClassList("hud-weapon--pulse");
            _weaponPulseEnd = _weaponPanel.schedule.Execute(EndWeaponPulse).StartingIn(WeaponPulseMs);
        }

        private void HidePickup()
        {
            _weaponPickup?.RemoveFromClassList("hud-weapon__pickup--visible");
        }

        private void EndWeaponPulse()
        {
            _weaponPanel?.RemoveFromClassList("hud-weapon--pulse");
        }

        // ---------- Ayuda de controles ----------

        /// <summary>Muestra la franja de controles una vez por sesión (los reinicios de piso no la repiten).</summary>
        private void ShowControlsHintOnce()
        {
            if (_controlsHint == null || !ControlsHint.TryConsume()) return;

            _controlsHint.text = ControlsHint.Text;
            _controlsHint.RemoveFromClassList("hidden");
            _controlsHint.schedule.Execute(RevealControlsHint).StartingIn(20);
            _hintHide = _controlsHint.schedule.Execute(FadeControlsHint).StartingIn(ControlsHint.VisibleMs);
        }

        private void RevealControlsHint()
        {
            _controlsHint?.AddToClassList("hud-controls-hint--visible");
        }

        private void FadeControlsHint()
        {
            if (_controlsHint == null) return;
            _controlsHint.RemoveFromClassList("hud-controls-hint--visible");
            _hintHide = _controlsHint.schedule.Execute(HideControlsHint).StartingIn(_reduceMotion ? 0 : HintFadeMs);
        }

        private void HideControlsHint()
        {
            _controlsHint?.AddToClassList("hidden");
        }

        private void HandleElenaThinking(bool thinking)
        {
            if (_typing != null) _typing.EnableInClassList("hidden", !thinking);
        }

        private void HandleElenaResponse(ElenaResponse response)
        {
            if (response == null) return;
            if (_typing != null) _typing.AddToClassList("hidden");

            SetEmotion(response.ParsedEmotion);
            if (_trustFill != null)
            {
                _trustFill.style.width = Length.Percent(Mathf.Clamp(response.trust_level, 0, 100));
            }
            StartTypewriter(response.dialogue);

            if (response.glitch_intensity > GlitchThreshold) PulseGlitch(response.glitch_intensity);
        }

        /// <summary>
        /// Un único destello breve de interferencia (WCAG 2.3.1); se omite si está activo reducir movimiento.
        /// </summary>
        private void PulseGlitch(float intensity)
        {
            if (_reduceMotion || _glitch == null) return;

            _glitchPulse?.Pause();
            _glitch.AddToClassList("hud-glitch-pulse");

            long durationMs = 80 + (long)(Mathf.Clamp01(intensity) * 220f);
            _glitchPulse = _glitch.schedule.Execute(EndGlitchPulse).StartingIn(durationMs);
        }

        private void EndGlitchPulse()
        {
            _glitch.RemoveFromClassList("hud-glitch-pulse");
        }

        private void SetEmotion(ElenaEmotion emotion)
        {
            if (_portrait == null) return;
            if (_emotionClass != null) _portrait.RemoveFromClassList(_emotionClass);

            _emotionClass = "emotion-" + emotion.ToString().ToLowerInvariant();
            _portrait.AddToClassList(_emotionClass);
            if (_emotionLabel != null) _emotionLabel.text = ElenaEmotionLabels.ToSpanish(emotion);
        }

        private void StartTypewriter(string text)
        {
            if (_dialogue == null) return;
            _typewriter?.Pause();

            // Solo se traduce lo que se muestra: la respuesta de Elena queda intacta
            text = HudTextSanitizer.Humanize(text) ?? string.Empty;
            int shown = 0;
            _dialogue.text = string.Empty;
            _typewriter = _dialogue.schedule.Execute(() =>
            {
                shown = Mathf.Min(shown + 2, text.Length);
                _dialogue.text = text.Substring(0, shown);
            }).Every(25).Until(() => shown >= text.Length);
        }

        private void HandleQuickLabels(string[] labels)
        {
            for (int i = 0; i < _quick.Length; i++)
            {
                if (_quick[i] == null) continue;
                bool has = labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]);
                _quick[i].SetEnabled(has);
                if (has) _quick[i].text = $"[{i + 1}] {labels[i]}";
            }
        }

        private void HandleInfectedNearbyChanged(int count)
        {
            _infectedNearby = Mathf.Max(0, count);
        }

        /// <summary>
        /// Traduce las órdenes tácticas de Elena (ya validadas por ElenaUiCommandRouter) en notificaciones del HUD.
        /// </summary>
        private void HandleUiAction(ElenaUiAction action, string targetId)
        {
            switch (action)
            {
                case ElenaUiAction.PingEnemyRadar:
                    string plural = _infectedNearby == 1 ? "firma hostil" : "firmas hostiles";
                    ShowNotification(new HudNotification("RADAR", $"⚠ {_infectedNearby} {plural} a menos de {Mathf.RoundToInt(_radarRangeMeters)} m. Muévete en silencio.", false));
                    break;

                case ElenaUiAction.HighlightSafeRoute:
                    // Nunca se muestra el DoorId técnico: se traduce a un nombre en español
                    string destination = DoorDisplayNames.Destination(string.IsNullOrEmpty(targetId) ? _safeRouteDefaultTarget : targetId);
                    ShowNotification(new HudNotification("RUTA SEGURA", $"➜ Ruta despejada hacia {destination}. Mantente agachado.", false));
                    _crosshair?.AddToClassList("crosshair--route");
                    _root.schedule.Execute(ClearRouteHighlight).StartingIn(NotificationMs);
                    break;

                case ElenaUiAction.ShowHint:
                    string hint = string.IsNullOrEmpty(targetId) ? "Revisa la terminal de seguridad: controla las puertas del piso." : targetId;
                    ShowNotification(new HudNotification("PISTA", $"ℹ {hint}", false));
                    break;
            }
        }

        private void ClearRouteHighlight()
        {
            _crosshair?.RemoveFromClassList("crosshair--route");
        }

        private void ShowCaptureScreen()
        {
            if (_capture == null || !_capture.ClassListContains("hidden")) return;

            _capture.RemoveFromClassList("hidden");
            _capture.schedule.Execute(AddCaptureVisible).StartingIn(20);
        }

        private void AddCaptureVisible()
        {
            _capture.AddToClassList("hud-capture--visible");
        }

        private void HandleFloorChanged(int floor)
        {
            ShowControlsHintOnce();

            // El piso se (re)cargó: retirar la pantalla de captura con fundido
            if (_capture == null || _capture.ClassListContains("hidden")) return;

            _capture.RemoveFromClassList("hud-capture--visible");
            _capture.schedule.Execute(HideCapture).StartingIn(400);
        }

        private void HideCapture()
        {
            _capture.AddToClassList("hidden");
        }

        public void SetInteractPrompt(string prompt)
        {
            if (_interactPrompt == null) return;
            bool hasPrompt = !string.IsNullOrEmpty(prompt);
            _interactPrompt.EnableInClassList("hidden", !hasPrompt);
            _interactPrompt.text = HudTextSanitizer.Humanize(prompt);
        }

        /// <summary>
        /// Muestra una notificación con los ids internos traducidos. Una idéntica ya visible solo reinicia su tiempo de vida
        /// (con un pulso breve salvo reducir movimiento); hay como máximo NotificationStack.DefaultMaxVisible tarjetas.
        /// </summary>
        public void ShowNotification(HudNotification data)
        {
            if (_notifications == null) return;

            string senderText = HudTextSanitizer.Humanize(data.Sender) ?? string.Empty;
            string bodyText = HudTextSanitizer.Humanize(data.Body) ?? string.Empty;

            NotificationResult result = _notificationStack.Push(senderText, bodyText, data.IsFake);
            switch (result.Action)
            {
                case NotificationAction.Suppress:
                    return;
                case NotificationAction.Refresh:
                    RefreshCard(result.CardId);
                    return;
            }

            if (result.EvictedId != NotificationResult.None) RemoveCardNow(result.EvictedId);

            var card = new VisualElement();
            card.AddToClassList("hud-notification");
            card.AddToClassList("flex-col");
            card.EnableInClassList("hud-notification--fake", data.IsFake);

            var header = new VisualElement();
            header.AddToClassList("flex-row");
            header.AddToClassList("items-center");
            header.AddToClassList("justify-between");

            var sender = new Label(senderText);
            sender.AddToClassList("hud-notification__sender");
            sender.AddToClassList("text-sm");
            sender.AddToClassList("text-accent");
            header.Add(sender);

            // Anomalía 1: el mensaje falso omite el sello BIO-ID
            if (!data.IsFake)
            {
                var seal = new Label("✔ BIO-ID");
                seal.AddToClassList("hud-seal");
                seal.AddToClassList("text-xs");
                header.Add(seal);
            }

            var body = new Label(bodyText);
            body.AddToClassList("text-sm");
            body.AddToClassList("text-main");
            body.AddToClassList("wrap");
            body.AddToClassList("mt-1");

            card.Add(header);
            card.Add(body);
            card.Add(BuildWave(data.IsFake)); // Anomalía 2: forma de onda irregular
            _notifications.Add(card);

            card.schedule.Execute(() => card.AddToClassList("hud-notification--visible")).StartingIn(20);

            int cardId = result.CardId;
            // Cada tarjeta dura lo que se tarda en leerla (mín. 3,5 s, máx. 11 s; más con «lectura lenta»)
            long lifetime = (long)(MessageTiming.Seconds(senderText.Length + bodyText.Length, 0f, MessageTiming.ReadingSpeed) * 1000f);
            var state = new NotificationCard { Element = card, LifetimeMs = System.Math.Max(NotificationMs, lifetime) };
            state.Dismiss = card.schedule.Execute(() => ExpireCard(cardId)).StartingIn(state.LifetimeMs);
            _cards[cardId] = state;
        }

        /// <summary>Reinicia el tiempo de vida de una tarjeta idéntica ya visible.</summary>
        private void RefreshCard(int cardId)
        {
            if (!_cards.TryGetValue(cardId, out NotificationCard state)) return;

            state.Dismiss?.Pause();
            state.Dismiss = state.Element.schedule.Execute(() => ExpireCard(cardId)).StartingIn(state.LifetimeMs);

            if (_reduceMotion) return;
            state.PulseEnd?.Pause();
            state.Element.AddToClassList("hud-notification--pulse");
            state.PulseEnd = state.Element.schedule.Execute(() => state.Element.RemoveFromClassList("hud-notification--pulse")).StartingIn(NotificationPulseMs);
        }

        /// <summary>Fin del tiempo de vida: la tarjeta se desvanece y deja de contar como visible.</summary>
        private void ExpireCard(int cardId)
        {
            if (!_cards.TryGetValue(cardId, out NotificationCard state)) return;

            _cards.Remove(cardId);
            _notificationStack.Expire(cardId);
            DismissCard(state.Element);
        }

        /// <summary>Retira al instante la tarjeta más antigua cuando se supera el tope (sin esperar al fundido).</summary>
        private void RemoveCardNow(int cardId)
        {
            if (!_cards.TryGetValue(cardId, out NotificationCard state)) return;

            _cards.Remove(cardId);
            state.Dismiss?.Pause();
            state.PulseEnd?.Pause();
            state.Element.RemoveFromHierarchy();
        }

        private void ClearNotifications()
        {
            foreach (NotificationCard state in _cards.Values)
            {
                state.Dismiss?.Pause();
                state.PulseEnd?.Pause();
                state.Element.RemoveFromHierarchy();
            }
            _cards.Clear();
            _notificationStack.Clear();
        }

        private static VisualElement BuildWave(bool irregular)
        {
            var wave = new VisualElement();
            wave.AddToClassList("flex-row");
            wave.AddToClassList("items-end");
            wave.AddToClassList("h-4");
            wave.AddToClassList("mt-1");

            for (int i = 0; i < 16; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("wave-bar");
                float level = irregular ? Random.Range(0.1f, 1f) : 0.3f + 0.6f * Mathf.Abs(Mathf.Sin(i * 0.45f));
                bar.style.height = Length.Percent(level * 100f);
                wave.Add(bar);
            }
            return wave;
        }

        private static void DismissCard(VisualElement card)
        {
            card.RemoveFromClassList("hud-notification--visible");
            card.schedule.Execute(() => card.RemoveFromHierarchy()).StartingIn(300);
        }

        public void PlayCorruptionSequence()
        {
            _spectro?.Pause();
            // Desde aquí los vitales de PlayerHealth ya no sobrescriben la verdad ni se muestra la viñeta de daño
            _hudCorrupted = true;
            _hitEnd?.Pause();
            _damage?.RemoveFromClassList("damage-vignette--hit");
            RefreshLowHealth();
            long t = 0;

            // 1) Destellos controlados (máximo 3; 0 si se reduce movimiento por WCAG 2.3.1)
            if (!_reduceMotion && _glitch != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    _root.schedule.Execute(() => _glitch.AddToClassList("hud-glitch-critical")).StartingIn(t);
                    _root.schedule.Execute(() => _glitch.RemoveFromClassList("hud-glitch-critical")).StartingIn(t + 120);
                    t += 380;
                }
            }

            // 2) Dispersión visual de paneles
            _root.schedule.Execute(ShatterPanels).StartingIn(t);
            t += 900;

            // 3) Conmutación a Colmena (rojo) y panel ROOT DEBUG
            _root.schedule.Execute(() =>
            {
                _root.RemoveFromClassList(ThemeNormal);
                _root.AddToClassList(ThemeHive);
                _root.AddToClassList("hud-infected-mode");

                if (_vitalsValue != null) _vitalsValue.text = "98.4% INFECTADO";
                if (_vitalsStatus != null) _vitalsStatus.text = "ROOT DEBUG";
                if (_vitalsInfection != null) _vitalsInfection.AddToClassList("hidden");
                if (_seal != null) _seal.AddToClassList("hud-seal--hidden");
                if (_debug != null) _debug.RemoveFromClassList("hidden");
                if (_debugText != null) _debugText.text = string.Empty;

                UnshatterPanels();
            }).StartingIn(t);

            t += 600;

            // 4) Revelado secuencial de líneas de desclasificación
            var shown = new List<string>();
            foreach (string line in RootDebugLines)
            {
                string current = line;
                _root.schedule.Execute(() =>
                {
                    shown.Add(current);
                    if (_debugText != null) _debugText.text = string.Join("\n", shown);
                }).StartingIn(t);
                t += 800;
            }

            _root.schedule.Execute(GameEvents.RaiseHudCorruptionFinished).StartingIn(t + 500);
        }

        private void ShatterPanels()
        {
            _root.Q("hud-vitals")?.AddToClassList("hud-shattered-a");
            _root.Q("hud-radio")?.AddToClassList("hud-shattered-b");
            _root.Q("hud-noise")?.AddToClassList("hud-shattered-c");
            _root.Q("hud-battery")?.AddToClassList("hud-shattered-c");
            _root.Q("hud-weapon")?.AddToClassList("hud-shattered-b");
        }

        private void UnshatterPanels()
        {
            foreach (string panel in new[] { "hud-vitals", "hud-radio", "hud-noise", "hud-battery", "hud-weapon" })
            {
                var el = _root.Q(panel);
                if (el == null) continue;
                el.RemoveFromClassList("hud-shattered-a");
                el.RemoveFromClassList("hud-shattered-b");
                el.RemoveFromClassList("hud-shattered-c");
            }
        }
    }
}
