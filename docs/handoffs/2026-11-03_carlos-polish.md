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

## Segunda sesión (2026-11-03, tarde)

- **Verificación:** batchmode con el Editor cerrado, 151/151 tests EditMode (146 previos + 5 de la red de cámaras), sin errores de compilación.
- **Regresión local revertida:** a las 11:01:26, con el Editor cerrándose tras el build lanzado por MCP, `ElenaConfig.asset`, `PF_Player.prefab` y `Systems_Persistent.unity` quedaron sobrescritos con versiones antiguas (commits `4657d1b` y `cccde31`; sin `StruggleUI` ni `CameraNetworkUI`, y con un solo fallback de Elena). No era un cambio de nadie: se restauraron con `git restore` (copia en el scratchpad de la sesión). El build `Builds/NexoProtocoloSombra_Windows_Polish` es de las 10:53 y no está afectado.
- **`develop` integrado** (merge `303140e`, sin conflictos): trae `feature/hud-polish` y el PR #8 (higiene de textos del HUD).
- **Ids crudos en notificaciones (pedido en `2026-11-03_hud-polish.md` §5):** `DoorController` y `SecurityTerminal` usan ahora `DoorDisplayNames.Label/Destination`.
- **Para Gabriel:** `DoorDisplayNames` llama a `F06_Door_Lab` «Puerta del laboratorio», pero en el Piso 6 nuevo esa puerta es la de **Contención** (la voz falsa dice «estoy en Contención, ábreme»). Sugerencia: «Puerta de Contención» / «la sala de contención». No se tocó porque es archivo de UI.
- **Notificación duplicada de munición** (`PlayerWeapon.AnnounceAmmo`): no se quitó porque `PlayerWeapon.cs` estaba protegido en este encargo. Es una línea; decidid quién la quita.

## Tercera tanda (2026-11-03, noche)

- **Arma invisible y linterna:** el pivote del arma, la inercia y el balanceo la sacaban por debajo del campo de visión (70°). Valores corregidos en `FirstPersonFeel` y `PF_Player`; la linterna baja de intensidad 2.8/13 m a 1.5/10 m. Comprobado en Play: el arma ocupa del 13 % al 48 % de la altura de pantalla.
- **Piso 6 — reptantes** (`InfectedBehavior.Crawler`): modelo boca abajo sobre el agente (pose procedural; no hay animación de arrastre en los packs), lentos, colisión baja, vista 8 m / 120°, agarran del tobillo e inician el forcejeo. Dos: junto a los tubos del Lab A y en la penumbra del pasillo (a 15 m del spawn).
- **Piso 6 — escondites** (`HidingSpot` + `PlayerHiding`): 3 armarios huecos (pasillo, Lab A, Lab B). [E] entra y sale; dentro no se ve al jugador salvo si un infectado le vio entrar, que lo saca a la fuerza.
- **Piso 6 — puzle de energía** (`PowerBreaker`): tras el apagón el interruptor del Lab B queda «sin corriente»; el diferencial del pasillo la restablece en 3 s con ruido 70 repetido (atrae infectados).
- **Red de cámaras — enlace diferido vivo:** los pisos no cargados se ven en reproducción: paneo lento, barra de interferencia, silueta que cruza el encuadre y reloj que avanza.
- **Piso 1 — ambientación sin tocar el giro** (`Nexo/Floor01/Construir ambientación del vestíbulo`): raíz propia `F01_Ambience` con `PlotTwistSafeZone` (se apaga entera al empezar el giro). Tormenta con relámpago que entra por la cristalera, emergencias en ascensores, tono de sala, 2 sustos, 3 notas que no revelan el giro, terminal de seguridad en recepción y un reptante junto a los ascensores con NavMesh propio. Verificado: 0 objetos y 0 valores originales perdidos en `Floor01_Lobby.unity`; BioScanner, PlotTwistManager y sus luces intactos.

## No verificado / intervención manual

- **Recorrido completo a mano** (12 → 6 → 1 → créditos → menú → JUGAR) tras estos cambios: pendiente de jugarlo una persona.
- **Red de cámaras en juego** (abrir con [C], cambiar de piso, estática, RT liberada): implementada pero no probada con teclado/ratón.
- **Sensación del arma** (retroceso, balanceo, posición): revisar a mano; los valores están en `FirstPersonFeel` en el Inspector de `PF_Player`.
- **Elena humanoide (fase 9): NO hecho.** No hay ningún modelo humanoide femenino en el proyecto ni en los packs; hace falta descargar uno (p. ej. de Mixamo con la cuenta del equipo) y su retrato `Resources/Elena/portrait.png`. No se inventó ni se sustituyó nada.
- **Ragdoll:** no se configuró; las caídas son procedurales.
- Si se cambia el Piso 6 a mano, volver a ejecutar el constructor lo **borra y rehace** `F06_Sombra` (lo de fuera de esa raíz no se toca).
- Archivo de cámaras: regenerar con `Nexo/Cámaras/Capturar archivo de la red` si cambian las escenas. La captura F12-02 sale oscura en el Editor.

## Cuarta tanda (2026-11-03, noche)

- **Piso 6 alineado con el guion oficial** (`Narrativa del Piso 6`): tiempos y emociones de los Momentos 0-4, subtítulo de llegada y de la voz en la oscuridad; la terminal de SOMBRA ya **no** abre la puerta (objetivo: «Pídele a Elena que abra la puerta de las escaleras [2] y baja al Piso 1»); N5 y N6 en el Lab B; N7 en Contención; **Contención sin infectado** (trampa de confianza: celda vacía, goteo y grabación); subtítulos en crujidos, truenos y goteo.
- **Piso 1 ampliado (para Gabriel, es su escena):** raíz nueva `F01_Expansion` con un circuito de servicio: puerta nueva al norte del vestíbulo → pasillo del correo (patrulla, cobertura) → Sala de seguridad (terminal, monitores, nota, munición) → pasillo este (salida de emergencia sellada) → vuelta al vestíbulo por una puerta nueva al este; sala de descanso a oscuras (reptante, armario-escondite, munición). Las paredes originales `Wall_North` y `Wall_East` **solo se desactivaron** (m_IsActive 0) y se sustituyeron por versiones con hueco; ningún otro objeto ni valor original cambió (comprobado). El camino al escáner sigue siendo de 18 m y el giro no se toca. Infectados, sustos y audio de las alas cuelgan de `F01_Ambience` y se apagan al empezar el giro.
- **Red de cámaras:** 12 cámaras (2 nuevas en las alas del Piso 1), todas con archivo.
