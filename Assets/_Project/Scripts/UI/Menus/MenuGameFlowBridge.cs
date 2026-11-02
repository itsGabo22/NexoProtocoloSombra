using Nexo.Core;
using Nexo.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Traduce las peticiones de los menús (MenuEvents) a la API existente de carga de pisos:
    /// JUGAR → SceneLoader.LoadFloor (o ReloadFloor si el piso ya está cargado detrás del menú),
    /// REINICIAR → SceneLoader.ReloadFloor, MENÚ PRINCIPAL → descarga el piso activo y muestra el hub,
    /// SALIR → Application.Quit. Vive en Systems_Persistent junto a MenuFlowController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuGameFlowBridge : MonoBehaviour
    {
        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private MenuFlowController _menuFlow;

        [Header("Piso inicial")]
        [Tooltip("Escena que carga JUGAR (debe estar en Build Profiles).")]
        [SerializeField] private string _firstFloorScene = "Floor12_Offices";

        private void Awake()
        {
            if (_sceneLoader == null) Debug.LogError($"[MenuGameFlowBridge] Falta asignar _sceneLoader en {name}.", this);
            if (_menuFlow == null) Debug.LogError($"[MenuGameFlowBridge] Falta asignar _menuFlow en {name}.", this);
        }

        private void OnEnable()
        {
            MenuEvents.PlayRequested += HandlePlayRequested;
            MenuEvents.RestartFloorRequested += HandleRestartFloorRequested;
            MenuEvents.ReturnToMainMenuRequested += HandleReturnToMainMenuRequested;
            MenuEvents.QuitRequested += HandleQuitRequested;
        }

        private void OnDisable()
        {
            MenuEvents.PlayRequested -= HandlePlayRequested;
            MenuEvents.RestartFloorRequested -= HandleRestartFloorRequested;
            MenuEvents.ReturnToMainMenuRequested -= HandleReturnToMainMenuRequested;
            MenuEvents.QuitRequested -= HandleQuitRequested;
        }

        private void HandlePlayRequested()
        {
            if (_sceneLoader == null || _menuFlow == null) return;

            Time.timeScale = 1f;
            _menuFlow.ShowScreen(ScreenId.None);

            // Con SceneLoader._loadOnStart activo el piso ya está cargado detrás del menú: se reinicia para empezar limpio
            if (FloorMetadata.Active != null) _sceneLoader.ReloadFloor();
            else _sceneLoader.LoadFloor(_firstFloorScene);
        }

        private void HandleRestartFloorRequested()
        {
            if (_sceneLoader == null || _menuFlow == null) return;

            Time.timeScale = 1f;
            _menuFlow.ShowScreen(ScreenId.None);
            if (!_sceneLoader.ReloadFloor())
            {
                Debug.LogWarning("[MenuGameFlowBridge] Ya hay una carga de piso en curso; se ignora el reinicio.", this);
            }
        }

        private void HandleReturnToMainMenuRequested()
        {
            Time.timeScale = 1f;

            // Tras el giro final quedan bloqueados jugador, linterna, rueda, preguntas rápidas y el HUD en modo colmena:
            // se recarga Systems_Persistent entero (descarga también el piso) y el menú arranca sin la secuencia de inicio
            if (NarrativeEvents.TwistHasPlayed)
            {
                NarrativeEvents.TwistHasPlayed = false;
                MenuStartState.SkipBootOnce = true;
                int persistentIndex = gameObject.scene.buildIndex >= 0 ? gameObject.scene.buildIndex : 0;
                SceneManager.LoadScene(persistentIndex, LoadSceneMode.Single);
                return;
            }

            if (_menuFlow == null) return;

            // SceneLoader no expone descarga: se descarga la escena del piso activo; JUGAR la vuelve a cargar con LoadFloor
            FloorMetadata floor = FloorMetadata.Active;
            if (floor != null)
            {
                Scene floorScene = floor.gameObject.scene;
                if (floorScene.isLoaded) SceneManager.UnloadSceneAsync(floorScene);
            }

            _menuFlow.ShowScreen(ScreenId.MainMenu);
        }

        private void HandleQuitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
