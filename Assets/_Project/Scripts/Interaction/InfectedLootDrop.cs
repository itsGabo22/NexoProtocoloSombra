using Nexo.AI.Infected;
using UnityEngine;
using UnityEngine.AI;

namespace Nexo.Interaction
{
    /// <summary>
    /// Botín de un infectado: va en su InfectedSpawnPoint y, cuando ese infectado muere, deja en el suelo
    /// los objetos configurados (siempre los «garantizados», como el cuchillo del patrullero del Piso 12, y con
    /// cierta probabilidad un botiquín). Escucha el evento estático SmartInfectedBrain.Died (sin sondeo).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InfectedSpawnPoint))]
    public sealed class InfectedLootDrop : MonoBehaviour
    {
        [Tooltip("Objetos que suelta siempre (p. ej. PF_KnifePickup).")]
        [SerializeField] private GameObject[] _guaranteed = System.Array.Empty<GameObject>();
        [Tooltip("Objeto con probabilidad (p. ej. PF_Medkit).")]
        [SerializeField] private GameObject _chanceDrop;
        [SerializeField, Range(0f, 1f)] private float _chance = 0.35f;

        private InfectedSpawnPoint _spawnPoint;
        private bool _dropped;

        public GameObject[] Guaranteed => _guaranteed;
        public float Chance => _chance;

        private void Awake() => _spawnPoint = GetComponent<InfectedSpawnPoint>();

        private void OnEnable() => SmartInfectedBrain.Died += HandleInfectedDied;

        private void OnDisable() => SmartInfectedBrain.Died -= HandleInfectedDied;

        private void HandleInfectedDied(SmartInfectedBrain brain)
        {
            if (_dropped || brain == null || _spawnPoint == null || brain != _spawnPoint.Instance) return;
            _dropped = true;

            Vector3 origin = brain.transform.position;
            int index = 0;
            foreach (GameObject item in _guaranteed) Drop(item, origin, index++);
            if (_chanceDrop != null && Random.value < _chance) Drop(_chanceDrop, origin, index);
        }

        private void Drop(GameObject prefab, Vector3 origin, int index)
        {
            if (prefab == null) return;
            // Repartidos alrededor del cuerpo, sobre el NavMesh, para que se vean y se puedan recoger
            float angle = index * 2.2f;
            Vector3 p = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.8f;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
            // Hijo de la escena del piso (se descarga con ella)
            Instantiate(prefab, p + Vector3.up * 0.05f, Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f), transform.parent);
        }
    }
}
