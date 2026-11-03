# Handoff: integración final, pulido, build y entrega

- **Fecha:** 2026-11-03 (entrega 2026-11-04)
- **De:** Gabriel (sesión con Claude Code)
- **Para:** Gabriel / Carlos
- **Prioridad:** Alta
- **Rama:** `feature/final-integration` (creada desde `origin/develop` a1328c4)

## Cómo retomar

1. `git fetch origin && git switch feature/final-integration`.
2. Lee la última entrada de «Progreso por fase» y continúa con la siguiente fase pendiente.
3. Reglas: nunca push a `main`/`develop`, nunca merge de PR, commits atómicos con `.meta`, nada de `Packages/**`/`ProjectSettings/**`.

## Progreso por fase

### Fase 0: auditoría (hecha)

- Ruta del proyecto en Unity = ruta del repo (`C:/Users/Gabriel/Desktop/Tareas/DisenoInterfaces/NexoProtocoloSombra`). Unity 6000.6.7f1, sin pipeline SRP asignado (Built-in).
- `origin/develop` = a1328c4 y contiene 0525ead (PR #3). `develop` local se avanzó por fast-forward a a1328c4.
- `skip-worktree` activo en `Packages/manifest.json` y `Packages/packages-lock.json`; hook `pre-commit` presente. **Desviación:** no se hizo `git restore` del manifest porque el cambio local es `com.unity.ai.assistant` (aloja el puente MCP) y `develop` no toca `Packages/**`; el cambio de rama fue limpio.
- `git lfs pull` hecho (5 `.ttf` en LFS).
- Escaneo de secretos de **todo** el historial: limpio (solo referencias de código a la cabecera `x-goog-api-key`; nunca se versionó `ElenaSecrets.asset`).
- Lista de escenas: Systems_Persistent, Floor12_Offices, Floor06_Labs, Floor01_Lobby (todas activas).
- Tests EditMode de base: **35/35**.
- Materiales URP/rotos en `Assets/_Project`: 0.
- **Packs de terceros:** los packs están en `Assets/ThirdPartyAssets/` (sin guion bajo; ignorado igual que `_ThirdPartyAssets/`): `Pack_Nokobot_Guns`, `Pack_OfficeBuilding`, `Pack_UnityLearnTemplate`, `Unused/Ch33_nonPBR.fbx`. **FALTA `Pack_ZombieMale_AAB`**: Unity marca `PF_Infected_Variant.prefab` con «Missing Nested Prefab (guid 2f6b5402acaac7e45a0b798395dd250b)» y el infectado no tiene malla ni avatar en esta máquina. No se encontró el zip en Escritorio/Descargas/Documentos. **Acción para Gabriel:** extraer el pack del zip de Carlos (con sus `.meta`) en `Assets/ThirdPartyAssets/` con Unity cerrado. No afecta a lo versionado (la referencia por GUID está bien en el repo).
- Consola tras compilar: 1 error (el del pack del zombi), avisos del pack de oficinas (`Door.cs` UAC1001) y de la cuenta de Unity AI. Sin errores de compilación.
- Herramientas MCP disponibles: RunCommand, ManageEditor, ManageScene, ManageGameObject, ReadConsole/GetConsoleLogs, Camera_Capture, SceneView captures, FindProjectAssets, ManageAsset, ManageMenuItem.
- `gh` no está instalado (se comprobó en la sesión anterior): los PR se entregan como texto.

### Fase 1: base de integración limpia (hecha, con una lectura distinta del prompt)

- **No había pistola duplicada.** `PlayerWeapon` (de Carlos) busca su modelo por nombre (`ViewmodelName = "Viewmodel_M1911"`) y `PF_Player.prefab` no trae ningún modelo propio: el `Viewmodel_M1911` de `Systems_Persistent` **es** la pistola de Carlos. Borrarlo dejaba al jugador sin arma, así que se conserva.
- Problema real: en la posición de cadera (0.12, -0.15, 0.08) la pistola quedaba fuera del encuadre (FOV 70). Se movió a (0.13, -0.13, 0.20). Captura de la Main Camera: una sola pistola, con textura (Standard, sin rosa), abajo a la derecha. La posición de apuntado (`_aimLocalPosition` de Carlos) no se tocó.
- Materiales URP/rosas en `Assets/_Project`: 0. Sin scripts faltantes salvo el modelo del zombi (pack ausente, ver Fase 0).
- Aviso: al entrar en Play Unity reescribe `Materials/Floor12/MAT_F12_ExitButton.mat` (quita `_EMISSION`). No se ha versionado; descártalo con `git restore` si aparece modificado.

### Fase 2: pisos 6 y 1 con los sistemas nuevos (hecha)

Recorrido en Play desde `Systems_Persistent`:
- Menú → JUGAR → Piso 12 (2/0 balas, vida 3/3). Se fijó munición 5/9 y 3 golpes, se mataron los 2 infectados con `SmartInfectedBrain.ApplyHit` → `LevelExitGate` abrió la persiana → `StairsExit_F12` → **Piso 6 con 5/9 y 3 golpes** (persistencia OK).
- REINICIAR en el Piso 6 tras gastar munición: vuelve al checkpoint (5/9). Igual en el Piso 1.
- Piso 6: pregunta rápida [2] → `F06_Door_Stairs` abierta → `StairsExit_F06` → Piso 1.
- Piso 1: BioScanner → giro: movimiento congelado, arma bloqueada (`IsInputLocked`), linterna apagada → créditos → menú: un solo jugador/HUD/menú, `TwistHasPlayed` limpio → JUGAR: Piso 12 desde cero (2/0, 3/3, índice de progresión 0).
- Captura (muerte): `PlayerCaptureHandler` reinicia el piso a los 3 s y restaura el checkpoint.

Arreglos:
- `PauseMenuController.TryPause`: no se puede pausar (ni REINICIAR) durante el giro final.
- La pantalla «Piso reiniciado» existía pero nadie la abría. Ahora `MenuGameFlowBridge` la abre (en pausa) cuando `PlayerCaptureHandler` termina de recargar tras una captura; REINTENTAR reanuda desde el checkpoint ya restaurado y MENÚ PRINCIPAL vuelve al hub. No se tocó código de Carlos.
- Pisos sin infectados ni waypoints: sin errores (6 y 1 no tienen IA hostil).

### Fase 3: pisos 6 y 1 «de juego» (omitida, MEDIA)

Omitida a propósito: el modelo del infectado depende del pack `Pack_ZombieMale_AAB`, que falta en esta máquina, así que un infectado nuevo en el Piso 6 no se podría verificar visualmente y además consume el presupuesto de las fases críticas.

### Fase 4: visión térmica sin URP (hecha)

- `ThermalVisionController` (de Carlos) mezcla el Volume `VP_Thermal`, que no hace nada en Built-in. Se añadió `Scripts/UI/ThermalOverlayController.cs` (HUD): escucha `FlashlightSystem.StateChanged` (método con nombre, sin `Update`) y activa `.thermal-on` en `hud-thermal-overlay` (primer hijo de `hud-root`: tinte cian 14 % + viñeta `UI/Textures/T_ThermalVignette.png`, transición de 250 ms, anulada por `.reduce-motion`; más suave con `.a11y-hc`).
- Verificado con captura de pantalla en Play: tinte y viñeta visibles, paneles del HUD por encima, barra «VISIÓN TÉRMICA 90 %» intacta.
- Sin test nuevo: no hay lógica pura (solo enlaza un evento con una clase USS).
