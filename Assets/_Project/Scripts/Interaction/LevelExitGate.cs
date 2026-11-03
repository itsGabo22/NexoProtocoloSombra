using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Condición de victoria del piso: la puerta de salida permanece bloqueada (también para Elena) hasta que el
    /// jugador elimine a los infectados clave. Al cumplirse, la puerta se abre y StairsExit permite bajar al
    /// siguiente piso (FloorProgression carga el Piso 6).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelExitGate : MonoBehaviour
    {
        [SerializeField] private DoorController _exitDoor;
        [SerializeField, Min(1)] private int _requiredKills = 2;
        [SerializeField] private GameObject[] _activateOnClear;

        private int _kills;

        public bool IsCleared => _kills >= _requiredKills;

        private void OnEnable() => SmartInfectedBrain.Died += HandleInfectedDied;

        private void OnDisable() => SmartInfectedBrain.Died -= HandleInfectedDied;

        private void Start()
        {
            if (_exitDoor != null) _exitDoor.BlockRemoteUnlock(true);
        }

        private void HandleInfectedDied(SmartInfectedBrain brain)
        {
            if (IsCleared) return;
            _kills++;

            if (!IsCleared)
            {
                StorySequence.SetObjective($"Infectados eliminados: {_kills}/{_requiredKills}. La persiana sigue bloqueada mientras quede presencia hostil.");
                return;
            }

            if (_exitDoor != null)
            {
                _exitDoor.BlockRemoteUnlock(false);
                _exitDoor.UnlockAndOpen();
            }
            if (_activateOnClear != null)
            {
                foreach (GameObject go in _activateOnClear) if (go != null) go.SetActive(true);
            }
            GameEvents.RaiseSubtitle("(Un chasquido metálico: la persiana de la escalera se abre)");
            StorySequence.SetObjective("Piso despejado. Ve a la escalera de emergencia (pasillo oeste) y baja al Piso 6 [E].");
        }
    }
}
