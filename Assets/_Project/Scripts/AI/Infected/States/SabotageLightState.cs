using Nexo.Interaction;
using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Camina hasta el interruptor encendido más cercano, ejecuta SabotageTrigger (1.2s) y corta la luz del cuarto,
    /// obligando al jugador a recurrir a la visión térmica.
    /// </summary>
    public sealed class SabotageLightState : IInfectedState
    {
        private const float SabotageDuration = 1.2f;
        private const float ReachDistance = 1.2f;
        private const float GiveUpTime = 15f;

        private readonly SmartInfectedBrain _brain;
        private LightSwitchInteractable _target;
        private bool _sabotaging;
        private float _actionStartTime;
        private float _enterTime;

        public SabotageLightState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        /// <summary>Define el interruptor objetivo antes de entrar en el estado.</summary>
        public void SetTarget(LightSwitchInteractable target)
        {
            _target = target;
        }

        public void Enter()
        {
            _sabotaging = false;
            _enterTime = Time.time;

            if (_target == null)
            {
                _brain.ChangeState(_brain.Patrol);
                return;
            }

            _brain.MoveTo(_target.transform.position, _brain.WalkSpeed);
        }

        public void Tick()
        {
            if (_brain.CanSeePlayer)
            {
                _brain.ChangeState(_brain.Chase);
                return;
            }

            // El jugador ya apagó la luz o el interruptor desapareció
            if (_target == null || (!_target.IsOn && !_sabotaging))
            {
                _brain.ChangeState(_brain.Patrol);
                return;
            }

            if (!_sabotaging)
            {
                float sqrDistance = (_brain.transform.position - _target.transform.position).sqrMagnitude;
                if (sqrDistance <= ReachDistance * ReachDistance || _brain.HasArrived(ReachDistance))
                {
                    _sabotaging = true;
                    _actionStartTime = Time.time;
                    _brain.StopMoving();
                    _brain.TriggerSabotage();
                }
                else if (Time.time - _enterTime > GiveUpTime)
                {
                    // Ruta inalcanzable: no insistir y no reintentar hasta el próximo cooldown
                    _brain.MarkSabotageUsed();
                    _brain.ChangeState(_brain.Patrol);
                }
                return;
            }

            if (Time.time - _actionStartTime >= SabotageDuration)
            {
                _target.Sabotage(); // SetPower(false) -> GameEvents.OnLightsChanged
                _brain.MarkSabotageUsed();
                _brain.ChangeState(_brain.Patrol);
            }
        }

        public void Exit()
        {
            _sabotaging = false;
            _target = null;
        }
    }
}
