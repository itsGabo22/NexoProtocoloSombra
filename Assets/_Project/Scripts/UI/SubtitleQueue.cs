using System.Collections.Generic;

namespace Nexo.UI
{
    /// <summary>
    /// Cola de subtítulos (lógica pura, testeada). Las líneas nuevas esperan su turno: la actual dura su tiempo
    /// completo si no hay nadie esperando, y su tiempo mínimo si hay cola. Una línea de prioridad mayor
    /// (avisos de daño o agarre) entra al instante. Las repeticiones exactas se ignoran y la cola no crece sin límite.
    /// </summary>
    public sealed class SubtitleQueue
    {
        public const int Capacity = 5;

        public struct Entry
        {
            public string Text;
            public float Duration;
            public float Minimum;
            public int Priority;
        }

        private readonly List<Entry> _pending = new List<Entry>();
        private Entry _current;
        private bool _hasCurrent;
        private float _currentStart;

        public bool HasCurrent => _hasCurrent;
        public Entry Current => _current;
        public int PendingCount => _pending.Count;

        /// <summary>Añade una línea. Devuelve true si la línea visible cambia ahora.</summary>
        public bool Push(Entry entry, float now)
        {
            if (string.IsNullOrEmpty(entry.Text)) return false;
            if (_hasCurrent && _current.Text == entry.Text) return false;
            foreach (Entry p in _pending) if (p.Text == entry.Text) return false;

            if (!_hasCurrent || entry.Priority > _current.Priority)
            {
                Show(entry, now);
                return true;
            }

            if (_pending.Count >= Capacity)
            {
                // Se descarta la más antigua de menor prioridad
                int drop = 0;
                for (int i = 1; i < _pending.Count; i++) if (_pending[i].Priority < _pending[drop].Priority) drop = i;
                _pending.RemoveAt(drop);
            }
            _pending.Add(entry);
            return false;
        }

        /// <summary>Avanza la cola. Devuelve true si la línea visible cambió (o se ocultó).</summary>
        public bool Tick(float now)
        {
            if (!_hasCurrent) return false;
            if (now < Deadline()) return false;

            if (_pending.Count > 0)
            {
                Entry next = _pending[0];
                _pending.RemoveAt(0);
                Show(next, now);
            }
            else
            {
                _hasCurrent = false;
            }
            return true;
        }

        /// <summary>Instante en que la línea actual debe ceder (fin completo o, con cola, su mínimo).</summary>
        public float Deadline()
        {
            if (!_hasCurrent) return float.PositiveInfinity;
            float hold = _pending.Count > 0 ? System.Math.Min(_current.Minimum, _current.Duration) : _current.Duration;
            return _currentStart + hold;
        }

        /// <summary>Vacía la línea actual y la cola.</summary>
        public void Clear()
        {
            _pending.Clear();
            _hasCurrent = false;
        }

        private void Show(Entry entry, float now)
        {
            _current = entry;
            _hasCurrent = true;
            _currentStart = now;
        }
    }
}
