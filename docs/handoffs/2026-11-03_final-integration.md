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

### Fase 5: créditos y assets (hecha)

- `Credits.uxml` pasa a dos columnas (cabe en 1080 px sin desplazamiento): diseño y desarrollo, materia, Gemini 2.5 Flash (Google), tipografías (Barlow Condensed / IBM Plex · OFL 1.1), sonido procedural, M1911 (Nokobot), Mixamo (Adobe), Starter Assets de Unity Learn (Unity Companion License), Polygon Office Building (Asset Store EULA).
- Se versionan las licencias OFL junto a las fuentes (`UI/Fonts/OFL_*.txt`, copiadas de las descargas de Google Fonts de Gabriel).
- `docs/ASSETS.md` reconciliado. El pack de oficinas **no** es POLYGON Office de Synty (ni prefijos ni estructura de Synty); su `Read me.txt` solo da un correo.
- **MISSING (fuera de los créditos):** autor del pack de oficinas; fuente/autor/licencia de `Pack_ZombieMale_AAB`; origen de las capturas de los clips `Infected/*.anim` y `Elena/*.anim`; confirmación de que los clips de 752cb8f son procedurales.

### Fase 6: documentación (hecha)

- `CLAUDE.md` reescrito: 3 pisos, Built-in + Standard, sistemas de combate/vida/QTE/persistencia, propiedad Gabriel/Carlos (Piso 12 terminado), Elena y `_forceFallback`, regla de secretos, packs, Git, notas y límites de Unity MCP. Conserva los estándares de C# y UI Toolkit.
- `docs/DESIGN_RATIONALE.md`: resueltas 5 marcas `[verificar]` (Okabe-Ito, arco estático, espacial, ayuda, BioScanner); quedan 2 (razones de contraste sin medir, mando físico). Nuevas secciones 11 (combate, vida, QTE, persistencia), 12 (audio e infectados) y 13 (Built-in frente a URP).
- `README.md`: versión 6000.6.7f1, Built-in, `git lfs pull`, carpeta de packs y lista de packs, controles reales (de `NexoControls.inputactions` y del código).

### Fase 7: verificación en vivo (hecha)

- Tests EditMode: **35/35**.
- Accesibilidad: desde el menú principal se abre Opciones › Accesibilidad; alto contraste y reducir movimiento se aplican en vivo (captura).
- Captura en el Piso 12 → recarga → pantalla «SEÑAL PERDIDA / PISO 12 REINICIADO» en pausa (captura) → REINTENTAR → juego con el checkpoint restaurado.
- **Recorrido 1:** 12 (munición 4/6 + 1 golpe, infectados eliminados) → 6 (persistencia OK; REINICIAR vuelve a 4/6; Elena [2] en fallback abre `F06_Door_Stairs` tras ~5 s) → 1 (pausa muestra «PISO 1 · VESTÍBULO»; REINICIAR OK) → BioScanner → giro (bloqueo, HUD corrupto, luces rojas, captura) → créditos → menú.
- **Recorrido 2:** JUGAR desde cero (2/0, 3/3, índice 0) → REINICIAR en el 12 → 12 → 6 (3/3 conservado) → [2] → 1 → giro → créditos → menú (1 jugador, 1 HUD, 1 puente, `TwistHasPlayed` limpio) → JUGAR de nuevo desde cero.
- Créditos: se veían encima del vestíbulo y del arma sin velo. Se añadió el `nx-scrim` del tema (hive) como primer hijo de `cr-root`; captura: texto legible en dos columnas.
- Tecla de voz de Elena (V): `ElenaVoiceInput` activo y con binding; no se probó dictado real (requiere micrófono y reconocimiento de Windows) [verificar en el ensayo].
- Consola: **0 errores**. Avisos repetidos: «Animator is not playing an AnimatorController» desde `SmartInfectedBrain.UpdateAnimatorSpeed` (el infectado no tiene modelo/avatar porque falta `Pack_ZombieMale_AAB`); avisos del pack de oficinas y de Unity AI.
- Piso 12 (solo lectura): aparición, 2 infectados, `LevelExitGate` y `StairsExit_F12` → Piso 6 funcionan. No se modificó nada del Piso 12.

### Fase 8: build de Windows con Elena offline (hecha)

- `ElenaConfig`: `_forceFallback` = true y `_secrets` = null solo para el build (los 3 JSON de fallback estaban en la lista). En esta máquina no existe `ElenaSecrets.asset` (la referencia ya estaba rota), así que ninguna clave pudo entrar al build. Después se restauró el asset con `git restore`: `_forceFallback: 0` y la referencia original, sin cambios en Git.
- `BuildPipeline.BuildPlayer`, StandaloneWindows64, release, escenas en orden (Systems_Persistent primero). Resultado **Succeeded**, 0 errores, 3 min 20 s.
- Salida: `Builds/NexoProtocoloSombra_Windows/` (**174,9 MB**) y `Builds/NexoProtocoloSombra_Windows.zip` (**62,8 MB**). `Builds/` está ignorado.
- Ejecutado 60 s: no se cerró. `%USERPROFILE%\AppData\LocalLow\DefaultCompany\NexoProtocoloSombra\Player.log` sin excepciones; registra «[ElenaAIService] Sin API key configurada: operando en modo contingencia (fallback local).»
- Avisos del build (no bloqueantes): shaders URP (`Universal Render Pipeline/Lit`, `FallbackError`) eliminados del build por no haber asset URP; scripts TMP ausentes en assets de `Pack_UnityLearnTemplate`; 144 mallas sin colisión precocinada (pack de oficinas).
- `PlayerSettings.companyName` sigue siendo `DefaultCompany` (no se tocó ProjectSettings).

### Fase 9: push y textos de PR (hecha)

- `gh` no está instalado: los PR se entregan como texto (abajo).
- PR 1: https://github.com/itsGabo22/NexoProtocoloSombra/compare/develop...feature/final-integration?expand=1
- PR 2 (después del 1, **Create a merge commit**, nunca squash): https://github.com/itsGabo22/NexoProtocoloSombra/compare/main...develop?expand=1

### Fase 10: guion de demo (hecha)

- `docs/DEMO_SCRIPT.md`: guion de 6:30, controles verificados, mentiras del HUD frente a la verdad, plan de contingencia y limitaciones.

## Informe final

| Fase | Estado | Nota |
| --- | --- | --- |
| 0 Auditoría | Hecha | Falta `Pack_ZombieMale_AAB` |
| 1 Base limpia | Hecha | No había duplicado; se reencuadró la pistola |
| 2 Pisos 6 y 1 | Hecha | Pausa bloqueada en el giro; pantalla «Piso reiniciado» conectada |
| 3 Pisos «de juego» | Omitida | MEDIA; sin modelo de zombi no se puede verificar un infectado nuevo |
| 4 Térmica sin URP | Hecha | Capa UI Toolkit |
| 5 Créditos y assets | Hecha | Dos columnas + velo |
| 6 Docs | Hecha | CLAUDE.md, rationale, README |
| 7 Verificación | Hecha | 35/35; 2 recorridos; 0 errores |
| 8 Build | Hecha | 174,9 MB; zip 62,8 MB; 60 s sin excepciones |
| 9 PR | Hecha (texto) | Sin `gh` |
| 10 Demo | Hecha | `docs/DEMO_SCRIPT.md` |

**Cambios en código de Carlos o en el Piso 12:** ninguno. El único cambio que toca su sistema es la posición del `Viewmodel_M1911` en `Systems_Persistent` (override de escena de Gabriel; `PlayerWeapon` no se modificó).

**MISSING:** autor del pack de oficinas; fuente, autor y licencia de `Pack_ZombieMale_AAB`; origen de los clips `Infected/*.anim` y `Elena/*.anim`; confirmar que los clips de 752cb8f son procedurales.
**`[verificar]` restantes:** razones de contraste sin medir; mando físico; dictado por voz de Elena.

**Para Gabriel, a mano:**
1. Abrir el PR 1 con el enlace de arriba (base `develop`, revisor `simviz12`) y pegar el texto. Mergearlo.
2. Después, abrir el PR 2 `develop → main` y mergearlo con **Create a merge commit**.
3. Pedir a Carlos `Pack_ZombieMale_AAB` y extraerlo en `Assets/ThirdPartyAssets/` con Unity cerrado.
4. Enviar `Builds/NexoProtocoloSombra_Windows.zip` al equipo; si se rehace el build con el pack del zombi, repetir la fase 8.
5. Ensayar con `docs/DEMO_SCRIPT.md`.

## Textos de los PR
## PR 1 · `feature/final-integration` → `develop`

**Título:** `feat: integración final, visión térmica, créditos verificados y docs de entrega`

**Revisor:** `simviz12`

**Cuerpo:**

## Resumen por fase

- **Auditoría (fase 0):** rama creada desde `origin/develop` (a1328c4). Escaneo de secretos de todo el historial limpio. Tests de base 35/35. Falta `Pack_ZombieMale_AAB` en la máquina de Gabriel (ver «Problemas conocidos»).
- **Base limpia (fase 1):** no había pistola duplicada: `PlayerWeapon` usa el `Viewmodel_M1911` de `Systems_Persistent` por nombre. Ese modelo quedaba fuera del encuadre en la posición de cadera: se movió a (0.13, -0.13, 0.20). La posición de apuntado no se tocó.
- **Pisos 6 y 1 (fase 2):** persistencia 12 → 6 → 1, REINICIAR en los tres pisos, giro con el jugador bloqueado y vuelta al menú limpia, todo verificado. Arreglos: no se puede pausar durante el giro, y la pantalla «Piso reiniciado» ahora se abre tras una captura (REINTENTAR reanuda desde el checkpoint). **No se tocó código de Carlos ni el Piso 12.**
- **Visión térmica sin URP (fase 4):** `ThermalOverlayController` + capa `hud-thermal-overlay` (tinte cian y viñeta) que se activa con la F. Respeta `.reduce-motion` y `.a11y-hc`.
- **Créditos y assets (fase 5):** créditos en dos columnas solo con datos verificados, velo del tema detrás, licencias OFL junto a las fuentes y `docs/ASSETS.md` reconciliado.
- **Docs (fase 6):** `CLAUDE.md` reescrito (3 pisos, Built-in, dos desarrolladores, MCP), `DESIGN_RATIONALE.md` con secciones de combate, persistencia, audio y Built-in frente a URP, y `README.md` con los pasos de instalación y los controles reales.
- **Demo (fase 10):** `docs/DEMO_SCRIPT.md`.
- Bitácora completa en `docs/handoffs/2026-11-03_final-integration.md`.

## Cómo probar

1. `git switch feature/final-integration`, `git lfs pull`, y packs en `Assets/_ThirdPartyAssets/` (incluido `Pack_ZombieMale_AAB`).
2. Abre `Systems_Persistent.unity` y pulsa Play → JUGAR.
3. Piso 12: elimina a los 2 infectados → escalera [E] → Piso 6: pulsa 2 (Elena abre la puerta) → escalera → Piso 1: BioScanner [E] → giro → créditos → Esc → JUGAR (empieza de cero).
4. Pulsa F: tinte cian con viñeta. Esc → REINICIAR PISO en cada piso. Déjate atrapar: aparece «SEÑAL PERDIDA».
5. Window › General › Test Runner › EditMode › Run All.

## Problemas conocidos

- Sin `Pack_ZombieMale_AAB` el infectado no tiene modelo y la consola repite «Animator is not playing an AnimatorController».
- Al entrar en Play, Unity reescribe `Materials/Floor12/MAT_F12_ExitButton.mat`. No se versionó: descártalo con `git restore`.
- Los clips de 752cb8f, el autor del pack de oficinas y la fuente del pack del zombi están pendientes en `docs/ASSETS.md`.

## Tests

EditMode **35/35**. Dos recorridos completos en Play con 0 errores en la consola.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

---

## PR 2 · `develop` → `main` (solo texto; abrir DESPUÉS de mergear el PR 1)

**Título:** `release: entrega final (develop → main)`

**Cuerpo:**

## Estado del juego

`main` (1275d15) solo tiene la preparación del Piso 12. Este PR lleva a `main` el juego completo:

- **3 pisos jugables:** 12 (oficinas, combate, 2 infectados, historia), 6 (laboratorios, Elena abre la escalera) y 1 (vestíbulo, BioScanner y giro final).
- **Menús NexoOS en UI Toolkit:** arranque, menú, opciones de video y accesibilidad, pausa, piso reiniciado y créditos.
- **Sistemas:** HUD biométrico que miente, Elena con Gemini 2.5 Flash y fallback offline, M1911, vida, forcejeo, persistencia entre pisos, visión térmica en Built-in, audio procedural e IA de infectados con FSM.
- **Calidad:** 35/35 tests EditMode.
- **Documentación:** `docs/DESIGN_RATIONALE.md`, `docs/DEMO_SCRIPT.md` y `docs/ASSETS.md`.

## Checklist de entrega

- [ ] PR `feature/final-integration` → `develop` mergeado.
- [ ] Recorrido completo en `develop` (menú → 12 → 6 → 1 → créditos → menú).
- [ ] Build de Windows con Elena en fallback (`Builds/NexoProtocoloSombra_Windows.zip`) probado en otra máquina.
- [ ] Packs (incluido `Pack_ZombieMale_AAB`) en el zip compartido del equipo.
- [ ] Ensayo de la demo con `docs/DEMO_SCRIPT.md`.

**Importante:** mergear con **«Create a merge commit»**. Nunca «Squash»: aplastaría la historia sobre la que trabaja la rama de Carlos.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
