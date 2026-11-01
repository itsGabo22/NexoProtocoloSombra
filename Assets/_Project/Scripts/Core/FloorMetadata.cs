using Nexo.Interaction;
using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Metadatos del piso cargado de forma aditiva (raíz de Floor12_Offices).
    /// Expone los puntos de aparición y publica en el bus el número de piso, las puertas disponibles y el jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorMetadata : MonoBehaviour
    {
        [Header("Identificación del Piso")]
        [SerializeField] private int _floorNumber = 12;

        [Header("Puntos de Aparición")]
        [SerializeField] private Transform _playerSpawn;
        [SerializeField] private Transform _elenaSpawn;

        [Header("Puertas del Piso (publicadas a GameStateTracker)")]
        [SerializeField] private DoorController[] _doors;

        /// <summary>
        /// Piso activo actualmente (registrado en OnEnable). Lo consulta SceneLoader tras la carga aditiva.
        /// </summary>
        public static FloorMetadata Active { get; private set; }

        public int FloorNumber => _floorNumber;
        public Transform PlayerSpawn => _playerSpawn;
        public Transform ElenaSpawn => _elenaSpawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void OnEnable()
        {
            if (Active != null && Active != this)
            {
                Debug.LogWarning($"[FloorMetadata] Ya existe un piso activo ({Active.name}); se reemplaza por {name}.", this);
            }
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        /// <summary>
        /// Coloca al jugador en el PlayerSpawn y publica el estado del piso en el bus de eventos.
        /// </summary>
        public void Publish(Transform player)
        {
            if (player != null && _playerSpawn != null)
            {
                // El CharacterController sobrescribe la posición si está activo durante el teletransporte
                var controller = player.GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (wasEnabled) controller.enabled = false;

                player.SetPositionAndRotation(_playerSpawn.position, _playerSpawn.rotation);

                if (wasEnabled) controller.enabled = true;
            }

            GameEvents.RaiseFloorChanged(_floorNumber);
            GameEvents.RaiseAvailableDoorsChanged(CollectDoorIds());

            if (player != null)
            {
                GameEvents.RaisePlayerSpawned(player);
            }
        }

        private string[] CollectDoorIds()
        {
            if (_doors == null) return System.Array.Empty<string>();

            int count = 0;
            foreach (DoorController door in _doors)
            {
                if (door != null && !string.IsNullOrEmpty(door.DoorId)) count++;
            }

            var ids = new string[count];
            int i = 0;
            foreach (DoorController door in _doors)
            {
                if (door != null && !string.IsNullOrEmpty(door.DoorId)) ids[i++] = door.DoorId;
            }
            return ids;
        }
    }
}
