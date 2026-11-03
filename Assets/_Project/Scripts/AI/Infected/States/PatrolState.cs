using Nexo.Interaction;
using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Estado de reposo del infectado según su perfil:
    /// - Patroller / Listener: recorre sus PatrolPoints, se detiene en cada uno y a veces mira alrededor.
    /// - Sentinel: espera inmóvil en su posición y gira de vez en cuando.
    /// - Wanderer: deambula despacio por puntos aleatorios cerca de su posición inicial.
    /// Si tiene memoria de aprendizaje, a veces se desvía a revisar la zona donde oyó disparos o perdió al jugador.
    /// Prioridad: ver jugador -> Chase > silueta/ruido -> Suspicious/Investigate > luz cercana -> Sabotage > Mimicry.
    /// </summary>
    public sealed class PatrolState : IInfectedState
    {
        private const float SentinelLookInterval = 7f;

        private readonly SmartInfectedBrain _brain;
        private int _index = -1;
        private bool _pausing;
        private float _resumeTime;
        private float _nextSentinelLook;

        public PatrolState(SmartInfectedBrain brain)
        {
            _brain = brain;
        }

        public void Enter()
        {
            _brain.SetAlerted(false);
            _pausing = false;
            _nextSentinelLook = Time.time + Random.Range(2f, SentinelLookInterval);

            if (_brain.Behavior == InfectedBehavior.Sentinel) GoHome();
            else GoToNextPoint();
        }

        public void Tick()
        {
            if (_brain.ReactToStimuli()) return;

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

            if (_brain.Behavior == InfectedBehavior.Sentinel)
            {
                TickSentinel();
                return;
            }

            if (_pausing)
            {
                if (Time.time >= _resumeTime)
                {
                    _pausing = false;
                    GoToNextPoint();
                }
                return;
            }

            if (_brain.HasArrived() || _brain.NavigationBlocked)
            {
                BeginPause();
            }
        }

        public void Exit()
        {
            _pausing = false;
            _brain.StopLookAround();
        }

        private void TickSentinel()
        {
            // Si algo lo movió de su puesto (persecución, empujón), vuelve a él
            if ((_brain.transform.position - _brain.HomePosition).sqrMagnitude > 1f)
            {
                if (_brain.HasArrived() || _brain.NavigationBlocked) GoHome();
                return;
            }

            if (Time.time >= _nextSentinelLook)
            {
                _nextSentinelLook = Time.time + Random.Range(SentinelLookInterval * 0.6f, SentinelLookInterval * 1.4f);
                _brain.StartLookAround(Random.Range(2f, 3.5f), 70f);
            }
        }

        private void BeginPause()
        {
            _pausing = true;
            _resumeTime = Time.time + Random.Range(_brain.PatrolPause.x, _brain.PatrolPause.y);
            _brain.StopMoving();
            if (Random.value < 0.55f) _brain.StartLookAround(Mathf.Min(2.5f, _resumeTime - Time.time), 90f);
        }

        private void GoHome()
        {
            _brain.MoveTo(_brain.HomePosition, _brain.WalkSpeed * 0.8f);
        }

        private void GoToNextPoint()
        {
            float speed = _brain.Behavior == InfectedBehavior.Wanderer ? _brain.WalkSpeed * 0.7f : _brain.WalkSpeed;

            // Aprendizaje: revisa una zona recordada en lugar del siguiente punto
            if (_brain.Memory != null && _brain.Memory.TryGetHotspot(out Vector3 hotspot))
            {
                _brain.MoveTo(hotspot, speed);
                return;
            }

            if (_brain.Behavior == InfectedBehavior.Wanderer)
            {
                if (_brain.TryGetRandomPoint(_brain.HomePosition, _brain.WanderRadius, out Vector3 point)) _brain.MoveTo(point, speed);
                else _brain.StopMoving();
                return;
            }

            var points = _brain.PatrolPoints;
            if (points == null || points.Length == 0)
            {
                // Sin ruta: se comporta como centinela en su posición
                _brain.StopMoving();
                return;
            }

            // Saltar entradas nulas sin bloquear el bucle
            for (int attempt = 0; attempt < points.Length; attempt++)
            {
                _index = (_index + 1) % points.Length;
                if (points[_index] != null)
                {
                    _brain.MoveTo(points[_index].position, speed);
                    return;
                }
            }

            _brain.StopMoving();
        }
    }
}
