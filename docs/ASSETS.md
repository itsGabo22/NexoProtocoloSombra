# Registro de assets — NEXO: PROTOCOLO SOMBRA

**Responsable:** Carlos.

Este registro documenta todo asset externo necesario para abrir y compilar el proyecto. Los packs completos no se suben a GitHub: se sincronizan por Drive dentro de `Assets/_ThirdPartyAssets/`, incluyendo sus `.meta`.

## Registro

| Pack / archivo | Uso | Fuente | Licencia | Versión de Drive | Responsable | Estado |
| --- | --- | --- | --- | --- | --- | --- |
| Sin assets registrados todavía | — | — | — | — | Carlos | Pendiente |

## Protocolo obligatorio para packs

1. Carlos importa el pack una sola vez en su proyecto.
2. Comprime la carpeta `Pack_<nombre>` completa, **con todos sus `.meta`**.
3. Sube ese paquete a Drive y registra aquí fuente, licencia y versión.
4. Gabriel cierra Unity y copia la carpeta en `Assets/_ThirdPartyAssets/`.
5. Gabriel no vuelve a importar el pack ni modifica sus GUIDs.
6. Después del día 2 no se altera el contenido interno de un pack ya compartido.

## Assets propios curados

Solo los modelos, texturas y audio propios que terminan en el build se versionan con Git LFS, dentro de `Assets/_Project/`. Objetivo total orientativo: menos de 500 MB.

## Fuentes y licencias pendientes

- [ ] Modelo infantil de Elena: registrar licencia y atribución antes de integrarlo.
- [ ] Animaciones de Mixamo: registrar cuenta, personaje y clips utilizados.
- [ ] Fuente monoespaciada del HUD: registrar licencia.
- [ ] Fuente corrupta para Mimicry: registrar licencia.
- [ ] Packs de oficina, laboratorio y lobby: registrar enlaces y licencias.
