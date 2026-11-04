using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Volumen invisible de punto de control: al cruzarlo el jugador por primera vez en esta carga del piso,
    /// PlayerCheckpoints guarda esta posición como reaparición tras una caída. El aviso del HUD es discreto y único.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CheckpointTrigger : MonoBehaviour
    {
        [SerializeField] private string _label = "Punto de control";
        [SerializeField] private bool _notify = true;

        private bool _reached;
        private int _playerLayer = -1;

        public bool Reached => _reached;
        public string Label => _label;

        private void Awake()
        {
            _playerLayer = LayerMask.NameToLayer("Player");
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_reached || other.gameObject.layer != _playerLayer) return;
            _reached = true;
            GameEvents.RaiseCheckpointReached(transform.position, transform.eulerAngles.y, _label);
            if (_notify) GameEvents.RaiseNotification(new HudNotification("NEXOOS", "Punto de control registrado.", false));
        }

        /// <summary>Comprobaciones del Editor: simula el cruce.</summary>
        public void ForceReach()
        {
            if (_reached) return;
            _reached = true;
            GameEvents.RaiseCheckpointReached(transform.position, transform.eulerAngles.y, _label);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider>();
            if (box != null) Gizmos.DrawCube(box.center, box.size);
        }
#endif
    }
}
