using System;
using Nexo.AI.Elena;
using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Rastrea y consolida el estado global del jugador y del entorno para alimentar el contexto de la IA.
    /// Actualizado exclusivamente por eventos simétricos (cero Update).
    /// </summary>
    public sealed class GameStateTracker : MonoBehaviour
    {
        [Header("Estado Actual de Piso y Habitación")]
        [SerializeField] private int _floor = 12;
        [SerializeField] private string _room = "F12_Office_Entrance";

        [Header("Puertas Disponibles en Piso")]
        [SerializeField] private string[] _availableDoors = new[] { "F12_Door_Stairs", "F12_Door_Security" };

        private float _noise = 0f;
        private float _battery = 1f;
        private int _infectedNearby = 0;
        private int _anomaliesDetected = 0;
        private int _trust = 50;

        private void OnEnable()
        {
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnNoiseChanged += HandleNoiseChanged;
            GameEvents.OnBatteryChanged += HandleBatteryChanged;
            GameEvents.OnAvailableDoorsChanged += SetAvailableDoors;
        }

        private void OnDisable()
        {
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnNoiseChanged -= HandleNoiseChanged;
            GameEvents.OnBatteryChanged -= HandleBatteryChanged;
            GameEvents.OnAvailableDoorsChanged -= SetAvailableDoors;
        }

        private void HandleFloorChanged(int floor)
        {
            _floor = floor;
        }

        private void HandleNoiseChanged(float noise)
        {
            _noise = noise;
        }

        private void HandleBatteryChanged(float battery)
        {
            _battery = battery;
        }

        public void SetRoom(string room) => _room = room;
        public void SetInfectedNearby(int count) => _infectedNearby = Mathf.Max(0, count);
        public void SetAvailableDoors(string[] doors) => _availableDoors = doors ?? Array.Empty<string>();
        public void RegisterAnomaly() => _anomaliesDetected++;
        public void SetTrust(int trust) => _trust = Mathf.Clamp(trust, 0, 100);

        /// <summary>
        /// Captura una instantánea del estado del juego en un DTO para enviar a Gemini o al Fallback.
        /// </summary>
        public ElenaRequestContext CaptureContext(string questionText)
        {
            return new ElenaRequestContext
            {
                floor = _floor,
                player_room = _room,
                player_noise = _noise,
                player_battery = _battery,
                infected_nearby = _infectedNearby,
                anomalies_detected = _anomaliesDetected,
                available_doors = (string[])_availableDoors.Clone(),
                trust_level = _trust,
                question_selected = questionText ?? string.Empty
            };
        }
    }
}
