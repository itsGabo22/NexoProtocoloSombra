using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Acude al último ruido (o a la última posición conocida del jugador) a WalkSpeed * 1.4,
    /// gira buscando durante 3s y vuelve a patrullar.
    /// </summary>
    public sealed class InvestigateNoiseState : IInfectedState
    {
        private const float SpeedMultiplier = 1.4f;
        private const float SearchDuration = 3f;
        private const float GiveUpTime = 15f;

        private readonly SmartInfectedBrain _brain;
        private Vector3 _target;
        private bool _searching;
        private float _searchStartTime;
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

            if (!_searching)
            {
                if (_brain.HasArrived(0.6f) || Time.time - _enterTime > GiveUpTime)
                {
                    _searching = true;
                    _searchStartTime = Time.time;
                    _brain.StopMoving();
                    _brain.StartLookAround(SearchDuration);
                }
                return;
            }

            if (Time.time - _searchStartTime >= SearchDuration)
            {
                _brain.ChangeState(_brain.Patrol);
            }
        }

        public void Exit()
        {
            _searching = false;
            _brain.StopLookAround();
        }

        private void GoToTarget()
        {
            _searching = false;
            _brain.StopLookAround();
            _brain.MoveTo(_target, _brain.WalkSpeed * SpeedMultiplier);
        }
    }
}
