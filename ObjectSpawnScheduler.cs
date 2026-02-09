using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawnScheduler : MonoBehaviour
{
    [Tooltip("Distância máxima para instanciar um ponto (em metros)")]
    public float spawnDistance = 80f;
    [Tooltip("Quantos objetos instanciar por Update (limite para evitar spikes)")]
    public int spawnPerFrame = 25;

    [Tooltip("Distância para despawn e devolver ao pool")]
    public float despawnDistance = 120f;

    public ChunkObjectSpawner spawner;
    public Transform player;

    private ObjectPoolManager poolManager;

    // usadoPositions: posições marcadas por chunk generation para evitar duplicação entre chunks
    private static HashSet<int> usedPositions = new HashSet<int>();

    // registry de posições já spawnadas fisicamente (evita instanciar duas vezes)
    private HashSet<int> spawnedRegistry = new HashSet<int>();

    // objetos instanciados por chunk para facilitar despawn
    private Dictionary<Vector2Int, List<GameObject>> spawnedByChunk = new Dictionary<Vector2Int, List<GameObject>>();

    void Awake()
    {
        if (spawner == null) spawner = FindObjectOfType<ChunkObjectSpawner>();
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        poolManager = FindObjectOfType<ObjectPoolManager>();
        if (poolManager == null) poolManager = gameObject.AddComponent<ObjectPoolManager>();
    }

    void Update()
    {
        if (player == null || spawner == null) return;

        SpawnNearPlayerLimited();
        DespawnFarObjects();
    }

    // Public utilities for ChunkObjectSpawner to use
    public static int HashPosition(Vector3 pos)
    {
        // quantize position to grid (ajuste precision se quiser)
        int q = 10; // 0.1m precision
        int x = Mathf.RoundToInt(pos.x * q);
        int z = Mathf.RoundToInt(pos.z * q);
        unchecked
        {
            int h = x * 73856093 ^ z * 19349663;
            return h;
        }
    }

    public static bool IsPositionUsed(int hash)
    {
        lock (usedPositions)
        {
            return usedPositions.Contains(hash);
        }
    }

    public static void MarkPositionUsed(int hash)
    {
        lock (usedPositions)
        {
            usedPositions.Add(hash);
        }
    }

    public static void UnmarkPositionUsed(int hash)
    {
        lock (usedPositions)
        {
            usedPositions.Remove(hash);
        }
    }

    // Spawn gradual com limite por frame
    private void SpawnNearPlayerLimited()
    {
        int spawnedThisFrame = 0;
        Vector3 playerPos = player.position;

        // percorre uma cópia das chaves para evitar collection-modification issues
        var keys = new List<Vector2Int>(spawner.spawnData.Keys);

        // ordenar por distância ao player (opcional, para priorizar)
        keys.Sort((a, b) =>
        {
            Vector3 pa = new Vector3(a.x * InfiniteTerrain.Instance.chunkSize + InfiniteTerrain.Instance.chunkSize / 2f, 0, a.y * InfiniteTerrain.Instance.chunkSize + InfiniteTerrain.Instance.chunkSize / 2f);
            Vector3 pb = new Vector3(b.x * InfiniteTerrain.Instance.chunkSize + InfiniteTerrain.Instance.chunkSize / 2f, 0, b.y * InfiniteTerrain.Instance.chunkSize + InfiniteTerrain.Instance.chunkSize / 2f);
            float da = (pa - playerPos).sqrMagnitude;
            float db = (pb - playerPos).sqrMagnitude;
            return da.CompareTo(db);
        });

        foreach (var chunkCoord in keys)
        {
            if (spawnedThisFrame >= spawnPerFrame) break;
            if (!spawner.spawnData.ContainsKey(chunkCoord)) continue;
            var data = spawner.spawnData[chunkCoord];
            if (data == null || data.spawnPoints == null || data.spawnPoints.Count == 0) continue;

            // ensure we have list for this chunk
            if (!spawnedByChunk.ContainsKey(chunkCoord)) spawnedByChunk[chunkCoord] = new List<GameObject>();
            var list = spawnedByChunk[chunkCoord];

            // iterate spawnPoints in reverse so we can RemoveAt safely
            for (int i = data.spawnPoints.Count - 1; i >= 0; i--)
            {
                if (spawnedThisFrame >= spawnPerFrame) break;

                SpawnPoint sp = data.spawnPoints[i];
                float dist = Vector3.Distance(playerPos, sp.position);
                if (dist > spawnDistance) continue;

                int h = HashPosition(sp.position);
                if (spawnedRegistry.Contains(h))
                {
                    // já spawnado fisicamente (caso outra instância tenha pego)
                    data.spawnPoints.RemoveAt(i);
                    continue;
                }

                // REAL spawn via pool
                var def = spawner.objectDefinitions[sp.prefabIndex];
                if (def == null || def.prefabs == null || def.prefabs.Length == 0)
                {
                    data.spawnPoints.RemoveAt(i);
                    continue;
                }

                GameObject prefab = def.prefabs[Random.Range(0, def.prefabs.Length)];
                GameObject go = poolManager.GetFromPool(prefab, sp.position, Quaternion.identity, transform);

                // add to tracking
                spawnedRegistry.Add(h);
                spawnedThisFrame++;
                list.Add(go);

                // Remove point from chunk data (já foi instanciado)
                data.spawnPoints.RemoveAt(i);
            }
        }
    }

    // Despawns objetos muito distantes
    private void DespawnFarObjects()
    {
        Vector3 ppos = player.position;
        var chunksToClear = new List<Vector2Int>();

        foreach (var kv in spawnedByChunk)
        {
            var chunkCoord = kv.Key;
            var list = kv.Value;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var obj = list[i];
                if (obj == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                float d = Vector3.Distance(ppos, obj.transform.position);
                if (d > despawnDistance)
                {
                    // Return to pool and remove registry hash
                    int h = HashPosition(obj.transform.position);
                    spawnedRegistry.Remove(h);
                    poolManager.ReturnToPoolByInstance(obj);
                    list.RemoveAt(i);
                }
            }

            // If list empty and chunk has no spawnPoints we can optionally remove entry
            if (list.Count == 0 && (!spawner.spawnData.ContainsKey(chunkCoord) || spawner.spawnData[chunkCoord].spawnPoints.Count == 0))
                chunksToClear.Add(chunkCoord);
        }

        foreach (var c in chunksToClear)
        {
            spawnedByChunk.Remove(c);
        }
    }

    // chamado externamente (e.g. InfiniteTerrain.RecycleChunk) para despawn imediato os objetos e limpar marcações
    public void UnloadChunk(Vector2Int chunkCoord)
    {
        // devolver objetos spawnados deste chunk
        if (spawnedByChunk.TryGetValue(chunkCoord, out var list))
        {
            foreach (var obj in list)
            {
                if (obj == null) continue;
                int h = HashPosition(obj.transform.position);
                spawnedRegistry.Remove(h);
                poolManager.ReturnToPoolByInstance(obj);
            }
            spawnedByChunk.Remove(chunkCoord);
        }

        // limpar spawnData e unmark posicoes usadas globalmente
        if (spawner != null)
        {
            spawner.UnregisterSpawnData(chunkCoord);
        }
    }
}
