using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace Nexo.Player
{
    /// <summary>
    /// Hablar con Elena por voz: mantener [V] (o el botón norte del mando) y soltar para enviar.
    /// Si Windows tiene reconocimiento de voz se usa su dictado; si no, se graba el micrófono y Gemini transcribe
    /// el audio (requiere la API key de ElenaSecrets). La frase se envía a Elena por GameEvents.OnPlayerSpoke.
    /// Todo por callbacks y await: sin Update().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElenaVoiceInput : MonoBehaviour
    {
        private const int RecordRate = 16000;
        private const int MaxRecordSeconds = 10;

        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private MicrophoneNoiseSensor _noiseSensor;
        [Tooltip("Configuración de Elena (modelo y API key) para transcribir con Gemini.")]
        [SerializeField] private ElenaConfig _elenaConfig;
        [Tooltip("Ruido emitido al empezar a hablar (los infectados también te oyen).")]
        [SerializeField, Range(0f, 100f)] private float _talkNoise = 35f;

        private InputAction _talkAction;
        private bool _isListening;
        private bool _isTranscribing;
        private string _lastHypothesis = "";
        private string _device;
        private AudioClip _recording;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _dictation;
#endif

        private static bool UseWindowsDictation
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return PhraseRecognitionSystem.isSupported;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            _talkAction = new InputAction("TalkToElena", InputActionType.Button);
            _talkAction.AddBinding("<Keyboard>/v");
            _talkAction.AddBinding("<Gamepad>/buttonNorth");
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_noiseSensor == null) _noiseSensor = GetComponent<MicrophoneNoiseSensor>();
        }

        private void OnEnable()
        {
            _talkAction.started += HandleTalkStarted;
            _talkAction.canceled += HandleTalkReleased;
            _talkAction.Enable();
        }

        private void OnDisable()
        {
            _talkAction.started -= HandleTalkStarted;
            _talkAction.canceled -= HandleTalkReleased;
            _talkAction.Disable();
            CancelListening();
        }

        private void OnDestroy()
        {
            _talkAction?.Dispose();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_dictation != null)
            {
                _dictation.DictationHypothesis -= HandleHypothesis;
                _dictation.DictationResult -= HandleResult;
                _dictation.DictationError -= HandleError;
                _dictation.Dispose();
                _dictation = null;
            }
#endif
        }

        private void HandleTalkStarted(InputAction.CallbackContext context)
        {
            if (_isListening || _isTranscribing) return;
            if (_playerMovement != null && _playerMovement.IsInputLocked) return;

            _isListening = true;
            _lastHypothesis = "";
            GameEvents.RaiseSubtitle("(Hablando con Elena... suelta [V] para enviar)");
            GameEvents.RaiseNoiseEmitted(transform.position, _talkNoise);

            if (UseWindowsDictation) StartDictation();
            else StartRecording();
        }

        private void HandleTalkReleased(InputAction.CallbackContext context)
        {
            if (!_isListening) return;
            _isListening = false;

            if (UseWindowsDictation)
            {
                if (!string.IsNullOrWhiteSpace(_lastHypothesis)) Send(_lastHypothesis);
                StopDictation();
            }
            else
            {
                FinishRecording();
            }
        }

        private void CancelListening()
        {
            _isListening = false;
            StopDictation();
            if (_recording != null && Microphone.IsRecording(_device)) Microphone.End(_device);
            _recording = null;
        }

        // ---------- Grabación + Gemini ----------

        private void StartRecording()
        {
            if (Microphone.devices.Length == 0)
            {
                _isListening = false;
                GameEvents.RaiseSubtitle("(No hay micrófono conectado)");
                return;
            }

            if (_noiseSensor != null) _noiseSensor.Pause();
            _device = Microphone.devices[0];
            _recording = Microphone.Start(_device, false, MaxRecordSeconds, RecordRate);
        }

        private async void FinishRecording()
        {
            if (_recording == null) return;

            int samplesCount = Microphone.GetPosition(_device);
            Microphone.End(_device);
            AudioClip clip = _recording;
            _recording = null;
            if (_noiseSensor != null) _noiseSensor.Resume();

            if (samplesCount < RecordRate / 3)
            {
                GameEvents.RaiseSubtitle("(Mantén [V] mientras hablas)");
                return;
            }

            if (_elenaConfig == null || _elenaConfig.Secrets == null || !_elenaConfig.Secrets.HasApiKey)
            {
                GameEvents.RaiseSubtitle("(Elena no puede oírte: falta la API key de Gemini en ElenaSecrets)");
                return;
            }

            var samples = new float[samplesCount];
            clip.GetData(samples, 0);

            _isTranscribing = true;
            GameEvents.RaiseElenaThinking(true);
            string text = null;
            try
            {
                text = await GeminiSpeechTranscriber.TranscribeAsync(_elenaConfig, samples, RecordRate);
            }
            finally
            {
                _isTranscribing = false;
                GameEvents.RaiseElenaThinking(false);
            }

            if (string.IsNullOrWhiteSpace(text)) GameEvents.RaiseSubtitle("(Elena no entendió lo que dijiste)");
            else Send(text);
        }

        // ---------- Dictado de Windows ----------

        private void StartDictation()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_dictation == null)
            {
                _dictation = new DictationRecognizer();
                _dictation.DictationHypothesis += HandleHypothesis;
                _dictation.DictationResult += HandleResult;
                _dictation.DictationError += HandleError;
            }
            if (_dictation.Status != SpeechSystemStatus.Running) _dictation.Start();
#endif
        }

        private void StopDictation()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_dictation != null && _dictation.Status == SpeechSystemStatus.Running) _dictation.Stop();
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void HandleHypothesis(string text)
        {
            _lastHypothesis = text;
            GameEvents.RaiseSubtitle("TÚ: " + text + "...");
        }

        private void HandleResult(string text, ConfidenceLevel confidence)
        {
            _lastHypothesis = "";
            Send(text);
        }

        private void HandleError(string error, int hresult)
        {
            _isListening = false;
            Debug.LogWarning($"[ElenaVoiceInput] Dictado no disponible: {error} ({hresult}).", this);
            GameEvents.RaiseSubtitle("(Elena no te oye: revisa el reconocimiento de voz en Configuración de Windows > Privacidad > Voz)");
        }
#endif

        private void Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _lastHypothesis = "";
            GameEvents.RaiseSubtitle("TÚ: " + text);
            GameEvents.RaisePlayerSpoke(text);
        }
    }
}
