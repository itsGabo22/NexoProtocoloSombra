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
        public static event Action<int> OnInfectedNearbyChanged;
        public static event Action<int> OnAmmoPicked;
        public static event Action<string> OnPlayerSpoke;
        /// <summary>Estado de la entrada de voz [V] para la HUD (ESCUCHANDO..., PROCESANDO..., errores). Vacío = ocultar.</summary>
        public static event Action<string> OnVoiceStatus;
        public static event Action<Transform> OnPlayerAttacked;
        /// <summary>Munición del arma tras cualquier cambio: cargador, reserva y tamaño del cargador (panel de arma del HUD).</summary>
        public static event Action<int, int, int> OnAmmoChanged;
        /// <summary>La recarga del arma empieza (true) o termina (false).</summary>
        public static event Action<bool> OnWeaponReloadChanged;
        /// <summary>Empieza una cinemática (id del clip): el HUD se oculta y las UI modales (terminal, red de cámaras, forcejeo) se cierran.</summary>
        public static event Action<string> OnCinematicStarted;
        /// <summary>Termina una cinemática (fin, salto o error): todo vuelve al estado previo.</summary>
        public static event Action<string> OnCinematicFinished;
        /// <summary>El jugador cruza un punto de control (posición de reaparición, orientación y etiqueta).</summary>
        public static event Action<Vector3, float, string> OnCheckpointReached;
        /// <summary>Fundido a negro del HUD: opacidad destino (0-1) y duración en segundos (tiempo real).</summary>
        public static event Action<float, float> OnScreenFade;
        /// <summary>El jugador recupera vida (puntos sumados).</summary>
        public static event Action<int> OnPlayerHealed;
        /// <summary>Un ataque del jugador alcanza a un infectado (true si lo mata): marca de impacto del HUD.</summary>
        public static event Action<bool> OnHitConfirmed;
        /// <summary>El jugador consigue (true) o pierde (false) el cuchillo: panel del arma del HUD.</summary>
        public static event Action<bool> OnKnifeChanged;

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
        public static void RaiseInfectedNearbyChanged(int count) => OnInfectedNearbyChanged?.Invoke(count);
        public static void RaiseAmmoPicked(int rounds) => OnAmmoPicked?.Invoke(rounds);
        public static void RaisePlayerSpoke(string text) => OnPlayerSpoke?.Invoke(text);
        public static void RaiseVoiceStatus(string text) => OnVoiceStatus?.Invoke(text);
        public static void RaisePlayerAttacked(Transform attacker) => OnPlayerAttacked?.Invoke(attacker);
        public static void RaiseAmmoChanged(int magazine, int reserve, int magazineSize) => OnAmmoChanged?.Invoke(magazine, reserve, magazineSize);
        public static void RaiseWeaponReloadChanged(bool isReloading) => OnWeaponReloadChanged?.Invoke(isReloading);
        public static void RaiseCinematicStarted(string id) => OnCinematicStarted?.Invoke(id);
        public static void RaiseCinematicFinished(string id) => OnCinematicFinished?.Invoke(id);
        public static void RaiseCheckpointReached(Vector3 position, float yaw, string label) => OnCheckpointReached?.Invoke(position, yaw, label);
        public static void RaiseScreenFade(float alpha, float seconds) => OnScreenFade?.Invoke(alpha, seconds);
        public static void RaisePlayerHealed(int points) => OnPlayerHealed?.Invoke(points);
        public static void RaiseHitConfirmed(bool killed) => OnHitConfirmed?.Invoke(killed);
        public static void RaiseKnifeChanged(bool hasKnife) => OnKnifeChanged?.Invoke(hasKnife);

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
            OnInfectedNearbyChanged = null;
            OnAmmoPicked = null;
            OnPlayerSpoke = null;
            OnVoiceStatus = null;
            OnPlayerAttacked = null;
            OnAmmoChanged = null;
            OnWeaponReloadChanged = null;
            OnCinematicStarted = null;
            OnCinematicFinished = null;
            OnCheckpointReached = null;
            OnScreenFade = null;
            OnPlayerHealed = null;
            OnHitConfirmed = null;
            OnKnifeChanged = null;
        }
    }
}
