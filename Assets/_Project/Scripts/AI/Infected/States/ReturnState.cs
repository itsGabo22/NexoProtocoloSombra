using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Regreso: tras perder el rastro vuelve caminando a su puesto (punto de patrulla más cercano o posición inicial)
    /// y retoma la patrulla. Sigue atento: puede volver a sospechar o perseguir por el camino.
    /// </summary>
    public sealed class ReturnState : IInfectedState
    {
        private const float GiveUpTime = 25f;

        private readonly SmartInfectedBrain _brain;
        private float _enterTime;

        public ReturnState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _enterTime = Time.time;
            _brain.SetAlerted(false);
            _brain.MoveTo(FindPost(), _brain.WalkSpeed);
        }

        public void Tick()
        {
            if (_brain.ReactToStimuli()) return;

            if (_brain.HasArrived(0.6f) || _brain.NavigationBlocked || Time.time - _enterTime > GiveUpTime)
            {
                if (_brain.Behavior == InfectedBehavior.Sentinel || _brain.Behavior == InfectedBehavior.Crawler) _brain.transform.rotation = _brain.HomeRotation;
                _brain.ChangeState(_brain.Patrol);
            }
        }

        public void Exit()
        {
        }

        private Vector3 FindPost()
        {
            if (_brain.Behavior != InfectedBehavior.Patroller && _brain.Behavior != InfectedBehavior.Listener) return _brain.HomePosition;

            Transform[] points = _brain.PatrolPoints;
            if (points == null || points.Length == 0) return _brain.HomePosition;

            Vector3 position = _brain.transform.position;
            Vector3 best = _brain.HomePosition;
            float bestDistance = float.MaxValue;
            foreach (Transform point in points)
            {
                if (point == null) continue;
                float distance = (point.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point.position;
                }
            }
            return best;
        }
    }
}
