using System.Collections.Generic;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Red de cámaras de seguridad de los tres pisos (12, 6 y 1). Datos puros: el panel CameraNetworkController
    /// muestra en vivo las cámaras del piso cargado y el archivo de los demás.
    /// </summary>
    [CreateAssetMenu(fileName = "SecurityCameraNetwork", menuName = "Nexo/Red de cámaras de seguridad")]
    public sealed class SecurityCameraNetwork : ScriptableObject
    {
        [Tooltip("Pisos en el orden de las pestañas.")]
        [SerializeField] private int[] _floors = { 12, 6, 1 };
        [Tooltip("Escena de cada piso, en el mismo orden (la usa la herramienta de captura del archivo).")]
        [SerializeField] private string[] _floorScenePaths =
        {
            "Assets/_Project/Scenes/Floors/Floor12_Offices.unity",
            "Assets/_Project/Scenes/Floors/Floor06_Labs.unity",
            "Assets/_Project/Scenes/Floors/Floor01_Lobby.unity"
        };
        [SerializeField] private List<CameraFeedDefinition> _feeds = new();

        public IReadOnlyList<int> Floors => _floors;
        public IReadOnlyList<CameraFeedDefinition> Feeds => _feeds;

        public string GetScenePath(int floor)
        {
            for (int i = 0; i < _floors.Length && i < _floorScenePaths.Length; i++)
            {
                if (_floors[i] == floor) return _floorScenePaths[i];
            }
            return string.Empty;
        }

        /// <summary>Rellena <paramref name="results"/> con las cámaras del piso indicado (sin asignar memoria nueva).</summary>
        public void GetFeeds(int floor, List<CameraFeedDefinition> results)
        {
            results.Clear();
            foreach (CameraFeedDefinition feed in _feeds)
            {
                if (feed != null && feed.Floor == floor) results.Add(feed);
            }
        }
    }
}
