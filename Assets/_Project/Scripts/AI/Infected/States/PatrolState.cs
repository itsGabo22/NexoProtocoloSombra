using Nexo.Interaction;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Recorre los PatrolPoints en bucle a velocidad de paseo.
    /// Prioridad: ver jugador -> Chase > ruido -> Investigate > luz encendida cercana -> Sabotage > Mimicry.
    /// </summary>
    public sealed class PatrolState : IInfectedState
    {
        private readonly SmartInfectedBrain _brain;
        private int _index = -1;

        public PatrolState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _brain.SetAlerted(false);
            GoToNextPoint();
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

            if (_brain.SabotageReady &&
                LightSwitchInteractable.TryFindLitSwitchNear(_brain.transform.position, _brain.SabotageRadius, out LightSwitchInteractable lightSwitch))
            {
                _brain.Sabotage.SetTarget(lightSwitch);
                _brain.ChangeState(_brain.Sabotage);
                return;
            }

            if (_brain.MimicryReady)
            {
                _brain.ChangeState(_brain.Mimicry);
                return;
            }

            if (_brain.HasArrived())
            {
                GoToNextPoint();
            }
        }

        public void Exit()
        {
        }

        private void GoToNextPoint()
        {
            var points = _brain.PatrolPoints;
            if (points == null || points.Length == 0)
            {
                _brain.StopMoving();
                return;
            }

            // Saltar entradas nulas sin bloquear el bucle
            for (int attempt = 0; attempt < points.Length; attempt++)
            {
                _index = (_index + 1) % points.Length;
                if (points[_index] != null)
                {
                    _brain.MoveTo(points[_index].position, _brain.WalkSpeed);
                    return;
                }
            }

            _brain.StopMoving();
        }
    }
}
