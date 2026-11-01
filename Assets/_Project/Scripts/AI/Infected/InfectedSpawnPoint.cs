using Nexo.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Punto de aparición del infectado en F12_Waypoints.
    /// Al recibir GameEvents.OnPlayerSpawned instancia PF_Infected_Variant y le inyecta el jugador y su ruta.
    /// </summary>
    public sealed class InfectedSpawnPoint : MonoBehaviour
    {
        [Header("Prefab e Inicialización")]
        [SerializeField] private SmartInfectedBrain _infectedPrefab;
        [SerializeField] private Transform[] _patrolPoints;
        [SerializeField] private float _navMeshSnapDistance = 2f;

        private SmartInfectedBrain _instance;

        public SmartInfectedBrain Instance => _instance;

        private void OnEnable()
        {
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
        }

        private void HandlePlayerSpawned(Transform player)
        {
            Spawn(player);
        }

        /// <summary>
        /// Instancia (una sola vez) al infectado sobre el NavMesh y lo inicializa con la referencia del jugador.
        /// </summary>
        public SmartInfectedBrain Spawn(Transform player)
        {
            if (_instance != null)
            {
                _instance.Initialize(player, _patrolPoints);
                return _instance;
            }

            if (_infectedPrefab == null)
            {
                Debug.LogError($"[InfectedSpawnPoint] Falta asignar _infectedPrefab en {name}.", this);
                return null;
            }

            Vector3 position = transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, _navMeshSnapDistance, NavMesh.AllAreas))
            {
                position = hit.position;
            }
            else
            {
                Debug.LogWarning($"[InfectedSpawnPoint] {name} no está sobre el NavMesh (¿falta hornear NavMeshSurface?).", this);
            }

            // Se instancia como hijo de la escena del piso para descargarse junto a ella
            _instance = Instantiate(_infectedPrefab, position, transform.rotation, transform.parent);
            _instance.Initialize(player, _patrolPoints);
            return _instance;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
#endif
    }
}
