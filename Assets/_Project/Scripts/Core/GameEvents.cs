using System;
using Nexo.AI.Elena;
using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Datos biométricos del jugador para actualización del HUD.
    /// </summary>
    [Serializable]
    public struct VitalsData
    {
        public string ValueText;
        public string StatusText;
        public int HeartRate;

        public VitalsData(string valueText, string statusText, int heartRate)
        {
            ValueText = valueText;
            StatusText = statusText;
            HeartRate = heartRate;
        }
    }

    /// <summary>
    /// Notificación diegética de NexoOS para el HUD.
    /// </summary>
    [Serializable]
    public struct HudNotification
    {
        public string Sender;
        public string Body;
        public bool IsFake;

        public HudNotification(string sender, string body, bool isFake)
        {
            Sender = sender;
            Body = body;
            IsFake = isFake;
        }
    }

    /// <summary>
    /// Bus de eventos global desacoplado para comunicación entre sistemas (HUD, IA, Jugador, Entorno).
    /// Evita sondeos en Update() y centraliza las invocaciones con limpieza en recarga de dominio.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<float> OnNoiseChanged;
        public static event Action<float> OnBatteryChanged;
        public static event Action<VitalsData> OnVitalsChanged;
        public static event Action<HudNotification> OnNotification;
        public static event Action<ElenaResponse> OnElenaResponse;
        public static event Action<bool> OnElenaThinking;
        public static event Action<string[]> OnQuickQuestionsChanged;
        public static event Action<int> OnQuickQuestionSelected;
        public static event Action<ElenaUiAction, string> OnUiAction;
        public static event Action<int> OnFloorChanged;
        public static event Action<Vector3, float> OnNoiseEmitted;
        public static event Action<string, bool> OnLightsChanged;
        public static event Action<string> OnSubtitle;
        public static event Action OnHudCorruptionRequested;
        public static event Action OnHudCorruptionFinished;
        public static event Action OnPlotTwistTriggered;
        public static event Action<string[]> OnAvailableDoorsChanged;
        public static event Action<Transform> OnPlayerSpawned;
        public static event Action OnPlayerCaught;

        public static void RaiseNoiseChanged(float noise) => OnNoiseChanged?.Invoke(noise);
        public static void RaiseBatteryChanged(float battery) => OnBatteryChanged?.Invoke(battery);
        public static void RaiseVitalsChanged(VitalsData vitals) => OnVitalsChanged?.Invoke(vitals);
        public static void RaiseNotification(HudNotification notification) => OnNotification?.Invoke(notification);
        public static void RaiseElenaResponse(ElenaResponse response) => OnElenaResponse?.Invoke(response);
        public static void RaiseElenaThinking(bool isThinking) => OnElenaThinking?.Invoke(isThinking);
        public static void RaiseQuickQuestionsChanged(string[] questions) => OnQuickQuestionsChanged?.Invoke(questions);
        public static void RaiseQuickQuestionSelected(int index) => OnQuickQuestionSelected?.Invoke(index);
        public static void RaiseUiAction(ElenaUiAction action, string parameter) => OnUiAction?.Invoke(action, parameter);
        public static void RaiseFloorChanged(int floor) => OnFloorChanged?.Invoke(floor);
        public static void RaiseNoiseEmitted(Vector3 position, float loudness) => OnNoiseEmitted?.Invoke(position, loudness);
        public static void RaiseLightsChanged(string roomId, bool isOn) => OnLightsChanged?.Invoke(roomId, isOn);
        public static void RaiseSubtitle(string text) => OnSubtitle?.Invoke(text);
        public static void RaiseHudCorruptionRequested() => OnHudCorruptionRequested?.Invoke();
        public static void RaiseHudCorruptionFinished() => OnHudCorruptionFinished?.Invoke();
        public static void RaisePlotTwistTriggered() => OnPlotTwistTriggered?.Invoke();
        public static void RaiseAvailableDoorsChanged(string[] doorIds) => OnAvailableDoorsChanged?.Invoke(doorIds);
        public static void RaisePlayerSpawned(Transform player) => OnPlayerSpawned?.Invoke(player);
        public static void RaisePlayerCaught() => OnPlayerCaught?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            OnNoiseChanged = null;
            OnBatteryChanged = null;
            OnVitalsChanged = null;
            OnNotification = null;
            OnElenaResponse = null;
            OnElenaThinking = null;
            OnQuickQuestionsChanged = null;
            OnQuickQuestionSelected = null;
            OnUiAction = null;
            OnFloorChanged = null;
            OnNoiseEmitted = null;
            OnLightsChanged = null;
            OnSubtitle = null;
            OnHudCorruptionRequested = null;
            OnHudCorruptionFinished = null;
            OnPlotTwistTriggered = null;
            OnAvailableDoorsChanged = null;
            OnPlayerSpawned = null;
            OnPlayerCaught = null;
        }
    }
}
