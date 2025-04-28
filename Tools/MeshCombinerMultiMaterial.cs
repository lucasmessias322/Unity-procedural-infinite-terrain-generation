using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MeshCombinerMultiMaterial : MonoBehaviour
{
    public bool desactiveOriginals = true;
    public bool destroyOriginals = false;

    public GameObject combinedObject;

    [ContextMenu("Combine Meshes")]
    public void CombineMeshes()
    {
        // Obtém a matriz que transforma do espaço mundial para o espaço local do pai.
        Matrix4x4 parentInverseMatrix = transform.worldToLocalMatrix;

        // Pega todos os MeshFilters e LODGroups dos filhos.
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        LODGroup[] lodGroups = GetComponentsInChildren<LODGroup>();

        // Usaremos um HashSet para marcar quais objetos já foram processados (via LODGroup).
        HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        List<Material> materials = new List<Material>();
        List<List<CombineInstance>> subMeshCombineInstances = new List<List<CombineInstance>>();

        // 1. Processa objetos com LODGroup.
        foreach (LODGroup lodGroup in lodGroups)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length > 0)
            {
                // Seleciona o LOD0 (mais detalhado).
                foreach (Renderer renderer in lods[0].renderers)
                {
                    MeshFilter mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        // Marca o objeto para não processá-lo novamente.
                        processedObjects.Add(mf.gameObject);

                        // Cria uma cópia da malha se ela não for legível.
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

        // 2. Processa os MeshFilters que não fazem parte de um LODGroup.
        foreach (MeshFilter mf in meshFilters)
        {
            // Ignora o MeshFilter do próprio objeto que possui este script.
            if (mf == GetComponent<MeshFilter>())
                continue;
            // Se já foi processado via LODGroup, pula.
            if (processedObjects.Contains(mf.gameObject))
                continue;
            if (mf.sharedMesh == null)
                continue;

            Renderer renderer = mf.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials.Length == 0)
                continue;

            // Cria uma cópia da malha se necessário.
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

        // Cria a mesh combinada com suporte a mais de 65.535 vértices.
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

        // Cria um novo objeto para a mesh combinada e o coloca como filho deste objeto.
        combinedObject = new GameObject(gameObject.name + "_CombinedMesh");
        combinedObject.transform.parent = transform;
        combinedObject.transform.localPosition = Vector3.zero;
        combinedObject.transform.localRotation = Quaternion.identity;
        combinedObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = combinedMesh;

        MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = materials.ToArray();

#if UNITY_EDITOR
        // Cria a pasta "CombinedMeshes" dentro de "Assets", se ela ainda não existir.
        string folderPath = "Assets/CombinedMeshes";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "CombinedMeshes");
        }

        // Define o caminho para salvar o asset.
        string assetPath = folderPath + "/" + combinedObject.name + ".asset";
        AssetDatabase.CreateAsset(combinedMesh, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log("Mesh combinada salva em: " + assetPath);
#endif

        // Se desejado, destrói os objetos originais.
        if (destroyOriginals)
        {
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject != gameObject)
                    DestroyImmediate(mf.gameObject);
            }
        }

        // Ou os desativa.
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
