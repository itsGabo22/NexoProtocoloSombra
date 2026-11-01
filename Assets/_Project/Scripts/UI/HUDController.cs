using System;
using System.Collections.Generic;
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
        [Tooltip("Destino por defecto de HIGHLIGHT_SAFE_ROUTE cuando Elena no indica target_id.")]
        [SerializeField] private string _safeRouteDefaultTarget = "F12_Door_Stairs";
        [SerializeField] private float _radarRangeMeters = 18f;

        private VisualElement _root;
        private VisualElement _spectrogram;
        private VisualElement _notifications;
        private VisualElement _glitch;
        private VisualElement _debug;
        private VisualElement _capture;
        private VisualElement _crosshair;
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

        private readonly List<VisualElement> _bars = new();
        private readonly float[] _barLevels = new float[BarCount];
        private readonly Button[] _quick = new Button[3];
        private readonly Action[] _quickHandlers = new Action[3];

        private IVisualElementScheduledItem _typewriter;
        private IVisualElementScheduledItem _spectro;
        private IVisualElementScheduledItem _pulse;
        private IVisualElementScheduledItem _glitchPulse;
        private string _emotionClass;
        private float _noise;
        private int _infectedNearby;

        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

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
        }

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

            _portrait = _root.Q<VisualElement>("radio-elena-portrait");
            _trustFill = _root.Q<VisualElement>("radio-trust-fill");
            _batteryFill = _root.Q<VisualElement>("hud-battery-fill");
            _spectrogram = _root.Q<VisualElement>("hud-spectrogram");
            _notifications = _root.Q<VisualElement>("hud-notifications");
            _glitch = _root.Q<VisualElement>("hud-glitch");
            _debug = _root.Q<VisualElement>("hud-debug");
            _capture = _root.Q<VisualElement>("hud-capture");
            _crosshair = _root.Q<VisualElement>("hud-crosshair");

            for (int i = 0; i < _quick.Length; i++)
            {
                _quick[i] = _root.Q<Button>($"radio-quick-{i + 1}");
            }
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
            if (_vitalsValue != null) _vitalsValue.text = data.ValueText;
            if (_vitalsStatus != null) _vitalsStatus.text = data.StatusText;
            if (_bpm != null) _bpm.text = $"♥ {data.HeartRate} BPM";
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
            if (_emotionLabel != null) _emotionLabel.text = emotion.ToString().ToUpperInvariant();
        }

        private void StartTypewriter(string text)
        {
            if (_dialogue == null) return;
            _typewriter?.Pause();

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
                    string destination = string.IsNullOrEmpty(targetId) ? _safeRouteDefaultTarget : targetId;
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
            _interactPrompt.text = prompt;
        }

        public void ShowNotification(HudNotification data)
        {
            if (_notifications == null) return;

            var card = new VisualElement();
            card.AddToClassList("hud-notification");
            card.AddToClassList("flex-col");
            card.EnableInClassList("hud-notification--fake", data.IsFake);

            var header = new VisualElement();
            header.AddToClassList("flex-row");
            header.AddToClassList("items-center");
            header.AddToClassList("justify-between");

            var sender = new Label(data.Sender);
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

            var body = new Label(data.Body);
            body.AddToClassList("text-sm");
            body.AddToClassList("text-main");
            body.AddToClassList("wrap");
            body.AddToClassList("mt-1");

            card.Add(header);
            card.Add(body);
            card.Add(BuildWave(data.IsFake)); // Anomalía 2: forma de onda irregular
            _notifications.Add(card);

            card.schedule.Execute(() => card.AddToClassList("hud-notification--visible")).StartingIn(20);
            card.schedule.Execute(() => DismissCard(card)).StartingIn(NotificationMs);
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
        }

        private void UnshatterPanels()
        {
            foreach (string panel in new[] { "hud-vitals", "hud-radio", "hud-noise", "hud-battery" })
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
