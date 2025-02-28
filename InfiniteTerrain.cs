
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class GlobalLayerDefinition
{
    public TerrainLayer terrainLayer;
    public float minHeight; // altura mínima para aplicação
    public float maxHeight; // altura máxima para aplicação
    public float minSlope;  // inclinação mínima
    public float maxSlope;  // inclinação máxima
    // Outras configurações podem ser adicionadas aqui...
}

[RequireComponent(typeof(TerrainObjectSpawner))]
public class InfiniteTerrain : MonoBehaviour
{
    public static InfiniteTerrain Instance { get; private set; }

    [Header("Referências")]
    public Transform player;

    [Header("Configurações do Terreno")]
    [Range(256, 1024)]
    public int chunkSize = 512;
    [Range(257, 1025)]
    public int terrainResolution = 513; // (2^n) + 1
    public float terrainHeight = 100f;
    public int seed = 42;

    [Header("Configurações dos Biomas")]
    [Tooltip("Escala do ruído para a determinação dos biomas.")]
    public float biomeNoiseScale = 0.001f;
    [Tooltip("Definições dos biomas disponíveis.")]
    public BiomeDefinition[] biomeDefinitions;

    [Header("Distância de Renderização")]
    public int renderDistance = 2;

    [Tooltip("Resolução do detail map (quanto maior, mais detalhes).")]
    public int detailResolution = 256;
    public int detailResolutionPerPacht = 16;
    public float wavingGrassStrength = 0.2f;
    public float wavingGrassAmount = 0.5f;
    public Color wavingGrassTint;

    [Header("Alphamap")]
    public int alphamapResolution = 512;

    [Header("Limite de Chunks")]
    public int maxChunkCount = 50;

    public TerrainObjectSpawner objectSpawner;

    // Campos privados para controle interno
    private Dictionary<Vector2Int, Terrain> terrainChunks = new Dictionary<Vector2Int, Terrain>();
    private Queue<Vector2Int> chunkQueue = new Queue<Vector2Int>();
    private bool isChunkCoroutineRunning = false;
    private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

    [Header("Configurações da Camada Global")]
    public GlobalLayerDefinition[] globalLayerDefinitions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Se desejar que essa instância persista entre cenas, descomente a linha abaixo:
        // DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        Vector2Int currentPlayerChunk = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        if (currentPlayerChunk != lastPlayerChunkCoord)
        {
            //Debug.Log("Chunks atualizados");
            AtualizarChunks();
            lastPlayerChunkCoord = currentPlayerChunk;
        }
    }

    void AtualizarChunks()
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

        Vector2Int playerChunkCoord = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        // Enfileira os chunks dentro da área de renderização
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

        LimitarChunks();
    }

    void LimitarChunks()
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
            CriarChunkAsync(coord);
            yield return new WaitForSeconds(0.5f);
        }
        isChunkCoroutineRunning = false;
    }

    async void CriarChunkAsync(Vector2Int coord)
    {
        Vector3 chunkWorldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

        // Determina o bioma predominante para este chunk (usando a posição central)
        BiomeDefinition biomeAtCenter = GetBiomeAtPosition(chunkWorldPos);

        // Calcula quantas layers teremos: as do bioma + global layers (se definidas)
        int biomeLayerCount = (biomeAtCenter.terrainLayerDefinitions != null ? biomeAtCenter.terrainLayerDefinitions.Length : 0);
        int globalLayerCount = (globalLayerDefinitions != null ? globalLayerDefinitions.Length : 0);
        int totalLayers = biomeLayerCount + globalLayerCount;

        var result = await Task.Run(() =>
        {
            // Gera as alturas com blending entre biomas
            float[,] alturas = GenerateHeights(chunkWorldPos);
            float[,,] splatmapData = null;

            if (totalLayers > 0)
            {
                splatmapData = new float[alphamapResolution, alphamapResolution, totalLayers];
                for (int z = 0; z < alphamapResolution; z++)
                {
                    for (int x = 0; x < alphamapResolution; x++)
                    {
                        float heightNormalized = alturas[z, x];
                        float worldHeight = heightNormalized * terrainHeight;
                        float slope = CalcularSlope(alturas, x, z);

                        float totalWeight = 0f;
                        float[] weights = new float[totalLayers];

                        // Calcula pesos para as layers do bioma
                        for (int i = 0; i < biomeLayerCount; i++)
                        {
                            TerrainLayerDefinition def = biomeAtCenter.terrainLayerDefinitions[i];
                            float weight = 1f;
                            if (worldHeight < def.minHeight || worldHeight > def.maxHeight)
                                weight = 0f;
                            if (slope < def.minSlope || slope > def.maxSlope)
                                weight = 0f;
                            weights[i] = weight;
                            totalWeight += weight;
                        }

                        // Calcula pesos para cada global layer
                        for (int i = 0; i < globalLayerCount; i++)
                        {
                            GlobalLayerDefinition globalDef = globalLayerDefinitions[i];
                            float weight = 1f;
                            if (worldHeight < globalDef.minHeight || worldHeight > globalDef.maxHeight)
                                weight = 0f;
                            if (slope < globalDef.minSlope || slope > globalDef.maxSlope)
                                weight = 0f;
                            weights[biomeLayerCount + i] = weight;
                            totalWeight += weight;
                        }

                        // Caso nenhum peso seja atribuído, define fallback para a primeira layer disponível
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
            }

            return (alturas, splatmapData);
        });

        float[,] alturasResult = result.alturas;
        float[,,] splatmapDataResult = result.splatmapData;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = terrainResolution;
        terrainData.size = new Vector3(chunkSize, terrainHeight, chunkSize);
        terrainData.alphamapResolution = alphamapResolution;
        terrainData.wavingGrassStrength = wavingGrassStrength;
        terrainData.wavingGrassAmount = wavingGrassAmount;
        terrainData.wavingGrassTint = wavingGrassTint;
        terrainData.wavingGrassSpeed = 0;

        terrainData.SetHeights(0, 0, alturasResult);

        // Configura as layers do TerrainData usando as layers do bioma e as global layers, se definidas
        if (totalLayers > 0)
        {
            TerrainLayer[] layers = new TerrainLayer[totalLayers];
            // Layers do bioma
            for (int i = 0; i < biomeLayerCount; i++)
            {
                layers[i] = biomeAtCenter.terrainLayerDefinitions[i].terrainLayer;
            }
            // Global layers
            for (int i = 0; i < globalLayerCount; i++)
            {
                layers[biomeLayerCount + i] = globalLayerDefinitions[i].terrainLayer;
            }
            terrainData.terrainLayers = layers;
            terrainData.SetAlphamaps(0, 0, splatmapDataResult);
        }

        AplicarDetalhesGrama(terrainData, splatmapDataResult, biomeAtCenter);

        GameObject terrenoObj = Terrain.CreateTerrainGameObject(terrainData);
        terrenoObj.transform.SetParent(transform);
        terrenoObj.transform.position = chunkWorldPos;
        Terrain terreno = terrenoObj.GetComponent<Terrain>();

        // Adiciona o componente e atribui o bioma predominante
        TerrainChunkInfo chunkInfo = terrenoObj.AddComponent<TerrainChunkInfo>();
        chunkInfo.biome = biomeAtCenter;

        if (!terrainChunks.ContainsKey(coord))
            terrainChunks.Add(coord, terreno);

        if (objectSpawner != null)
        {
            StartCoroutine(objectSpawner.SpawnObjectsOnChunkCoroutine(terreno, chunkWorldPos, terrenoObj, chunkSize));
        }
    }

    private float[,] GenerateHeights(Vector3 offset)
    {
        float[,] alturas = new float[terrainResolution, terrainResolution];
        for (int x = 0; x < terrainResolution; x++)
        {
            for (int z = 0; z < terrainResolution; z++)
            {
                float percentX = (float)x / (terrainResolution - 1);
                float percentZ = (float)z / (terrainResolution - 1);
                float worldX = offset.x + percentX * chunkSize;
                float worldZ = offset.z + percentZ * chunkSize;
                float altura = ComputeBlendedHeight(worldX, worldZ);
                alturas[z, x] = altura / terrainHeight;
            }
        }
        return alturas;
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

        BiomeDefinition desert = GetBiomeByType(BiomeType.Deserto);
        BiomeDefinition forest = GetBiomeByType(BiomeType.Floresta);
        BiomeDefinition tundra = GetBiomeByType(BiomeType.Tundra);

        float hDesert = CalcularAltura(worldX, worldZ, desert);
        float hForest = CalcularAltura(worldX, worldZ, forest);
        float hTundra = CalcularAltura(worldX, worldZ, tundra);

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
            return GetBiomeByType(BiomeType.Deserto);
        else if (bn < 0.66f)
            return GetBiomeByType(BiomeType.Floresta);
        else
            return GetBiomeByType(BiomeType.Tundra);
    }

    void AplicarDetalhesGrama(TerrainData terrainData, float[,,] splatmapData, BiomeDefinition biome)
    {
        // Verifica se o bioma possui uma configuração de grama definida
        if (biome.grassDetailDefinition == null || splatmapData == null)
            return;

        GrassDetailDefinition grassDef = biome.grassDetailDefinition;

        // Valida o índice da camada de grama com base nas layers do bioma
        if (biome.terrainLayerDefinitions == null || grassDef.targetLayerIndex < 0 ||
            grassDef.targetLayerIndex >= biome.terrainLayerDefinitions.Length)
        {
            Debug.LogError("Índice da camada de grama inválido!");
            return;
        }

        // Verifica se as configurações são válidas de acordo com o modo de renderização
        bool validForMesh = grassDef.grassRenderMode == GrassRenderMode.Mesh && grassDef.grassPrefab != null;
        bool validForBillboard = grassDef.grassRenderMode == GrassRenderMode.Billboard2D && grassDef.grassTexture != null;

        if (!validForMesh && !validForBillboard)
        {
            Debug.LogError("Configure corretamente o prefab ou a texture para a grama, conforme o modo selecionado.");
            return;
        }

        terrainData.SetDetailResolution(detailResolution, detailResolutionPerPacht);
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

    public void RenomearChunksFronteiraBioma()
    {
        // Percorre todos os chunks existentes
        foreach (var kv in terrainChunks)
        {
            Vector2Int coord = kv.Key;
            Terrain currentTerrain = kv.Value;
            TerrainChunkInfo currentInfo = currentTerrain.GetComponent<TerrainChunkInfo>();

            if (currentInfo == null)
                continue; // Caso não haja a informação do bioma, pula para o próximo

            bool temVizinhoDiferente = false;

            // Define as direções dos vizinhos (norte, leste, sul e oeste)
            Vector2Int[] direcoes = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Norte
                new Vector2Int(1, 0),   // Leste
                new Vector2Int(0, -1),  // Sul
                new Vector2Int(-1, 0)   // Oeste
            };

            foreach (Vector2Int direcao in direcoes)
            {
                Vector2Int neighborCoord = coord + direcao;
                if (terrainChunks.TryGetValue(neighborCoord, out Terrain neighborTerrain))
                {
                    TerrainChunkInfo neighborInfo = neighborTerrain.GetComponent<TerrainChunkInfo>();
                    // Se o vizinho existir e tiver um bioma diferente
                    if (neighborInfo != null && neighborInfo.biome != currentInfo.biome)
                    {
                        neighborInfo.hasNeibhorChunk = true;
                        temVizinhoDiferente = true;
                        break;
                    }
                }
            }

            // Se o chunk atual faz fronteira com um bioma diferente, renomeia o GameObject
            if (temVizinhoDiferente)
            {
                currentTerrain.gameObject.name = "Vizinho_" + coord;
            }
        }
    }

    float CalcularAltura(float worldX, float worldZ, BiomeDefinition biome)
    {
        float y = Mathf.PerlinNoise((worldX + seed) * biome.highFrequencyScale, (worldZ + seed) * biome.highFrequencyScale) * biome.highFrequencyAmplitude;
        y += Mathf.PerlinNoise((worldX + seed) * biome.lowFrequencyScale, (worldZ + seed) * biome.lowFrequencyScale) * biome.lowFrequencyAmplitude;
        return y;
    }

    float CalcularSlope(float[,] alturas, int x, int z)
    {
        int xLeft = Mathf.Max(x - 1, 0);
        int xRight = Mathf.Min(x + 1, alturas.GetLength(1) - 1);
        int zDown = Mathf.Max(z - 1, 0);
        int zUp = Mathf.Min(z + 1, alturas.GetLength(0) - 1);

        float heightL = alturas[z, xLeft] * terrainHeight;
        float heightR = alturas[z, xRight] * terrainHeight;
        float heightD = alturas[zDown, x] * terrainHeight;
        float heightU = alturas[zUp, x] * terrainHeight;

        float cellSize = chunkSize / (float)(terrainResolution - 1);
        float dX = (heightR - heightL) / (2f * cellSize);
        float dZ = (heightU - heightD) / (2f * cellSize);
        float gradient = Mathf.Sqrt(dX * dX + dZ * dZ);
        return Mathf.Atan(gradient) * Mathf.Rad2Deg;
    }
}
