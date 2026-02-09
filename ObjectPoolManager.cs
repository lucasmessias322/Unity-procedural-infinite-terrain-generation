// using System.Collections.Generic;
// using UnityEngine;

// public class ObjectPoolManager : MonoBehaviour
// {
//     private Dictionary<GameObject, Queue<GameObject>> objectPools = new Dictionary<GameObject, Queue<GameObject>>();

//     public void InitializePool(GameObject prefab, int count, Transform parent)
//     {
//         if (!objectPools.ContainsKey(prefab))
//             objectPools[prefab] = new Queue<GameObject>();

//         for (int i = 0; i < count; i++)
//         {
//             GameObject obj = Instantiate(prefab, parent);
//             obj.SetActive(false);
//             objectPools[prefab].Enqueue(obj);
//         }
//     }

//     public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
//     {
//         if (!objectPools.ContainsKey(prefab) || objectPools[prefab].Count == 0)
//         {
//             InitializePool(prefab, 1, parent);
//         }

//         GameObject obj = objectPools[prefab].Dequeue();
//         obj.transform.SetParent(parent);
//         obj.transform.SetPositionAndRotation(position, rotation);
//         obj.SetActive(true);
//         return obj;
//     }

//     public void ReturnToPool(GameObject prefab, GameObject obj)
//     {
//         obj.SetActive(false);
//         obj.transform.SetParent(transform);
//         objectPools[prefab].Enqueue(obj);
//     }

//     public void Clear()
//     {
//         foreach (var kvp in objectPools)
//         {
//             while (kvp.Value.Count > 0)
//             {
//                 GameObject obj = kvp.Value.Dequeue();
//                 Destroy(obj);
//             }
//         }
//         objectPools.Clear();
//     }
// }
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool simples e seguro para objetos usados no spawn. 
/// Se preferir, troque por sua implementação atual.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    private class Pool
    {
        public Queue<GameObject> queue = new Queue<GameObject>();
        public GameObject prefab;
    }

    private Dictionary<GameObject, Pool> poolsByPrefab = new Dictionary<GameObject, Pool>();
    private Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();

    public int defaultInitialSize = 10;

    public void InitializePool(GameObject prefab, int initialSize = -1, Transform parent = null)
    {
        if (prefab == null) return;
        if (initialSize <= 0) initialSize = defaultInitialSize;
        if (poolsByPrefab.ContainsKey(prefab)) return;

        Pool p = new Pool { prefab = prefab };
        poolsByPrefab[prefab] = p;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject go = Instantiate(prefab, parent);
            go.SetActive(false);
            p.queue.Enqueue(go);
            instanceToPrefab[go] = prefab;
        }
    }

    public GameObject GetFromPool(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;

        if (!poolsByPrefab.ContainsKey(prefab))
        {
            InitializePool(prefab, defaultInitialSize, parent);
        }

        var pool = poolsByPrefab[prefab];
        GameObject go;
        if (pool.queue.Count > 0)
        {
            go = pool.queue.Dequeue();
            if (go == null)
            {
                go = Instantiate(prefab, parent);
            }
        }
        else
        {
            go = Instantiate(prefab, parent);
        }

        instanceToPrefab[go] = prefab;
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.SetActive(true);
        if (parent != null) go.transform.SetParent(parent, true);
        else go.transform.SetParent(null, true);

        return go;
    }

    public void ReturnToPool(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;
        if (!poolsByPrefab.ContainsKey(prefab))
        {
            InitializePool(prefab, defaultInitialSize, null);
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform, true);
        poolsByPrefab[prefab].queue.Enqueue(instance);
        if (instanceToPrefab.ContainsKey(instance)) instanceToPrefab.Remove(instance);
    }

    // útil quando só temos a instância
    public void ReturnToPoolByInstance(GameObject instance)
    {
        if (instance == null) return;
        if (!instanceToPrefab.TryGetValue(instance, out var prefab))
        {
            // fallback: destruir se não souber qual pool
            Destroy(instance);
            return;
        }
        ReturnToPool(prefab, instance);
    }
}
