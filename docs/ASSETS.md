# Registro de assets — NEXO: PROTOCOLO SOMBRA

**Responsables:** Carlos (packs del Piso 12, jugador, IA y audio) y Gabriel (UI, tipografías, pisos 6 y 1).

Este registro documenta todo asset externo necesario para abrir, compilar y acreditar el proyecto. Los packs completos **no** se suben a GitHub (el repo es público): se comparten en un zip y se extraen, con sus `.meta`, en `Assets/_ThirdPartyAssets/` o `Assets/ThirdPartyAssets/` (ambas rutas están en `.gitignore`; usa solo una para no duplicar GUIDs).

Reconciliado el 2026-11-03 (noche) contra los packs presentes en la máquina de Gabriel, sus `Read me`, los metadatos de los archivos, el historial de Git y búsquedas web por el nombre exacto. Lo marcado `[sin confirmar]` no tiene prueba suficiente y en los créditos del juego solo aparece de forma genérica («Modelos y animaciones adicionales: Unity Asset Store y Mixamo»), nunca como un autor concreto.

## Packs de terceros (no versionados)

| Pack / carpeta | Uso | Fuente | Autor | Licencia | Estado en la máquina de Gabriel |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Pack_Nokobot_Guns` | Modelo M1911 del viewmodel | Unity Asset Store: *Modern Guns: Handgun* v1.2 (`README.txt` del pack) | Nokobot | Standard Unity Asset Store EULA | Presente |
| `Pack_OfficeBuilding` | Edificio, puertas, ascensor y props del Piso 12 | Probado: el `Read me.txt` dice «Thanks for buying the polygon office building» (sin autor; solo un correo de contacto). [sin confirmar] coincide por nombre con la ficha gratuita *POLYGON office building* de la Unity Asset Store (id 82282, v1.0, 2018, editor «255 pixel studios»); el pack no trae metadatos que lo enlacen. No es *POLYGON Office* de Synty | [sin confirmar] | Standard Unity Asset Store EULA (la de cualquier ficha de la Asset Store) | Presente |
| `Pack_UnityLearnTemplate` | Skyboxes, VFX, animaciones de locomoción `Stand--Idle`, `Locomotion--Walk_N`, `Locomotion--Run_N` (carpeta `SourceFiles/StarterAssets/ThirdPersonController`) que usa `AC_SmartInfected`, y **audio del Piso 12**: `SourceFiles/SoundFX/Music_Suspenseful.ogg` y `SFX_AmbienceClose.ogg` (referenciados en `Floor12_Offices.unity`, comprobado por GUID el 2026-11-03) | Unity Technologies (plantilla de Unity Learn / Starter Assets) | Unity Technologies | Unity Companion License | Presente |
| `Pack_ZombieMale_AAB` | Modelo `ZombieModel` anidado en `PF_Infected_Variant.prefab` (GUID `2f6b5402acaac7e45a0b798395dd250b`) | [sin confirmar]: sin readme, licencia ni metadatos de autor (solo `ZombieMale_AAB.fbx`, `_BodyParts.fbx`, materiales HDRP/URP/Standard y una escena). El nombre «Zombie Male AA_» coincide con la serie de *Studio New Punch* (fichas «Zombie Male AAA/AAC» para Unity y Unreal), pero no se encontró una ficha «AAB» | [sin confirmar] | [sin confirmar] | Presente (comprobado el 2026-11-03 en `Assets/ThirdPartyAssets/`) |
| `Unused/Ch33_nonPBR.fbx` | Copia original del personaje Mixamo (no se usa en escenas) | Mixamo (Adobe) | Adobe | Términos de Mixamo: uso libre en proyectos, sin atribución obligatoria (se acredita igualmente) | Presente |

## Assets versionados en `Assets/_Project/`

| Archivo o carpeta | Origen | Licencia / nota |
| :--- | :--- | :--- |
| `Models/Characters/Ch33_nonPBR1.fbx` | Personaje Alex · Mixamo (Adobe) | Términos de Mixamo |
| `Models/Characters/Ch33_Textures/Ch33_Diffuse.jpg`, `Ch33_Normal.png`, `Ch33_Hair.png` | Texturas incrustadas en `Ch33_nonPBR1.fbx` (Mixamo), extraídas y reducidas por `Scripts/Editor/NexoCharacterColors.cs` (2026-11-04) | Términos de Mixamo |
| `Materials/Characters/MAT_Alex_*.mat`, `MAT_Elena_*.mat` | Materiales Standard de Alex y Elena creados por `NexoCharacterColors.cs` (Nexo › Characters › Aplicar color) | Propio |
| `Prefabs/Characters/PF_ElenaVisual.prefab` | Figura de Elena tras el cristal del Piso 12: Ch33 (Mixamo) escalado a 1,30 m, `NexoElenaVisualBuilder.cs` | Términos de Mixamo + propio |
| `Audio/Elena/Voice/elena_*.wav` (51) + `voice_manifest.json` | 28 líneas con Gemini TTS (Google, voz Leda) y 23 con la voz de Windows (Microsoft Helena), igualadas con `NexoElenaVoiceLeveler.cs` (24 kHz, −18 dBFS RMS). Ver `docs/ELENA_VOICE_REGEN.md` | Términos de la API de Gemini · voz del sistema Windows |
| `Prefabs/Interactables/PF_KnifePickup.prefab`, `PF_Medkit.prefab` | Geometría procedural de `NexoGameplayPassBuilder.cs` | Propio |
| `Animations/Clips/Ch33_nonPBR@Pistol Walk Backward.fbx` | Animación Mixamo (Adobe) | Términos de Mixamo |
| `Animations/Clips/Infected/*.anim`, `Animations/Clips/Elena/*.anim` | Creados dentro del proyecto como `.anim` de Unity con fotogramas clave (commits `3bb3d22`, `40f3a7a`, `760eec1`, `decfb8a` de Carlos, 2026-11-02): no tienen FBX de origen y son curvas cortas (p. ej. `Elena_Idle`: 14 curvas, 28 claves; `Zombie_Walk`: 60 curvas, 134 claves), no capturas de movimiento importadas | Propio |
| `UI/Fonts/BarlowCondensed-*.ttf` | Barlow Condensed, The Barlow Project Authors | SIL Open Font License 1.1 (`UI/Fonts/OFL_BarlowCondensed.txt`) |
| `UI/Fonts/IBMPlexSans-Regular.ttf`, `UI/Fonts/IBMPlexMono-Medium.ttf` | IBM Plex, IBM Corp. | SIL Open Font License 1.1 (`UI/Fonts/OFL_IBMPlex.txt`) |
| `UI/Textures/T_ThermalVignette.png` | Generada por código en el Editor (2026-11-03) | Propia |
| `Resources/Elena/portrait.png` | Retrato holográfico de Elena generado por `Scripts/Editor/NexoElenaPortraitGenerator.cs` (menú Nexo › Elena › Generate Portrait, determinista; 2026-11-03). Créditos: «Retrato de Elena: avatar holográfico generado en Unity» | Propio |
| `Audio/Ambience/SFX_F12_FluorescentHum.wav` | Sonido procedural creado para el proyecto (commit db9f112) | Propio |
| `Audio/Voices/SFX_Infected_Breath.wav`, `SFX_Mimic_Radio_01.wav`, `SFX_Mimic_Radio_02.wav` | Sonido procedural (commit db9f112) | Propio |
| `Audio/SFX/SFX_F12_DistantBang.wav`, `SFX_F12_MetalCreak.wav`, `SFX_F12_Whisper.wav` | Sonido procedural (commit f9f6d71) | Propio |
| `Audio/SFX/SFX_M1911_Shot.wav`, `SFX_M1911_Empty.wav`, `SFX_M1911_Reload.wav` | Sonido procedural (commit 030cbea) | Propio |
| `Audio/SFX/SFX_F12_ContainmentSiren.wav`, `SFX_F12_PhoneRing.wav`, `SFX_F12_TapeRecorder.wav` | Commit `752cb8f` (Carlos). Misma firma que los clips procedurales confirmados (`db9f112`, `030cbea`, `f9f6d71`): WAV mono 44,1 kHz 16 bits con solo los bloques `fmt` y `data` (sin metadatos de ninguna librería) y duraciones exactas de 2,0 / 4,0 / 3,0 s. El mensaje del commit no dice «procedural» [sin confirmar por su autor] | Propio (se acreditan dentro de «Efectos procedurales creados para el proyecto») |
| `Audio/SFX/SFX_F06_Thunder_01/02.wav`, `SFX_F06_DoorKnocks.wav`, `SFX_F06_ObjectFall.wav`, `SFX_Infected_Step_01-03.wav`, `SFX_Infected_HitFlesh.wav`, `SFX_Infected_Pain.wav` | Síntesis propia con `Scripts/Editor/NexoProceduralAudioGenerator.cs` (menú Nexo/Audio, semilla fija) | Propio |
| `Audio/Ambience/SFX_F06_Breathing_Loop.wav`, `SFX_F06_ElectricHum_Loop.wav`, `SFX_F06_AlarmDistant_Loop.wav`, `SFX_F06_LabRoomTone_Loop.wav` | Síntesis propia con `NexoProceduralAudioGenerator.cs` | Propio |
| `UI/Textures/CameraArchive/CAM_*.png` | Capturas de las escenas propias del proyecto generadas con `Scripts/Editor/NexoCameraArchiveCapture.cs` | Propio |
| `Materials/Floor06/MAT_F06_*.mat` | Materiales Standard creados por `NexoFloor06Builder.cs` | Propio |
| `Materials/Floor01/MAT_F01_*.mat` | Materiales Standard creados por `NexoFloor01AmbienceBuilder.cs` | Propio |
| `Data/ElenaConfig.asset` → `gemini-2.5-flash` (reservas `gemini-3.5-flash-lite`, `gemini-3.8-flash`) | Diálogos de Elena y transcripción de la voz [V] generados en tiempo real por Gemini (Google); voz dinámica con Gemini TTS (`gemini-3.8-flash-lite-tts`, voz Leda); sin conexión se usan los JSON de fallback escritos por el equipo | Términos de la API de Gemini |

## Cinemáticas (no versionadas)

| Archivo | Origen | Licencia / nota |
| :--- | :--- | :--- |
| `Assets/StreamingAssets/Cinematics/intro.mp4`, `floor_transition.mp4`, `ending.mp4` (opcionales `floor_transition_to_06.mp4`, `floor_transition_to_01.mp4`) | Vídeos generados con IA por el equipo (Gemini y Veo, Google). Créditos: bloque «CINEMÁTICAS» | Términos de uso de Gemini / Veo. La carpeta está en `.gitignore`: se copian a mano antes del build (ver `docs/DEMO_SCRIPT.md`) |

## Protocolo para actualizar packs

1. Quien importa el pack lo hace una sola vez en su proyecto y registra aquí fuente, autor, versión y licencia.
2. Comprime la carpeta `Pack_<nombre>` completa, **con todos sus `.meta`**, y compártela (Drive/zip).
3. El otro miembro cierra Unity y extrae la carpeta dentro de la carpeta de packs que ya use (`_ThirdPartyAssets/` o `ThirdPartyAssets/`), sin duplicarla.
4. Nadie vuelve a importar el pack desde la Asset Store ni modifica sus GUIDs.
5. Los packs nunca se versionan con Git (ni con LFS).

## Créditos del juego (`UI/Layouts/Menus/Credits.uxml`)

Cada línea tiene su fuente arriba: equipo, materia, Gemini (2.5 Flash con reservas 3.5 Flash-Lite y 3.8 Flash), tipografías OFL, efectos procedurales propios, retrato generado en Unity, Nokobot M1911, Mixamo (Alex), «Modelos y animaciones adicionales: Unity Asset Store y Mixamo» (genérico a propósito: cubre el infectado), plantilla de Unity Learn (locomoción, música y ambiente), Polygon Office Building (nombre del propio `Read me`) y el bloque CINEMÁTICAS.

## Pendientes

- [ ] `Pack_ZombieMale_AAB`: que quien lo descargó confirme la ficha exacta (cuenta de la Asset Store → *My Assets*) y su licencia; compartir el pack.
- [ ] `Pack_OfficeBuilding`: confirmar en *My Assets* que es la ficha 82282 y su editor.
- [ ] Que Carlos confirme que los tres clips de `752cb8f` son procedurales.
