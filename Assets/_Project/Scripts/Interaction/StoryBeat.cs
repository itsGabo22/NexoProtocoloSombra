using System.Collections;
using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Momento del modo historia del piso: muestra un mensaje (notificación y subtítulo) una sola vez.
    /// Se dispara al cargar el piso (con retardo), al entrar el jugador en su volumen o al desbloquearse una puerta.
    /// Opcionalmente exige que otro StoryBeat haya ocurrido antes, para encadenar objetivos en orden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryBeat : MonoBehaviour
    {
        public enum TriggerMode { OnFloorStart, OnPlayerEnter, OnDoorUnlocked }

        [SerializeField] private TriggerMode _mode = TriggerMode.OnPlayerEnter;
        [SerializeField] private float _delay = 0f;
        [Tooltip("Solo para OnDoorUnlocked: DoorId que dispara el momento.")]
        [SerializeField] private string _doorId = "";
        [Tooltip("Momento que debe haber ocurrido antes (opcional).")]
        [SerializeField] private StoryBeat _requires;

        [Header("Mensaje")]
        [SerializeField] private string _sender = "OBJETIVO";
        [SerializeField, TextArea(2, 4)] private string _body = "";
        [SerializeField, TextArea(1, 3)] private string _subtitle = "";

        public bool HasFired { get; private set; }

        private void OnEnable()
        {
            if (_mode == TriggerMode.OnDoorUnlocked) GameEvents.OnUiAction += HandleUiAction;
        }

        private void OnDisable()
        {
            if (_mode == TriggerMode.OnDoorUnlocked) GameEvents.OnUiAction -= HandleUiAction;
        }

        private void Start()
        {
            if (_mode == TriggerMode.OnFloorStart) StartCoroutine(FireRoutine());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_mode != TriggerMode.OnPlayerEnter || other.gameObject.layer != LayerMask.NameToLayer("Player")) return;
            StartCoroutine(FireRoutine());
        }

        private void HandleUiAction(ElenaUiAction action, string targetId)
        {
            if (action == ElenaUiAction.UnlockDoor && string.Equals(targetId, _doorId, System.StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(FireRoutine());
            }
        }

        /// <summary>Permite dispararlo desde otros sistemas (p. ej. la terminal de seguridad).</summary>
        public void Fire()
        {
            if (isActiveAndEnabled) StartCoroutine(FireRoutine());
        }

        private IEnumerator FireRoutine()
        {
            if (HasFired || (_requires != null && !_requires.HasFired)) yield break;
            HasFired = true;

            if (_delay > 0f) yield return new WaitForSeconds(_delay);

            if (!string.IsNullOrEmpty(_body)) GameEvents.RaiseNotification(new HudNotification(_sender, _body, false));
            if (!string.IsNullOrEmpty(_subtitle)) GameEvents.RaiseSubtitle(_subtitle);
        }
    }
}
