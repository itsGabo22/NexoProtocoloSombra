using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Configuración central de ElenaAIService (modelos, timeouts, assets de prompt y fallbacks).
    /// Los modelos de diálogo, transcripción de voz y síntesis de voz se cambian desde el Inspector, sin código.
    /// </summary>
    [CreateAssetMenu(fileName = "ElenaConfig", menuName = "Nexo/AI/Elena Config")]
    public sealed class ElenaConfig : ScriptableObject
    {
        public const string DefaultDialogModel = "gemini-3.8-flash";
        /// <summary>La variante lite responde en ~2 s (la normal ~3,7 s): cabe en el timeout de la voz dinámica.</summary>
        public const string DefaultTtsModel = "gemini-3.8-flash-lite-tts";
        public const string DefaultTtsVoice = "Leda";
        public const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

        [Header("Modelos Gemini (editables sin código)")]
        [Tooltip("Modelo de diálogo de Elena.")]
        [SerializeField] private string _modelName = DefaultDialogModel;
        [Tooltip("Modelos de reserva si el principal responde 503/429/404 (se prueban en orden dentro del mismo timeout).")]
        [SerializeField] private string[] _fallbackModelNames = { "gemini-3.5-flash-lite" };
        [Tooltip("Modelo que transcribe la voz del jugador ([V] sin dictado de Windows). Vacío = el de diálogo.")]
        [SerializeField] private string _speechToTextModel = DefaultDialogModel;
        [Tooltip("Modelo de síntesis de voz (respuestas dinámicas sin clip pregrabado y Nexo › Elena › Generate Voice).")]
        [SerializeField] private string _ttsModel = DefaultTtsModel;
        [SerializeField] private string _ttsVoice = DefaultTtsVoice;
        [SerializeField] private string _endpointTemplate = DefaultEndpoint;

        [Header("Latencia")]
        [Tooltip("Nivel de razonamiento (thinkingConfig.thinkingLevel): low es el mínimo que acepta gemini-3.8-flash. Vacío = no se envía.")]
        [SerializeField] private string _thinkingLevel = "low";
        [SerializeField] private float _timeoutSeconds = 5.0f;
        [Tooltip("Voz en tiempo real para respuestas dinámicas sin clip (si no llega a tiempo, solo subtítulo).")]
        [SerializeField] private bool _runtimeTts = true;
        [SerializeField] private float _ttsTimeoutSeconds = 4.0f;

        [Header("Conversación")]
        [SerializeField] private int _maxHistoryTurns = 6;
        [SerializeField] [Range(0f, 1f)] private float _temperature = 0.7f;
        [SerializeField] private bool _forceFallback = false;

        [Header("Assets de Texto y Secretos")]
        [SerializeField] private TextAsset _systemPrompt;
        [SerializeField] private TextAsset[] _fallbackFiles;
        [SerializeField] private ElenaSecrets _secrets;

        public string ModelName => string.IsNullOrWhiteSpace(_modelName) ? DefaultDialogModel : _modelName.Trim();
        public string[] FallbackModelNames => _fallbackModelNames ?? System.Array.Empty<string>();
        public string SpeechToTextModel => string.IsNullOrWhiteSpace(_speechToTextModel) ? ModelName : _speechToTextModel.Trim();
        public string TtsModel => string.IsNullOrWhiteSpace(_ttsModel) ? DefaultTtsModel : _ttsModel.Trim();
        public string TtsVoice => string.IsNullOrWhiteSpace(_ttsVoice) ? DefaultTtsVoice : _ttsVoice.Trim();
        public string EndpointTemplate => string.IsNullOrWhiteSpace(_endpointTemplate) ? DefaultEndpoint : _endpointTemplate;
        public string ThinkingLevel => _thinkingLevel == null ? string.Empty : _thinkingLevel.Trim();
        public float TimeoutSeconds => _timeoutSeconds;
        public bool RuntimeTts => _runtimeTts;
        public float TtsTimeoutSeconds => _ttsTimeoutSeconds;
        public int MaxHistoryTurns => _maxHistoryTurns;
        public float Temperature => _temperature;
        public bool ForceFallback => _forceFallback;
        public TextAsset SystemPrompt => _systemPrompt;
        public TextAsset[] FallbackFiles => _fallbackFiles;
        public ElenaSecrets Secrets => _secrets;

        public string BuildEndpointUrl() => BuildEndpointUrl(ModelName);

        public string BuildEndpointUrl(string model) => string.Format(EndpointTemplate, model);
    }
}
