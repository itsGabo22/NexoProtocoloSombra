# Registro de assets — NEXO: PROTOCOLO SOMBRA

**Responsable:** Carlos.

Este registro documenta todo asset externo necesario para abrir y compilar el proyecto. Los packs completos no se suben a GitHub: se sincronizan por Drive dentro de `Assets/_ThirdPartyAssets/`, incluyendo sus `.meta`.

## Registro

| Pack / archivo | Uso | Fuente | Licencia | Versión de Drive | Responsable | Estado |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `Pack_Nokobot_Guns` | Modelo y prefab M1911 Handgun (piso 12) | Unity Asset Store (Nokobot Modern Guns) | Standard Unity Asset Store EULA | v1.0 (Oct 30) | Carlos | Sincronizado en `_ThirdPartyAssets/` |
| `Pack_OfficeBuilding` | Módulos, muros, puertas y props de oficina (piso 12) | Unity Asset Store (Modular Office Building) | Standard Unity Asset Store EULA | v1.0 (Oct 30) | Carlos | Sincronizado en `_ThirdPartyAssets/` |
| `Pack_UnityLearnTemplate` | Skyboxes, audio base y VFX del prototipo | Unity Technologies | Unity Companion License | v1.0 (Oct 30) | Carlos | Sincronizado en `_ThirdPartyAssets/` |

## Protocolo obligatorio para packs

1. Carlos importa el pack una sola vez en su proyecto.
2. Comprime la carpeta `Pack_<nombre>` completa, **con todos sus `.meta`**.
3. Sube ese paquete a Drive y registra aquí fuente, licencia y versión.
4. Gabriel cierra Unity y copia la carpeta en `Assets/_ThirdPartyAssets/`.
5. Gabriel no vuelve a importar el pack ni modifica sus GUIDs.
6. Después del día 2 no se altera el contenido interno de un pack ya compartido.

## Assets propios curados

Solo los modelos, texturas y audio propios que terminan en el build se versionan con Git LFS, dentro de `Assets/_Project/`. Objetivo total orientativo: menos de 500 MB.
- `Assets/_Project/Models/Characters/Ch33_nonPBR1.fbx` (Personaje Alex / Mixamo)
- `Assets/_Project/Animations/Clips/Ch33_nonPBR@Pistol Walk Backward.fbx` (Animación de retroceso)
- `Assets/_Project/Animations/Controllers/AC_Alex.controller` (Animator Controller)
- `Assets/_Project/Scenes/Floors/Floor12_Offices.unity` (Escena Piso 12 de Carlos)

## Fuentes y licencias pendientes

- [ ] Modelo infantil de Elena: registrar licencia y atribución antes de integrarlo.
- [ ] Animaciones de Mixamo: registrar cuenta, personaje y clips utilizados.
- [ ] Fuente monoespaciada del HUD: registrar licencia.
- [ ] Fuente corrupta para Mimicry: registrar licencia.
- [ ] Packs de laboratorio (Piso 6) y lobby (Piso 1): registrar enlaces y licencias.
