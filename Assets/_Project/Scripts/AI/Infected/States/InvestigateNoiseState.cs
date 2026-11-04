using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Acude al último ruido (o a la última posición conocida del jugador) a WalkSpeed * 1.4.
    /// Al llegar examina la zona (SearchState). Un ruido nuevo redirige la investigación.
    /// </summary>
    public sealed class InvestigateNoiseState : IInfectedState
    {
        private const float SpeedMultiplier = 1.4f;
        private const float GiveUpTime = 15f;

        private readonly SmartInfectedBrain _brain;
        private Vector3 _target;
        private float _enterTime;

        public InvestigateNoiseState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        /// <summary>Define el punto a investigar antes de entrar en el estado.</summary>
        public void SetTarget(Vector3 target)
        {
            _target = target;
        }

        public void Enter()
        {
            _brain.SetAlerted(true);
            _enterTime = Time.time;
            GoToTarget();
        }

        public void Tick()
        {
            if (_brain.CanSeePlayer)
            {
                _brain.ChangeState(_brain.Chase);
                return;
            }

            // Un ruido nuevo redirige la investigación
            if (_brain.ConsumeHeardNoise())
            {
                _target = _brain.LastNoisePos;
                _enterTime = Time.time;
                GoToTarget();
                return;
            }

            if (_brain.HasArrived(0.6f) || _brain.NavigationBlocked || Time.time - _enterTime > GiveUpTime)
            {
                _brain.Search.SetCenter(_target);
                _brain.ChangeState(_brain.Search);
            }
        }

        public void Exit()
        {
            _brain.StopLookAround();
        }

        private void GoToTarget()
        {
            _brain.StopLookAround();
            _brain.MoveTo(_target, _brain.WalkSpeed * SpeedMultiplier);
        }
    }
}
