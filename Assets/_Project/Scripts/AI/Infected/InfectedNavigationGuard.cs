using UnityEngine;
using UnityEngine.AI;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Detección y recuperación de bloqueos de navegación del infectado (columnas, esquinas de mesas, rutas parciales).
    /// Se evalúa en el tick de la IA (0.15 s), nunca por frame, y solo calcula rutas cuando detecta un problema.
    /// Escalada de recuperación:
    ///   1) Recalcular la ruta al mismo destino.
    ///   2) Si la ruta es parcial o inválida, buscar el punto alcanzable más cercano alrededor del destino.
    ///   3) Rodear el obstáculo con un paso lateral sobre el NavMesh.
    ///   4) Si sigue bloqueado, informar al cerebro para que cambie de comportamiento.
    /// </summary>
    public sealed class InfectedNavigationGuard
    {
        private const float StuckSpeed = 0.12f;
        private const float StuckTime = 1.1f;
        private const float DestinationChangeSqr = 0.36f; // 0.6 m: no se reprograma la ruta por cambios menores
        private const int AlternativeSamples = 10;
        private const float SidestepDistance = 1.4f;
        private const int MaxAttempts = 3;

        private readonly NavMeshAgent _agent;
        private readonly NavMeshPath _scratchPath = new NavMeshPath();

        private Vector3 _requestedDestination;
        private bool _hasRequest;
        private float _stuckSince = -1f;
        private int _attempts;

        /// <summary>True cuando se agotaron los intentos de recuperación hacia el destino actual.</summary>
        public bool GaveUp { get; private set; }

        /// <summary>True mientras el agente va a un punto de rodeo en lugar de a su destino real.</summary>
        public bool IsOnDetour { get; private set; }

        /// <summary>Número de bloqueos resueltos (para depuración y pruebas).</summary>
        public int RecoveredCount { get; private set; }

        public InfectedNavigationGuard(NavMeshAgent agent)
        {
            _agent = agent;
        }

        /// <summary>
        /// Fija el destino solo si cambió lo suficiente (evita recalcular la ruta cada tick durante la persecución).
        /// </summary>
        public void SetDestination(Vector3 destination, bool force = false)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            bool changed = !_hasRequest || (destination - _requestedDestination).sqrMagnitude > DestinationChangeSqr;
            if (!changed && !force && _agent.hasPath) return;

            if (changed)
            {
                _attempts = 0;
                GaveUp = false;
            }

            IsOnDetour = false;
            _requestedDestination = destination;
            _hasRequest = true;
            _stuckSince = -1f;
            _agent.SetDestination(ResolveReachable(destination));
        }

        public void Clear()
        {
            _hasRequest = false;
            _stuckSince = -1f;
            _attempts = 0;
            GaveUp = false;
            IsOnDetour = false;
        }

        /// <summary>
        /// Comprueba el avance del agente. Devuelve true si aplicó una recuperación en este tick.
        /// </summary>
        public bool Tick(float now)
        {
            if (!_hasRequest || _agent == null || !_agent.isOnNavMesh || _agent.isStopped || _agent.pathPending) return false;

            // Ruta parcial: el destino no es alcanzable (p. ej. jugador pegado a una columna, dentro del margen recortado)
            if (_agent.pathStatus != NavMeshPathStatus.PathComplete && _agent.remainingDistance <= _agent.stoppingDistance + 0.4f)
            {
                return Recover(now);
            }

            bool wantsToMove = _agent.remainingDistance > _agent.stoppingDistance + 0.25f;
            bool barelyMoving = _agent.velocity.sqrMagnitude < StuckSpeed * StuckSpeed;

            // Rodeo completado: retomar el destino real
            if (IsOnDetour && !wantsToMove)
            {
                IsOnDetour = false;
                _stuckSince = -1f;
                _agent.SetDestination(ResolveReachable(_requestedDestination));
                return true;
            }

            if (!wantsToMove || !barelyMoving)
            {
                _stuckSince = -1f;
                return false;
            }

            if (_stuckSince < 0f)
            {
                _stuckSince = now;
                return false;
            }

            return now - _stuckSince >= StuckTime && Recover(now);
        }

        private bool Recover(float now)
        {
            _stuckSince = now;
            _attempts++;

            if (_attempts > MaxAttempts)
            {
                GaveUp = true;
                return false;
            }

            Vector3 target;
            switch (_attempts)
            {
                case 1:
                    // 1) Recalcular (la ruta pudo quedar obsoleta por un obstáculo con carving que se movió)
                    _agent.ResetPath();
                    target = ResolveReachable(_requestedDestination);
                    break;
                case 2:
                    // 2) Punto alternativo alcanzable alrededor del destino
                    target = FindReachableAround(_requestedDestination, 2.5f, out bool found);
                    if (!found) target = ResolveReachable(_requestedDestination);
                    break;
                default:
                    // 3) Rodear: paso lateral hacia el lado libre y después retomar el destino
                    target = Sidestep();
                    IsOnDetour = true;
                    break;
            }

            _agent.SetDestination(target);
            RecoveredCount++;
            return true;
        }

        /// <summary>Devuelve el propio destino si la ruta es completa; si no, el punto alcanzable más próximo a él.</summary>
        private Vector3 ResolveReachable(Vector3 destination)
        {
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, _agent.areaMask)) destination = hit.position;

            if (_agent.CalculatePath(destination, _scratchPath) && _scratchPath.status == NavMeshPathStatus.PathComplete)
            {
                return destination;
            }

            Vector3 alternative = FindReachableAround(destination, 2f, out bool found);
            return found ? alternative : destination;
        }

        private Vector3 FindReachableAround(Vector3 center, float radius, out bool found)
        {
            found = false;
            Vector3 best = center;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < AlternativeSamples; i++)
            {
                float angle = i * Mathf.PI * 2f / AlternativeSamples;
                float r = radius * (0.5f + 0.5f * (i % 2));
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, _agent.areaMask)) continue;
                if (!_agent.CalculatePath(hit.position, _scratchPath) || _scratchPath.status != NavMeshPathStatus.PathComplete) continue;

                float distance = (hit.position - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = hit.position;
                    found = true;
                }
            }
            return best;
        }

        private Vector3 Sidestep()
        {
            Transform t = _agent.transform;
            Vector3 toDestination = _requestedDestination - t.position;
            toDestination.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toDestination.sqrMagnitude > 0.01f ? toDestination.normalized : t.forward);

            // Prueba ambos lados y se queda con el que tenga NavMesh y no esté pegado a un borde
            for (int sign = 1; sign >= -1; sign -= 2)
            {
                Vector3 candidate = t.position + side * (sign * SidestepDistance) + toDestination.normalized * 0.5f;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.8f, _agent.areaMask) &&
                    !NavMesh.Raycast(t.position, hit.position, out _, _agent.areaMask))
                {
                    return hit.position;
                }
            }
            return ResolveReachable(_requestedDestination);
        }
    }
}
