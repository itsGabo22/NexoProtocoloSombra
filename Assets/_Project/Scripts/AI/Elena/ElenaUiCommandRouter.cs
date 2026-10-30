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

            string target = response.ui_parameter ?? string.Empty;

            // Validación estricta: UnlockDoor solo puede ejecutarse si el ID está en available_doors
            if (action == ElenaUiAction.UnlockDoor)
            {
                if (context.available_doors == null || Array.IndexOf(context.available_doors, target) < 0)
                {
                    Debug.LogWarning($"[ElenaUiCommandRouter] Orden de abrir puerta rechazada (no está en available_doors): '{target}'");
                    return;
                }
            }

            // Validación estricta: Radar solo si hay infectados cercanos
            if (action == ElenaUiAction.PingEnemyRadar && context.infected_nearby <= 0)
            {
                Debug.LogWarning("[ElenaUiCommandRouter] Radar omitido: no hay infectados en el radio de detección.");
                return;
            }

            GameEvents.RaiseUiAction(action, target);
        }
    }
}
