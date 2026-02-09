
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

[System.Serializable]
public class ObjectSpawnDefinition
{
    public bool spawnActive = true;
    public GameObject[] prefabs;
    public int maxCount = 50;
    public int samplesPerChunk = 2000;
    public float spawnThreshold = 0.5f;

    public int noiseOctaves = 4;
    public float noisePersistence = 0.5f;
    public float noiseLacunarity = 2f;
    public float noiseScale = 0.1f;

    public float minHeight = 0f;
    public float maxHeight = 200f;
    public float minSlope = 0f;
    public float maxSlope = 45f;

    public float minDistance = 1f;
    public int rarityLevel = 1;
    public float verticalSink = 0.2f;


    public BiomeType[] allowedBiomes;
}

public class ChunkObjectSpawner : MonoBehaviour
{
    public ObjectSpawnDefinition[] objectDefinitions;
    public int globalSeed = 1234;

    private System.Random rnd;

    // Armazena dados de spawn por chunk (não instanciados)
    public Dictionary<Vector2Int, ChunkSpawnData> spawnData = new Dictionary<Vector2Int, ChunkSpawnData>();

    private void Awake()
    {
        rnd = new System.Random(globalSeed);
    }

    /// <summary>
    /// Coroutine chamada por InfiniteTerrain quando o chunk é aplicado. 
    /// Ela gera apenas SpawnPoints — NÃO instancia objetos.
    /// </summary>
    public IEnumerator SpawnObjectsForChunkCoroutine(Terrain terrain, Vector2Int chunkCoord, float[,] heights, BiomeType biomeType)
    {
        if (objectDefinitions == null || objectDefinitions.Length == 0) yield break;
        if (terrain == null) yield break;

        int resolution = heights.GetLength(0);
        float maxHeight = terrain.terrainData.size.y;
        float step = (float)terrain.terrainData.size.x / (resolution - 1);
        int chunkSize = InfiniteTerrain.Instance.chunkSize;

        // array temporário de heights para jobs
        NativeArray<float> heightsArray = new NativeArray<float>(resolution * resolution, Allocator.TempJob);
        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heightsArray[z * resolution + x] = heights[z, x];

        List<SpawnPoint> finalPoints = new List<SpawnPoint>();
        // grid local para minDistance por definição
        Dictionary<int, Dictionary<Vector2Int, List<Vector2>>> localDistanceGrid =
            new Dictionary<int, Dictionary<Vector2Int, List<Vector2>>>();

        // Para cada definição, processa amostras em JOB (mesma lógica de filtragem)
        for (int defIndex = 0; defIndex < objectDefinitions.Length; defIndex++)
        {
            var def = objectDefinitions[defIndex];
            if (!def.spawnActive || def.prefabs == null || def.prefabs.Length == 0) continue;

            int samples = Mathf.Max(64, Mathf.RoundToInt(def.samplesPerChunk * ((float)chunkSize * chunkSize) / (256f * 256f)));
            NativeArray<float2> samplePositions = CreateStratifiedSamples(samples, chunkSize, new Vector2(0, 0), globalSeed ^ (defIndex + chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663));

            using (NativeArray<SpawnCandidate> results = new NativeArray<SpawnCandidate>(samplePositions.Length, Allocator.TempJob))
            {
                ObjectSpawnJob job = new ObjectSpawnJob
                {
                    chunkCoord = chunkCoord,
                    chunkSize = chunkSize,
                    heights = heightsArray,
                    resolution = resolution,
                    maxHeight = maxHeight,
                    step = step,
                    worldOffset = InfiniteTerrain.Instance.noiseOffset,
                    spawnDef = new ObjectSpawnJob.SpawnDef
                    {
                        noiseScale = def.noiseScale,
                        spawnThreshold = def.spawnThreshold,
                        minHeight = def.minHeight,
                        maxHeight = def.maxHeight,
                        minSlope = def.minSlope,
                        maxSlope = def.maxSlope,
                        rarityLevel = Mathf.Max(1, def.rarityLevel),
                        octaves = Mathf.Max(1, def.noiseOctaves),
                        persistence = def.noisePersistence,
                        lacunarity = def.noiseLacunarity
                    },
                    samplePositions = samplePositions,
                    output = results,
                    defIndex = defIndex
                };

                JobHandle handle = job.Schedule(samplePositions.Length, 64);
                handle.Complete();

                int collected = 0;
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i].valid) continue;

                    // checar bioma permitido (sem many allocations)
                    if (def.allowedBiomes != null && def.allowedBiomes.Length > 0)
                    {
                        BiomeType localBiome = InfiniteTerrain.Instance.GetBiomeAt(results[i].worldX, results[i].worldZ);
                        bool match = false;
                        for (int bi = 0; bi < def.allowedBiomes.Length; bi++)
                        {
                            if (def.allowedBiomes[bi] == localBiome) { match = true; break; }
                        }
                        if (!match) continue;
                    }

                    // limita por maxCount
                    if (collected >= def.maxCount) break;

                    Vector3 worldPos = new Vector3(results[i].worldX, results[i].height - def.verticalSink, results[i].worldZ);

                    // evita duplicatas globais antes de adicionar (usa hash global do scheduler)
                    int h = ObjectSpawnScheduler.HashPosition(worldPos);
                    if (ObjectSpawnScheduler.IsPositionUsed(h))
                        continue;

                    // marca como usada globalmente (impede duplicação entre chunks)
                    ObjectSpawnScheduler.MarkPositionUsed(h);

                    // SpawnPoint sp = new SpawnPoint
                    // {
                    //     prefabIndex = defIndex,
                    //     position = worldPos,
                    //     normal = results[i].normal
                    // };

                    // finalPoints.Add(sp);
                    // collected++;
                    // --------------------------------------------------------
                    // SISTEMA DE MIN DISTANCE (por definição, por chunk)
                    // --------------------------------------------------------

                    Vector2 pos2D = new Vector2(results[i].worldX, results[i].worldZ);

                    // grid local por definição dentro deste chunk
                    if (!localDistanceGrid.TryGetValue(defIndex, out var grid))
                    {
                        grid = new Dictionary<Vector2Int, List<Vector2>>();
                        localDistanceGrid[defIndex] = grid;
                    }

                    float cellSize = math.max(4f, def.minDistance);

                    // checar se está perto demais de outro ponto do mesmo tipo
                    if (IsTooCloseSqr(pos2D, def.minDistance, grid, cellSize))
                    {
                        // se estiver perto demais, liberar a marca global e pular
                        ObjectSpawnScheduler.UnmarkPositionUsed(h);
                        continue;
                    }

                    // registrar posição na grid
                    Vector2Int cell = new Vector2Int(
                        Mathf.FloorToInt(pos2D.x / cellSize),
                        Mathf.FloorToInt(pos2D.y / cellSize)
                    );

                    if (!grid.ContainsKey(cell))
                        grid[cell] = new List<Vector2>();

                    grid[cell].Add(pos2D);

                    // --------------------------------------------------------
                    // CRIAÇÃO DO SPAWNPOINT
                    // --------------------------------------------------------

                    SpawnPoint sp = new SpawnPoint
                    {
                        prefabIndex = defIndex,
                        position = worldPos,
                        normal = results[i].normal
                    };

                    finalPoints.Add(sp);
                    collected++;

                }

                samplePositions.Dispose();
            }
            // liberar frame para evitar travada se muitas definições
            yield return null;
        }

        heightsArray.Dispose();

        // registra os dados no dicionário
        if (!spawnData.ContainsKey(chunkCoord))
            spawnData[chunkCoord] = new ChunkSpawnData();

        spawnData[chunkCoord].spawnPoints = finalPoints;

        yield break;
    }

    /// <summary>
    /// Remove spawnData (e limpa marcações globais). Chamado quando chunk é reciclado.
    /// </summary>
    public void UnregisterSpawnData(Vector2Int chunkCoord)
    {
        if (!spawnData.ContainsKey(chunkCoord)) return;

        var data = spawnData[chunkCoord];
        if (data != null && data.spawnPoints != null)
        {
            foreach (var sp in data.spawnPoints)
            {
                int h = ObjectSpawnScheduler.HashPosition(sp.position);
                ObjectSpawnScheduler.UnmarkPositionUsed(h);
            }
        }

        spawnData.Remove(chunkCoord);
    }

    // Estratified jittered samples (retorna NativeArray)
    private NativeArray<float2> CreateStratifiedSamples(int samples, int chunkSize, Vector2 offset, int seed)
    {
        int cells = Mathf.CeilToInt(Mathf.Sqrt(samples));
        int total = cells * cells;
        NativeArray<float2> arr = new NativeArray<float2>(total, Allocator.TempJob);

        Unity.Mathematics.Random r = new Unity.Mathematics.Random((uint)math.max(1, seed));
        float cellSize = (float)chunkSize / cells;
        int idx = 0;
        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                float jitterX = r.NextFloat(0f, 1f) * cellSize;
                float jitterY = r.NextFloat(0f, 1f) * cellSize;

                float sx = x * cellSize + jitterX;
                float sy = y * cellSize + jitterY;

                sx = math.clamp(sx, 0f, chunkSize - 0.0001f);
                sy = math.clamp(sy, 0f, chunkSize - 0.0001f);

                arr[idx++] = new float2(sx + offset.x, sy + offset.y);
            }
        }

        return arr;
    }

    private bool IsTooCloseSqr(Vector2 pos, float minDist, Dictionary<Vector2Int, List<Vector2>> grid, float cellSize)
    {
        float minDistSq = minDist * minDist;
        Vector2Int cell = new Vector2Int(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.y / cellSize)
        );

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                Vector2Int n = new Vector2Int(cell.x + dx, cell.y + dz);

                if (grid.TryGetValue(n, out var list))
                {
                    foreach (var p in list)
                    {
                        if ((pos - p).sqrMagnitude < minDistSq)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    // ========================= JOBS =========================
    [BurstCompile]
    struct ObjectSpawnJob : IJobParallelFor
    {
        [System.Serializable]
        public struct SpawnDef
        {
            public float noiseScale;
            public float spawnThreshold;
            public float minHeight;
            public float maxHeight;
            public float minSlope;
            public float maxSlope;
            public int rarityLevel;
            public int octaves;
            public float persistence;
            public float lacunarity;
        }

        public Vector2Int chunkCoord;
        public int chunkSize;
        public int resolution;
        public float maxHeight;
        public float step;
        public Vector2 worldOffset;
        public SpawnDef spawnDef;
        public int defIndex;

        [ReadOnly] public NativeArray<float> heights;
        [ReadOnly] public NativeArray<float2> samplePositions;
        [WriteOnly] public NativeArray<SpawnCandidate> output;

        static float FBM(float2 p, int octaves, float lacunarity, float persistence)
        {
            float amp = 1f;
            float freq = 1f;
            float sum = 0f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = noise.snoise(p * freq) * 0.5f + 0.5f;
                sum += n * amp;
                norm += amp;
                freq *= lacunarity;
                amp *= persistence;
            }
            return sum / math.max(0.0001f, norm);
        }

        void SampleHeightAndDerivatives(float localX, float localZ, out float worldHeight, out float dhx, out float dhz)
        {
            float normalizedX = localX / (float)chunkSize;
            float normalizedZ = localZ / (float)chunkSize;

            float fx = normalizedX * (resolution - 1);
            float fz = normalizedZ * (resolution - 1);

            int ix = (int)math.floor(fx);
            int iz = (int)math.floor(fz);

            ix = math.clamp(ix, 0, resolution - 1);
            iz = math.clamp(iz, 0, resolution - 1);

            float h = heights[iz * resolution + ix];
            worldHeight = h * maxHeight;

            dhx = 0f; dhz = 0f;
            if (ix > 0 && ix < resolution - 1)
            {
                float hL = heights[iz * resolution + (ix - 1)];
                float hR = heights[iz * resolution + (ix + 1)];
                dhx = (hR - hL) * maxHeight / (2f * step);
            }
            if (iz > 0 && iz < resolution - 1)
            {
                float hB = heights[(iz - 1) * resolution + ix];
                float hF = heights[(iz + 1) * resolution + ix];
                dhz = (hF - hB) * maxHeight / (2f * step);
            }
        }

        public void Execute(int index)
        {
            output[index] = new SpawnCandidate { valid = false, defIndex = -1 };

            if (index < 0 || index >= samplePositions.Length) return;
            float2 local = samplePositions[index];

            Unity.Mathematics.Random rnd = new Unity.Mathematics.Random((uint)math.max(1, index * 73856093 ^ chunkCoord.x * 19349663 ^ chunkCoord.y * 83492791));

            float worldX = chunkCoord.x * chunkSize + local.x;
            float worldZ = chunkCoord.y * chunkSize + local.y;

            float worldHeight;
            float dhx, dhz;
            SampleHeightAndDerivatives(local.x, local.y, out worldHeight, out dhx, out dhz);

            float slope = math.degrees(math.atan(math.sqrt(dhx * dhx + dhz * dhz)));

            if (worldHeight < spawnDef.minHeight || worldHeight > spawnDef.maxHeight) return;
            if (slope < spawnDef.minSlope || slope > spawnDef.maxSlope) return;

            float2 samplePos = new float2((worldX + worldOffset.x) * spawnDef.noiseScale, (worldZ + worldOffset.y) * spawnDef.noiseScale);
            float noiseVal = FBM(samplePos, spawnDef.octaves, spawnDef.lacunarity, spawnDef.persistence);

            if (noiseVal < spawnDef.spawnThreshold) return;
            if (rnd.NextInt(0, spawnDef.rarityLevel) != 0) return;

            float3 n = math.normalize(new float3(-dhx, 1f, -dhz));

            output[index] = new SpawnCandidate
            {
                valid = true,
                defIndex = defIndex,
                worldX = worldX,
                worldZ = worldZ,
                height = worldHeight,
                normal = n
            };
        }
    }

    struct SpawnCandidate
    {
        public bool valid;
        public int defIndex;
        public float worldX;
        public float worldZ;
        public float height;
        public float3 normal;
    }
}

// ===== Helper classes =====

[System.Serializable]
public class SpawnPoint
{
    public int prefabIndex;
    public Vector3 position;
    public float3 normal;
}

public class ChunkSpawnData
{
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
}
