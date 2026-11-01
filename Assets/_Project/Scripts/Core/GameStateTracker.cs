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
        [SerializeField] private string _room = "F12_Cubicles";

        [Header("Puertas Disponibles en Piso")]
        [SerializeField] private string[] _availableDoors = new[] { "F12_Door_Stairs" };

        private float _noise = 0f;
        private float _battery = 1f;
        private int _infectedNearby = 0;
        private int _anomaliesSeen = 0;
        private int _trust = 50;
        private bool _lightsOn = true;

        public string CurrentRoom => _room;

        private void OnEnable()
        {
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnNoiseChanged += HandleNoiseChanged;
            GameEvents.OnBatteryChanged += HandleBatteryChanged;
            GameEvents.OnAvailableDoorsChanged += SetAvailableDoors;
            GameEvents.OnLightsChanged += HandleLightsChanged;
            GameEvents.OnElenaResponse += HandleElenaResponse;
            GameEvents.OnNotification += HandleNotification;
            GameEvents.OnInfectedNearbyChanged += SetInfectedNearby;
        }

        private void OnDisable()
        {
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnNoiseChanged -= HandleNoiseChanged;
            GameEvents.OnBatteryChanged -= HandleBatteryChanged;
            GameEvents.OnAvailableDoorsChanged -= SetAvailableDoors;
            GameEvents.OnLightsChanged -= HandleLightsChanged;
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            GameEvents.OnNotification -= HandleNotification;
            GameEvents.OnInfectedNearbyChanged -= SetInfectedNearby;
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

        private void HandleLightsChanged(string roomId, bool isOn)
        {
            if (string.Equals(roomId, _room, StringComparison.OrdinalIgnoreCase)) _lightsOn = isOn;
        }

        private void HandleElenaResponse(ElenaResponse response)
        {
            if (response != null) SetTrust(response.trust_level);
        }

        private void HandleNotification(HudNotification notification)
        {
            // Cada mensaje falso (mimetismo) cuenta como anomalía vista por el jugador
            if (notification.IsFake) RegisterAnomaly();
        }

        public void SetRoom(string room) => _room = room;
        public void SetInfectedNearby(int count) => _infectedNearby = Mathf.Max(0, count);
        public void SetAvailableDoors(string[] doors) => _availableDoors = doors ?? Array.Empty<string>();
        public void RegisterAnomaly() => _anomaliesSeen++;
        public void SetTrust(int trust) => _trust = Mathf.Clamp(trust, 0, 100);

        /// <summary>
        /// Captura una instantánea del estado del juego en un DTO para enviar a Gemini o al Fallback.
        /// </summary>
        public ElenaRequestContext CaptureContext(string intentKey, string playerText)
        {
            return new ElenaRequestContext
            {
                floor = _floor,
                room_id = _room,
                infected_nearby = _infectedNearby,
                lights_on = _lightsOn,
                noise_level = _noise,
                battery = _battery,
                anomalies_seen = _anomaliesSeen,
                trust_level = _trust,
                player_intent = string.IsNullOrEmpty(intentKey) ? "GENERIC" : intentKey,
                player_text = playerText ?? string.Empty,
                available_doors = (string[])_availableDoors.Clone()
            };
        }
    }
}
