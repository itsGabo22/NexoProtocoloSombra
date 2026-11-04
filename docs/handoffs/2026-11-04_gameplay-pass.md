# Handoff: pase de jugabilidad (playtest 2026-11-04)

- **Fecha:** 2026-11-04 · **Rama:** `feature/gameplay-pass` desde `origin/develop` `dca7f9a` · **Para:** Gabriel
- **Estado:** Partes 1-7 hechas y subidas. **Partes 8 y 9 PENDIENTES** (por petición, para llegar a la demo).

## Hecho

| Parte | Resumen |
| --- | --- |
| 1 | Hueco del Piso 12: umbral de los ascensores (x 2,4-5,4; z ≈ -14,1, caída de 22,8 m por el hueco) sin collider → losa invisible `F12_GameplayPass/F12_FallSafety/Patch_ElevatorThreshold`. `Nexo › Checks › Floor Holes` da 0 zonas en los 3 pisos. `PlayerCheckpoints`: caída por debajo del piso → fundido, reaparece en el último punto de control o suelo seguro, −1 vida, «Punto de control restaurado» (si era la última vida, captura normal). 3 `CheckpointTrigger` y 2 botiquines por piso (Piso 1 fuera de 10 m del BioScanner). Salto [Espacio], mirada ±70°/±35° en escondites, botiquín (+1 vida, también soltado por infectados al 35 %), cabeza letal / 3 al cuerpo / reptante 2, marca de impacto. Aviso de `AC_Alex`: inofensivo (controlador sin parámetros), silenciado. |
| 2 | Tiempos de lectura (clamp(1,4 + 0,055·c, 3,5, 11) s, o voz + 0,8 s), cola de subtítulos con prioridad para daño/agarre, notificaciones según su texto, opción «Velocidad de lectura» (Normal/Lento). |
| 3 | 51/51 líneas fijas de Elena con voz: 28 Gemini TTS (voz Leda) y 23 con la voz de Windows (Helena, tono subido) porque la cuota gratuita de TTS se agotó (~10 peticiones/día por modelo). `ElenaVoicePlayer` con filtro de radio, pausa y silencio en cinemáticas. Créditos: «Voz de Elena». |
| 4 | Modelos editables (diálogo/STT/TTS), `gemini-3.8-flash` + `thinkingLevel: low` (minimal → 400; thinkingBudget 0 → 400 en 3.x), reserva `gemini-3.5-flash-lite` dentro del mismo plazo de 5 s, filtro de spoilers, [T] texto a Elena, mensaje de micrófono accionable, build «IA real con respaldo» / «Elena offline». |
| 5 | Empujón [G] (1,5 s, desequilibra 1,2 s, rompe el agarre), espacio personal anti-acorralamiento, cuchillo [X] procedural (botín del `InfectedSpawn` del Piso 12; 2 de daño, ejecución por la espalda), estado en el panel del arma. |
| 6 | Notas como papel a pantalla completa (IBM Plex Mono, sonido de papel, [E]/[Esc]), resalte al mirarlas, diario [J]. |
| 7 | `PF_ElenaVisual` (Ch33 a 1,30 m, tinte y melena) tras el cristal del Piso 12; desaparece al salir de la sala. Primitivas azules ocultas. Ver `docs/ELENA_VISUAL.md`. Piso 6: no se añadió figura en cámaras. |

## Actualización 2026-11-04 (Carlos, rama `feature/final-verification`)

- **Parte 9: HECHA.** `Nexo › Checks › Playthrough` tiene una sección nueva, «Mecánicas nuevas (Piso 12)»: aviso de controles, voces 51/51, figura de Elena solo en el Piso 12 y cápsula azul oculta, nota a pantalla completa y diario sin duplicados, [T] texto a Elena (offline), cola de mensajes con prioridad para el daño, voz con filtro de radio, caída → punto de control y −1 vida, botiquín (+1, no se gasta con la vida llena), empujón (aparta, enfriamiento, rompe el agarre), cuchillo (botín del patrullero, recogida, ejecución por la espalda, panel de munición intacto) y salto. Además, límites de mirada en el escondite del Piso 6. Recorrido completo en verde (ver el informe del PR). `LEEME_EJECUCION.md`, `DEMO_SCRIPT.md` y `ASSETS.md` están al día.
- Arreglado en el check: el clip corrupto creaba un archivo en `StreamingAssets/Cinematics` aunque la carpeta no existiera. Ahora la crea y la borra después.
- Voces: las 51 igualadas (`Nexo › Elena › Igualar voces`). Para regenerar las 23 de Windows: `docs/ELENA_VOICE_REGEN.md`.
- Alex y Elena con color: `Nexo › Characters › Aplicar color` (texturas de Ch33 extraídas; ropa de niña para Elena). `Rebuild Visual` lo conserva.

## PENDIENTE

- **Parte 8** (ambientación del Piso 6): sin empezar (se dejó fuera para no arriesgar la demo).
- Elena en las cámaras del Piso 6 (Parte 7, opcional).
- Repetir **Nexo › Elena › Generate Voice** cuando vuelva la cuota de Gemini: solo rehace las 23 líneas de Windows.
- Latencia «después» de `gemini-3.8-flash`: hoy devuelve 503/429 a esta clave en todas las pruebas; la reserva `gemini-3.5-flash-lite` respondió en 1,3-1,8 s (mediana) con el prompt real.

## Verificación

- EditMode **235/235**; Integridad **0 problemas**; Floor Holes 0 zonas. Recorrido completo (Playthrough) **no ejecutado** en esta pasada.
- Sin probar a mano: sensación del salto/empujón/cuchillo, audio por oído, micrófono, proyector.
