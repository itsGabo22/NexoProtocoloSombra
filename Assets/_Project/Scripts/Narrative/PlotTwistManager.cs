using System.Collections;
using Nexo.Accessibility;
using Nexo.Core;
using Nexo.Player;
using Nexo.UI.Menus;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// Giro final: al recibir NarrativeEvents.BioScannerHandPlaced reproduce, con una corrutina en tiempo real,
    /// la línea de tiempo de TwistTimeline: t=0 bloqueo del jugador + OnPlotTwistTriggered; +0.8 s OnHudCorruptionRequested;
    /// +2.0 s luces de emergencia a rojo (~1.5 s; instantáneo con reducir movimiento); +3.5 s líneas finales de Elena
    /// como subtítulos; créditos 4 s después de la última línea (MenuEvents.ShowCreditsRequested). Solo se reproduce una vez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlotTwistManager : MonoBehaviour
    {
        [Header("Referencias (Systems_Persistent / escena del piso)")]
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("Luces de emergencia")]
        [SerializeField] private Light[] _emergencyLights;
        [SerializeField] private Color _emergencyColor = new Color(1f, 0.08f, 0.08f);
        [SerializeField] private float _lightRampSeconds = 1.5f;

        [Header("Líneas finales de Elena (subtítulos)")]
        [TextArea(1, 3)]
        [SerializeField] private string[] _elenaLines =
        {
            "Perdóname, Alex.",
            "Necesitaba que llegaras hasta aquí.",
            "Tú abriste la puerta por los dos."
        };
        [SerializeField] private float _secondsPerLine = 3.5f;
        [SerializeField] private float _creditsDelay = 4f;

        private Coroutine _sequence;
        private Coroutine _lightRamp;
        private bool _played;

        public bool HasPlayed => _played;

        private void OnEnable()
        {
            NarrativeEvents.BioScannerHandPlaced += HandleBioScannerHandPlaced;
        }

        private void OnDisable()
        {
            NarrativeEvents.BioScannerHandPlaced -= HandleBioScannerHandPlaced;
            if (_sequence != null) StopCoroutine(_sequence);
            if (_lightRamp != null) StopCoroutine(_lightRamp);
            _sequence = null;
            _lightRamp = null;
        }

        private void HandleBioScannerHandPlaced()
        {
            if (_played) return;

            _played = true;
            NarrativeEvents.TwistHasPlayed = true;
            _sequence = StartCoroutine(PlaySequence());
        }

        /// <summary>
        /// Prueba rápida desde el Inspector (menú del componente, en Play mode) sin recorrer el nivel.
        /// </summary>
        [ContextMenu("Probar giro final")]
        private void PlayForTesting()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlotTwistManager] La prueba del giro solo funciona en Play mode.", this);
                return;
            }
            HandleBioScannerHandPlaced();
        }

        private IEnumerator PlaySequence()
        {
            int lineCount = _elenaLines != null ? _elenaLines.Length : 0;
            bool reduceMotion = AccessibilitySettings.Current.ReduceMotion;
            TwistTimeline timeline = TwistTimeline.CreateDefault(lineCount, _secondsPerLine, _lightRampSeconds, _creditsDelay, reduceMotion);

            // Tiempo real: el giro no depende de Time.timeScale
            float elapsed = 0f;
            foreach (TwistStep step in timeline.Steps)
            {
                float wait = step.Offset - elapsed;
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                elapsed = step.Offset;
                Execute(step);
            }

            _sequence = null;
        }

        private void Execute(TwistStep step)
        {
            switch (step.Action)
            {
                case TwistAction.StartTwist:
                    if (_playerMovement != null) _playerMovement.SetInputLocked(true, false);
                    GameEvents.RaisePlotTwistTriggered();
                    break;
                case TwistAction.HudCorruption:
                    GameEvents.RaiseHudCorruptionRequested();
                    break;
                case TwistAction.LightRamp:
                    if (step.Duration <= 0f) SetEmergencyLights(1f, null);
                    else _lightRamp = StartCoroutine(RampEmergencyLights(step.Duration));
                    break;
                case TwistAction.ElenaLine:
                    if (_elenaLines != null && step.LineIndex >= 0 && step.LineIndex < _elenaLines.Length)
                    {
                        NarrativeSubtitles.Say(NarrativeSubtitles.ElenaSpeaker, _elenaLines[step.LineIndex]);
                    }
                    break;
                case TwistAction.ShowCredits:
                    NarrativeSubtitles.Clear();
                    MenuEvents.RaiseShowCreditsRequested();
                    break;
            }
        }

        private IEnumerator RampEmergencyLights(float seconds)
        {
            Color[] startColors = CaptureLightColors();
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetEmergencyLights(Mathf.Clamp01(t / seconds), startColors);
                yield return null;
            }

            SetEmergencyLights(1f, startColors);
            _lightRamp = null;
        }

        private Color[] CaptureLightColors()
        {
            if (_emergencyLights == null) return null;

            var colors = new Color[_emergencyLights.Length];
            for (int i = 0; i < _emergencyLights.Length; i++)
            {
                colors[i] = _emergencyLights[i] != null ? _emergencyLights[i].color : _emergencyColor;
            }
            return colors;
        }

        private void SetEmergencyLights(float t, Color[] startColors)
        {
            if (_emergencyLights == null) return;

            for (int i = 0; i < _emergencyLights.Length; i++)
            {
                Light emergency = _emergencyLights[i];
                if (emergency == null) continue;

                Color from = startColors != null && i < startColors.Length ? startColors[i] : emergency.color;
                emergency.color = Color.Lerp(from, _emergencyColor, t);
                if (!emergency.enabled) emergency.enabled = true;
            }
        }
    }
}
