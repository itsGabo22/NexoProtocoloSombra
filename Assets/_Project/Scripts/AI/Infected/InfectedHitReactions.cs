using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Reacciones físicas procedurales del infectado: retroceso del cuerpo según la dirección y la zona del impacto,
    /// pérdida de equilibrio, caída al morir y derribo al ser empujado en el forcejeo.
    /// No usa ragdoll: inclina la pieza visual (Body) sobre los pies y desplaza al agente con NavMeshAgent.Move,
    /// así nunca atraviesa paredes ni sale del NavMesh. Todo por corrutinas temporales (sin Update).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfectedHitReactions : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Pieza visual que se inclina y cae (hijo 'Body').")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private NavMeshAgent _agent;
        [Tooltip("Fuente 3D para impactos y quejidos (separada de la voz y de los pasos).")]
        [SerializeField] private AudioSource _hitSource;
        [SerializeField] private AudioClip _fleshHitClip;
        [SerializeField] private AudioClip _painClip;

        [Header("Zonas (altura sobre los pies, en metros)")]
        [SerializeField] private float _headHeight = 1.45f;
        [SerializeField] private float _legsHeight = 0.85f;

        [Header("Retroceso por zona")]
        [SerializeField] private float _torsoPush = 0.28f;
        [SerializeField] private float _headPush = 0.45f;
        [SerializeField] private float _legsPush = 0.12f;
        [SerializeField] private float _torsoLean = 14f;
        [SerializeField] private float _headLean = 26f;
        [Tooltip("Cuánto se hunde el cuerpo al flaquear una pierna (sin meter los pies en el suelo).")]
        [SerializeField] private float _kneeDrop = 0.12f;

        private Coroutine _routine;
        private Quaternion _restRotation = Quaternion.identity;
        private Vector3 _restPosition;
        private bool _isFallen;

        public bool IsFallen => _isFallen;

        private void Awake()
        {
            if (_visualRoot == null) _visualRoot = transform.Find("Body");
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_visualRoot != null)
            {
                _restRotation = _visualRoot.localRotation;
                _restPosition = _visualRoot.localPosition;
            }
        }

        /// <summary>
        /// Determina la zona alcanzada repitiendo el rayo de la cámara del jugador contra los colliders de este infectado.
        /// Si el rayo no lo toca (disparo desde la cadera con dispersión), se toma el torso.
        /// </summary>
        public HitZone ResolveZone(Transform aimCamera)
        {
            if (aimCamera == null) return HitZone.Torso;

            var ray = new Ray(aimCamera.position, aimCamera.forward);
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger || !col.enabled) continue;
                if (!col.Raycast(ray, out RaycastHit hit, 60f)) continue;

                float height = hit.point.y - transform.position.y;
                if (height >= _headHeight) return HitZone.Head;
                if (height <= _legsHeight) return HitZone.Legs;
                return HitZone.Torso;
            }
            return HitZone.Torso;
        }

        /// <summary>Reacción a un impacto no mortal: empuje, inclinación y recuperación progresiva.</summary>
        public void PlayHit(HitZone zone, Vector3 shotOrigin, float recoverTime)
        {
            if (_isFallen) return;
            PlayOneShot(_fleshHitClip, 0.9f);
            if (zone != HitZone.Legs || Random.value < 0.5f) PlayOneShot(_painClip, zone == HitZone.Head ? 0.9f : 0.6f, Random.Range(0.85f, 1.05f));

            Vector3 away = FlatDirectionFrom(shotOrigin);
            Restart(HitRoutine(zone, away, recoverTime));
        }

        /// <summary>Caída al morir: se desploma alejándose del disparo y queda tendido.</summary>
        public void PlayDeath(Vector3 shotOrigin)
        {
            PlayOneShot(_fleshHitClip, 1f);
            _isFallen = true;
            Restart(DeathRoutine(FlatDirectionFrom(shotOrigin)));
        }

        /// <summary>Derribo tras ser empujado en el forcejeo: cae de espaldas y se levanta tras <paramref name="downTime"/>.</summary>
        public void PlayKnockdown(Vector3 pusherPosition, float downTime)
        {
            Restart(KnockdownRoutine(FlatDirectionFrom(pusherPosition), downTime));
        }

        /// <summary>Deja la pieza visual en pie al instante (reinicio de piso o reaparición).</summary>
        public void ResetPose()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _isFallen = false;
            ApplyPose(_restRotation, _restPosition);
        }

        private void OnDisable()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        }

        // ---------- Corrutinas ----------

        private IEnumerator HitRoutine(HitZone zone, Vector3 away, float recoverTime)
        {
            float push = zone == HitZone.Head ? _headPush : zone == HitZone.Legs ? _legsPush : _torsoPush;
            float lean = zone == HitZone.Head ? _headLean : zone == HitZone.Legs ? -10f : _torsoLean;
            float drop = zone == HitZone.Legs ? _kneeDrop : 0f;
            // Variación lateral leve: dos impactos iguales no se ven idénticos
            Vector3 axis = Vector3.Cross(Vector3.up, Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f) * away);
            Quaternion target = LocalTilt(axis, lean);
            Vector3 targetPos = _restPosition + Vector3.down * drop;

            // Impacto: inclinación rápida y empuje del cuerpo sobre el NavMesh
            const float impactTime = 0.12f;
            for (float t = 0f; t < impactTime; t += Time.deltaTime)
            {
                float k = t / impactTime;
                ApplyPose(Quaternion.Slerp(_restRotation, target, EaseOut(k)), Vector3.Lerp(_restPosition, targetPos, EaseOut(k)));
                MoveAgent(away * (push * Time.deltaTime / impactTime));
                yield return null;
            }
            ApplyPose(target, targetPos);

            // Mantiene el desequilibrio un instante y recupera la postura
            yield return new WaitForSeconds(recoverTime * 0.35f);
            float recover = Mathf.Max(0.2f, recoverTime * 0.65f);
            for (float t = 0f; t < recover; t += Time.deltaTime)
            {
                float k = EaseInOut(t / recover);
                ApplyPose(Quaternion.Slerp(target, _restRotation, k), Vector3.Lerp(targetPos, _restPosition, k));
                yield return null;
            }
            ApplyPose(_restRotation, _restPosition);
            _routine = null;
        }

        private IEnumerator FallRoutine(Vector3 away, float angle, float duration, float slide)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion start = _visualRoot != null ? _visualRoot.localRotation : _restRotation;
            Quaternion target = LocalTilt(axis, angle);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // Aceleración tipo gravedad y un rebote pequeño al tocar el suelo
                float k = t / duration;
                float eased = k * k;
                ApplyPose(Quaternion.Slerp(start, target, eased), _restPosition);
                if (k < 0.6f) MoveAgent(away * (slide * Time.deltaTime / (duration * 0.6f)));
                yield return null;
            }

            Quaternion bounce = LocalTilt(axis, angle - 6f);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { ApplyPose(Quaternion.Slerp(target, bounce, Mathf.Sin(t / 0.12f * Mathf.PI)), _restPosition); yield return null; }
            ApplyPose(target, _restPosition);
        }

        private IEnumerator DeathRoutine(Vector3 away)
        {
            yield return FallRoutine(away, 88f, 0.7f, 0.35f);
            _routine = null;
        }

        private IEnumerator KnockdownRoutine(Vector3 away, float downTime)
        {
            _isFallen = true;
            yield return FallRoutine(away, 80f, 0.45f, 1.2f);
            yield return new WaitForSeconds(Mathf.Max(0.2f, downTime - 1f));

            // Se incorpora en dos tiempos (rodillas y luego en pie) en lugar de saltar a la vertical
            Quaternion lying = _visualRoot != null ? _visualRoot.localRotation : _restRotation;
            Quaternion kneel = LocalTilt(Vector3.Cross(Vector3.up, -away), 25f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { ApplyPose(Quaternion.Slerp(lying, kneel, EaseInOut(t / 0.5f)), _restPosition + Vector3.down * 0.3f * (1f - t / 0.5f)); yield return null; }
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { ApplyPose(Quaternion.Slerp(kneel, _restRotation, EaseOut(t / 0.4f)), _restPosition); yield return null; }
            ApplyPose(_restRotation, _restPosition);
            _isFallen = false;
            _routine = null;
        }

        // ---------- Utilidades ----------

        private void Restart(IEnumerator routine)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (isActiveAndEnabled) _routine = StartCoroutine(routine);
        }

        private Vector3 FlatDirectionFrom(Vector3 origin)
        {
            Vector3 away = transform.position - origin;
            away.y = 0f;
            return away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
        }

        /// <summary>Rotación local de Body que inclina la parte superior del cuerpo alrededor de un eje horizontal del mundo.</summary>
        private Quaternion LocalTilt(Vector3 worldAxis, float angle)
        {
            Vector3 localAxis = transform.InverseTransformDirection(worldAxis.normalized);
            return Quaternion.AngleAxis(angle, localAxis) * _restRotation;
        }

        private void ApplyPose(Quaternion rotation, Vector3 position)
        {
            if (_visualRoot == null) return;
            _visualRoot.localRotation = rotation;
            _visualRoot.localPosition = position;
        }

        private void MoveAgent(Vector3 offset)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(offset);
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch = 1f)
        {
            if (_hitSource == null || clip == null) return;
            _hitSource.pitch = pitch;
            _hitSource.PlayOneShot(clip, volume);
        }

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
        private static float EaseInOut(float k) => k * k * (3f - 2f * k);
    }
}
