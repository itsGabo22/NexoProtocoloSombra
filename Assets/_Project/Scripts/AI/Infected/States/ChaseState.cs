using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Grita (ScreamTrigger) y persigue al jugador a RunSpeed mientras lo ve. Sin visión va a la última posición
    /// conocida; si lo pierde de vista durante 4 s, lo busca por la zona (SearchState).
    /// Dentro de AttackRange pasa a AttackState.
    /// </summary>
    public sealed class ChaseState : IInfectedState
    {
        private const float ScreamDuration = 0.8f;
        private const float LoseSightTime = 4f;

        private readonly SmartInfectedBrain _brain;
        private float _enterTime;
        private float _lastSeenTime;

        public ChaseState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _enterTime = Time.time;
            _lastSeenTime = Time.time;

            _brain.SetAlerted(true);
            _brain.StopMoving();
            _brain.FacePlayer();
            _brain.TriggerScream();
        }

        public void Tick()
        {
            if (_brain.Player == null)
            {
                _brain.ChangeState(_brain.Return);
                return;
            }

            // Pausa breve mientras se reproduce el grito
            if (Time.time - _enterTime < ScreamDuration) return;

            if (_brain.CanSeePlayer)
            {
                _lastSeenTime = Time.time;
            }
            else if (Time.time - _lastSeenTime >= LoseSightTime || _brain.NavigationBlocked)
            {
                _brain.LoseTrackOfPlayer();
                return;
            }

            // El ruido escuchado durante la persecución no cambia el estado
            _brain.ConsumeHeardNoise();

            if (_brain.CanSeePlayer && _brain.DistanceToPlayer() <= _brain.AttackRange)
            {
                _brain.StopMoving();
                _brain.FacePlayer();
                if (_brain.CanAttackNow) _brain.ChangeState(_brain.Attack);
                return;
            }

            // Sin visión persigue la última posición conocida; con visión, al jugador directamente
            Vector3 destination = _brain.CanSeePlayer ? _brain.Player.position : _brain.LastKnownPlayerPos;
            _brain.MoveTo(destination, _brain.RunSpeed);
        }

        public void Exit()
        {
        }
    }
}
