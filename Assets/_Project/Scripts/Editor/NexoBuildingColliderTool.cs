using UnityEditor;
using UnityEngine;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Genera las colisiones de un modelo de edificio importado (FBX con muchas mallas hijas):
    /// - Quita MeshCollider vacíos (sin malla), que no colisionan con nada.
    /// - Añade un MeshCollider (no convexo) a cada MeshFilter estático con malla.
    /// - Omite props diminutos (bolígrafos, tazas, teclados) para que el CharacterController no se enganche.
    /// - Omite SkinnedMeshRenderer (puertas y cristales animados del pack): sus vanos quedan transitables.
    /// - Asigna la capa Environment y los Static Flags (GI + Batching) para NavMesh y oclusión de la IA.
    /// </summary>
    public static class NexoBuildingColliderTool
    {
        private const string MenuPath = "Nexo/Floor12/Generar Colisiones del Edificio Seleccionado";
        private const float MinPropSize = 0.3f;

        [MenuItem(MenuPath)]
        private static void GenerateForSelection()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null || EditorUtility.IsPersistent(root))
            {
                EditorUtility.DisplayDialog("Nexo Colisiones",
                    "Selecciona en la Jerarquía de la escena la raíz del edificio (p. ej. 'office building').", "OK");
                return;
            }

            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0)
            {
                Debug.LogWarning("[NexoColliders] No existe la capa 'Environment': se mantiene la capa actual.");
            }

            Undo.SetCurrentGroupName("Nexo: generar colisiones del edificio");
            int group = Undo.GetCurrentGroup();

            int removed = 0, added = 0, skippedSmall = 0, skippedSkinned = 0;

            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh != null) continue;
                Undo.DestroyObjectImmediate(collider);
                removed++;
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject go = filter.gameObject;
                if (filter.sharedMesh == null) continue;

                ApplyEnvironmentSetup(go, environmentLayer);

                if (go.GetComponent<Collider>() != null) continue;

                Renderer renderer = go.GetComponent<Renderer>();
                Vector3 size = renderer != null ? renderer.bounds.size : filter.sharedMesh.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < MinPropSize)
                {
                    skippedSmall++;
                    continue;
                }

                var meshCollider = Undo.AddComponent<MeshCollider>(go);
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = false;
                added++;
            }

            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ApplyEnvironmentSetup(skinned.gameObject, environmentLayer);
                skippedSkinned++;
            }

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

            string report = $"MeshCollider añadidos: {added}\nMeshCollider vacíos eliminados: {removed}\n" +
                            $"Props pequeños omitidos (< {MinPropSize} m): {skippedSmall}\n" +
                            $"Mallas animadas omitidas (puertas/cristales): {skippedSkinned}";
            Debug.Log($"[NexoColliders] '{root.name}'\n{report}", root);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Nexo Colisiones", report + "\n\nGuarda la escena (Ctrl+S).", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool GenerateForSelectionValidate() => Selection.activeGameObject != null;

        private static void ApplyEnvironmentSetup(GameObject go, int environmentLayer)
        {
            Undo.RecordObject(go, "Nexo: capa Environment");
            if (environmentLayer >= 0) go.layer = environmentLayer;

            GameObjectUtility.SetStaticEditorFlags(go,
                GameObjectUtility.GetStaticEditorFlags(go) |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.BatchingStatic);
        }
    }
}
