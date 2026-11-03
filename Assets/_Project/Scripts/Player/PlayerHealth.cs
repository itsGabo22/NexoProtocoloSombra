using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Vida de Alex por puntos. Cada ataque de un infectado suma un golpe; cada _hitsPerPoint golpes se pierde
    /// un punto de vida y al llegar a 0 el jugador es capturado (PlayerCaptureHandler reinicia el piso).
    /// Si el jugador está ileso, el primer ataque inicia un forcejeo (StruggleQTE) en lugar de dañarle.
    /// Publica el estado en el panel de vitales del HUD (GameEvents.OnVitalsChanged).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maxPoints = 3;
        [SerializeField, Min(1)] private int _hitsPerPoint = 4;
        [SerializeField] private StruggleQTE _struggle;

        private int _points;
        private int _hits;
        private bool _isDown;

        public int Points => _points;
        public int Hits => _hits;
        public int MaxPoints => _maxPoints;
        public bool IsUnharmed => _points == _maxPoints && _hits == 0;

        private void Awake()
        {
            _points = _maxPoints;
            if (_struggle == null) _struggle = GetComponent<StruggleQTE>();
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerAttacked += HandlePlayerAttacked;
            GameEvents.OnFloorChanged += HandleFloorChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerAttacked -= HandlePlayerAttacked;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
        }

        private void Start() => PublishVitals();

        private void HandleFloorChanged(int floor)
        {
            _isDown = false;
            PublishVitals();
        }

        private void HandlePlayerAttacked(Transform attacker)
        {
            if (_isDown || (_struggle != null && _struggle.IsActive)) return;

            SmartInfectedBrain brain = attacker != null ? attacker.GetComponent<SmartInfectedBrain>() : null;
            if (IsUnharmed && _struggle != null && brain != null)
            {
                _struggle.Begin(brain);
                return;
            }

            RegisterHit();
        }

        /// <summary>Suma un golpe; cada _hitsPerPoint golpes resta un punto de vida.</summary>
        public void RegisterHit()
        {
            if (_isDown) return;

            _hits++;
            if (_hits >= _hitsPerPoint)
            {
                _hits = 0;
                _points--;
                GameEvents.RaiseSubtitle(_points > 0 ? "(Las heridas te queman. Pierdes fuerzas)" : "(Caes al suelo)");
            }
            else
            {
                GameEvents.RaiseSubtitle($"(Te golpean: {_hits}/{_hitsPerPoint})");
            }

            PublishVitals();
            if (_points <= 0)
            {
                _isDown = true;
                GameEvents.RaisePlayerCaught();
            }
        }

        /// <summary>Pierde de golpe el punto de vida actual (forcejeo fallido).</summary>
        public void LosePoint()
        {
            int remaining = _hitsPerPoint - _hits;
            for (int i = 0; i < remaining && !_isDown; i++) RegisterHit();
        }

        public void SetState(int points, int hits)
        {
            _points = Mathf.Clamp(points, 1, _maxPoints);
            _hits = Mathf.Clamp(hits, 0, _hitsPerPoint - 1);
            _isDown = false;
            PublishVitals();
        }

        private void PublishVitals()
        {
            float health = Mathf.Clamp01((_points - (float)_hits / _hitsPerPoint) / _maxPoints);
            int percent = Mathf.RoundToInt(health * 100f);
            string status = _points == _maxPoints && _hits == 0 ? "ILESO" : _points >= 2 ? "HERIDO" : "CRÍTICO";
            GameEvents.RaiseVitalsChanged(new VitalsData($"{percent}% {status}", $"VIDA {_points}/{_maxPoints} · GOLPES {_hits}/{_hitsPerPoint}", 72 + (_maxPoints - _points) * 28 + _hits * 6));
        }
    }
}
