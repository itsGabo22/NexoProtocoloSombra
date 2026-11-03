# Handoff: pulido de combate, IA, red de cámaras y Piso 6

- **Fecha:** 2026-11-03
- **De:** Carlos (rama `feature/carlos-polish`, creada desde `develop` en `62de04d`)
- **Para:** Gabriel
- **Prioridad:** Alta (presentación 2026-11-04)
- **Archivos protegidos sin tocar:** `Scripts/Core/GameEvents.cs`, `Scripts/Player/PlayerWeapon.cs` (los modifica `feature/hud-polish`). Tampoco se tocaron `TerminalController.cs`, `SecurityTerminal.uxml`, `HUDController.cs` ni las escenas `Floor12_Offices` / `Floor01_Lobby` / `Systems_Persistent`.

## Qué cambia

| Área | Cambio | Archivos |
| --- | --- | --- |
| Bug arma | El cuerpo Mixamo solo proyecta sombra (ya no se ve detrás del arma). El viewmodel pasa en tiempo de ejecución a un pivote hijo de la cámara: el retroceso de cámara mueve arma y mira a la vez y el rayo del disparo sigue saliendo del centro. Retroceso con recuperación, balanceo al caminar (menos precisión en movimiento), inercia, sacudida al recibir golpes y temblor de forcejeo. | `Player/FirstPersonFeel.cs`, `PF_Player.prefab` |
| Bug columnas | Guardia de navegación: detecta agente parado o ruta parcial (p. ej. jugador pegado a una columna) → recalcula → punto alcanzable alrededor → rodeo lateral → cambia de estado. | `AI/Infected/InfectedNavigationGuard.cs` |
| Bug `OnFootstep` | Receptor en `ZombieModel` con fuente de audio propia; solo suena el clip dominante del Blend Tree. Se comprobó en Play: 0 errores (antes 59). | `AI/Infected/InfectedAnimationEvents.cs`, `PF_Infected_Variant.prefab` |
| IA de sigilo | Estados Patrol, Suspicious, Investigate, Search, Chase, Attack (con anticipación) y Return; perfiles Patroller / Sentinel / Wanderer / Listener; vista gradual opcional; oído atenuado por paredes; ajustes por punto de aparición. En el Piso 12 la vista sigue siendo instantánea (sin cambios de dificultad). | `SmartInfectedBrain.cs`, `States/*`, `InfectedSpawnPoint.cs`, `InfectedSpawnOverrides.cs`, `InfectedBehavior.cs` |
| Infectado que aprende | Memoria de 6 zonas: disparos oídos, carreras (sube el oído hasta x1.4), sitios donde perdió al jugador; se desvanece con el silencio. | `InfectedLearningMemory.cs` |
| Impactos | Zona por altura (cabeza x3 daño, torso, piernas = cojera), retroceso direccional sobre el NavMesh, caída al morir y derribo con incorporación en dos tiempos tras el forcejeo (sin teletransporte). Sin ragdoll. | `InfectedHitReactions.cs`, `HitZone.cs` |
| Red de cámaras | Con cualquier terminal de seguridad abierta, **[C]** abre la red de 3 pisos: pestañas de piso, lista, OSD, estado EN VIVO / SIN LUZ / DIFERIDO / SIN SEÑAL, estática al cambiar. El piso cargado se ve en vivo (reutiliza `Cam_F12_01`; si no hay cámara, una sola cámara compartida y una RT 640x360 que se libera al cerrar). Los otros pisos no están cargados, así que muestran el último fotograma archivado. | `UI/CameraNetworkController.cs`, `UI/Layouts/CameraNetwork.uxml`, `UI/Styles/camera-network.uss`, `Data/SecurityCameraNetwork.asset`, `Interaction/CameraFeedDefinition.cs`, `Interaction/SecurityCameraNetwork.cs`, `Editor/NexoCameraArchiveCapture.cs` |
| Piso 6 | Construido con `Nexo/Floor06/Construir Laboratorio de Sombra` (todo bajo la raíz `F06_Sombra`; reejecutable). Ver detalle abajo. | `Editor/NexoFloor06Builder.cs`, `Floor06_Labs.unity`, `Floor06_Labs/NavMesh-Floor06_Labs.asset`, `Materials/Floor06/*` |
| Audio | Generador procedural (truenos en 3 capas, golpes, caídas, respiración, zumbido, alarma lejana, tono de sala, pasos, impactos, quejido). | `Editor/NexoProceduralAudioGenerator.cs`, `Audio/**/SFX_F06_*`, `SFX_Infected_*` |
| Puertas | Hacen ruido (35) al moverse y emiten `OpenChanged`; `DoorNavMeshLink` abre el paso de la IA por vanos estrechos solo con la puerta abierta. | `Interaction/DoorController.cs`, `Interaction/DoorNavMeshLink.cs` |
| Demo | `DemoShortcuts` en `PF_Player` con `EnableDemoShortcuts = false`. Si se activa: F5 piso siguiente, F6 vida, F7 munición, F8 al BioScanner. | `Player/DemoShortcuts.cs` |

## Piso 6 — contenido

- **Luz:** fluorescentes en blanco estéril (03, 05 y 06 parpadean), emergencias rojas/verdes independientes de los interruptores, ambiente plano muy bajo y niebla. Interruptores por sala: `F06_Corridor`, `F06_LabA`, `F06_LabB`, `F06_Containment`.
- **Apagón del Lab B:** al entrar, `ScareTrigger` corta solo su interruptor; la secuencia activa la respiración en bucle, la voz falsa y los avisos de Elena. Las demás salas conservan la luz.
- **Narrativa:** Momentos 0-4 con los textos del encargo (`Seq_F06_00…04`), notas N1-N6 (`LoreNote`) y N7 como grabación. La terminal de SOMBRA (Lab A) abre `F06_Door_Stairs` con la acción existente `UnlockDoor`; la pregunta rápida [2] de Elena sigue funcionando igual que antes (el guion de la demo no cambia).
- **Infectados (5):** patrulla del pasillo que aprende, centinela del Lab A de espaldas, imitador del Lab B (4 mensajes falsos, voz más grave con eco), trampa en Contención (la voz falsa pide abrir esa puerta) y perseguidor que acude al ruido de la puerta de las escaleras.
- **Sustos:** golpes tras la pared del almacén sellado, voz de Elena desde el almacén vacío, caída metálica en el Lab A, silueta que solo aparece en la cámara F06-03 y desaparece al entrar. Tormenta: sin ventanas, el relámpago es una sobretensión de las luces del pasillo seguida del trueno.
- **Terminal de seguridad** en el pasillo: controla la puerta de Contención y da acceso a la red de cámaras.

## Verificado en Unity (2026-11-03)

- Compilación sin errores.
- Play en Piso 12: los 2 infectados patrullan sobre el NavMesh; ningún error `OnFootstep`.
- Play en Piso 6 (llegando con `FloorProgression.LoadNext`): carga, Momento 0 y objetivo correctos, 5 infectados sobre el NavMesh reaccionando a ruidos y persiguiendo; sin errores en la Console.
- NavMesh del Piso 6: todas las salas alcanzables desde el spawn; Contención solo con la puerta abierta; almacén sellado inalcanzable (a propósito).

## No verificado / intervención manual

- **Recorrido completo a mano** (12 → 6 → 1 → créditos → menú → JUGAR) tras estos cambios: pendiente de jugarlo una persona.
- **Red de cámaras en juego** (abrir con [C], cambiar de piso, estática, RT liberada): implementada pero no probada con teclado/ratón.
- **Sensación del arma** (retroceso, balanceo, posición): revisar a mano; los valores están en `FirstPersonFeel` en el Inspector de `PF_Player`.
- **Elena humanoide (fase 9): NO hecho.** No hay ningún modelo humanoide femenino en el proyecto ni en los packs; hace falta descargar uno (p. ej. de Mixamo con la cuenta del equipo) y su retrato `Resources/Elena/portrait.png`. No se inventó ni se sustituyó nada.
- **Ragdoll:** no se configuró; las caídas son procedurales.
- Si se cambia el Piso 6 a mano, volver a ejecutar el constructor lo **borra y rehace** `F06_Sombra` (lo de fuera de esa raíz no se toca).
- Archivo de cámaras: regenerar con `Nexo/Cámaras/Capturar archivo de la red` si cambian las escenas. La captura F12-02 sale oscura en el Editor.
