using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Sospecha: oyó un ruido lejano o vio una silueta sin reconocerla. Se detiene, gira hacia el origen y presta
    /// atención unos segundos. Si el estímulo se repite o se confirma, investiga; si no, vuelve a su rutina.
    /// No conoce la posición del jugador: solo el punto donde percibió algo.
    /// </summary>
    public sealed class SuspiciousState : IInfectedState
    {
        private const float MinAttention = 2.2f;
        private const float MaxAttention = 3.4f;

        private readonly SmartInfectedBrain _brain;
        private Vector3 _target;
        private float _endTime;
        private int _stimuli;

        public SuspiciousState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        /// <summary>Punto que llamó su atención (ruido o silueta).</summary>
        public void SetTarget(Vector3 target)
        {
            _target = target;
        }

        public void Enter()
        {
            _stimuli = 1;
            _endTime = Time.time + Random.Range(MinAttention, MaxAttention);
            _brain.SetAlerted(false);
            _brain.StopMoving();
            _brain.StartTurnTowards(_target, 150f);
        }

        public void Tick()
        {
            if (_brain.CanSeePlayer)
            {
                _brain.ChangeState(_brain.Chase);
                return;
            }

            // La silueta sigue ahí y la alerta sube: ya no es imaginación
            if (_brain.SeesSomething && _brain.Awareness >= 0.65f)
            {
                GoInvestigate(_brain.SuspicionPoint);
                return;
            }

            if (_brain.ConsumeHeardNoise())
            {
                _stimuli++;
                // Un segundo ruido o uno fuerte confirma que hay algo
                if (_brain.LastNoiseAlarming || _stimuli >= 2)
                {
                    GoInvestigate(_brain.LastNoisePos);
                    return;
                }
                _target = _brain.LastNoisePos;
                _brain.StartTurnTowards(_target, 150f);
                _endTime = Time.time + MinAttention;
            }

            if (Time.time < _endTime) return;

            // Si la alerta quedó alta, se acerca a mirar; si no, lo da por nada
            if (_brain.Awareness >= 0.45f) GoInvestigate(_target);
            else _brain.ChangeState(_brain.Return);
        }

        public void Exit()
        {
            _brain.StopLookAround();
        }

        private void GoInvestigate(Vector3 point)
        {
            _brain.Investigate.SetTarget(point);
            _brain.ChangeState(_brain.Investigate);
        }
    }
}
