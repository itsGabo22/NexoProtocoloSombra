# Handoff: pulido final para la demo y la entrega

- **Fecha:** 2026-11-03 (noche)
- **De:** Gabriel (agente de código; desde hoy también responsable de lo que era de Carlos)
- **Para:** Gabriel
- **Prioridad:** Alta (demo y entrega 2026-11-04)
- **Rama:** `feature/final-polish` desde `origin/develop` `d1d5571` (PR #10 de cinemáticas ya integrado)
- **Archivos afectados:** `Resources/Elena/portrait.png`, `Scripts/Editor/{NexoElenaPortraitGenerator,NexoVisualCapture,NexoReleaseBuilder,NexoPlaythroughCheck}.cs`, `Scripts/Player/ElenaVoiceInput.cs`, `Scripts/UI/CameraNetworkController.cs`, `Scripts/UI/Menus/OptionsController.cs`, `Scripts/AI/Elena/ElenaAIService.cs`, `UI/Styles/{components,camera-network}.uss`, `UI/Layouts/{HUD_Biometric.uxml,Menus/Credits.uxml}`, `Prefabs/Systems/PF_Player.prefab` (1 línea), `Scenes/Floors/Floor12_Offices.unity` (2 líneas de RenderSettings), `ProjectSettings/ProjectSettings.asset`, docs.

## Necesidad

Dejar el juego listo para la demo en clase y para la entrega con la máxima automatización: Elena con retrato, sin defectos visibles, legible en un proyector, créditos sin afirmaciones sin probar, build de entrega reproducible (Elena offline + atajos de demo solo en el build) y documentación al día. Los vídeos reales y el build final quedan para el último paso.

## Auditoría (paso 0)

| Elemento | Estado encontrado | Acción |
| --- | --- | --- |
| Git | `develop` = `d1d5571` (incluye PR #9 y #10); `main` en `64b05cf`; skip-worktree en manifest/lock sin cambios locales; stash antiguo `gabo-local-noise-2026-11-01` intacto | Rama desde `origin/develop` |
| Base | Compila; EditMode 190/190 | — |
| Elena física | Voz y retrato en todas las escenas (radio de las gafas). La biblia preveía verla tras un cristal (Piso 12 / esclusa del Piso 1), pero nunca se construyó y el Piso 1 del giro no se toca. El único humanoide (Ch33) es Alex, adulto | **No se añade modelo**; solo retrato |
| `[verificar]` en ASSETS.md | Office pack (autor/enlace), zombi (todo), clips `.anim`, 3 WAV de `752cb8f` | Investigado; ver «Créditos» |
| Player Settings | `DefaultCompany`, `NexoProtocoloSombra`, pantalla completa ventana nativa, sin icono, Mono, D3D11 + D3D12, `runInBackground` 0, splash de Unity | Ajustados (ver abajo) |
| Calidad | Por defecto Ultra en Standalone (4 luces por píxel, sombras suaves) | Sin cambio |
| Luces | Piso 12: 23 activas, 22 tiempo real, 4 con sombra (≤ 1 cerca de la cámara). Piso 6: 19 / 19 / 1. Piso 1: 26 / 26 / 1. Más la linterna (sombras suaves) | Sin cambio de sombras: nunca más de 1 cerca del jugador |
| Secretos | 408 commits de todas las ramas, el stash y el árbol de trabajo: ninguna clave (`AIza…`, `sk-`, `ghp_`, `Bearer`, `_apiKey`, `"api_key"`). Nunca se versionó `ElenaSecrets.asset` | Nada que reportar |
| `.gitignore` | Cubre Builds, Logs, Library, Temp, UserSettings, packs, `StreamingAssets/Cinematics`, Secrets y `ElenaSecrets.asset`. Lo más grande versionado: 2 FBX de Mixamo (52 MB, en LFS) | Sin cambio |

## Qué cambia

| Área | Cambio |
| --- | --- |
| Retrato de Elena | `Nexo › Elena › Generate Portrait` genera un busto infantil holográfico (pelo largo, anillo con marcas, líneas de barrido, sin rostro realista) blanco sobre transparente, 256 px; el USS lo tiñe con `var(--c-accent)` (azul, rojo colmena, alto contraste y daltonismo) |
| Red de cámaras | La pestaña del piso actual dibujaba «PISO 6» y «● AQUÍ» superpuestos; el fondo dejaba ver la terminal. Ahora son dos etiquetas en fila y fondo opaco |
| Linterna | `RenderMode` Auto → Important en `PF_Player`: con 22-26 luces por piso y 4 luces por píxel se degradaba a luz por vértice. Intensidad, alcance y ángulo de Carlos intactos |
| Piso 12 | Solo RenderSettings: ambiente plano 0,055/0,06/0,075 → 0,12/0,13/0,16, niebla 0,055 → 0,045 |
| Voz [V] | Ningún fallo lanza excepciones (crear/arrancar/parar el dictado, micrófono, transcripción async). Todo fallo muestra un subtítulo que remite a [1]-[3]; en el build sin conexión avisa al pulsar en vez de grabar |
| Pregunta [2] | «Abre la puerta norte» (no existe) → «Abre la puerta de las escaleras» (HUD y texto enviado a Gemini) |
| Opciones › Video | Resolución y modo de pantalla se aplican (`Screen.SetResolution`) y reflejan el estado real |
| Build | `Nexo › Build › Windows (entrega: Elena offline + atajos de demo)`: `_forceFallback = true`, `_secrets = null` y `EnableDemoShortcuts = true` solo durante el build; en `finally` reescribe los bytes originales de `ElenaConfig.asset` y `PF_Player.prefab`. Copia en `Temp/NexoReleaseBackup` por si el Editor se cae. Variante sin atajos, flags de batch y lista de cinemáticas incluidas |
| Player Settings | Producto «NEXO - PROTOCOLO SOMBRA» (con «:» la ruta de `Player.log` en LocalLow no es válida en Windows), compañía «Equipo NEXO», ventana 1920×1080 redimensionable por defecto, Alt+Intro activo, `runInBackground` 1. Mono, APIs gráficas y splash sin tocar; no hay diálogo de resolución en Unity 6 |
| Créditos | «EFECTOS DE SONIDO» ya no da a entender que todo el audio es procedural: la música y el ambiente del Piso 12 son de la plantilla de Unity Learn (bloque «LOCOMOCIÓN, MÚSICA Y AMBIENTE»). Nuevos: «MODELOS Y ANIMACIONES ADICIONALES: Unity Asset Store y Mixamo» y «RETRATO DE ELENA: Avatar holográfico generado en Unity» |
| Comprobaciones | Playthrough pregunta [1]-[3] a Elena en los 3 pisos (la respuesta debe salir del JSON de ese piso). Nueva `Nexo › Checks › Visual Capture` |
| Docs | `README.md` reescrito, `docs/LEEME_EJECUCION.md` nuevo, `DEMO_SCRIPT.md` (10-12 min, plan de atajos y contingencias), `ASSETS.md`, `CLAUDE.md` (§4 y §8) |

## Créditos: qué se probó

- **Office:** el `Read me.txt` dice «polygon office building» y solo da un correo. Coincide por nombre con la ficha gratuita *POLYGON office building* (Asset Store, id 82282, «255 pixel studios»), sin prueba en el pack → `[sin confirmar]`; el crédito usa el nombre del propio readme.
- **Zombi:** sin readme ni metadatos. El nombre encaja con la serie «Zombie Male AA_» de Studio New Punch, pero no hay ficha «AAB» → `[sin confirmar]`; crédito genérico.
- **Clips `.anim` de infectados y Elena:** creados en el proyecto (curvas cortas sin FBX de origen) → propios.
- **WAV de `752cb8f`:** misma firma que los procedurales confirmados → propios, pendiente de que Carlos lo confirme.
- **Hallazgo:** el Piso 12 usa `Music_Suspenseful.ogg` y `SFX_AmbienceClose.ogg` de la plantilla de Unity Learn (Unity Companion License).

## Verificación

| Qué | Cómo | Resultado |
| --- | --- | --- |
| Compilación + EditMode | batchmode `-runTests` con el código final | **190/190**, 0 errores de compilación |
| Integridad | `NexoIntegrityCheck.RunBatch` con el código final | **0 problemas** (4 escenas, 1855 objetos, 8 prefabs, 70 materiales) |
| Recorrido completo | `NexoPlaythroughCheck.Run` (GPU) con clips de prueba generados con ffmpeg (borrados después) | **223/223**, 0 errores de consola, incluidas las 9 preguntas de Elena (12/6/1, todas del fallback de su piso). Se ejecutó a las 22:56, **antes** de los commits de voz, opciones, builder y etiqueta [2]; la repetición final se interrumpió por falta de memoria del sistema |
| Capturas | `NexoVisualCapture.Run` (GPU) tras los cambios de luz, red de cámaras y créditos | 0 errores de consola; terminales cerradas con control y cursor restaurados en los 3 pisos |
| Build | `BuildBatch -nexoFolder NexoTestBuild_Agent` | Succeeded, 0 errores, 249,6 MB, 57 s; valores del build `_forceFallback=True`, `EnableDemoShortcuts=True`; 3 clips copiados; zip 105 MB; `ElenaConfig.asset` y `PF_Player.prefab` restaurados (git limpio). Arranque headless del `.exe` 30 s sin excepciones («modo contingencia»). Carpeta de prueba borrada; el build de las 08:10 no se tocó |

Luminancia media (luma 0-255, linterna apagada; antes → después del cambio del Piso 12):

| Vista | Antes | Después |
| --- | --- | --- |
| Piso 12 spawn | 14,6 (89 % casi negro) | 21,8 (59 %) |
| Piso 12 terminal / despacho N1 | 9,5 / 8,2 | 16,0 / 14,2 (con linterna 19,2 / 17,5: ahora ilumina la mesa) |
| Piso 12 salida / notas 2-3 | 22,3 / 24,8 / 24,3 | 25,0 / 31,9 / 30,8 |
| Piso 12 cámara CAM 01 (terminal) | 19,0 | 27,9 |
| Piso 6 spawn / terminal / diferencial / salida | 70,6 / 49,2 / 77,6 / 31,6 | sin cambio (no se tocó) |
| Piso 1 spawn / BioScanner / recepción | 26,6 / 58,5 / 34,9 | sin cambio (no se tocó) |

Lab B del Piso 6 (≈ 21) y la sala de descanso del Piso 1 (≈ 20) quedan oscuros a propósito (apagón y reptante). Capturas en `Logs/VisualCapture/` (no versionadas). Nota menor: el Piso 1 tiene dos terminales y la herramienta guarda ambas como `f01_terminal` (la segunda sobrescribe la primera).

## Lo que queda manual

- Repetir **Nexo › Checks › Playthrough** con el Editor cerrado y memoria libre (la última pasada completa, 223/223, es anterior a los commits de voz, opciones, builder y etiqueta [2]).

- Jugar 12 → 6 → 1 → créditos con teclado y ratón: sensación del combate, ritmo, sustos y la oscuridad en el proyector real.
- Voz [V] con micrófono real y dictado de Windows; Gemini real en el Editor (con `ElenaSecrets.asset`).
- Los vídeos reales: copiarlos, **Nexo › Cinematics › Validate**, verlos en el monitor de la presentación.
- Build final: **Nexo › Build › Windows (entrega: Elena offline + atajos de demo)** con los vídeos ya copiados; después `git status` debe estar limpio.
- Opcional: icono del ejecutable (no hay ninguno; el retrato de Elena podría servir).

## Criterio de aceptación

- [x] Compila sin errores y todos los EditMode en verde.
- [x] Integridad y recorrido completo sin fallos.
- [x] Sin vídeos, packs, secretos, logs ni restos de Unity AI en Git; `_forceFallback` y `EnableDemoShortcuts` en false en el repo.
- [x] `Floor01_Lobby.unity`, `Systems_Persistent.unity` y `ElenaConfig.asset` sin cambios frente a `origin/develop`.
