# Handoff: pulido del HUD (arma/munición, narrativa e idioma)

- **Fecha:** 2026-11-03
- **De:** Gabriel (agente de código)
- **Para:** Gabriel / Carlos
- **Prioridad:** Alta (entrega 2026-11-04)
- **Rama:** `feature/hud-polish` (desde `origin/develop` 62de04d)

## 1. Auditoría (antes de cambiar nada)

### Estado de git
- `main` local = `origin/main` = 64b05cf. `origin/develop` = 62de04d (un merge más que `main`: PR #6, `MAT_F12_ExitButton.mat`, nada de UI).
- Rama creada: `feature/hud-polish` desde `origin/develop`. Árbol limpio (solo `ThirdPartyAssets.zip`, excluido en `.git/info/exclude`).
- Unity Editor **abierto** con el proyecto y **unity-mcp no conectado** (el paquete `com.unity.ai.assistant` se retiró del manifest local en la sincronización anterior). Batchmode exige cerrar el Editor.
- Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe`. `gh` **no** está instalado: el PR se abre a mano desde la URL de comparación.
- Línea base documentada en `docs/DESIGN_RATIONALE.md` §7: 35 pruebas EditMode en verde.

### HUD (`UI/Layouts/HUD_Biometric.uxml` + `Scripts/UI/HUDController.cs`)
- Paneles: vitales (arriba izq.) con notificaciones debajo, radio de Elena (arriba der., `w-80`), ruido (abajo izq.), batería térmica (abajo der., `w-64`), mira y prompt en el centro, subtítulos abajo (`left/right: 20%`).
- **No hay panel de arma.** La única información de munición es una notificación «M1911 · Cargador x/7 · Reserva y» tras recargar/recoger, y el subtítulo «El cargador está vacío».
- **Vitales:** `PlayerHealth.PublishVitals` envía `ValueText = "{p}% ILESO|HERIDO|CRÍTICO"` y `StatusText = "VIDA x/y · GOLPES x/y"`, que pisa el «NO INFECTADO» del UXML. La mentira del HUD se perdió.
- El giro (`PlayCorruptionSequence`) escribe «98.4% INFECTADO» / «ROOT DEBUG», pero `HandleVitals` lo sobrescribiría si llegara otro `OnVitalsChanged` (no hay guarda).
- **Emoción de Elena:** `SetEmotion` (línea 295) usa `emotion.ToString().ToUpperInvariant()` → CALM, WHISPER… El enum real `ElenaEmotion` = Calm, Happy, Scared, Urgent, Sad, Whisper.
- **Ruta segura:** `_safeRouteDefaultTarget = "F12_Door_Stairs"` y el `targetId` crudo se imprime en la notificación.
- **Retrato:** círculo vacío (`radio-portrait`, borde por emoción). No existe `Assets/_Project/Resources/`.
- **Daño:** solo subtítulo «(Te golpean: x/4)»; sin feedback visual.
- **Controles:** no hay ayuda de teclas en el HUD.

### Arma y vida
- `PlayerWeapon`: `Magazine`, `Reserve`, `_magazineSize = 7`; `_isReloading` privado (no expuesto); munición cambia en `SetAmmo`/`ResetToDefaults`, disparo, `ReloadRoutine`, `HandleAmmoPicked`. Restauración entre pisos/reinicios vía `PlayerStatePersistence` → `SetAmmo` y `enabled = HasWeapon`.
- `GameEvents` solo tiene `OnAmmoPicked(int)` para munición.
- `PlayerHealth` no expone eventos propios: el daño solo se observa por `OnVitalsChanged` (porcentaje en `ValueText`).

### Terminal (`SecurityTerminal.uxml` + `TerminalController.cs`)
- Título = `TerminalId.Replace('_',' ').ToUpper()` → «F12 TERMINAL SECURITY» (inglés).
- Lista de puertas muestra `door.DoorId` crudo; estados ya en español («BLOQUEADA · PROTOCOLO», «ABIERTA», «CERRADA»).
- Ventana de 900 px; cada cámara mide 256×144: con una sola cámara queda un hueco grande.

### DoorIds reales (escenas/prefabs)
`F12_Door_Stairs`, `F12_Door_Meeting`, `F06_Door_Stairs`, `F06_Door_Lab`. Terminal: `F12_Terminal_Security`.

### Textos con ids crudos fuera de mi alcance (código de Interaction, no editable en esta tarea)
- `DoorController.cs` líneas 75, 86, 97 y `SecurityTerminal.cs` línea 89 imprimen `_doorId` en notificaciones. Ver §5 (pendiente para Carlos/Gabriel).

### Fallbacks de Elena (`Data/ElenaFallback/*.json`)
- Ningún `dialogue` imprime ids crudos. `fallback_floor12.json` no lleva tildes («camara», «abri», «mama», «aqui», «¿si?», «Tu»); 06 y 01 sí.

### Entrada (verificada)
- `NexoControls.inputactions`: Move WASD, Look, Sprint Shift, Interact E, ThermalVision F, OrderWheel Q, Pause Esc/P, preguntas 1-3.
- En código: R recargar y clic der. apuntar (`PlayerWeapon`), V hablar (`ElenaVoiceInput`), C/Ctrl agacharse (`PlayerMovement`), Tab objetivo (`ObjectiveReminder`), Espacio forcejeo (`StruggleQTE`), F térmica (`FlashlightSystem`).

### Otros
- `FlashlightSystem` expone `IsOn`, `Battery` y el evento `StateChanged`; `ThermalOverlayController` ya tiene la referencia serializada.
- **No existe ningún `.mixer`** en el proyecto: solo es viable un volumen general con `AudioListener.volume`.
- Opciones: `OptionsController` con pestañas VIDEO/ACCESIBILIDAD; añadir una pestaña AUDIO exigiría una pantalla nueva registrada en `MenuFlowController` (escena) → se usaría una fila dentro de VIDEO.
- `PS_Nexo`: Scale With Screen Size, referencia 1920×1080, match 0.5 → a 1280×720 el layout es el mismo escalado.

## 2. Qué se hizo

| Parte | Estado | Detalle |
| --- | --- | --- |
| 1. Panel de arma y munición | Hecho | `hud-weapon` en la columna inferior derecha (encima de la batería). Estados normal / MUNICIÓN BAJA / RECARGAR [R] / SIN MUNICIÓN / RECARGANDO…, celdas del cargador, «+N» y pulso al recoger (sin pulso con reducir movimiento). Lógica en `Scripts/UI/AmmoDisplayState.cs` + `AmmoStatus.cs`. |
| 2.1 «NO INFECTADO» | Hecho | Línea propia `hud-vitals-infection` bajo el porcentaje; se mantiene «VIDA x/y · GOLPES x/y». La corrupción la oculta y, desde ese momento, `HandleVitals` no sobrescribe «98.4% INFECTADO». |
| 2.2 Emociones en español | Hecho | `ElenaEmotionLabels` (CALMA, ALEGRE, ASUSTADA, URGENTE, TRISTE, SUSURRO). |
| 2.3 Nombres de puertas | Hecho (parcial fuera de alcance) | `DoorDisplayNames` en la ruta segura y en la terminal. Los fallbacks no imprimían ids (ahora una prueba lo garantiza); se restauraron las tildes de `fallback_floor12.json` (solo `dialogue`). Ver §5 para `DoorController`/`SecurityTerminal`. |
| 2.4 Terminal | Hecho | «TERMINAL DE SEGURIDAD · PISO n» (`FloorMetadata.Active`, si no el último `OnFloorChanged`, si no 12), puertas con nombre, estado «APERTURA REMOTA EJECUTADA / CIERRE REMOTO EJECUTADO», una sola cámara a ancho completo (320 px de alto). |
| 2.5 Daño | Hecho | Viñeta roja `hud-damage` (reutiliza `T_ThermalVignette.png` tintada con `--c-danger`): destello de 250 ms al bajar el % de vida y pulso lento (1 s) con ≤ 34 %. Con reducir movimiento: sin destello y viñeta fija. |
| 2.6 Ayuda de controles | Hecho | Franja arriba al centro una vez por sesión, 6 s, al primer `OnFloorChanged`. Teclas verificadas. |
| 2.7 Linterna/térmica | Hecho | `ThermalOverlayController` (ya tenía la referencia a `FlashlightSystem`) escribe ACTIVA/APAGADA en `hud-battery-state`. |
| 2.8 Retrato de Elena | Hecho (sin PNG) | `Resources.Load<Texture2D>("Elena/portrait")`; sin archivo se mantiene el círculo y no se registra nada. |
| 3. Audio y brillo | Parcial | No hay `.mixer` → solo «Volumen general» (fila en VIDEO, `AudioPrefs` + `AudioListener.volume`, PlayerPrefs). Brillo: omitido (la fila existente sigue siendo visual; aplicarlo exige tocar la iluminación de las escenas). Pestaña AUDIO: omitida (requiere registrar una pantalla nueva en la escena). |
| 4. Docs | Hecho | `DESIGN_RATIONALE.md` §4.1.1 y §7; este handoff. |

### Líneas añadidas a archivos protegidos (solo aditivas)

- `Scripts/Core/GameEvents.cs`: eventos `OnAmmoChanged(int magazine, int reserve, int magazineSize)` y `OnWeaponReloadChanged(bool)`, sus `Raise*` y su limpieza en `ResetAll`.
- `Scripts/Player/PlayerWeapon.cs` (código de Carlos, cambio mínimo y comentado con `// hud-polish`): `RaiseAmmoChanged` en `SetAmmo`, `Start`, `OnEnable`, `HandleFloorChanged`, cada disparo, fin de recarga y recogida; `RaiseWeaponReloadChanged(true/false)` al empezar/terminar la recarga y al deshabilitar el arma con una recarga en curso. `HandleFloorChanged` pasó de expresión a bloque.

## 3. Verificación

- Batchmode (Editor cerrado): línea base **35/35**; tras los cambios **108/108** EditMode. Sin errores ni warnings de compilación, sin avisos de importación de USS/UXML.
- No se pudo hacer Play ni capturas: unity-mcp no está conectado (el paquete `com.unity.ai.assistant` no está en el manifest local).

- PR: abrir a mano en https://github.com/itsGabo22/NexoProtocoloSombra/compare/develop...feature/hud-polish?expand=1 (título y cuerpo en el informe del agente). No mergear sin la checklist.

## 4. Checklist manual para Gabriel (Play desde `Systems_Persistent`, 1920×1080 y 1280×720)

1. JUGAR: aparece la franja de controles arriba al centro ~6 s y se desvanece; el panel M1911 muestra «2 / 0» con 2 de 7 celdas llenas y «MUNICIÓN BAJA» en ámbar.
2. Disparar 2 veces: «0 / 0», borde y texto en rojo, «SIN MUNICIÓN». Recoger munición: «+N» verde junto a M1911, pulso del panel y «0 / N» con «RECARGAR [R]».
3. Pulsar R: «RECARGANDO…» ~1,6 s y después el cargador lleno; nada se solapa con la batería, los subtítulos ni las notificaciones.
4. Vitales: «100% ILESO», debajo «NO INFECTADO» (verde) y «VIDA 3/3 · GOLPES 0/4».
5. Dejarse golpear: destello rojo en los bordes en cada golpe; con 1 punto de vida, pulso lento. Activar «Reducir movimiento»: sin destello y viñeta fija.
6. Ser capturado y reiniciar el piso: la munición y la vida vuelven al checkpoint en el panel; la franja de controles **no** reaparece.
7. Pedir a Elena [1] (ruta segura): la notificación dice «Ruta despejada hacia la puerta de las escaleras»; la emoción se lee en español (p. ej. SUSURRO).
8. Terminal del Piso 12: título «TERMINAL DE SEGURIDAD · PISO 12», la cámara ocupa el ancho de la columna y las puertas dicen «Puerta de las escaleras» / «Sala de reuniones».
9. Temas: alto contraste y daltonismo en Opciones › ACCESIBILIDAD (el panel de arma y la franja siguen legibles); F: la batería indica ACTIVA/APAGADA.
10. Piso 6 y Piso 1 → BioScanner: el panel conserva la munición entre pisos; en el giro «NO INFECTADO» desaparece, el HUD pasa a rojo con «98.4% INFECTADO» y ningún golpe/vital lo sobrescribe. Opciones › VIDEO › «Volumen general» baja el audio y se recuerda al reiniciar el juego.

## 5. Pendiente (fuera del alcance de esta rama)

- **Carlos / Gabriel — ids crudos en notificaciones de `Scripts/Interaction`**: `DoorController.cs` (líneas 75, 86, 97) y `SecurityTerminal.cs` (línea 89) imprimen `_doorId` («Puerta F12_Door_Stairs desbloqueada remotamente»). Cambio sugerido: usar `Nexo.UI.DoorDisplayNames.Label(_doorId)` o `.Destination(...)`. No se tocó porque esta tarea solo permitía UI, narrativa, `GameEvents` y `PlayerWeapon`.
- **Carlos — notificación duplicada de munición**: `PlayerWeapon.AnnounceAmmo` sigue lanzando la tarjeta «M1911 · Cargador x/7 · Reserva y» tras recargar o recoger; con el panel nuevo es redundante. Se puede quitar si se prefiere.
- **Carlos — retrato**: añadir `Assets/_Project/Resources/Elena/portrait.png` (cuadrado, ≥ 128 px, Texture Type Default) cuando exista.
- **Arma deshabilitada**: si en el futuro `GameSession.HasWeapon` es false, el panel seguirá mostrando el último estado (hoy siempre es true).
