using UnityEngine;
using UnityEngine.AI;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Se oculta a 6-9m del jugador (preferiblemente fuera de su línea de visión), imita la voz de Elena
    /// y lanza una notificación falsa al HUD (IsFake). Si el jugador lo descubre, pasa a Chase.
    /// </summary>
    public sealed class MimicryAmbushState : IInfectedState
    {
        private const float MinDistance = 6f;
        private const float MaxDistance = 9f;
        private const int SampleAttempts = 8;
        private const float LurkDuration = 5f;
        private const float GiveUpTime = 12f;
        private const float PlayerEyeHeight = 1.6f;

        private readonly SmartInfectedBrain _brain;
        private bool _lurking;
        private float _lurkStartTime;
        private float _enterTime;

        public MimicryAmbushState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _lurking = false;
            _enterTime = Time.time;

            if (_brain.Player == null || !TryFindAmbushPoint(out Vector3 point))
            {
                _brain.MarkMimicryUsed();
                _brain.ChangeState(_brain.Patrol);
                return;
            }

            _brain.SetAlerted(false);
            _brain.MoveTo(point, _brain.WalkSpeed);
        }

        public void Tick()
        {
            if (_brain.CanSeePlayer)
            {
                _brain.MarkMimicryUsed();
                _brain.ChangeState(_brain.Chase);
                return;
            }

            if (_brain.ConsumeHeardNoise())
            {
                _brain.MarkMimicryUsed();
                _brain.Investigate.SetTarget(_brain.LastNoisePos);
                _brain.ChangeState(_brain.Investigate);
                return;
            }

            if (!_lurking)
            {
                if (_brain.HasArrived(0.5f) || Time.time - _enterTime > GiveUpTime)
                {
                    _lurking = true;
                    _lurkStartTime = Time.time;
                    _brain.StopMoving();
                    _brain.FacePlayer();
                    _brain.PlayMimicry();
                    _brain.MarkMimicryUsed();
                }
                return;
            }

            if (Time.time - _lurkStartTime >= LurkDuration)
            {
                _brain.ChangeState(_brain.Patrol);
            }
        }

        public void Exit()
        {
            _lurking = false;
        }

        /// <summary>
        /// Busca un punto del NavMesh en el anillo 6-9m alrededor del jugador, priorizando los ocultos a su vista.
        /// </summary>
        private bool TryFindAmbushPoint(out Vector3 result)
        {
            Vector3 playerPos = _brain.Player.position;
            Vector3 playerEyes = playerPos + Vector3.up * PlayerEyeHeight;
            bool hasFallback = false;
            result = Vector3.zero;

            for (int i = 0; i < SampleAttempts; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

                Vector3 candidate = playerPos + new Vector3(dir.x, 0f, dir.y) * Random.Range(MinDistance, MaxDistance);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

                // Oculto si hay geometría entre los ojos del jugador y el punto
                bool hidden = Physics.Linecast(playerEyes, hit.position + Vector3.up * 1f, _brain.ObstacleMask, QueryTriggerInteraction.Ignore);
                if (hidden)
                {
                    result = hit.position;
                    return true;
                }

                if (!hasFallback)
                {
                    hasFallback = true;
                    result = hit.position;
                }
            }

            return hasFallback;
        }
    }
}
