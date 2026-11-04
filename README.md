# NEXO: PROTOCOLO SOMBRA

Videojuego 3D de terror en primera persona hecho con **Unity 6 (6000.6.7f1), pipeline Built-in, C# y UI Toolkit**. Alex despierta en la torre NexoCorp y baja tres pisos guiado por Elena, una niña de 9 años que le habla por la radio de sus gafas. NexoOS, la interfaz de esas gafas, es a la vez el HUD y el narrador no fiable: dice «NO INFECTADO» hasta que el BioScanner del vestíbulo revela la verdad.

**Equipo:** Gabriel Esteban Paz Guerrero (`itsGabo22`) — arquitectura C#, IA de Elena, UI Toolkit, narrativa y pisos 6 y 1 · Carlos Julian Benavides Burbano (`simviz12`) — Piso 12, jugador, combate, IA de infectados y audio.
**Materia:** Diseño de Interfaces · Ingeniería de Software, 5.º semestre. **Entrega y demo:** 4 de noviembre de 2026.

## Qué hay en el juego

- **3 pisos** cargados de forma aditiva sobre `Scenes/Persistent/Systems_Persistent.unity`: Piso 12 (oficinas, combate), Piso 6 (laboratorios, apagón, escondites, reptantes) y Piso 1 (vestíbulo, BioScanner y giro final) → créditos.
- **Elena con IA real (Gemini)**: ver la sección siguiente. Retrato holográfico generado en Unity.
- **HUD biométrico NexoOS**, notificaciones reales/falsas, terminales de seguridad y red de cámaras de 3 pisos [C], visión térmica [F].
- **Accesibilidad:** escala de UI, alto contraste, daltonismo (Okabe-Ito), reducir movimiento y subtítulos.
- **Cinemáticas** opcionales (`intro`, `floor_transition`, `ending`) en `Assets/StreamingAssets/Cinematics/` (no versionadas); si faltan, el juego sigue sin ellas.

## IA: Elena con Gemini

- **Conversación libre** con Elena por **voz [V]**, **texto [T]** o **preguntas rápidas [1][2][3]**. Gemini genera cada respuesta en vivo con un system prompt de personaje (niña de 9 años que oculta un secreto), el **contexto real del piso** (sala, infectados cerca, ruido, batería, puertas disponibles) y salida **JSON estricta** (diálogo, emoción, acción de UI, confianza, interferencia).
- **Cadena robusta:** `gemini-2.5-flash` → `gemini-3.5-flash-lite` → `gemini-3.8-flash` (4,5 s por modelo, 8 s en total) → respaldo offline por piso → línea de emergencia. Elena nunca se queda callada ni bloquea el juego.
- **Marcador visible** bajo cada respuesta: «IA: Gemini» o «IA: respaldo».
- **Voz [V]:** micrófono grabado con Unity y transcrito por Gemini en español (máx. 8 s, estados ESCUCHANDO / PROCESANDO); el dictado de Windows queda como alternativa.
- **Seguridad:** filtro anti-spoilers y anti «salir del personaje», y las acciones de Elena (abrir puertas, radar) solo se ejecutan si son válidas en ese piso.
- Detalle técnico, modelos, latencias medidas y limitaciones: [`docs/ELENA_AI.md`](docs/ELENA_AI.md). Prueba en vivo: **Nexo › Checks › Elena Live (Gemini)**.

Cómo ejecutar el build: [`docs/LEEME_EJECUCION.md`](docs/LEEME_EJECUCION.md). Guion de la demo: [`docs/DEMO_SCRIPT.md`](docs/DEMO_SCRIPT.md). Créditos y licencias: [`docs/ASSETS.md`](docs/ASSETS.md) y la pantalla de créditos del juego.

## Abrir el proyecto

```bash
git clone https://github.com/itsGabo22/NexoProtocoloSombra.git
cd NexoProtocoloSombra
git lfs install
git lfs pull            # modelos, PNG y audio propios están en Git LFS
git config core.autocrlf false
```

1. **Packs de terceros** (no están en Git, el repo es público): pide el zip al equipo y extráelo **con Unity cerrado y con sus `.meta`** en `Assets/ThirdPartyAssets/` (o `Assets/_ThirdPartyAssets/`; usa solo una). Packs: `Pack_Nokobot_Guns`, `Pack_OfficeBuilding`, `Pack_UnityLearnTemplate` y `Pack_ZombieMale_AAB`. Si falta alguno, Unity muestra «Missing Nested Prefab»: no «arregles» el prefab, consigue el pack.
2. Abre la carpeta en Unity Hub con **exactamente 6000.6.7f1** y espera a que restaure los paquetes.
3. Escena de arranque: `Assets/_Project/Scenes/Persistent/Systems_Persistent.unity` → Play.
4. **Elena con Gemini (opcional):** crea `ElenaSecrets.asset` en `Assets/_Project/Data/Secrets/` (carpeta ignorada) y asígnalo en `Data/ElenaConfig.asset` **sin hacer commit de ese enlace** (ver `docs/ELENA_AI.md`). Sin clave, Elena usa el fallback offline sin errores.
5. Pipeline **Built-in** con materiales `Standard`: no asignes un asset URP (los materiales URP/Lit se ven rosas).

## Herramientas del Editor (menú Nexo)

| Menú | Qué hace |
| --- | --- |
| Nexo › Checks › Integrity | Scripts que faltan, shaders rosas, materiales vacíos, AudioClip rotos (`Logs/NexoIntegrityCheck.txt`) |
| Nexo › Checks › Playthrough | Recorrido automático completo 12 → 6 → 1 → giro → créditos → menú, incluidas las preguntas [1]-[3] de Elena en cada piso (`Logs/NexoPlaythroughCheck.txt`) |
| Nexo › Checks › Visual Capture | Capturas de cada piso (linterna on/off), luminancia, terminales y UI real (`Logs/VisualCapture/`) |
| Nexo › Checks › Elena Live (Gemini) | Pregunta a Gemini en los 3 pisos, fuerza modelo inválido y sin clave; origen y latencia en `Logs/NexoElenaLiveCheck.txt` (nunca registra la clave) |
| Nexo › Cinematics › Validate | Lista y valida los vídeos de `StreamingAssets/Cinematics` |
| Nexo › Elena › Generate Portrait | Regenera `Resources/Elena/portrait.png` |
| Nexo › Build › Windows (entrega: Elena offline / IA real con respaldo) | Builds de entrega (ver `docs/LEEME_EJECUCION.md`); restauran todo al terminar |

## Controles

| Acción | Teclado/ratón | Mando |
| --- | --- | --- |
| Moverse / correr | WASD / Shift izquierdo | Stick izquierdo / presionarlo |
| Mirar | Ratón | Stick derecho |
| Agacharse | Ctrl izquierdo o C | — |
| Interactuar | E | Botón oeste |
| Linterna / visión térmica | F | — |
| Disparar / apuntar / recargar | Clic izquierdo / clic derecho / R | Gatillo derecho / izquierdo / botón oeste |
| Saltar | Espacio | — |
| Forcejeo (cuando te agarran) | Espacio (G rompe el agarre) | Botón sur |
| Empujar / cuchillo | G / X | — |
| Diario de notas | J | — |
| Escribir a Elena | T | — |
| Preguntas rápidas a Elena | 1, 2, 3 | — |
| Hablar con Elena (voz, máx. 8 s) | Mantener V | Botón norte |
| Rueda de órdenes | Mantener Q | Hombro izquierdo |
| Red de cámaras (con una terminal abierta) | C | — |
| Ver objetivo | Tab | Select |
| Pausa | Esc o P | Start |
| Pantalla completa / ventana | Alt+Intro u Opciones › Video | — |

## Flujo Git

- Ramas de trabajo desde `origin/develop`; PR a `develop` con **merge commit** (nunca squash ni force-push). `main` solo recibe hitos.
- Conventional Commits en inglés y minúsculas; `git add <archivo> <archivo>.meta` (nunca `git add .`).
- Nunca se versionan secretos, builds, logs, packs de terceros ni los vídeos de las cinemáticas.
- Guía completa para agentes y personas: [`CLAUDE.md`](CLAUDE.md). Traspasos: `docs/handoffs/`.

## Créditos

Equipo NEXO (ver arriba). IA: Gemini y Gemini TTS (Google). Tipografías Barlow Condensed e IBM Plex (SIL OFL 1.1). Modelos, animaciones y sonido de packs de terceros (Unity Asset Store, Mixamo, Unity Learn) acreditados en la pantalla de créditos y en [`docs/ASSETS.md`](docs/ASSETS.md).

**No incluido en el repositorio (es público):** los packs de terceros (`Assets/ThirdPartyAssets/`), los vídeos de las cinemáticas (`Assets/StreamingAssets/Cinematics/`), los builds (`Builds/`) y cualquier clave (`Assets/_Project/Data/Secrets/`).
