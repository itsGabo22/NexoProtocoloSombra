using Nexo.Core;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Conecta al jugador con GameSession: al cargar un piso guarda un checkpoint con su munición, vida y equipo,
    /// y si el piso se recarga tras una captura restaura ese checkpoint. Así, al pasar al Piso 6 el jugador conserva
    /// exactamente lo que tenía al terminar el Piso 12, y morir no permite acumular la munición recogida.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatePersistence : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private PlayerHealth _health;

        private void Awake()
        {
            if (_weapon == null) _weapon = GetComponent<PlayerWeapon>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
        }

        private void OnDisable()
        {
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
        }

        private void Start()
        {
            // Systems_Persistent se recargó (p. ej. tras el giro final): se recupera el último estado guardado
            if (GameSession.HasSnapshot) Restore();
        }

        private void HandlePlayerCaught() => GameSession.RestorePending = true;

        private void HandleFloorChanged(int floor)
        {
            if (GameSession.NewGamePending)
            {
                // JUGAR desde el menú: se descarta lo de la partida anterior
                GameSession.NewGamePending = false;
                GameSession.RestorePending = false;
                if (_weapon != null) { _weapon.ResetToDefaults(); _weapon.enabled = true; }
                if (_health != null) _health.ResetFull();
                Save();
                return;
            }

            if (GameSession.RestorePending && GameSession.HasSnapshot)
            {
                GameSession.RestorePending = false;
                Restore();
                return;
            }

            GameSession.RestorePending = false;
            Save();
        }

        private void Save()
        {
            GameSession.Save(
                _weapon != null ? _weapon.Magazine : 0,
                _weapon != null ? _weapon.Reserve : 0,
                _weapon != null && _weapon.enabled,
                _health != null ? _health.Points : 1,
                _health != null ? _health.Hits : 0);
        }

        private void Restore()
        {
            if (_weapon != null)
            {
                _weapon.SetAmmo(GameSession.Magazine, GameSession.Reserve);
                _weapon.enabled = GameSession.HasWeapon;
            }
            if (_health != null) _health.SetState(GameSession.HealthPoints, GameSession.HitCount);
        }
    }
}
