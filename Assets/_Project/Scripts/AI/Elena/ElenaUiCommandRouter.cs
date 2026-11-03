using System;
using Nexo.Core;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Router seguro para órdenes emitidas por Elena hacia la UI y el mundo.
    /// Valida que las puertas existan en la lista blanca de puertas disponibles del piso
    /// y que las solicitudes de radar correspondan a la presencia real de infectados.
    /// </summary>
    public static class ElenaUiCommandRouter
    {
        public static void Dispatch(ElenaResponse response, ElenaRequestContext context)
        {
            if (response == null) return;

            ElenaUiAction action = response.ParsedAction;
            if (action == ElenaUiAction.None) return;

            string target = response.target_id ?? string.Empty;

            switch (action)
            {
                // Validación estricta: UnlockDoor solo puede ejecutarse si el target_id está en available_doors
                case ElenaUiAction.UnlockDoor:
                    if (!TryResolveDoor(target, context, out target))
                    {
                        Debug.LogWarning($"[ElenaUiCommandRouter] Orden de abrir puerta rechazada (no está en available_doors): '{target}'");
                        return;
                    }
                    break;

                // Validación estricta: Radar solo si hay infectados cercanos
                case ElenaUiAction.PingEnemyRadar:
                    if (context == null || context.infected_nearby <= 0)
                    {
                        Debug.LogWarning("[ElenaUiCommandRouter] Radar omitido: no hay infectados en el radio de detección.");
                        return;
                    }
                    break;

                // Sin destino explícito, las luces se alternan en el cuarto actual del jugador
                case ElenaUiAction.ToggleHallwayLights:
                    if (string.IsNullOrEmpty(target) && context != null) target = context.room_id ?? string.Empty;
                    break;
            }

            GameEvents.RaiseUiAction(action, target);
        }

        /// <summary>
        /// Busca el DoorId en la lista blanca sin distinguir mayúsculas y devuelve su forma canónica.
        /// </summary>
        private static bool TryResolveDoor(string doorId, ElenaRequestContext context, out string resolved)
        {
            resolved = doorId;
            if (string.IsNullOrEmpty(doorId) || context?.available_doors == null) return false;

            foreach (string available in context.available_doors)
            {
                if (string.Equals(available, doorId, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = available;
                    return true;
                }
            }
            return false;
        }
    }
}
