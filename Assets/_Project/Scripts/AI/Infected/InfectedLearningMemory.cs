using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Memoria del infectado que aprende del jugador. Solo registra lo que el propio infectado percibe
    /// (ruidos que oye y lugares donde vio o perdió al jugador), nunca la posición real del jugador.
    /// - Disparos oídos: marca la zona como "caliente" y la revisa en su patrulla.
    /// - Carreras oídas: aumenta su atención auditiva (hasta x1.4) en los corredores.
    /// - Sitios donde perdió al jugador: los inspecciona después (escondites repetidos).
    /// - Silencio prolongado: los recuerdos se desvanecen y la atención vuelve a la normalidad.
    /// Coste fijo y pequeño: 6 recuerdos en un array, actualizados en el tick de la IA (0.15 s).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfectedLearningMemory : MonoBehaviour
    {
        private const int Capacity = 6;
        private const float MergeDistance = 3.5f;

        [Header("Aprendizaje")]
        [SerializeField] private float _gunshotWeight = 3f;
        [SerializeField] private float _sprintWeight = 1f;
        [SerializeField] private float _lostPlayerWeight = 2.5f;
        [SerializeField] private float _sightingWeight = 1.5f;
        [Tooltip("Peso que pierde cada recuerdo por segundo.")]
        [SerializeField] private float _decayPerSecond = 0.035f;
        [Tooltip("Segundos sin percibir nada tras los que se relaja (el jugador se quedó en silencio).")]
        [SerializeField] private float _silenceForgetTime = 45f;
        [Tooltip("Probabilidad de desviar la patrulla hacia el recuerdo más fuerte.")]
        [SerializeField, Range(0f, 1f)] private float _hotspotChance = 0.45f;

        private readonly Vector3[] _positions = new Vector3[Capacity];
        private readonly float[] _weights = new float[Capacity];
        private float _hearingBoost = 1f;
        private float _lastStimulusTime;
        private float _lastDecayTime;

        /// <summary>Multiplicador del alcance auditivo (sube si oye correr a menudo).</summary>
        public float HearingMultiplier => _hearingBoost;

        /// <summary>Número de recuerdos activos (para HUD de depuración y pruebas).</summary>
        public int ActiveMemories
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Capacity; i++) if (_weights[i] > 0.2f) count++;
                return count;
            }
        }

        private void OnEnable()
        {
            _lastStimulusTime = Time.time;
            _lastDecayTime = Time.time;
        }

        /// <summary>Registra un ruido oído (intensidad 0-100).</summary>
        public void RecordNoise(Vector3 position, float intensity)
        {
            _lastStimulusTime = Time.time;
            if (intensity >= 90f) Remember(position, _gunshotWeight);
            else if (intensity >= 75f)
            {
                Remember(position, _sprintWeight);
                _hearingBoost = Mathf.Min(1.4f, _hearingBoost + 0.05f);
            }
        }

        /// <summary>Registra que vio al jugador en esta posición.</summary>
        public void RecordSighting(Vector3 position)
        {
            _lastStimulusTime = Time.time;
            Remember(position, _sightingWeight * 0.15f); // se llama por tick mientras lo ve: suma poco a poco
        }

        /// <summary>Registra el último lugar donde lo vio antes de perderlo (posible escondite).</summary>
        public void RecordLostPlayer(Vector3 position)
        {
            _lastStimulusTime = Time.time;
            Remember(position, _lostPlayerWeight);
        }

        /// <summary>Desvanece los recuerdos con el tiempo. Lo llama SmartInfectedBrain en su tick.</summary>
        public void Tick(float now)
        {
            float dt = now - _lastDecayTime;
            _lastDecayTime = now;
            if (dt <= 0f) return;

            bool silent = now - _lastStimulusTime > _silenceForgetTime;
            float decay = _decayPerSecond * dt * (silent ? 4f : 1f);
            for (int i = 0; i < Capacity; i++) _weights[i] = Mathf.Max(0f, _weights[i] - decay);
            if (silent) _hearingBoost = Mathf.MoveTowards(_hearingBoost, 1f, 0.02f * dt);
        }

        /// <summary>
        /// Devuelve, con cierta probabilidad, la zona recordada con más peso para que la patrulla la revise.
        /// </summary>
        public bool TryGetHotspot(out Vector3 position)
        {
            position = Vector3.zero;
            int best = -1;
            float bestWeight = 1f; // umbral mínimo para que valga la pena desviarse
            for (int i = 0; i < Capacity; i++)
            {
                if (_weights[i] > bestWeight)
                {
                    bestWeight = _weights[i];
                    best = i;
                }
            }

            if (best < 0 || Random.value > _hotspotChance) return false;

            position = _positions[best];
            // Revisar una zona la "gasta": no vuelve en bucle al mismo sitio
            _weights[best] *= 0.5f;
            return true;
        }

        private void Remember(Vector3 position, float weight)
        {
            int weakest = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (_weights[i] > 0f && (_positions[i] - position).sqrMagnitude <= MergeDistance * MergeDistance)
                {
                    // Refuerza el recuerdo existente y lo desplaza hacia el estímulo nuevo
                    _positions[i] = Vector3.Lerp(_positions[i], position, 0.3f);
                    _weights[i] = Mathf.Min(10f, _weights[i] + weight);
                    return;
                }
                if (_weights[i] < _weights[weakest]) weakest = i;
            }

            _positions[weakest] = position;
            _weights[weakest] = weight;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_weights[i] <= 0.05f) continue;
                Gizmos.color = new Color(1f, 0.3f, 0.9f, Mathf.Clamp01(_weights[i] / 5f));
                Gizmos.DrawSphere(_positions[i] + Vector3.up * 0.2f, 0.25f + _weights[i] * 0.05f);
            }
        }
#endif
    }
}
