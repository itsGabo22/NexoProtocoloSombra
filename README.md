# NEXO: PROTOCOLO SOMBRA

Videojuego 3D de terror en primera persona desarrollado con **Unity 6 LTS, URP, C# y UI Toolkit**. NexoOS, la interfaz de las gafas de Alex, actúa como sistema de ayuda y como narrador no confiable.

**Equipo:** Gabriel — arquitectura C#, IA, UI Toolkit y Git · Carlos — Unity Editor, niveles, animación y audio.  
**Entrega:** 15 de noviembre de 2026.  
**Estado actual:** estructura base del repositorio; todavía no contiene escenas, prefabs ni gameplay completo.

## Decisiones fijadas

- **Editor:** `6000.0.0f1`, declarado en `ProjectSettings/ProjectVersion.txt`. Ambos equipos deben usar exactamente la misma versión. Si deciden otra de la rama `6000.0.x`, actualizan ese archivo de forma coordinada.
- **Render:** URP 17; navegación con AI Navigation; entrada con Input System.
- **UI runtime:** exclusivamente UI Toolkit (`UIDocument`, UXML y USS), sin uGUI ni TextMeshPro.
- **Comunicación:** eventos C# mediante `GameEvents`; escenas aditivas.
- **IA de Elena:** proveedor remoto configurable con fallback local. Las claves nunca se versionan.
- **Assets:** packs completos por Drive; binarios propios curados con Git LFS.

## Qué incluye esta base

- Árbol de carpetas y separación de propiedad entre Gabriel y Carlos.
- Manifiesto de paquetes, capas iniciales, namespace raíz y serialización Force Text.
- Assembly definitions para runtime y pruebas EditMode.
- `NexoControls.inputactions` con teclado/ratón y mando.
- Utilidades USS, temas Nexo/Colmena y componentes visuales base.
- Reglas de Antigravity, CODEOWNERS, plantilla de PR y documentación del proyecto.
- La biblia técnica original en `docs/TECHNICAL_BIBLE.md`.

No incluye Library, builds, assets de terceros, secretos, escenas `.unity`, prefabs, modelos, audio ni implementación completa de sistemas. Unity generará los `.meta` al primer arranque; deben commitearse junto con sus archivos.

## Primer arranque

```bash
git clone https://github.com/itsGabo22/NexoProtocoloSombra.git
cd NexoProtocoloSombra
git lfs install
git lfs pull
git config core.autocrlf false
```

1. Añade la carpeta en Unity Hub y ábrela con la versión exacta indicada arriba.
2. Espera a que Unity restaure los paquetes de `Packages/manifest.json`.
3. Comprueba **Edit → Project Settings → Editor**: `Visible Meta Files` y `Force Text`.
4. URP está declarado como dependencia. En la primera configuración, Gabriel debe crear el asset URP con su renderer y asignarlo en **Graphics** y **Quality**; después se versionan esos ajustes para que Carlos no tenga que repetirlos.
5. Configura UnityYAMLMerge sustituyendo la ruta por tu instalación exacta:

```bash
git config merge.unityyamlmerge.name 'Unity SmartMerge'
git config merge.unityyamlmerge.driver 'C:/Program Files/Unity/Hub/Editor/6000.0.0f1/Editor/Data/Tools/UnityYAMLMerge.exe merge -p %O %B %A %P'
```

6. Abre y cierra Unity sin tocar contenido: `git status` solo debería mostrar los `.meta` iniciales y la configuración que Unity complete. Revisa esos cambios antes de commitearlos; no aceptes reserializaciones masivas inesperadas.

## Assets y secretos

- Copia los packs compartidos en `Assets/_ThirdPartyAssets/` **con Unity cerrado y conservando sus `.meta`**. Esta ruta está ignorada por Git.
- Registra cada pack y licencia en `docs/ASSETS.md`.
- Cuando se implemente Elena, crea `ElenaSecrets.asset` mediante el menú de ScriptableObjects en `Assets/_Project/Data/Secrets/`. Esa carpeta está ignorada; sin el asset, el sistema debe funcionar en fallback.
- No uses Git LFS para packs completos.

## Propiedad de carpetas

| Responsable | Rutas principales |
| --- | --- |
| Gabriel | `Scripts/`, `UI/`, `Data/`, `Input/`, `Prefabs/Systems/`, `Scenes/Persistent/`, `Scenes/Menus/`, `Scenes/Sandbox/`, `Packages/`, `ProjectSettings/` |
| Carlos | `Scenes/Floors/`, `Prefabs/Environment/`, `Prefabs/Characters/`, `Prefabs/Interactables/`, `Models/`, `Materials/`, `Animations/`, `Lighting/`, `Audio/`, `docs/ASSETS.md` |
| Ambos | `docs/handoffs/`, siempre con archivos nuevos por petición |

Una escena `.unity` tiene un único dueño. Todo cambio fuera de tu carpeta se solicita mediante una plantilla en `docs/handoffs/`.

**Antes de proteger ramas:** sustituye el propietario provisional de las rutas de Carlos en `.github/CODEOWNERS` por su usuario real de GitHub.

## Controles iniciales

| Acción | Teclado/ratón | Mando |
| --- | --- | --- |
| Movimiento | WASD | Stick izquierdo |
| Cámara | Ratón | Stick derecho |
| Correr | Shift izquierdo | Presionar stick izquierdo |
| Interactuar | E | Botón oeste |
| Visión térmica | F | Hombro derecho |
| Rueda de órdenes | Mantener Q | Mantener hombro izquierdo |
| Preguntas a Elena | 1, 2, 3 | Pendiente de diseño |
| Pausa / cancelar | Esc | Start |

## Flujo Git

- `main`: solo hitos demostrables etiquetados.
- `develop`: integración revisada.
- Ramas cortas: `feature/gabriel/<tema>` y `feature/carlos/<tema>`.
- PR a `develop` con **Rebase and merge** o **Merge commit**; nunca squash.
- Commits atómicos con Conventional Commits, en inglés y minúsculas.
- Añade rutas explícitas con `git add`; no uses `git add .`, `git add -A` ni `git push --force`.
- Cada archivo de Unity y su `.meta` van en el mismo commit.

Rutina diaria:

```bash
git checkout develop
git pull --rebase
git checkout -b feature/gabriel/<tema>
# Trabajo acotado, verificación y commit atómico.
git pull --rebase origin develop
git push -u origin feature/gabriel/<tema>
```

El backlog completo de 133 commits y los criterios de cada sprint están en `docs/TECHNICAL_BIBLE.md`.

## Primera publicación de esta base

Si el repositorio remoto está vacío y decides importar toda la base como un único commit inicial:

```bash
git add .gitignore .gitattributes .editorconfig README.md .antigravity/rules.md .github Packages ProjectSettings Assets docs
git commit -m 'chore(unity): create urp project skeleton and folder structure'
git branch -M main
git remote add origin https://github.com/itsGabo22/NexoProtocoloSombra.git
git push -u origin main
git checkout -b develop
git push -u origin develop
```

Si `origin` ya existe, omite la línea `git remote add`. Para seguir el desglose atómico de los commits 1–11 de la biblia, registra los archivos por unidades en lugar de usar el commit único anterior.

## Verificación antes de cada PR

1. Compilación sin errores nuevos en Unity.
2. Prueba real en Play Mode o EditMode del cambio entregado.
3. Sin secretos, builds, packs de terceros ni cambios fuera de tu propiedad.
4. Escenas y prefabs en YAML de texto; `.meta` incluidos.
5. README y handoffs actualizados cuando cambie un contrato de integración.
