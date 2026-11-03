using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Receptor de los Animation Events de los clips de locomoción del infectado (Walk_N, Run_N de Starter Assets).
    /// Debe ir en el mismo GameObject que el Animator (ZombieModel): Unity solo entrega los eventos ahí.
    /// Reenvía cada paso a la fuente de audio del infectado. En el Blend Tree "Locomotion" los clips de andar y
    /// correr suenan a la vez con pesos distintos, así que solo se acepta el evento del clip dominante (peso >= 0.5)
    /// y se ignoran los que llegan antes de _minStepInterval para no duplicar pisadas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class InfectedAnimationEvents : MonoBehaviour
    {
        [Header("Audio de pasos")]
        [Tooltip("Fuente 3D propia de los pasos (no la de la voz: el pitch aleatorio alteraría la voz imitada).")]
        [SerializeField] private AudioSource _footstepSource;
        [SerializeField] private AudioClip[] _footstepClips;
        [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _runVolume = 0.8f;
        [SerializeField] private Vector2 _pitchRange = new Vector2(0.88f, 1.05f);

        [Header("Filtro de duplicados")]
        [SerializeField] private float _minStepInterval = 0.18f;
        [Tooltip("Velocidad (parámetro Speed) a partir de la cual el paso suena como carrera.")]
        [SerializeField] private float _runSpeedThreshold = 3f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private Animator _animator;
        private float _lastStepTime = float.NegativeInfinity;
        private int _lastClipIndex = -1;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_footstepSource == null) _footstepSource = GetComponent<AudioSource>();
        }

        /// <summary>Evento de los clips Walk_N / Run_N (Starter Assets). El nombre debe coincidir exactamente.</summary>
        public void OnFootstep(AnimationEvent animationEvent)
        {
            // Solo el clip dominante del Blend Tree emite la pisada
            if (animationEvent != null && animationEvent.animatorClipInfo.weight < 0.5f) return;
            if (Time.time - _lastStepTime < _minStepInterval) return;
            _lastStepTime = Time.time;

            if (_footstepSource == null || _footstepClips == null || _footstepClips.Length == 0) return;

            float speed = _animator != null ? _animator.GetFloat(SpeedHash) : 0f;
            if (speed < 0.1f) return;

            float volume = speed >= _runSpeedThreshold ? _runVolume : _walkVolume;
            _footstepSource.pitch = Random.Range(_pitchRange.x, _pitchRange.y);
            _footstepSource.PlayOneShot(PickClip(), volume);
        }

        /// <summary>Evento de aterrizaje de los clips *_Land de Starter Assets (no se usan, pero se reciben sin error).</summary>
        public void OnLand(AnimationEvent animationEvent)
        {
        }

        private AudioClip PickClip()
        {
            if (_footstepClips.Length == 1) return _footstepClips[0];

            // Evita repetir la misma muestra dos veces seguidas
            int index = Random.Range(0, _footstepClips.Length - 1);
            if (index >= _lastClipIndex) index++;
            _lastClipIndex = index;
            return _footstepClips[index];
        }
    }
}
