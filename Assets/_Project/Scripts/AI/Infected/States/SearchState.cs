using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Búsqueda: revisa varios puntos cercanos al lugar del ruido o donde perdió al jugador, mirando alrededor en
    /// cada uno. No sabe dónde está el jugador: solo lo encontrará si lo ve o lo oye. Al terminar, regresa.
    /// </summary>
    public sealed class SearchState : IInfectedState
    {
        private const int ProbeCount = 3;
        private const float ProbeRadius = 6f;
        private const float LookTime = 2f;
        private const float MaxSearchTime = 20f;

        private readonly SmartInfectedBrain _brain;
        private Vector3 _center;
        private int _probesLeft;
        private bool _looking;
        private float _lookEnd;
        private float _enterTime;

        public SearchState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        /// <summary>Centro de la búsqueda (ruido investigado o última posición conocida).</summary>
        public void SetCenter(Vector3 center)
        {
            _center = center;
        }

        public void Enter()
        {
            _brain.SetAlerted(true);
            _enterTime = Time.time;
            _probesLeft = ProbeCount;
            // Primero mira en el propio punto
            BeginLook();
        }

        public void Tick()
        {
            if (_brain.CanSeePlayer)
            {
                _brain.ChangeState(_brain.Chase);
                return;
            }

            if (_brain.ConsumeHeardNoise())
            {
                _brain.Investigate.SetTarget(_brain.LastNoisePos);
                _brain.ChangeState(_brain.Investigate);
                return;
            }

            if (Time.time - _enterTime > MaxSearchTime)
            {
                _brain.ChangeState(_brain.Return);
                return;
            }

            if (_looking)
            {
                if (Time.time < _lookEnd) return;
                _looking = false;

                if (_probesLeft <= 0)
                {
                    _brain.ChangeState(_brain.Return);
                    return;
                }
                _probesLeft--;
                if (_brain.TryGetRandomPoint(_center, ProbeRadius, out Vector3 probe)) _brain.MoveTo(probe, _brain.WalkSpeed * 1.15f);
                else BeginLook();
                return;
            }

            if (_brain.HasArrived(0.5f) || _brain.NavigationBlocked) BeginLook();
        }

        public void Exit()
        {
            _looking = false;
            _brain.StopLookAround();
        }

        private void BeginLook()
        {
            _looking = true;
            _lookEnd = Time.time + LookTime;
            _brain.StopMoving();
            _brain.StartLookAround(LookTime, 110f);
        }
    }
}
