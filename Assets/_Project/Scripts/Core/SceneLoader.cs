using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexo.Core
{
    /// <summary>
    /// Vive en Systems_Persistent y carga de forma aditiva la escena del piso (Floor12_Offices).
    /// Tras la carga activa la escena del piso, coloca al jugador y delega la publicación en FloorMetadata.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("Escena del Piso")]
        [SerializeField] private string _floorSceneName = "Floor12_Offices";
        [SerializeField] private bool _loadOnStart = true;

        [Header("Jugador Persistente (PF_Player)")]
        [SerializeField] private Transform _player;

        private bool _isLoading;
        private string _loadedFloorScene;

        public bool IsLoading => _isLoading;

        private void Start()
        {
            if (_loadOnStart)
            {
                LoadFloor(_floorSceneName);
            }
        }

        /// <summary>
        /// Descarga el piso actual (si lo hay) y carga aditivamente el indicado.
        /// </summary>
        public void LoadFloor(string sceneName)
        {
            if (_isLoading || string.IsNullOrEmpty(sceneName)) return;
            StartCoroutine(LoadFloorRoutine(sceneName));
        }

        private IEnumerator LoadFloorRoutine(string sceneName)
        {
            _isLoading = true;

            if (!string.IsNullOrEmpty(_loadedFloorScene) && SceneManager.GetSceneByName(_loadedFloorScene).isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_loadedFloorScene);
                _loadedFloorScene = null;
            }

            // Si la escena ya está abierta (p. ej. ambas escenas abiertas en el Editor) no se carga de nuevo
            Scene floorScene = SceneManager.GetSceneByName(sceneName);
            if (!floorScene.isLoaded)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[SceneLoader] No se pudo cargar '{sceneName}'. ¿Está añadida en Build Profiles?", this);
                    _isLoading = false;
                    yield break;
                }
                yield return op;
                floorScene = SceneManager.GetSceneByName(sceneName);
            }

            _loadedFloorScene = sceneName;

            // La escena del piso define iluminación, skybox y NavMesh activos
            SceneManager.SetActiveScene(floorScene);

            FloorMetadata metadata = FloorMetadata.Active;
            if (metadata != null)
            {
                metadata.Publish(_player);
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] '{sceneName}' no contiene FloorMetadata en su raíz.", this);
            }

            _isLoading = false;
        }
    }
}
