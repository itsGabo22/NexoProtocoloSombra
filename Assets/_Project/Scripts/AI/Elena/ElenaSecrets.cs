using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Almacena de forma aislada la API Key de Gemini.
    /// Las instancias .asset de este ScriptableObject están ignoradas en .gitignore.
    /// </summary>
    [CreateAssetMenu(fileName = "ElenaSecrets", menuName = "Nexo/AI/Elena Secrets")]
    public sealed class ElenaSecrets : ScriptableObject
    {
        [SerializeField] private string _apiKey = string.Empty;

        public string ApiKey => _apiKey;
        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

        public void SetApiKey(string key) => _apiKey = key;
    }
}
