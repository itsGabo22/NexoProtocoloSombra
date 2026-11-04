using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Registro puro de puntos de reaparición del piso actual (testeable en EditMode):
    /// el inicio del piso, el último punto de control cruzado y las últimas posiciones seguras sobre suelo firme.
    /// Al caer se elige el más reciente de ellos, pero las posiciones seguras deben tener cierta antigüedad para no
    /// devolver al jugador justo al borde del que acaba de caerse.
    /// </summary>
    public sealed class CheckpointLog
    {
        private const int SafeCapacity = 8;

        private struct Point
        {
            public Vector3 Position;
            public float Yaw;
            public float Time;
            public bool Valid;
        }

        private Point _checkpoint;
        private readonly Point[] _safe = new Point[SafeCapacity];
        private int _safeNext;

        public string LastLabel { get; private set; } = string.Empty;

        /// <summary>Nuevo piso: el inicio es el primer punto de control y se olvidan las posiciones seguras.</summary>
        public void Reset(Vector3 position, float yaw, float time)
        {
            for (int i = 0; i < _safe.Length; i++) _safe[i] = default;
            _safeNext = 0;
            SetCheckpoint(position, yaw, time, "inicio");
        }

        public void SetCheckpoint(Vector3 position, float yaw, float time, string label)
        {
            _checkpoint = new Point { Position = position, Yaw = yaw, Time = time, Valid = true };
            LastLabel = label ?? string.Empty;
        }

        public void AddSafeGround(Vector3 position, float yaw, float time)
        {
            _safe[_safeNext] = new Point { Position = position, Yaw = yaw, Time = time, Valid = true };
            _safeNext = (_safeNext + 1) % _safe.Length;
        }

        /// <summary>
        /// Punto de reaparición tras una caída en el instante <paramref name="now"/>: la posición segura más reciente con
        /// al menos <paramref name="minSafeAge"/> segundos, si es posterior al último punto de control; si no, el punto de control.
        /// </summary>
        public bool TryChoose(float now, float minSafeAge, out Vector3 position, out float yaw)
        {
            Point best = _checkpoint;
            foreach (Point p in _safe)
            {
                if (!p.Valid || now - p.Time < minSafeAge) continue;
                if (!best.Valid || p.Time > best.Time) best = p;
            }
            position = best.Position;
            yaw = best.Yaw;
            return best.Valid;
        }

        /// <summary>Por debajo de esta altura el jugador ha caído del piso.</summary>
        public static bool IsFallen(float playerY, float floorY, float fallDepth) => playerY < floorY - Mathf.Abs(fallDepth);
    }
}
