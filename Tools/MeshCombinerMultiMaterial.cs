using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshCombinerMultiMaterial : MonoBehaviour
{
    public bool desactiveOriginals = true;
    public bool destroyOriginals = false;

    public GameObject combinedObject;

    public enum PivotMode { Center, TopCenter, BottomCenter }
    public PivotMode pivotMode = PivotMode.Center;

    [ContextMenu("Combine Meshes")]
    public void CombineMeshes()
    {
        Matrix4x4 parentInverseMatrix = transform.worldToLocalMatrix;

        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        LODGroup[] lodGroups = GetComponentsInChildren<LODGroup>();

        HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        List<Material> materials = new List<Material>();
        List<List<CombineInstance>> subMeshCombineInstances = new List<List<CombineInstance>>();

        // --- Processa LODGroups ---
        foreach (LODGroup lodGroup in lodGroups)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length > 0)
            {
                foreach (Renderer renderer in lods[0].renderers)
                {
                    MeshFilter mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        processedObjects.Add(mf.gameObject);

                        Mesh mesh = mf.sharedMesh;
                        if (!mesh.isReadable)
                        {
                            mesh = Instantiate(mesh);
                        }

                        for (int i = 0; i < mesh.subMeshCount; i++)
                        {
                            Material material = renderer.sharedMaterials[i];
                            int materialIndex = materials.IndexOf(material);
                            if (materialIndex == -1)
                            {
                                materialIndex = materials.Count;
                                materials.Add(material);
                                subMeshCombineInstances.Add(new List<CombineInstance>());
                            }

                            CombineInstance combineInstance = new CombineInstance
                            {
                                mesh = mesh,
                                subMeshIndex = i,
                                transform = parentInverseMatrix * mf.transform.localToWorldMatrix
                            };

                            subMeshCombineInstances[materialIndex].Add(combineInstance);
                        }
                    }
                }
            }
        }

        // --- Processa MeshFilters normais ---
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf == GetComponent<MeshFilter>())
                continue;
            if (processedObjects.Contains(mf.gameObject))
                continue;
            if (mf.sharedMesh == null)
                continue;

            Renderer renderer = mf.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials.Length == 0)
                continue;

            Mesh mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
                mesh = Instantiate(mesh);
            }

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Material material = renderer.sharedMaterials[i];
                int materialIndex = materials.IndexOf(material);
                if (materialIndex == -1)
                {
                    materialIndex = materials.Count;
                    materials.Add(material);
                    subMeshCombineInstances.Add(new List<CombineInstance>());
                }

                CombineInstance combineInstance = new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = i,
                    transform = parentInverseMatrix * mf.transform.localToWorldMatrix
                };

                subMeshCombineInstances[materialIndex].Add(combineInstance);
            }
        }

        if (materials.Count == 0)
        {
            Debug.LogWarning("Nenhum material encontrado para combinar.");
            return;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.subMeshCount = materials.Count;

        List<CombineInstance> finalCombineInstances = new List<CombineInstance>();

        for (int i = 0; i < materials.Count; i++)
        {
            Mesh subMesh = new Mesh();
            subMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            subMesh.CombineMeshes(subMeshCombineInstances[i].ToArray(), true, true);

            CombineInstance finalCombineInstance = new CombineInstance
            {
                mesh = subMesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            };

            finalCombineInstances.Add(finalCombineInstance);
        }

        combinedMesh.CombineMeshes(finalCombineInstances.ToArray(), false, false);

        // --- Ajusta o pivot ---
        Bounds bounds = combinedMesh.bounds;
        Vector3 pivotOffset = Vector3.zero;

        switch (pivotMode)
        {
            case PivotMode.Center:
                pivotOffset = bounds.center;
                break;
            case PivotMode.BottomCenter:
                pivotOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                break;
            case PivotMode.TopCenter:
                pivotOffset = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                break;
        }

        Vector3[] vertices = combinedMesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= pivotOffset;
        }
        combinedMesh.vertices = vertices;
        combinedMesh.RecalculateBounds();

        // Cria o objeto final
        combinedObject = new GameObject(gameObject.name + "_CombinedMesh");
        combinedObject.transform.parent = transform;
        combinedObject.transform.localPosition = pivotOffset;
        combinedObject.transform.localRotation = Quaternion.identity;
        combinedObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = combinedMesh;

        MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = materials.ToArray();

#if UNITY_EDITOR
        string folderPath = "Assets/CombinedMeshes";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "CombinedMeshes");
        }

        string assetPath = folderPath + "/" + combinedObject.name + ".asset";
        AssetDatabase.CreateAsset(combinedMesh, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log("Mesh combinada salva em: " + assetPath);
#endif

        if (destroyOriginals)
        {
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject != gameObject)
                    DestroyImmediate(mf.gameObject);
            }
        }

        if (desactiveOriginals)
        {
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject != gameObject)
                    mf.gameObject.SetActive(false);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MeshCombinerMultiMaterial))]
public class MeshCombinerMultiMaterialEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshCombinerMultiMaterial script = (MeshCombinerMultiMaterial)target;
        if (GUILayout.Button("Combine Meshes"))
        {
            script.CombineMeshes();
        }
    }
}
#endif
