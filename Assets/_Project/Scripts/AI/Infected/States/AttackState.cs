using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Ataque cuerpo a cuerpo con anticipación: levanta los brazos (AttackTrigger), espera la ventana de impacto y solo
    /// conecta si el jugador sigue a su alcance y delante. Esquivar a tiempo es posible. Después recupera y vuelve a
    /// perseguir. El golpe lo resuelve PlayerHealth (forcejeo si está ileso, daño si no).
    /// </summary>
    public sealed class AttackState : IInfectedState
    {
        private const float WindUp = 0.35f;
        private const float Recovery = 0.6f;
        private const float ReachTolerance = 1.25f;

        private readonly SmartInfectedBrain _brain;
        private float _enterTime;
        private bool _delivered;

        public AttackState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _enterTime = Time.time;
            _delivered = false;
            _brain.StopMoving();
            _brain.FacePlayer();
            _brain.TriggerAttackAnimation();
        }

        public void Tick()
        {
            float elapsed = Time.time - _enterTime;

            if (!_delivered)
            {
                if (elapsed < WindUp)
                {
                    _brain.FacePlayer();
                    return;
                }

                _delivered = true;
                if (_brain.DistanceToPlayer() <= _brain.AttackRange * ReachTolerance && _brain.IsPlayerInFront())
                {
                    _brain.DeliverAttack();
                }
                return;
            }

            if (elapsed < WindUp + Recovery) return;

            _brain.ConsumeHeardNoise();
            if (_brain.CanSeePlayer) _brain.ChangeState(_brain.Chase);
            else _brain.LoseTrackOfPlayer();
        }

        public void Exit()
        {
        }
    }
}
