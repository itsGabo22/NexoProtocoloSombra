using System;
using System.Collections.Generic;

namespace Nexo.UI
{
    /// <summary>
    /// Decide qué hacer con cada notificación del HUD (lógica pura con reloj inyectable, probada en EditMode):
    /// - Una idéntica (mismo remitente, mismo cuerpo ya traducido y mismo tipo real/falso) que sigue visible no crea
    ///   otra tarjeta: se reinicia su tiempo de vida.
    /// - Si caducó pero se mostró hace menos de la ventana de deduplicación, se suprime.
    /// - Como máximo hay MaxVisible tarjetas: al crear una de más se retira la más antigua.
    /// Las notificaciones falsas (mimetismo) nunca se funden con una real aunque el texto coincida.
    /// </summary>
    public sealed class NotificationStack
    {
        /// <summary>Tarjetas visibles a la vez.</summary>
        public const int DefaultMaxVisible = 3;

        /// <summary>Segundos durante los que una notificación idéntica no crea tarjeta nueva.</summary>
        public const double DefaultDedupeWindowSeconds = 8.0;

        private const char Separator = '\u001F';

        private readonly struct VisibleCard
        {
            public readonly int Id;
            public readonly string Key;

            public VisibleCard(int id, string key)
            {
                Id = id;
                Key = key;
            }
        }

        private readonly Func<double> _clock;
        private readonly int _maxVisible;
        private readonly double _dedupeWindow;
        private readonly List<VisibleCard> _visible = new();
        private readonly Dictionary<string, double> _lastShown = new();
        private readonly List<string> _expiredKeys = new();
        private int _nextId;

        public NotificationStack(Func<double> clock, int maxVisible = DefaultMaxVisible, double dedupeWindowSeconds = DefaultDedupeWindowSeconds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (maxVisible < 1) throw new ArgumentOutOfRangeException(nameof(maxVisible), "Debe haber al menos una tarjeta visible.");
            _maxVisible = maxVisible;
            _dedupeWindow = dedupeWindowSeconds < 0 ? 0 : dedupeWindowSeconds;
        }

        /// <summary>Número de tarjetas visibles.</summary>
        public int VisibleCount => _visible.Count;

        /// <summary>Id de la tarjeta visible en la posición indicada (0 = la más antigua).</summary>
        public int VisibleIdAt(int index) => _visible[index].Id;

        /// <summary>Registra una notificación entrante y devuelve qué debe hacer el HUD.</summary>
        public NotificationResult Push(string sender, string body, bool isFake)
        {
            double now = _clock();
            string key = Key(sender, body, isFake);
            PruneHistory(now);

            int visibleIndex = IndexOfKey(key);
            if (visibleIndex >= 0)
            {
                _lastShown[key] = now;
                return new NotificationResult(NotificationAction.Refresh, _visible[visibleIndex].Id, NotificationResult.None);
            }

            // Caducó hace poco: no se vuelve a mostrar (y no se alarga la ventana para que la repetición no la bloquee siempre)
            if (_lastShown.TryGetValue(key, out double shownAt) && now - shownAt < _dedupeWindow)
            {
                return new NotificationResult(NotificationAction.Suppress, NotificationResult.None, NotificationResult.None);
            }

            int id = _nextId++;
            _visible.Add(new VisibleCard(id, key));
            _lastShown[key] = now;

            int evicted = NotificationResult.None;
            if (_visible.Count > _maxVisible)
            {
                evicted = _visible[0].Id;
                _visible.RemoveAt(0);
            }

            return new NotificationResult(NotificationAction.Create, id, evicted);
        }

        /// <summary>La tarjeta terminó su tiempo de vida (o se retiró): deja de contar como visible.</summary>
        public void Expire(int cardId)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Id != cardId) continue;
                _visible.RemoveAt(i);
                return;
            }
        }

        /// <summary>Olvida todas las tarjetas y el historial (p. ej. al deshabilitar el HUD).</summary>
        public void Clear()
        {
            _visible.Clear();
            _lastShown.Clear();
        }

        private int IndexOfKey(string key)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Key == key) return i;
            }
            return -1;
        }

        /// <summary>Quita del historial las entradas fuera de la ventana que ya no están visibles (memoria acotada).</summary>
        private void PruneHistory(double now)
        {
            _expiredKeys.Clear();
            foreach (KeyValuePair<string, double> pair in _lastShown)
            {
                if (now - pair.Value >= _dedupeWindow && IndexOfKey(pair.Key) < 0) _expiredKeys.Add(pair.Key);
            }
            foreach (string key in _expiredKeys) _lastShown.Remove(key);
        }

        private static string Key(string sender, string body, bool isFake)
        {
            return (isFake ? "1" : "0") + Separator + (sender ?? string.Empty) + Separator + (body ?? string.Empty);
        }
    }
}
