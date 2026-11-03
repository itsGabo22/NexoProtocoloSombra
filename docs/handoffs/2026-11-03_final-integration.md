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
