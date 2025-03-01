using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum BorderDirection { North, East, South, West }

[RequireComponent(typeof(TerrainObjectSpawner))]
public class InfiniteTerrain : MonoBehaviour
{
    public static InfiniteTerrain Instance { get; private set; }

    [Header("References")]
    public Transform player;

    [Header("Terrain Settings")]
    [Range(256, 1024)]
    public int chunkSize = 512;
    [Range(257, 1025)]
    public int terrainResolution = 513; // (2^n) + 1
    public float terrainHeight = 100f;
    public int seed = 42;

    [Header("Biome Settings")]
    [Tooltip("Noise scale for biome determination.")]
    public float biomeNoiseScale = 0.001f;
    [Tooltip("Available biome definitions.")]
    public BiomeDefinition[] biomeDefinitions;

    [Header("Render Distance")]
    public int renderDistance = 2;

    [Tooltip("Detail map resolution (higher value means more details).")]
    public int detailResolution = 256;
    public int detailResolutionPerPatch = 16;
    public float wavingGrassStrength = 0.2f;
    public float wavingGrassAmount = 0.5f;
    public Color wavingGrassTint;

    [Header("Alphamap")]
    public int alphamapResolution = 512;

    [Header("Chunk Limit")]
    public int maxChunkCount = 50;

    public TerrainObjectSpawner objectSpawner;

    // Private fields for internal control
    private Dictionary<Vector2Int, Terrain> terrainChunks = new Dictionary<Vector2Int, Terrain>();
    private Queue<Vector2Int> chunkQueue = new Queue<Vector2Int>();
    private bool isChunkCoroutineRunning = false;
    private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

    [Header("Global Layer Settings")]
    public GlobalLayerDefinition[] globalLayerDefinitions;
    [Header("Transition Margin Settings")]
    public int minBlendMargin = 30;
    public int maxBlendMargin = 70;
    public float blendMarginNoiseScale = 0.05f;

    [Header("Transition Curve Settings")]
    public float transitionCurveExponent = 2f; // Valores maiores deixam a curva mais acentuada.


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        Vector2Int currentPlayerChunk = GetPlayerChunkCoord();

        if (currentPlayerChunk != lastPlayerChunkCoord)
        {
            UpdateChunks();
            lastPlayerChunkCoord = currentPlayerChunk;
        }
    }

    Vector2Int GetPlayerChunkCoord()
    {
        return new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );
    }

    void UpdateChunks()
    {
        // Remove chunks nulos
        List<Vector2Int> keysToClean = new List<Vector2Int>();
        foreach (var kv in terrainChunks)
        {
            if (kv.Value == null)
                keysToClean.Add(kv.Key);
        }
        foreach (var key in keysToClean)
            terrainChunks.Remove(key);

        Vector2Int playerChunkCoord = GetPlayerChunkCoord();

        // Enfileira chunks dentro da distância de renderização
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(playerChunkCoord.x + x, playerChunkCoord.y + z);
                if (!terrainChunks.ContainsKey(chunkCoord) && !chunkQueue.Contains(chunkCoord))
                    chunkQueue.Enqueue(chunkCoord);
            }
        }

        if (!isChunkCoroutineRunning && chunkQueue.Count > 0)
        {
            StartCoroutine(ProcessChunkQueue());
            isChunkCoroutineRunning = true;
        }

        LimitChunks();
    }

    void LimitChunks()
    {
        if (terrainChunks.Count <= maxChunkCount)
            return;

        List<Vector2Int> keys = new List<Vector2Int>(terrainChunks.Keys);
        keys.Sort((a, b) =>
        {
            Vector2 aPos = new Vector2(a.x * chunkSize, a.y * chunkSize);
            Vector2 bPos = new Vector2(b.x * chunkSize, b.y * chunkSize);
            float distA = Vector2.Distance(aPos, new Vector2(player.position.x, player.position.z));
            float distB = Vector2.Distance(bPos, new Vector2(player.position.x, player.position.z));
            return distB.CompareTo(distA);
        });

        while (terrainChunks.Count > maxChunkCount && keys.Count > 0)
        {
            Vector2Int keyToRemove = keys[0];
            keys.RemoveAt(0);
            if (terrainChunks.TryGetValue(keyToRemove, out Terrain terrain))
            {
                Destroy(terrain.gameObject);
            }
            terrainChunks.Remove(keyToRemove);
        }
    }

    IEnumerator ProcessChunkQueue()
    {
        while (chunkQueue.Count > 0)
        {
            Vector2Int coord = chunkQueue.Dequeue();
            CreateChunkAsync(coord);
            yield return new WaitForSeconds(0.5f);
        }
        isChunkCoroutineRunning = false;
    }

    async void CreateChunkAsync(Vector2Int coord)
    {
        Vector3 chunkWorldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

        // Determina o bioma predominante para este chunk (usando a posição central)
        BiomeDefinition centerBiome = GetBiomeAtPosition(chunkWorldPos);

        int biomeLayerCount = (centerBiome.terrainLayerDefinitions != null ? centerBiome.terrainLayerDefinitions.Length : 0);
        int globalLayerCount = (globalLayerDefinitions != null ? globalLayerDefinitions.Length : 0);
        int totalLayers = biomeLayerCount + globalLayerCount;

        var result = await Task.Run(() =>
        {
            float[,] heights = GenerateHeightMap(chunkWorldPos);
            float[,,] splatmapData = null;

            if (totalLayers > 0)
            {
                splatmapData = GenerateSplatmapData(heights, centerBiome, globalLayerDefinitions);
            }

            return (heights, splatmapData);
        });

        float[,] heightsResult = result.heights;
        float[,,] splatmapDataResult = result.splatmapData;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = terrainResolution;
        terrainData.size = new Vector3(chunkSize, terrainHeight, chunkSize);
        terrainData.alphamapResolution = alphamapResolution;
        terrainData.wavingGrassStrength = wavingGrassStrength;
        terrainData.wavingGrassAmount = wavingGrassAmount;
        terrainData.wavingGrassTint = wavingGrassTint;
        terrainData.wavingGrassSpeed = 0;

        terrainData.SetHeights(0, 0, heightsResult);

        if (totalLayers > 0)
        {
            TerrainLayer[] layers = new TerrainLayer[totalLayers];
            // Layers do bioma central
            for (int i = 0; i < biomeLayerCount; i++)
            {
                layers[i] = centerBiome.terrainLayerDefinitions[i].terrainLayer;
            }
            // Layers globais
            for (int i = 0; i < globalLayerCount; i++)
            {
                layers[biomeLayerCount + i] = globalLayerDefinitions[i].terrainLayer;
            }
            terrainData.terrainLayers = layers;
            terrainData.SetAlphamaps(0, 0, splatmapDataResult);
        }

        ApplyGrassDetails(terrainData, splatmapDataResult, centerBiome);

        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.transform.SetParent(transform);
        terrainObj.transform.position = chunkWorldPos;
        Terrain terrain = terrainObj.GetComponent<Terrain>();

        // Adiciona o TerrainChunkInfo e define o bioma predominante
        TerrainChunkInfo chunkInfo = terrainObj.AddComponent<TerrainChunkInfo>();
        chunkInfo.biome = centerBiome;

        // Atualiza os vizinhos – o chunk atual é responsável por marcar suas próprias bordas de blend
        UpdateNeighbors(coord, chunkInfo);

        if (!terrainChunks.ContainsKey(coord))
            terrainChunks.Add(coord, terrain);



        // Se houver bordas com vizinhos de bioma diferente, aplica o blend somente neste chunk
        if (chunkInfo.hasDifferentNeighbor && splatmapDataResult != null)
        {
            // Converte layers atuais para lista para facilitar a verificação
            List<TerrainLayer> layerList = new List<TerrainLayer>(terrainData.terrainLayers);
            int currentTotalLayers = layerList.Count;

            // Para cada borda diferente, verifica se a layer do vizinho já está presente; se não, adiciona e expande o splatmap.
            // Em seguida, aplica o blend na borda correspondente.
            if (chunkInfo.blendNorth)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapDataResult = ExpandSplatmapChannels(splatmapDataResult, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapDataResult, currentTotalLayers, neighborLayerIndex, heightsResult, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.North);
            }
            if (chunkInfo.blendEast)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapDataResult = ExpandSplatmapChannels(splatmapDataResult, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapDataResult, currentTotalLayers, neighborLayerIndex, heightsResult, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.East);
            }
            if (chunkInfo.blendSouth)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapDataResult = ExpandSplatmapChannels(splatmapDataResult, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapDataResult, currentTotalLayers, neighborLayerIndex, heightsResult, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.South);
            }
            if (chunkInfo.blendWest)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapDataResult = ExpandSplatmapChannels(splatmapDataResult, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapDataResult, currentTotalLayers, neighborLayerIndex, heightsResult, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.West);
            }
            terrainData.terrainLayers = layerList.ToArray();
            terrainData.SetAlphamaps(0, 0, splatmapDataResult);
        }

        if (objectSpawner != null)
        {
            StartCoroutine(objectSpawner.SpawnObjectsOnChunkCoroutine(terrain, chunkWorldPos, terrainObj, chunkSize));
        }


    }

    // Expande o splatmap para incluir um novo canal (layer)
    float[,,] ExpandSplatmapChannels(float[,,] splatmapData, int newTotalLayers)
    {
        int alphaRes = alphamapResolution;
        float[,,] newSplatmapData = new float[alphaRes, alphaRes, newTotalLayers];
        int currentTotalLayers = splatmapData.GetLength(2);
        for (int z = 0; z < alphaRes; z++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                for (int i = 0; i < currentTotalLayers; i++)
                {
                    newSplatmapData[z, x, i] = splatmapData[z, x, i];
                }
                newSplatmapData[z, x, newTotalLayers - 1] = 0f;
            }
        }
        return newSplatmapData;
    }

    // Gera o mapa de alturas
    private float[,] GenerateHeightMap(Vector3 offset)
    {
        float[,] heights = new float[terrainResolution, terrainResolution];
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int z = 0; z < terrainResolution; z++)
            {
                float percentX = (float)x / (terrainResolution - 1);
                float percentZ = (float)z / (terrainResolution - 1);
                float worldX = offset.x + percentX * chunkSize;
                float worldZ = offset.z + percentZ * chunkSize;
                float height = ComputeBlendedHeight(worldX, worldZ);
                heights[z, x] = height / terrainHeight;
            }
        }
        return heights;
    }

    // Gera os dados da splatmap usando os pesos das layers
    private float[,,] GenerateSplatmapData(float[,] heights, BiomeDefinition biome, GlobalLayerDefinition[] globalLayers)
    {
        int biomeLayerCount = (biome.terrainLayerDefinitions != null ? biome.terrainLayerDefinitions.Length : 0);
        int globalLayerCount = (globalLayers != null ? globalLayers.Length : 0);
        int totalLayers = biomeLayerCount + globalLayerCount;
        float[,,] splatmapData = new float[alphamapResolution, alphamapResolution, totalLayers];

        for (int z = 0; z < alphamapResolution; z++)
        {
            for (int x = 0; x < alphamapResolution; x++)
            {
                float heightNormalized = heights[z, x];
                float worldHeight = heightNormalized * terrainHeight;
                float slope = CalculateSlope(heights, x, z);

                float totalWeight = 0f;
                float[] weights = new float[totalLayers];

                // Pesos para as layers do bioma
                for (int i = 0; i < biomeLayerCount; i++)
                {
                    TerrainLayerDefinition def = biome.terrainLayerDefinitions[i];
                    float weight = 1f;
                    if (worldHeight < def.minHeight || worldHeight > def.maxHeight)
                        weight = 0f;
                    if (slope < def.minSlope || slope > def.maxSlope)
                        weight = 0f;
                    weights[i] = weight;
                    totalWeight += weight;
                }

                // Pesos para as layers globais
                for (int i = 0; i < globalLayerCount; i++)
                {
                    GlobalLayerDefinition globalDef = globalLayers[i];
                    float weight = 1f;
                    if (worldHeight < globalDef.minHeight || worldHeight > globalDef.maxHeight)
                        weight = 0f;
                    if (slope < globalDef.minSlope || slope > globalDef.maxSlope)
                        weight = 0f;
                    weights[biomeLayerCount + i] = weight;
                    totalWeight += weight;
                }

                if (totalWeight == 0f)
                {
                    if (biomeLayerCount > 0)
                    {
                        weights[0] = 1f;
                        totalWeight = 1f;
                    }
                    else if (globalLayerCount > 0)
                    {
                        weights[biomeLayerCount] = 1f;
                        totalWeight = 1f;
                    }
                }

                for (int i = 0; i < totalLayers; i++)
                {
                    splatmapData[z, x, i] = weights[i] / totalWeight;
                }
            }
        }
        return splatmapData;
    }

    private float ComputeBlendedHeight(float worldX, float worldZ)
    {
        float bn = Mathf.PerlinNoise((worldX + seed) * biomeNoiseScale, (worldZ + seed) * biomeNoiseScale);

        float wDesert = 1f - Mathf.InverseLerp(0.2f, 0.3f, bn);
        float wForest = 1f - Mathf.Abs(bn - 0.5f) / 0.2f;
        float wTundra = Mathf.InverseLerp(0.7f, 0.8f, bn);

        wDesert = Mathf.Max(0, wDesert);
        wForest = Mathf.Max(0, wForest);
        wTundra = Mathf.Max(0, wTundra);

        float total = wDesert + wForest + wTundra;
        wDesert /= total;
        wForest /= total;
        wTundra /= total;

        BiomeDefinition desert = GetBiomeByType(BiomeType.Desert);
        BiomeDefinition forest = GetBiomeByType(BiomeType.Forest);
        BiomeDefinition tundra = GetBiomeByType(BiomeType.Tundra);

        float hDesert = CalculateHeight(worldX, worldZ, desert);
        float hForest = CalculateHeight(worldX, worldZ, forest);
        float hTundra = CalculateHeight(worldX, worldZ, tundra);

        return wDesert * hDesert + wForest * hForest + wTundra * hTundra;
    }

    private BiomeDefinition GetBiomeByType(BiomeType type)
    {
        foreach (BiomeDefinition biome in biomeDefinitions)
        {
            if (biome.biomeType == type)
                return biome;
        }
        return biomeDefinitions.Length > 0 ? biomeDefinitions[0] : null;
    }

    private BiomeDefinition GetBiomeAtPosition(Vector3 pos)
    {
        float bn = Mathf.PerlinNoise((pos.x + seed) * biomeNoiseScale, (pos.z + seed) * biomeNoiseScale);
        if (bn < 0.33f)
            return GetBiomeByType(BiomeType.Desert);
        else if (bn < 0.66f)
            return GetBiomeByType(BiomeType.Forest);
        else
            return GetBiomeByType(BiomeType.Tundra);
    }

    void ApplyGrassDetails(TerrainData terrainData, float[,,] splatmapData, BiomeDefinition biome)
    {
        if (biome.grassDetailDefinition == null || splatmapData == null)
            return;

        GrassDetailDefinition grassDef = biome.grassDetailDefinition;

        if (biome.terrainLayerDefinitions == null || grassDef.targetLayerIndex < 0 ||
            grassDef.targetLayerIndex >= biome.terrainLayerDefinitions.Length)
        {
            Debug.LogError("Invalid grass layer index!");
            return;
        }

        bool validForMesh = grassDef.grassRenderMode == GrassRenderMode.Mesh && grassDef.grassPrefab != null;
        bool validForBillboard = grassDef.grassRenderMode == GrassRenderMode.Billboard2D && grassDef.grassTexture != null;

        if (!validForMesh && !validForBillboard)
        {
            Debug.LogError("Please configure the grass prefab or texture according to the selected mode.");
            return;
        }

        terrainData.SetDetailResolution(detailResolution, detailResolutionPerPatch);
        DetailPrototype[] detailPrototypes = new DetailPrototype[1];
        DetailPrototype prototype = new DetailPrototype();

        if (grassDef.grassRenderMode == GrassRenderMode.Mesh)
        {
            prototype.prototype = grassDef.grassPrefab;
            prototype.usePrototypeMesh = true;
        }
        else if (grassDef.grassRenderMode == GrassRenderMode.Billboard2D)
        {
            prototype.prototypeTexture = grassDef.grassTexture;
            prototype.usePrototypeMesh = false;
        }

        prototype.minWidth = grassDef.minWidth;
        prototype.maxWidth = grassDef.maxWidth;
        prototype.minHeight = grassDef.minHeight;
        prototype.maxHeight = grassDef.maxHeight;
        prototype.noiseSpread = grassDef.noiseSpread;
        prototype.healthyColor = grassDef.healthyColor;
        prototype.dryColor = grassDef.dryColor;
        prototype.density = grassDef.grassPrototypeDensity;

        detailPrototypes[0] = prototype;
        terrainData.detailPrototypes = detailPrototypes;

        int[,] detailLayer = new int[detailResolution, detailResolution];
        int alphaRes = terrainData.alphamapResolution;
        for (int z = 0; z < detailResolution; z++)
        {
            for (int x = 0; x < detailResolution; x++)
            {
                float normX = (float)x / (detailResolution - 1);
                float normZ = (float)z / (detailResolution - 1);
                int alphaX = Mathf.RoundToInt(normX * (alphaRes - 1));
                int alphaZ = Mathf.RoundToInt(normZ * (alphaRes - 1));
                float splatValue = splatmapData[alphaZ, alphaX, grassDef.targetLayerIndex];
                detailLayer[z, x] = splatValue >= grassDef.threshold ? grassDef.grassMapDensity : 0;
            }
        }
        terrainData.SetDetailLayer(0, 0, 0, detailLayer);
    }

    float CalculateHeight(float worldX, float worldZ, BiomeDefinition biome)
    {
        float y = Mathf.PerlinNoise((worldX + seed) * biome.highFrequencyScale, (worldZ + seed) * biome.highFrequencyScale) * biome.highFrequencyAmplitude;
        y += Mathf.PerlinNoise((worldX + seed) * biome.lowFrequencyScale, (worldZ + seed) * biome.lowFrequencyScale) * biome.lowFrequencyAmplitude;
        return y;
    }

    float CalculateSlope(float[,] heights, int x, int z)
    {
        int xLeft = Mathf.Max(x - 1, 0);
        int xRight = Mathf.Min(x + 1, heights.GetLength(1) - 1);
        int zDown = Mathf.Max(z - 1, 0);
        int zUp = Mathf.Min(z + 1, heights.GetLength(0) - 1);

        float heightL = heights[z, xLeft] * terrainHeight;
        float heightR = heights[z, xRight] * terrainHeight;
        float heightD = heights[zDown, x] * terrainHeight;
        float heightU = heights[zUp, x] * terrainHeight;

        float cellSize = chunkSize / (float)(terrainResolution - 1);
        float dX = (heightR - heightL) / (2f * cellSize);
        float dZ = (heightU - heightD) / (2f * cellSize);
        float gradient = Mathf.Sqrt(dX * dX + dZ * dZ);
        return Mathf.Atan(gradient) * Mathf.Rad2Deg;
    }

    void UpdateNeighbors(Vector2Int coord, TerrainChunkInfo chunkInfo)
    {
        // Definindo as direções: Norte, Leste, Sul e Oeste
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Norte
            new Vector2Int(1, 0),   // Leste
            new Vector2Int(0, -1),  // Sul
            new Vector2Int(-1, 0)   // Oeste
        };

        int differentCount = 0; // Contador de vizinhos com bioma diferente

        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborCoord = coord + direction;
            if (terrainChunks.TryGetValue(neighborCoord, out Terrain neighborTerrain))
            {
                TerrainChunkInfo neighborInfo = neighborTerrain.GetComponent<TerrainChunkInfo>();
                if (neighborInfo != null && neighborInfo.biome != chunkInfo.biome)
                {
                    differentCount++;

                    // Seta a flag e registra o bioma vizinho para cada lado
                    if (direction == new Vector2Int(0, 1)) // Norte
                    {
                        chunkInfo.blendNorth = true;
                        chunkInfo.neighborBiomeNorth = neighborInfo.biome;
                    }
                    else if (direction == new Vector2Int(1, 0)) // Leste
                    {
                        chunkInfo.blendEast = true;
                        chunkInfo.neighborBiomeEast = neighborInfo.biome;
                    }
                    else if (direction == new Vector2Int(0, -1)) // Sul
                    {
                        chunkInfo.blendSouth = true;
                        chunkInfo.neighborBiomeSouth = neighborInfo.biome;
                    }
                    else if (direction == new Vector2Int(-1, 0)) // Oeste
                    {
                        chunkInfo.blendWest = true;
                        chunkInfo.neighborBiomeWest = neighborInfo.biome;
                    }
                }
            }
        }

        // Registra se existe ao menos um vizinho com bioma diferente
        chunkInfo.hasDifferentNeighbor = differentCount > 0;
        // Registra se há mais de um vizinho com bioma diferente
        chunkInfo.hasMultipleDifferentNeighbors = differentCount > 1;
    }


    // Aplica o blend em apenas uma borda (lado) definido pelo BorderDirection
    // Aplica o blend em apenas uma borda (lado) definido pelo BorderDirection
    // com comprimento de margem variável e uma curva de transição não linear.
    void BlendBorderWithSide(
        ref float[,,] splatmapData,
        int totalLayers,
        int neighborLayerIndex,
        float[,] heightMap,
        float terrainHeight,
        float minTransitionHeight,
        float maxTransitionHeight,
        BorderDirection direction)
    {
        int width = splatmapData.GetLength(1);
        int height = splatmapData.GetLength(0);

        // Percorre cada pixel do splatmap
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float worldHeight = heightMap[z, x] * terrainHeight;
                if (worldHeight < minTransitionHeight || worldHeight > maxTransitionHeight)
                    continue;

                // Calcula um comprimento efetivo de margem para este pixel usando Perlin Noise
                float noiseValue = Mathf.PerlinNoise(x * blendMarginNoiseScale, z * blendMarginNoiseScale);
                int effectiveBlendMargin = Mathf.RoundToInt(Mathf.Lerp(minBlendMargin, maxBlendMargin, noiseValue));

                float blendFactor = 0f;
                float t = 0f;

                // Calcula a fração de transição (t) dependendo da direção e da distância em relação à borda
                if (direction == BorderDirection.North && z >= height - effectiveBlendMargin)
                {
                    t = (height - z - 1) / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.South && z < effectiveBlendMargin)
                {
                    t = z / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.East && x >= width - effectiveBlendMargin)
                {
                    t = (width - x - 1) / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.West && x < effectiveBlendMargin)
                {
                    t = x / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }

                if (blendFactor > 0f)
                {
                    // Aplica a interpolação entre o valor atual e o valor da layer vizinha
                    for (int layer = 0; layer < totalLayers; layer++)
                    {
                        float targetValue = (layer == neighborLayerIndex) ? 1f : 0f;
                        splatmapData[z, x, layer] = Mathf.Lerp(splatmapData[z, x, layer], targetValue, blendFactor);
                    }
                    // Normaliza os valores para garantir que a soma seja 1
                    float sum = 0f;
                    for (int layer = 0; layer < totalLayers; layer++)
                        sum += splatmapData[z, x, layer];
                    if (sum > 0f)
                    {
                        for (int layer = 0; layer < totalLayers; layer++)
                            splatmapData[z, x, layer] /= sum;
                    }
                }
            }
        }
    }

}