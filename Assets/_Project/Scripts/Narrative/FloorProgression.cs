using System.Collections;
using Nexo.Cinematics;
using Nexo.Core;
using Nexo.Player;
using Nexo.UI.Menus;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// Progresión de pisos (vive en Systems_Persistent): Piso 12 → Piso 6 → Piso 1.
    /// LoadNext delega la descarga/carga en SceneLoader.LoadFloor (no duplica su lógica) y, como PlayerCaptureHandler,
    /// bloquea al jugador mientras SceneLoader.IsLoading. JUGAR y volver al menú reinician el índice a 0.
    /// Antes de cargar reproduce la cinemática floor_transition_to_NN (o floor_transition si no existe); el piso se carga
    /// DESPUÉS del clip: así el Momento 0 del piso nuevo (subtítulos, Elena, objetivo) no ocurre detrás del vídeo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorProgression : MonoBehaviour
    {
        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("Orden de pisos (el primero debe coincidir con MenuGameFlowBridge._firstFloorScene)")]
        [SerializeField] private string[] _floorScenes = { "Floor12_Offices", "Floor06_Labs", "Floor01_Lobby" };

        [Header("Subtítulo al bajar ({0} = número de piso)")]
        [SerializeField] private string _descendingLine = "Bajando al Piso {0}.";

        private FloorOrder _order;
        private Coroutine _loadRoutine;
        private bool _waitingCinematic;

        /// <summary>Instancia activa (la usa StairsExit desde la escena del piso; registrada en OnEnable).</summary>
        public static FloorProgression Active { get; private set; }

        public int CurrentIndex => _order != null ? _order.CurrentIndex : 0;
        public bool IsLastFloor => _order == null || _order.IsLast;
        public bool IsBusy => _loadRoutine != null || (_sceneLoader != null && _sceneLoader.IsLoading);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            _order = new FloorOrder(_floorScenes);
            if (_sceneLoader == null) Debug.LogError($"[FloorProgression] Falta asignar _sceneLoader en {name}.", this);
        }

        private void OnEnable()
        {
            Active = this;
            MenuEvents.PlayRequested += HandlePlayRequested;
            MenuEvents.ReturnToMainMenuRequested += HandleReturnToMainMenuRequested;
        }

        private void OnDisable()
        {
            MenuEvents.PlayRequested -= HandlePlayRequested;
            MenuEvents.ReturnToMainMenuRequested -= HandleReturnToMainMenuRequested;
            if (Active == this) Active = null;

            _waitingCinematic = false;
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
                if (_playerMovement != null) _playerMovement.SetInputLocked(false, false);
            }
        }

        /// <summary>
        /// Descarga el piso actual y carga el siguiente. Devuelve false si ya es el último o hay una carga en curso.
        /// </summary>
        public bool LoadNext()
        {
            if (_sceneLoader == null || IsBusy || !_order.Advance()) return false;

            _loadRoutine = StartCoroutine(LoadRoutine(_order.CurrentScene));
            return true;
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            if (_playerMovement != null) _playerMovement.SetInputLocked(true, false);

            int floorNumber = FloorOrder.ParseFloorNumber(sceneName);

            // Sin clip, HandleTransitionCinematicFinished se llama al instante y no se espera ningún fotograma
            _waitingCinematic = true;
            CinematicService.Play(CinematicIds.ResolveFloorTransition(floorNumber, CinematicService.HasClip), HandleTransitionCinematicFinished);
            while (_waitingCinematic) yield return null;

            if (floorNumber > 0 && !string.IsNullOrEmpty(_descendingLine))
            {
                NarrativeSubtitles.Say(string.Empty, string.Format(_descendingLine, floorNumber));
            }

            // SceneLoader descarga el piso anterior, carga este de forma aditiva y llama a FloorMetadata.Publish
            _sceneLoader.LoadFloor(sceneName);
            while (_sceneLoader.IsLoading) yield return null;

            if (_playerMovement != null) _playerMovement.SetInputLocked(false, false);
            _loadRoutine = null;
            NarrativeEvents.RaiseFloorChanged(floorNumber);
        }

        private void HandleTransitionCinematicFinished()
        {
            _waitingCinematic = false;
        }

        private void HandlePlayRequested()
        {
            // MenuGameFlowBridge carga el primer piso; aquí solo se reinicia el índice
            _order.Reset();
        }

        private void HandleReturnToMainMenuRequested()
        {
            _order.Reset();
        }
    }
}
