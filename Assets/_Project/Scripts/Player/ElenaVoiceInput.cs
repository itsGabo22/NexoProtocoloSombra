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
    /// Cualquier fallo (sin micrófono, sin permiso, dictado rechazado, sin red o build sin conexión) se degrada a un
    /// subtítulo que remite a las preguntas rápidas [1]-[3] y nunca lanza excepciones (cambio de Gabriel, 2026-11-03).
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
        // Si el dictado de Windows falla una vez (privacidad de voz desactivada, servicio ocupado) no se reintenta
        private bool _dictationFailed;

        // Mensaje único cuando no hay forma de oír al jugador: siempre quedan las preguntas rápidas
        private const string NoVoiceFirstMessage = "(Elena no puede oír tu voz. Activa el reconocimiento de voz en línea en Configuración > Privacidad y seguridad > Voz. Mientras tanto, escríbele con [T] o usa [1] [2] [3])";
        private const string NoVoiceShortMessage = "(Sin voz: escribe a Elena con [T] o usa las preguntas rápidas [1] [2] [3])";
        private static bool s_noVoiceExplained;

        /// <summary>La primera vez explica cómo activar el dictado; después, un recordatorio corto.</summary>
        private static string NoVoiceMessage
        {
            get
            {
                if (s_noVoiceExplained) return NoVoiceShortMessage;
                s_noVoiceExplained = true;
                return NoVoiceFirstMessage;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNoVoiceNotice() => s_noVoiceExplained = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _dictation;
#endif

        private bool UseWindowsDictation
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (_dictationFailed) return false;
                try
                {
                    return PhraseRecognitionSystem.isSupported;
                }
                catch (System.Exception)
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

        /// <summary>Sin dictado, la voz solo llega a Elena si Gemini puede transcribirla (API key y sin modo offline).</summary>
        private bool CanTranscribe => _elenaConfig != null && !_elenaConfig.ForceFallback && _elenaConfig.Secrets != null && _elenaConfig.Secrets.HasApiKey;

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

            // Sin dictado ni transcripción (build sin conexión) se avisa al pulsar, sin grabar en vano
            bool dictation = UseWindowsDictation;
            if (!dictation && !CanTranscribe)
            {
                GameEvents.RaiseSubtitle(NoVoiceMessage);
                return;
            }

            _isListening = true;
            _lastHypothesis = "";
            GameEvents.RaiseSubtitle("(Hablando con Elena... suelta [V] para enviar)");
            GameEvents.RaiseNoiseEmitted(transform.position, _talkNoise);

            if (dictation && StartDictation()) return;
            if (CanTranscribe)
            {
                StartRecording();
            }
            else
            {
                _isListening = false;
                GameEvents.RaiseSubtitle(NoVoiceMessage);
            }
        }

        private void HandleTalkReleased(InputAction.CallbackContext context)
        {
            if (!_isListening) return;
            _isListening = false;

            if (IsDictationRunning)
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
            try
            {
                if (_recording != null && Microphone.IsRecording(_device)) Microphone.End(_device);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ElenaVoiceInput] No se pudo detener el micrófono: " + e.Message, this);
            }
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
            try
            {
                _recording = Microphone.Start(_device, false, MaxRecordSeconds, RecordRate);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ElenaVoiceInput] Micrófono no disponible: " + e.Message, this);
                _recording = null;
            }
            if (_recording == null)
            {
                // Sin permiso de micrófono o dispositivo ocupado
                _isListening = false;
                if (_noiseSensor != null) _noiseSensor.Resume();
                GameEvents.RaiseSubtitle("(No se puede usar el micrófono. Usa las preguntas rápidas [1] [2] [3])");
            }
        }

        private async void FinishRecording()
        {
            if (_recording == null) return;

            int samplesCount;
            try
            {
                samplesCount = Microphone.GetPosition(_device);
                Microphone.End(_device);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ElenaVoiceInput] Grabación interrumpida: " + e.Message, this);
                samplesCount = 0;
            }
            AudioClip clip = _recording;
            _recording = null;
            if (_noiseSensor != null) _noiseSensor.Resume();

            if (samplesCount < RecordRate / 3)
            {
                GameEvents.RaiseSubtitle("(Mantén [V] mientras hablas)");
                return;
            }

            if (!CanTranscribe)
            {
                GameEvents.RaiseSubtitle(NoVoiceMessage);
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
            catch (System.Exception e)
            {
                // async void: una excepción aquí no debe llegar a la consola como error ni romper la entrada
                Debug.LogWarning("[ElenaVoiceInput] Transcripción fallida: " + e.Message, this);
                text = null;
            }
            finally
            {
                _isTranscribing = false;
                GameEvents.RaiseElenaThinking(false);
            }

            if (string.IsNullOrWhiteSpace(text)) GameEvents.RaiseSubtitle("(Elena no entendió lo que dijiste. Prueba otra vez o usa [1] [2] [3])");
            else Send(text);
        }

        // ---------- Dictado de Windows ----------

        private bool IsDictationRunning
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return _dictation != null && !_dictationFailed && _dictation.Status == SpeechSystemStatus.Running;
#else
                return false;
#endif
            }
        }

        /// <summary>Arranca el dictado de Windows. Devuelve false (y no lo vuelve a intentar) si Windows lo rechaza.</summary>
        private bool StartDictation()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (_dictation == null)
                {
                    _dictation = new DictationRecognizer();
                    _dictation.DictationHypothesis += HandleHypothesis;
                    _dictation.DictationResult += HandleResult;
                    _dictation.DictationError += HandleError;
                }
                if (_dictation.Status != SpeechSystemStatus.Running) _dictation.Start();
                return true;
            }
            catch (System.Exception e)
            {
                // Privacidad de voz desactivada, otro reconocedor activo o servicio no disponible
                Debug.LogWarning("[ElenaVoiceInput] Dictado de Windows no disponible: " + e.Message, this);
                _dictationFailed = true;
                return false;
            }
#else
            return false;
#endif
        }

        private void StopDictation()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (_dictation != null && _dictation.Status == SpeechSystemStatus.Running) _dictation.Stop();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ElenaVoiceInput] No se pudo detener el dictado: " + e.Message, this);
            }
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
            _dictationFailed = true;
            Debug.LogWarning($"[ElenaVoiceInput] Dictado no disponible: {error} ({hresult}).", this);
            GameEvents.RaiseSubtitle(NoVoiceMessage);
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
