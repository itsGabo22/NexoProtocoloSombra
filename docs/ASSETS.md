# Registro de assets — NEXO: PROTOCOLO SOMBRA

**Responsables:** Carlos (packs del Piso 12, jugador, IA y audio) y Gabriel (UI, tipografías, pisos 6 y 1).

Este registro documenta todo asset externo necesario para abrir, compilar y acreditar el proyecto. Los packs completos **no** se suben a GitHub (el repo es público): se comparten en un zip y se extraen, con sus `.meta`, en `Assets/_ThirdPartyAssets/` o `Assets/ThirdPartyAssets/` (ambas rutas están en `.gitignore`; usa solo una para no duplicar GUIDs).

Reconciliado el 2026-11-03 contra los packs presentes en la máquina de Gabriel. Lo marcado `[verificar]` no tiene evidencia en el repo ni en los packs y **no** aparece en los créditos del juego.

## Packs de terceros (no versionados)

| Pack / carpeta | Uso | Fuente | Autor | Licencia | Estado en la máquina de Gabriel |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Pack_Nokobot_Guns` | Modelo M1911 del viewmodel | Unity Asset Store: *Modern Guns: Handgun* v1.2 (`README.txt` del pack) | Nokobot | Standard Unity Asset Store EULA | Presente |
| `Pack_OfficeBuilding` | Edificio, puertas, ascensor y props del Piso 12 | Unity Asset Store [verificar enlace]. El `Read me.txt` lo llama «polygon office building» (antes se registró como «Modular Office Building»). No es *POLYGON Office* de Synty: no usa sus prefijos ni su estructura de carpetas | [verificar]: el `Read me.txt` solo da un correo de contacto, sin nombre | Standard Unity Asset Store EULA | Presente |
| `Pack_UnityLearnTemplate` | Skyboxes, audio base, VFX y animaciones de locomoción `Stand--Idle`, `Locomotion--Walk_N`, `Locomotion--Run_N` (carpeta `SourceFiles/StarterAssets/ThirdPersonController`) que usa `AC_SmartInfected` | Unity Technologies (plantilla de Unity Learn / Starter Assets) | Unity Technologies | Unity Companion License | Presente |
| `Pack_ZombieMale_AAB` | Modelo `ZombieModel` anidado en `PF_Infected_Variant.prefab` (GUID `2f6b5402acaac7e45a0b798395dd250b`) | [verificar] | [verificar] | [verificar] | **Falta**: no está en el zip `ThirdPartyAssets.zip` del 2026-10-31. Pedir a Carlos el zip actualizado |
| `Unused/Ch33_nonPBR.fbx` | Copia original del personaje Mixamo (no se usa en escenas) | Mixamo (Adobe) | Adobe | Términos de Mixamo: uso libre en proyectos, sin atribución obligatoria (se acredita igualmente) | Presente |

## Assets versionados en `Assets/_Project/`

| Archivo o carpeta | Origen | Licencia / nota |
| :--- | :--- | :--- |
| `Models/Characters/Ch33_nonPBR1.fbx` | Personaje Alex · Mixamo (Adobe) | Términos de Mixamo |
| `Animations/Clips/Ch33_nonPBR@Pistol Walk Backward.fbx` | Animación Mixamo (Adobe) | Términos de Mixamo |
| `Animations/Clips/Infected/*.anim`, `Animations/Clips/Elena/*.anim` | Clips añadidos por Carlos al proyecto [verificar origen de la captura] | [verificar] |
| `UI/Fonts/BarlowCondensed-*.ttf` | Barlow Condensed, The Barlow Project Authors | SIL Open Font License 1.1 (`UI/Fonts/OFL_BarlowCondensed.txt`) |
| `UI/Fonts/IBMPlexSans-Regular.ttf`, `UI/Fonts/IBMPlexMono-Medium.ttf` | IBM Plex, IBM Corp. | SIL Open Font License 1.1 (`UI/Fonts/OFL_IBMPlex.txt`) |
| `UI/Textures/T_ThermalVignette.png` | Generada por código en el Editor (2026-11-03) | Propia |
| `Audio/Ambience/SFX_F12_FluorescentHum.wav` | Sonido procedural creado para el proyecto (commit db9f112) | Propio |
| `Audio/Voices/SFX_Infected_Breath.wav`, `SFX_Mimic_Radio_01.wav`, `SFX_Mimic_Radio_02.wav` | Sonido procedural (commit db9f112) | Propio |
| `Audio/SFX/SFX_F12_DistantBang.wav`, `SFX_F12_MetalCreak.wav`, `SFX_F12_Whisper.wav` | Sonido procedural (commit f9f6d71) | Propio |
| `Audio/SFX/SFX_M1911_Shot.wav`, `SFX_M1911_Empty.wav`, `SFX_M1911_Reload.wav` | Sonido procedural (commit 030cbea) | Propio |
| `Audio/SFX/SFX_F12_ContainmentSiren.wav`, `SFX_F12_PhoneRing.wav`, `SFX_F12_TapeRecorder.wav` | Sonido procedural (commit 752cb8f) [verificar: el mensaje no dice «procedural»] | Propio [verificar] |
| `Data/ElenaConfig.asset` → `gemini-2.5-flash` | Diálogos de Elena generados en tiempo real por Gemini 2.5 Flash (Google); sin conexión se usan los JSON de fallback escritos por el equipo | Términos de la API de Gemini |

## Protocolo para actualizar packs

1. Quien importa el pack lo hace una sola vez en su proyecto y registra aquí fuente, autor, versión y licencia.
2. Comprime la carpeta `Pack_<nombre>` completa, **con todos sus `.meta`**, y compártela (Drive/zip).
3. El otro miembro cierra Unity y extrae la carpeta dentro de la carpeta de packs que ya use (`_ThirdPartyAssets/` o `ThirdPartyAssets/`), sin duplicarla.
4. Nadie vuelve a importar el pack desde la Asset Store ni modifica sus GUIDs.
5. Los packs nunca se versionan con Git (ni con LFS).

## Pendientes

- [ ] `Pack_ZombieMale_AAB`: fuente, autor y licencia, y compartir el pack.
- [ ] `Pack_OfficeBuilding`: autor y enlace exacto de la Asset Store.
- [ ] Confirmar el origen de los tres clips de 752cb8f.
