using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Configuración central de ElenaAIService (modelo, timeouts, assets de prompt y fallbacks).
    /// </summary>
    [CreateAssetMenu(fileName = "ElenaConfig", menuName = "Nexo/AI/Elena Config")]
    public sealed class ElenaConfig : ScriptableObject
    {
        [Header("Configuración de Modelo Gemini")]
        [SerializeField] private string _modelName = "gemini-2.5-flash";
        [SerializeField] private string _endpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        [SerializeField] private float _timeoutSeconds = 6.0f;
        [SerializeField] private int _maxHistoryTurns = 6;
        [SerializeField] [Range(0f, 1f)] private float _temperature = 0.7f;
        [SerializeField] private bool _forceFallback = false;

        [Header("Assets de Texto y Secretos")]
        [SerializeField] private TextAsset _systemPrompt;
        [SerializeField] private TextAsset[] _fallbackFiles;
        [SerializeField] private ElenaSecrets _secrets;

        public string ModelName => _modelName;
        public string EndpointTemplate => _endpointTemplate;
        public float TimeoutSeconds => _timeoutSeconds;
        public int MaxHistoryTurns => _maxHistoryTurns;
        public float Temperature => _temperature;
        public bool ForceFallback => _forceFallback;
        public TextAsset SystemPrompt => _systemPrompt;
        public TextAsset[] FallbackFiles => _fallbackFiles;
        public ElenaSecrets Secrets => _secrets;

        public string BuildEndpointUrl()
        {
            return string.Format(_endpointTemplate, _modelName);
        }
    }
}
