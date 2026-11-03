using Unity.AI.Navigation;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Enlace de NavMesh a través del vano de una puerta estrecha (más estrecha que el radio de horneado del agente).
    /// Solo está activo mientras la puerta está abierta: con la puerta cerrada ningún infectado la atraviesa.
    /// Va en un objeto propio (no en la hoja de la puerta, que se desplaza al abrirse). Por evento: sin Update.
    /// </summary>
    [RequireComponent(typeof(NavMeshLink))]
    [DisallowMultipleComponent]
    public sealed class DoorNavMeshLink : MonoBehaviour
    {
        [SerializeField] private DoorController _door;

        private NavMeshLink _link;

        private void Awake()
        {
            _link = GetComponent<NavMeshLink>();
        }

        private void OnEnable()
        {
            if (_door == null) return;
            _door.OpenChanged += HandleOpenChanged;
            HandleOpenChanged(_door.IsOpen);
        }

        private void OnDisable()
        {
            if (_door != null) _door.OpenChanged -= HandleOpenChanged;
        }

        private void HandleOpenChanged(bool open)
        {
            if (_link != null) _link.enabled = open;
        }
    }
}
