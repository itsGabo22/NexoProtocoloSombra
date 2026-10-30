using System.Collections.Generic;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Interruptor de luz de habitación interactuable por el jugador y saboteable por la IA infectada.
    /// Registra su presencia de forma estática y actualiza luminarias y emisivos del entorno.
    /// </summary>
    public sealed class LightSwitchInteractable : MonoBehaviour, IInteractable
    {
        public static readonly List<LightSwitchInteractable> Registry = new();

        [Header("Identificación de Habitación")]
        [SerializeField] private string _roomId = "F12_Office_Entrance";

        [Header("Luminarias y Renderers Emisivos Controlados")]
        [SerializeField] private Light[] _lights;
        [SerializeField] private Renderer[] _emissiveRenderers;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public bool IsOn { get; private set; } = true;
        public string RoomId => _roomId;
        public string Prompt => IsOn ? "[E] Apagar iluminación" : "[E] Restaurar energía";

        private void OnEnable()
        {
            if (!Registry.Contains(this))
            {
                Registry.Add(this);
            }
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        public void Interact()
        {
            SetPower(!IsOn);
        }

        public void Sabotage()
        {
            SetPower(false);
        }

        public void SetPower(bool on)
        {
            IsOn = on;

            if (_lights != null)
            {
                foreach (Light lightSource in _lights)
                {
                    if (lightSource != null) lightSource.enabled = on;
                }
            }

            if (_emissiveRenderers != null)
            {
                foreach (Renderer rend in _emissiveRenderers)
                {
                    if (rend != null && rend.material != null)
                    {
                        if (on)
                        {
                            rend.material.EnableKeyword("_EMISSION");
                            rend.material.SetColor(EmissionColorId, Color.white);
                        }
                        else
                        {
                            rend.material.DisableKeyword("_EMISSION");
                            rend.material.SetColor(EmissionColorId, Color.black);
                        }
                    }
                }
            }

            GameEvents.RaiseLightsChanged(_roomId, on);
        }

        public static bool TryFindLitSwitchNear(Vector3 position, float radius, out LightSwitchInteractable result)
        {
            result = null;
            float sqrRadius = radius * radius;
            float bestDistanceSqr = float.MaxValue;

            foreach (LightSwitchInteractable sw in Registry)
            {
                if (sw == null || !sw.IsOn) continue;

                float distSqr = (sw.transform.position - position).sqrMagnitude;
                if (distSqr <= sqrRadius && distSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distSqr;
                    result = sw;
                }
            }

            return result != null;
        }
    }
}
