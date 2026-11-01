using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Grita (ScreamTrigger), persigue al jugador a RunSpeed y ataca dentro de AttackRange.
    /// Si pierde de vista al jugador durante 4s, investiga su última posición conocida.
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
                _brain.ChangeState(_brain.Patrol);
                return;
            }

            // Pausa breve mientras se reproduce el grito
            if (Time.time - _enterTime < ScreamDuration) return;

            if (_brain.CanSeePlayer)
            {
                _lastSeenTime = Time.time;
            }
            else if (Time.time - _lastSeenTime >= LoseSightTime)
            {
                _brain.Investigate.SetTarget(_brain.LastKnownPlayerPos);
                _brain.ChangeState(_brain.Investigate);
                return;
            }

            // El ruido escuchado durante la persecución no cambia el estado
            _brain.ConsumeHeardNoise();

            if (_brain.DistanceToPlayer() <= _brain.AttackRange)
            {
                _brain.StopMoving();
                _brain.TryAttack();
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
