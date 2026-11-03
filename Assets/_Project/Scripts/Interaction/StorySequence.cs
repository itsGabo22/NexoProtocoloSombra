using System;
using System.Collections;
using Nexo.AI.Elena;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Secuencia guionizada del modo historia (una sola vez por carga del piso). Cada línea la dice un emisor
    /// (Elena por su canal de radio, NEXO OS, una grabación, la voz falsa o un subtítulo) y puede disparar
    /// un efecto de escena: apagón, alarma, abrir/bloquear puertas o una animación de Elena.
    /// Se inicia al cargar el piso, al entrar el jugador en su volumen, al desbloquearse una puerta o
    /// manualmente (StoryInteractable, LoreNote). Puede exigir que otra secuencia haya terminado antes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StorySequence : MonoBehaviour
    {
        public enum TriggerMode { OnFloorStart, OnPlayerEnter, OnDoorUnlocked, Manual }
        public enum Speaker { Elena, NexoOS, Recording, FakeVoice, Subtitle, Objective, SystemError }
        public enum SceneAction { None, UnlockDoor, LockDoor, Blackout, AlarmOn, PlayClip, ElenaAnimTrigger, ElenaFollow }

        [Serializable]
        public sealed class Line
        {
            public Speaker Speaker = Speaker.Elena;
            [TextArea(1, 4)] public string Text = "";
            [Tooltip("Pausa antes de esta línea (segundos).")]
            public float Delay = 2f;
            [Tooltip("Solo Elena: CALM, HAPPY, SCARED, URGENT, SAD, WHISPER.")]
            public string Emotion = "calm";
            [Range(0f, 1f)] public float Glitch = 0f;
            public SceneAction Action = SceneAction.None;
            [Tooltip("DoorId para UnlockDoor / nombre del trigger para ElenaAnimTrigger.")]
            public string ActionParam = "";
        }

        [SerializeField] private TriggerMode _mode = TriggerMode.OnPlayerEnter;
        [SerializeField] private string _doorId = "";
        [SerializeField] private StorySequence _requires;
        [SerializeField] private Line[] _lines = Array.Empty<Line>();

        [Header("Efectos de escena")]
        [SerializeField] private DoorController[] _doorsToLock;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _clip;
        [SerializeField] private GameObject[] _alarmObjects;
        [SerializeField] private Animator _elenaAnimator;
        [SerializeField] private ElenaCompanion _elenaCompanion;
        [SerializeField] private Transform _elenaExitPoint;

        private bool _started;

        public bool HasStarted => _started;
        public bool HasFinished { get; private set; }

        private void OnEnable()
        {
            if (_mode == TriggerMode.OnDoorUnlocked) GameEvents.OnUiAction += HandleUiAction;
        }

        private void OnDisable()
        {
            if (_mode == TriggerMode.OnDoorUnlocked) GameEvents.OnUiAction -= HandleUiAction;
        }

        private void Start()
        {
            if (_mode == TriggerMode.OnFloorStart) Play();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_mode == TriggerMode.OnPlayerEnter && other.gameObject.layer == LayerMask.NameToLayer("Player")) Play();
        }

        private void HandleUiAction(ElenaUiAction action, string targetId)
        {
            if (action == ElenaUiAction.UnlockDoor && string.Equals(targetId, _doorId, StringComparison.OrdinalIgnoreCase)) Play();
        }

        /// <summary>Inicia la secuencia si no se ha reproducido y se cumple su requisito.</summary>
        public void Play()
        {
            if (_started || !isActiveAndEnabled) return;
            if (_requires != null && !_requires.HasFinished) return;

            _started = true;
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            foreach (Line line in _lines)
            {
                if (line.Delay > 0f) yield return new WaitForSeconds(line.Delay);
                Say(line);
                RunAction(line);
            }
            HasFinished = true;
        }

        private static void Say(Line line)
        {
            if (string.IsNullOrEmpty(line.Text)) return;

            switch (line.Speaker)
            {
                case Speaker.Elena:
                    GameEvents.RaiseElenaResponse(new ElenaResponse
                    {
                        dialogue = line.Text,
                        emotion = line.Emotion,
                        glitch_intensity = line.Glitch,
                        trust_level = 50
                    });
                    GameEvents.RaiseSubtitle("ELENA: " + line.Text);
                    break;
                case Speaker.NexoOS:
                    GameEvents.RaiseNotification(new HudNotification("NEXO OS", line.Text, false));
                    break;
                case Speaker.Recording:
                    GameEvents.RaiseSubtitle("GRABACIÓN: " + line.Text);
                    break;
                case Speaker.FakeVoice:
                    GameEvents.RaiseNotification(new HudNotification("ELENA", line.Text, true));
                    GameEvents.RaiseSubtitle("¿ELENA?: " + line.Text);
                    break;
                case Speaker.Objective:
                    GameEvents.RaiseNotification(new HudNotification("OBJETIVO", line.Text, false));
                    break;
                case Speaker.SystemError:
                    // Alerta corrupta: estilo de notificación anómala (naranja, sin sello BIO-ID)
                    GameEvents.RaiseNotification(new HudNotification("NEXO OS", line.Text, true));
                    GameEvents.RaiseSubtitle(line.Text);
                    break;
                default:
                    GameEvents.RaiseSubtitle(line.Text);
                    break;
            }
        }

        private void RunAction(Line line)
        {
            switch (line.Action)
            {
                case SceneAction.UnlockDoor:
                    GameEvents.RaiseUiAction(ElenaUiAction.UnlockDoor, line.ActionParam);
                    break;
                case SceneAction.LockDoor:
                    if (_doorsToLock != null)
                    {
                        foreach (DoorController door in _doorsToLock) if (door != null) door.Lock();
                    }
                    break;
                case SceneAction.Blackout:
                    for (int i = LightSwitchInteractable.Registry.Count - 1; i >= 0; i--)
                    {
                        LightSwitchInteractable sw = LightSwitchInteractable.Registry[i];
                        if (sw != null && sw.IsOn) sw.SetPower(false);
                    }
                    break;
                case SceneAction.AlarmOn:
                    if (_alarmObjects != null)
                    {
                        foreach (GameObject go in _alarmObjects) if (go != null) go.SetActive(true);
                    }
                    if (_audioSource != null && _clip != null) { _audioSource.clip = _clip; _audioSource.loop = true; _audioSource.Play(); }
                    break;
                case SceneAction.PlayClip:
                    if (_audioSource != null && _clip != null) _audioSource.PlayOneShot(_clip);
                    break;
                case SceneAction.ElenaAnimTrigger:
                    if (_elenaAnimator != null && !string.IsNullOrEmpty(line.ActionParam)) _elenaAnimator.SetTrigger(line.ActionParam);
                    break;
                case SceneAction.ElenaFollow:
                    if (_elenaCompanion != null) _elenaCompanion.StartFollowing(_elenaExitPoint);
                    break;
            }
        }
    }
}
