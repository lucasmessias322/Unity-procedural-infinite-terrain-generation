
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    public static InfiniteTerrain Instance { get; private set; }
    [Header("Chunk Settings")]
    [Tooltip("Tamanho de cada chunk em metros")]
    public int chunkSize = 256;
    [Tooltip("Resolução do heightmap (must be 2^n + 1, ex: 513)")]
    public enum TerrainResolution
    {
        _513 = 513,
        _257 = 257,
        _129 = 129
    }

    public TerrainResolution baseResolution = TerrainResolution._513;
    [Header("Terrain Settings")]
    [Tooltip("Altura máxima global do terreno em metros")]
    public float terrainHeight = 200f;

    [Tooltip("Número de chunks de raio carregados")]
    public int renderDistance = 2;

    [Header("Terrain Template")]
    [Tooltip("Prefab contendo um GameObject com Terrain + TerrainCollider. Será usado para instanciar / pool.")]
    public GameObject terrainPrefab; // prefab com Terrain e TerrainCollider (sem TerrainData obrigatório)

    [Header("Noise")]
    public int seed = 42;
    public Vector2 noiseOffset = Vector2.zero;
    public NoiseLayersSO noiseSettings;


    [Header("Performance")]
    [Tooltip("Número máximo de chunks a gerar por frame (aplicação de heights na main thread)")]
    public int maxApplyPerFrame = 1;

    private SemaphoreSlim generationSemaphore = new SemaphoreSlim(2, 2); // Máx 2 tasks simultâneas

    [Header("References")]
    public Transform player;
    private Dictionary<TerrainLayer, int> layerToIndex = new Dictionary<TerrainLayer, int>();

    // Internals
    private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new Dictionary<Vector2Int, TerrainChunk>();
    private readonly Queue<TerrainChunk> chunkPool = new Queue<TerrainChunk>();
    private readonly Queue<TerrainChunk> applyQueue = new Queue<TerrainChunk>(); // chunks prontos para aplicar na main thread
    private readonly System.Random rnd = new System.Random();
    private Vector2 worldOffset;

    // Control
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    public float applyInterval = 0.1f; // tempo mínimo entre applies
    private float lastApplyTime;

    // Para atualizações preguiçosas
    private int updateFrameSkip = 5; // Atualiza chunks a cada 5 frames
    private int frameCounter = 0;

    [Header("Global Layer Settings")]
    public GlobalLayerDefinition[] globalLayerDefinitions;

    [Header("Biome Settings")]
    [Tooltip("Noise scale for biome determination.")]
    public float biomeNoiseScale = 0.001f;
    [Tooltip("Available biome definitions.")]
    public BiomeDefinition[] biomeDefinitions;
    [Tooltip("Sharpness of biome transitions. Higher values make sharper (narrower) transitions.")]
    public float biomeBlendSharpness = 1f;

    [Header("Detail Settings")]
    [Tooltip("Resolução do detail map (quanto maior, mais detalhes, mas mais memória/CPU)")]
    private int detailResolution;
    [Tooltip("Resolução por patch (tipicamente 8 ou 16)")]
    public int detailResolutionPerPatch = 8;
    public float wavingGrassStrength = 0.2f;
    public float wavingGrassAmount = 0.5f;
    public Color wavingGrassTint;

    private TerrainLayer[] allTerrainLayers;

    [Header("Water Settings")]
    public bool SpawnWaterTile;
    public GameObject waterTilePrefab;
    public float WaterTileSize = 26.3f;
    public float waterHeight = 50.5f;
    private ChunkObjectSpawner chunkObjectSpawner; // Add reference to spawner
    public bool spawnobjects;
    // Callback disparado quando ApplyHeightmapToTerrain terminar
    public event Action<TerrainChunk> OnChunkApplied;


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        if (terrainPrefab == null)
            Debug.LogError("terrainPrefab não atribuído! Crie um prefab com Terrain + TerrainCollider.");

        // Initialize spawner reference
        chunkObjectSpawner = FindObjectOfType<ChunkObjectSpawner>();
        if (chunkObjectSpawner == null)
            Debug.LogError("ChunkObjectSpawner não encontrado na cena! Adicione um componente ChunkObjectSpawner.");
    }


    private void Start()
    {
        worldOffset = noiseOffset;

        // Build all unique TerrainLayers from global and all biomes (safer)
        HashSet<TerrainLayer> uniqueLayers = new HashSet<TerrainLayer>();
        foreach (var def in globalLayerDefinitions)
        {
            if (def != null && def.terrainLayer != null) uniqueLayers.Add(def.terrainLayer);
        }
        if (biomeDefinitions != null)
        {
            foreach (var biome in biomeDefinitions)
            {
                if (biome == null || biome.terrainLayerDefinitions == null) continue;
                foreach (var def in biome.terrainLayerDefinitions)
                {
                    if (def != null && def.terrainLayer != null) uniqueLayers.Add(def.terrainLayer);
                }
            }
        }
        allTerrainLayers = uniqueLayers.ToArray();
        layerToIndex = new Dictionary<TerrainLayer, int>();
        for (int i = 0; i < allTerrainLayers.Length; i++)
        {
            layerToIndex[allTerrainLayers[i]] = i;
        }


        // Garante que a resolução de grama bate com a do heightmap
        detailResolution = GetResolution() - 1;

        // Pré-popular pool com alguns chunks (opcional)
        for (int i = 0; i < (renderDistance * 4 + 8); i++)
            CreatePoolChunk();
        UpdateChunksImmediate(); // cria os chunks iniciais
        StartCoroutine(ApplyQueueCoroutine());
    }

    private void Update()
    {

        if (player == null) return;

        frameCounter++;
        if (frameCounter % updateFrameSkip == 0)
        {
            Vector2Int playerChunk = WorldPosToChunk(player.position);
            if (playerChunk != lastPlayerChunk)
            {
                lastPlayerChunk = playerChunk;
                UpdateChunksImmediate();
            }
        }
    }

    // Se precisar usar como int:
    public int GetResolution()
    {
        return (int)baseResolution;
    }
    #region Chunk Management

    private void UpdateChunksImmediate()
    {
        Vector2Int center = WorldPosToChunk(player.position);
        HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();

        for (int z = -renderDistance; z <= renderDistance; z++)
            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                Vector2Int c = new Vector2Int(center.x + x, center.y + z);
                wanted.Add(c);
                if (!activeChunks.ContainsKey(c))
                {
                    // Acquire chunk from pool and start generation
                    var chunk = GetChunkFromPool();
                    InitializeChunkTransform(chunk, c);
                    activeChunks.Add(c, chunk);
                    StartGenerateChunk(chunk);
                }
            }

        // Deactivate chunks that are not wanted
        var toRemove = new List<Vector2Int>();
        foreach (var kv in activeChunks)
        {
            if (!wanted.Contains(kv.Key))
            {
                // cancel generation if running
                kv.Value.CancelGeneration();
                // put back in pool
                RecycleChunk(kv.Value);
                toRemove.Add(kv.Key);
            }
        }

        foreach (var k in toRemove) activeChunks.Remove(k);
    }

    private Vector2Int WorldPosToChunk(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x / chunkSize);
        int cz = Mathf.FloorToInt(worldPos.z / chunkSize);
        return new Vector2Int(cx, cz);
    }

    #endregion

    #region Pooling

    private void CreatePoolChunk()
    {
        if (terrainPrefab == null) return;
        GameObject go = Instantiate(terrainPrefab, transform);
        go.name = "PooledTerrain";
        var terrain = go.GetComponent<Terrain>();
        if (terrain == null) terrain = go.AddComponent<Terrain>();
        var collider = go.GetComponent<TerrainCollider>();
        if (collider == null) collider = go.AddComponent<TerrainCollider>();

        // Create empty TerrainData now and attach (so we can reuse)
        TerrainData td = new TerrainData();
        td.heightmapResolution = GetResolution();
        td.size = new Vector3(chunkSize, 1, chunkSize);
        terrain.terrainData = td;
        collider.terrainData = td;
        ConfigureTerrain(td);

        var chunk = new TerrainChunk
        {
            gameObject = go,
            terrain = terrain,
            terrainCollider = collider,
            terrainData = td,
            chunkCoord = new Vector2Int(int.MinValue, int.MinValue)
        };
        go.SetActive(false);
        chunkPool.Enqueue(chunk);
    }

    private TerrainChunk GetChunkFromPool()
    {
        if (chunkPool.Count == 0) CreatePoolChunk();
        var c = chunkPool.Dequeue();
        c.gameObject.SetActive(true);
        return c;
    }

    private void ConfigureTerrain(TerrainData td)
    {
        td.wavingGrassStrength = wavingGrassStrength;
        td.wavingGrassAmount = wavingGrassAmount;
        td.wavingGrassTint = wavingGrassTint;
    }

    public int GetReadyChunkCount()
    {
        int count = 0;
        foreach (var kv in activeChunks)
        {
            if (kv.Value.isReady)
                count++;
        }
        return count;
    }

    private void RecycleChunk(TerrainChunk chunk)
    {
        if (chunk == null) return;

        // Centraliza cancelamento / limpeza rápida (implementado dentro de TerrainChunk.ResetForPool)
        // Certifique-se de que TerrainChunk.cs contenha o método ResetForPool().
        chunk.ResetForPool();

        // Clear spawned objects (se você tiver lógica extra de spawner)
        if (chunkObjectSpawner != null)
        {

            // Ao reciclar chunk:
            var coord = chunk.chunkCoord;

        }

        // Limpe heights / texturas para footprint mínimo (reduz memória enquanto está no pool)
        if (chunk.terrainData != null)
        {
            try
            {
                chunk.terrainData.terrainLayers = new TerrainLayer[0];
                chunk.terrainData.detailPrototypes = new DetailPrototype[0];
                chunk.terrainData.SetDetailResolution(32, 8);
                chunk.terrainData.alphamapResolution = 32;
                chunk.terrainData.heightmapResolution = 33;
                chunk.terrainData.SetHeights(0, 0, new float[33, 33]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RecycleChunk: falha ao limpar TerrainData do chunk: {ex}");
            }
        }

        // Desativa e devolve ao pool
        try
        {
            chunk.gameObject.SetActive(false);
        }
        catch { /* swallow possible exceptions on destroy-time */ }

        chunk.chunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        chunkPool.Enqueue(chunk);
        chunk.isReady = false;
    }

    #endregion

    #region Generation (background) & Application (main thread)

    private void InitializeChunkTransform(TerrainChunk chunk, Vector2Int coord)
    {
        chunk.chunkCoord = coord;
        Vector3 pos = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
        chunk.gameObject.transform.position = pos;
    }

    private async void StartGenerateChunk(TerrainChunk chunk)
    {
        // Cancel old generation if exists
        chunk.CancelGeneration();
        CancellationTokenSource cts = new CancellationTokenSource();
        chunk.generationToken = cts;
        // Use baseResolution directly
        int targetRes = GetResolution();
        await generationSemaphore.WaitAsync(cts.Token);
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    float[,] heights = GenerateHeightMap(chunk.chunkCoord, targetRes, cts.Token);
                    if (cts.Token.IsCancellationRequested) return;
                    int alphamapRes = targetRes - 1;
                    float[,,] alphamaps = GenerateAlphamap(heights, targetRes, alphamapRes, chunk.chunkCoord, cts.Token);

                    var detailMaps = GenerateGrassDetailMaps(alphamaps);
                    chunk.pendingDetailMaps = detailMaps;

                    if (cts.Token.IsCancellationRequested) return;
                    // Keep the result inside the chunk until main thread applies
                    chunk.pendingHeights = heights;
                    chunk.pendingResolution = targetRes;
                    chunk.pendingAlphamaps = alphamaps;
                    chunk.pendingAlphamapRes = alphamapRes;
                    // enqueue for main-thread application
                    lock (applyQueue)
                    {
                        applyQueue.Enqueue(chunk);
                    }

                    if (SpawnWaterTile && waterTilePrefab != null)
                    {
                        Vector3 chunkWorldPos = new Vector3(chunk.chunkCoord.x * chunkSize, 0, chunk.chunkCoord.y * chunkSize);
                        chunk.waterPosition = new Vector3(chunkWorldPos.x + chunkSize / 2, waterHeight, chunkWorldPos.z + chunkSize / 2);
                        chunk.needsWater = true;
                    }
                }
                catch (OperationCanceledException) { /* canceled */ }
                catch (Exception ex)
                {
                    Debug.LogError("Erro geração heightmap ou alphamap: " + ex);
                }
            }, cts.Token);
        }
        finally
        {
            generationSemaphore.Release();
        }
    }


    private float GetMaxHeight()
    {
        if (noiseSettings == null || noiseSettings.noiseLayers.Count == 0)
            return 1f;

        float max = 0f;
        foreach (var l in noiseSettings.noiseLayers)
            max += Mathf.Abs(l.amplitude);

        return Mathf.Max(1f, max);
    }

    private float[,,] GenerateAlphamap(
        float[,] heights,
        int resolution,
        int alphamapRes,
        Vector2Int chunkCoord,
        CancellationToken token)
    {
        if (heights == null) return null;
        if (allTerrainLayers == null || allTerrainLayers.Length == 0) return null;

        // Prepara definições globais
        List<GlobalLayerDefinition> globalDefs =
            new List<GlobalLayerDefinition>(globalLayerDefinitions ?? new GlobalLayerDefinition[0]);

        float maxHeight = GetMaxHeight();
        float step = (float)chunkSize / Mathf.Max(1, (resolution - 1));
        float alphaStep = (float)chunkSize / Mathf.Max(1, alphamapRes);
        float[,] slopes = ComputeSlopes(heights, resolution, step, maxHeight);

        int numLayers = allTerrainLayers.Length;
        float[,,] alphamaps = new float[alphamapRes, alphamapRes, numLayers];

        for (int az = 0; az < alphamapRes; az++)
        {
            if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();

            for (int ax = 0; ax < alphamapRes; ax++)
            {
                // Safe height sampling
                int h00z = Mathf.Clamp(az, 0, resolution - 1);
                int h00x = Mathf.Clamp(ax, 0, resolution - 1);
                int h01x = Mathf.Clamp(ax + 1, 0, resolution - 1);
                int h10z = Mathf.Clamp(az + 1, 0, resolution - 1);

                float avgH = (
                    heights[h00z, h00x] +
                    heights[h00z, h01x] +
                    heights[h10z, h00x] +
                    heights[h10z, h01x]
                ) / 4f * maxHeight;

                float avgSlope = (
                    slopes[h00z, h00x] +
                    slopes[h00z, h01x] +
                    slopes[h10z, h00x] +
                    slopes[h10z, h01x]
                ) / 4f;

                float worldX = chunkCoord.x * chunkSize + ax * alphaStep;
                float worldZ = chunkCoord.y * chunkSize + az * alphaStep;

                // ⭐ AQUI É A GRANDE MUDANÇA ⭐
                // Terrain agora usa o MESMO bioma lógico do GetBiomeAt()
                BiomeType biome = GetBiomeAt(worldX, worldZ);

                // pega definição deste bioma
                BiomeDefinition biomeDef =
                    biomeDefinitions.First(b => b.biomeType == biome);

                // compõe layers globais + layers do bioma
                List<GlobalLayerDefinition> defs = new List<GlobalLayerDefinition>(globalDefs);
                if (biomeDef.terrainLayerDefinitions != null)
                    defs.AddRange(biomeDef.terrainLayerDefinitions);

                float[] alphaFinal = new float[numLayers];
                ComputeAlphaForDefs(defs, avgH, avgSlope, alphaFinal);

                // normalize α
                float sum = alphaFinal.Sum();
                if (sum > 0f)
                {
                    for (int ch = 0; ch < numLayers; ch++)
                        alphamaps[az, ax, ch] = alphaFinal[ch] / sum;
                }
                else
                {
                    // fallback
                    alphamaps[az, ax, 0] = 1f;
                }
            }
        }

        return alphamaps;
    }


    private List<int[,]> GenerateGrassDetailMaps(float[,,] splatmapData)
    {
        if (splatmapData == null || biomeDefinitions == null || biomeDefinitions.Length == 0)
            return null;

        // Junta todas as grass definitions de todos os biomas
        List<GrassDetailDefinition> allGrassDefs = new List<GrassDetailDefinition>();
        foreach (var biome in biomeDefinitions)
        {
            if (biome.grassDetailDefinitions != null)
                allGrassDefs.AddRange(biome.grassDetailDefinitions);
        }
        if (allGrassDefs.Count == 0) return null;

        List<int[,]> detailMaps = new List<int[,]>();

        int alphaRes = splatmapData.GetLength(0); // assumindo quadrado
        for (int i = 0; i < allGrassDefs.Count; i++)
        {
            var grassDef = allGrassDefs[i];
            if (grassDef.targetLayer == null || !layerToIndex.ContainsKey(grassDef.targetLayer))
            {
                detailMaps.Add(new int[detailResolution, detailResolution]);
                continue;
            }

            int targetChannel = layerToIndex[grassDef.targetLayer];
            int[,] detailLayer = new int[detailResolution, detailResolution];

            for (int z = 0; z < detailResolution; z++)
            {
                for (int x = 0; x < detailResolution; x++)
                {
                    float normX = (float)x / (detailResolution - 1);
                    float normZ = (float)z / (detailResolution - 1);
                    int alphaX = Mathf.RoundToInt(normX * (alphaRes - 1));
                    int alphaZ = Mathf.RoundToInt(normZ * (alphaRes - 1));

                    float splatValue = splatmapData[alphaZ, alphaX, targetChannel];
                    detailLayer[z, x] = splatValue >= grassDef.threshold ? grassDef.grassMapDensity : 0;
                }
            }
            detailMaps.Add(detailLayer);
        }

        return detailMaps;
    }

    private void ComputeAlphaForDefs(List<GlobalLayerDefinition> defs, float avgH, float avgSlope, float[] alphaOut)
    {
        if (defs.Count == 0)
        {
            if (globalLayerDefinitions.Length > 0 && globalLayerDefinitions[0].terrainLayer != null)
            {
                int channel = layerToIndex[globalLayerDefinitions[0].terrainLayer];
                alphaOut[channel] = 1f;
            }
            return;
        }

        float[] weights = new float[defs.Count];
        float sumWeights = 0f;

        for (int l = 0; l < defs.Count; l++)
        {
            var def = defs[l];
            float heightWeight = ComputeSoftWeight(avgH, def.minHeight, def.maxHeight, def.heightBlendDistance);
            float slopeWeight = (def.minSlope == 0f && def.maxSlope == 0f) ? 1f : ComputeSoftWeight(avgSlope, def.minSlope, def.maxSlope, def.slopeBlendDistance);
            weights[l] = heightWeight * slopeWeight;
            sumWeights += weights[l];
        }

        if (sumWeights > 0f)
        {
            for (int l = 0; l < defs.Count; l++)
            {
                var def = defs[l];
                if (def.terrainLayer == null) continue;
                int channel = layerToIndex.ContainsKey(def.terrainLayer) ? layerToIndex[def.terrainLayer] : -1;
                if (channel >= 0)
                {
                    alphaOut[channel] = weights[l] / sumWeights;
                }
            }
        }
        else
        {
            // Fallback: apply first global layer if available
            if (globalLayerDefinitions.Length > 0 && globalLayerDefinitions[0].terrainLayer != null)
            {
                int channel = layerToIndex[globalLayerDefinitions[0].terrainLayer];
                alphaOut[channel] = 1f;
            }
        }
    }

    private float ComputeSoftWeight(float value, float minV, float maxV, float blend)
    {
        if (minV > maxV) return 0f;
        if (value < minV || value > maxV) return 0f;
        if (blend <= 0f) return 1f;

        float weight = 1f;
        if (value < minV + blend)
        {
            weight = Mathf.Min(weight, (value - minV) / blend);
        }
        if (value > maxV - blend)
        {
            weight = Mathf.Min(weight, (maxV - value) / blend);
        }
        return weight;
    }

    private float[,] ComputeSlopes(float[,] heights, int res, float step, float maxHeight)
    {
        float[,] slopes = new float[res, res];

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float dhx = 0f;
                if (x > 0 && x < res - 1)
                {
                    dhx = (heights[z, x + 1] - heights[z, x - 1]) * maxHeight / (2f * step);
                }
                else if (x == 0)
                {
                    dhx = (heights[z, x + 1] - heights[z, x]) * maxHeight / step;
                }
                else
                {
                    dhx = (heights[z, x] - heights[z, x - 1]) * maxHeight / step;
                }

                float dhz = 0f;
                if (z > 0 && z < res - 1)
                {
                    dhz = (heights[z + 1, x] - heights[z - 1, x]) * maxHeight / (2f * step);
                }
                else if (z == 0)
                {
                    dhz = (heights[z + 1, x] - heights[z, x]) * maxHeight / step;
                }
                else
                {
                    dhz = (heights[z, x] - heights[z - 1, x]) * maxHeight / step;
                }

                slopes[z, x] = Mathf.Atan(Mathf.Sqrt(dhx * dhx + dhz * dhz)) * Mathf.Rad2Deg;
            }
        }

        return slopes;
    }

    private IEnumerator ApplyQueueCoroutine()
    {
        while (true)
        {
            int applied = 0;
            while (applyQueue.Count > 0 && applied < maxApplyPerFrame)
            {
                var chunk = applyQueue.Dequeue();
                ApplyHeightmapToTerrain(chunk);
                applied++;
            }
            yield return new WaitForSeconds(applyInterval); // Espalha no tempo
        }
    }

    private void ApplyHeightmapToTerrain(TerrainChunk chunk)
    {
        if (chunk == null || chunk.pendingHeights == null) return;

        TerrainData td = chunk.terrainData;

        ApplyHeights(td, chunk);
        ApplyAlphamaps(td, chunk);
        ApplyPrecomputedGrass(td, chunk);
        ApplyWater(chunk);
        ApplyBiomeAndSpawn(chunk);


        // clean pending
        chunk.pendingHeights = null;
        chunk.pendingAlphamaps = null;
        chunk.pendingDetailMaps = null;
        // 🔥 Dispara o evento global
        OnChunkApplied?.Invoke(chunk);


    }

    private void ApplyHeights(TerrainData td, TerrainChunk chunk)
    {
        int targetRes = chunk.pendingResolution;
        if (td == null || td.heightmapResolution != targetRes)
        {
            td.heightmapResolution = targetRes;
        }
        td.size = new Vector3(chunkSize, terrainHeight, chunkSize);

        try
        {
            td.SetHeightsDelayLOD(0, 0, chunk.pendingHeights);
        }
        catch (Exception ex)
        {
            Debug.LogError("Falha ao SetHeights: " + ex);
        }
    }



    private void ApplyAlphamaps(TerrainData td, TerrainChunk chunk)
    {
        if (chunk.pendingAlphamaps == null || allTerrainLayers.Length == 0) return;

        if (td.alphamapResolution != chunk.pendingAlphamapRes)
        {
            td.alphamapResolution = chunk.pendingAlphamapRes;
        }
        td.terrainLayers = allTerrainLayers;

        try
        {
            td.SetAlphamaps(0, 0, chunk.pendingAlphamaps);
        }
        catch (Exception ex)
        {
            Debug.LogError("Falha ao SetAlphamaps: " + ex);
        }
    }

    private void ApplyPrecomputedGrass(TerrainData td, TerrainChunk chunk)
    {
        if (chunk.pendingDetailMaps == null || chunk.pendingDetailMaps.Count == 0) return;

        // Monta DetailPrototypes
        List<GrassDetailDefinition> allGrassDefs = new List<GrassDetailDefinition>();
        foreach (var biome in biomeDefinitions)
        {
            if (biome.grassDetailDefinitions != null)
                allGrassDefs.AddRange(biome.grassDetailDefinitions);
        }

        DetailPrototype[] detailPrototypes = new DetailPrototype[allGrassDefs.Count];
        for (int i = 0; i < allGrassDefs.Count; i++)
        {
            var grassDef = allGrassDefs[i];
            DetailPrototype prototype = new DetailPrototype();
            if (grassDef.grassRenderMode == GrassRenderMode.Mesh && grassDef.grassPrefab != null)
            {
                prototype.prototype = grassDef.grassPrefab;
                prototype.usePrototypeMesh = true;
                prototype.useInstancing = grassDef.useInstancing;
                prototype.renderMode = DetailRenderMode.VertexLit;

            }
            else if (grassDef.grassRenderMode == GrassRenderMode.Billboard2D && grassDef.grassTexture != null)
            {
                prototype.usePrototypeMesh = false;
                prototype.prototypeTexture = grassDef.grassTexture;
            }

            prototype.minWidth = grassDef.minWidth;
            prototype.maxWidth = grassDef.maxWidth;
            prototype.minHeight = grassDef.minHeight;
            prototype.maxHeight = grassDef.maxHeight;
            prototype.noiseSpread = grassDef.noiseSpread;
            prototype.healthyColor = grassDef.healthyColor;
            prototype.dryColor = grassDef.dryColor;
            prototype.density = grassDef.grassPrototypeDensity;

            detailPrototypes[i] = prototype;
        }

        td.SetDetailResolution(detailResolution, detailResolutionPerPatch);
        td.detailPrototypes = detailPrototypes;

        for (int i = 0; i < chunk.pendingDetailMaps.Count; i++)
        {
            td.SetDetailLayer(0, 0, i, chunk.pendingDetailMaps[i]);
        }
    }

    private void ApplyWater(TerrainChunk chunk)
    {
        if (!chunk.needsWater || waterTilePrefab == null) return;

        if (chunk.waterTile == null)
        {
            chunk.waterTile = Instantiate(
                waterTilePrefab,
                chunk.waterPosition,
                Quaternion.identity,
                chunk.gameObject.transform
            );
        }
        else
        {
            chunk.waterTile.transform.position = chunk.waterPosition;
        }
        chunk.waterTile.transform.localScale = new Vector3(WaterTileSize, 1, WaterTileSize);

        chunk.needsWater = false;
    }



    private void ApplyBiomeAndSpawn(TerrainChunk chunk)
    {
        chunk.biomeType = GetChunkBiome(chunk.chunkCoord);

        if (chunkObjectSpawner != null && chunk.pendingHeights != null && spawnobjects)
        {
            StartCoroutine(chunkObjectSpawner.SpawnObjectsForChunkCoroutine(chunk.terrain, chunk.chunkCoord, chunk.pendingHeights, chunk.biomeType));
        }

        chunk.isReady = true;
    }

    public BiomeType GetChunkBiome(Vector2Int chunkCoord)
    {
        float worldX = chunkCoord.x * chunkSize + chunkSize * 0.5f;
        float worldZ = chunkCoord.y * chunkSize + chunkSize * 0.5f;

        return GetBiomeAt(worldX, worldZ);
    }

    public BiomeType GetBiomeAt(float worldX, float worldZ)
    {
        if (biomeDefinitions == null || biomeDefinitions.Length == 0)
            return BiomeType.Forest;

        float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

        List<int> validBiomes = new List<int>();

        for (int i = 0; i < biomeDefinitions.Length; i++)
        {
            var b = biomeDefinitions[i];

            if (dist >= b.minRadius && (b.maxRadius < 0f || dist <= b.maxRadius))
                validBiomes.Add(i);
        }

        // Se nenhum bioma estiver no range, fallback para o mais próximo
        if (validBiomes.Count == 0)
        {
            // fallback = bioma cujo minRadius é o mais próximo
            int closest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < biomeDefinitions.Length; i++)
            {
                float d = Mathf.Abs(dist - biomeDefinitions[i].minRadius);
                if (d < best)
                {
                    best = d;
                    closest = i;
                }
            }
            return biomeDefinitions[closest].biomeType;
        }

        // Se houver 1 bioma possível → retornamos ele
        if (validBiomes.Count == 1)
            return biomeDefinitions[validBiomes[0]].biomeType;

        // Se houver vários, ainda aplicamos o Perlin Noise para variar dentro da faixa
        float noiseVal = Mathf.PerlinNoise(
            (worldX + worldOffset.x) * biomeNoiseScale,
            (worldZ + worldOffset.y) * biomeNoiseScale
        );

        int idx = Mathf.FloorToInt(noiseVal * validBiomes.Count);
        idx = Mathf.Clamp(idx, 0, validBiomes.Count - 1);

        return biomeDefinitions[validBiomes[idx]].biomeType;
    }

    #endregion

    #region Utility & Debug

    public void ForceRebuildAll()
    {
        // Cancela e regenera todos
        foreach (var kv in activeChunks)
        {
            StartGenerateChunk(kv.Value);
        }
    }

    #endregion

    [BurstCompile]
    struct SlopeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heights;  // Input: heightmap flattened
        [WriteOnly] public NativeArray<float> slopes;  // Output: slopes flattened
        public int res;
        public float step;
        public float maxHeight;

        public void Execute(int index)
        {
            int z = index / res;
            int x = index % res;

            float dhx = 0f;
            if (x > 0 && x < res - 1)
            {
                dhx = (heights[z * res + (x + 1)] - heights[z * res + (x - 1)]) * maxHeight / (2f * step);
            }
            else if (x == 0)
            {
                dhx = (heights[z * res + (x + 1)] - heights[z * res + x]) * maxHeight / step;
            }
            else
            {
                dhx = (heights[z * res + x] - heights[z * res + (x - 1)]) * maxHeight / step;
            }

            float dhz = 0f;
            if (z > 0 && z < res - 1)
            {
                dhz = (heights[(z + 1) * res + x] - heights[(z - 1) * res + x]) * maxHeight / (2f * step);
            }
            else if (z == 0)
            {
                dhz = (heights[(z + 1) * res + x] - heights[z * res + x]) * maxHeight / step;
            }
            else
            {
                dhz = (heights[z * res + x] - heights[(z - 1) * res + x]) * maxHeight / step;
            }

            slopes[index] = Mathf.Atan(Mathf.Sqrt(dhx * dhx + dhz * dhz)) * Mathf.Rad2Deg;
        }
    }

    [BurstCompile]
    struct HeightJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> heights;
        public int resolution;
        public float worldXStart;
        public float worldZStart;
        public float step;
        public Vector2 worldOffset;
        public float maxHeight;
        public float blendDistance;
        [ReadOnly] public NativeArray<NoiseLayer> noiseLayers;
        public float terrainHeight; // novo campo
        public void Execute(int index)
        {
            int z = index / resolution;
            int x = index % resolution;
            float wx = worldXStart + x * step + worldOffset.x;
            float wz = worldZStart + z * step + worldOffset.y;
            float h = CalculateHeightAt(wx, wz);
            heights[index] = h / terrainHeight;
        }

        private float CalculateHeightAt(float worldX, float worldZ)
        {
            float heightSum = 0f;

            for (int i = 0; i < noiseLayers.Length; i++)
            {
                var layer = noiseLayers[i];

                // Camada desativada
                if (!layer.enabled)
                    continue;

                float n = FractalPerlin(worldX, worldZ, layer);
                float contribution = n * layer.amplitude;


                bool applyLayer = false;

                if (Mathf.Approximately(layer.heightThreshold, 0f))
                {
                    applyLayer = true;
                }
                else if (layer.heightThreshold > 0f)
                {
                    if (heightSum >= layer.heightThreshold) applyLayer = true;
                }
                else
                {
                    if (heightSum <= layer.heightThreshold) applyLayer = true;
                }

                float weight = 1f;

                // ⭐ Blend distance agora é individual por layer
                float blend = Mathf.Max(0.0001f, layer.blendDistance);

                if (!Mathf.Approximately(layer.heightThreshold, 0f))
                {
                    float diff = heightSum - layer.heightThreshold;

                    // Aqui usamos o blend individual
                    weight = Mathf.Clamp01(0.5f + diff / blend);
                }

                if (applyLayer)
                    heightSum += contribution * weight;
            }

            return Mathf.Clamp(heightSum, -10000f, 10000f);
        }

        private float FractalPerlin(
    float x,
    float z,
    NoiseLayer layer
)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            int octaves = Mathf.Max(1, layer.octaves);

            for (int i = 0; i < octaves; i++)
            {
                float seedOffset = (i + 1) * 37.13f;

                float nx = (x + seedOffset) * layer.scaleX * frequency;
                float nz = (z + seedOffset) * layer.scaleY * frequency;


                float perlin = Mathf.PerlinNoise(nx, nz);
                value += perlin * amplitude;

                maxValue += amplitude;

                amplitude *= layer.persistence;
                frequency *= layer.lacunarity;
            }

            return value / maxValue; // normaliza 0–1
        }


    }

    private float[,] GenerateHeightMap(Vector2Int chunkCoord, int resolution, CancellationToken token)
    {
        // resolution is heightmap resolution (N). We'll produce values in 0..1 normalized by terrainHeight.
        float[,] heights = new float[resolution, resolution];

        // compute offsets in world coordinates
        float worldXStart = chunkCoord.x * chunkSize;
        float worldZStart = chunkCoord.y * chunkSize;

        // Each sample spacing in world coords:
        float step = (float)chunkSize / (resolution - 1);

        // Pré-calcule maxHeight fora do loop
        float maxHeight = GetMaxHeight();

        // local RNG seed per chunk for deterministic results (not used in this function, but kept for consistency)
        System.Random localRnd = new System.Random(seed + chunkCoord.x * 73856093 ^ chunkCoord.y * 19349663);

        // Use Burst Job for height calculation
        using (var heightsArray = new NativeArray<float>(resolution * resolution, Allocator.TempJob))

        using (var noiseLayersArray = new NativeArray<NoiseLayer>(
                   noiseSettings != null ? noiseSettings.noiseLayers.ToArray() : new NoiseLayer[0],
                   Allocator.TempJob))
        {
            var job = new HeightJob
            {
                heights = heightsArray,
                resolution = resolution,
                worldXStart = worldXStart,
                worldZStart = worldZStart,
                step = step,
                worldOffset = worldOffset,
                terrainHeight = terrainHeight,
                noiseLayers = noiseLayersArray
            };


            var handle = job.Schedule(resolution * resolution, 64); // Batch size 64 for parallelism
            handle.Complete();

            // Copy from NativeArray to float[,]
            for (int i = 0; i < heightsArray.Length; i++)
            {
                int z = i / resolution;
                int x = i % resolution;
                heights[z, x] = heightsArray[i];
            }
        }

        return heights;
    }
}