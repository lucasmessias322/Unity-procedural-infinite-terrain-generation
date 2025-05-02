// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using UnityEngine;

// public enum BorderDirection { North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest }

// [RequireComponent(typeof(TerrainObjectSpawner))]
// public partial class InfiniteTerrain : MonoBehaviour
// {
//     public static InfiniteTerrain Instance { get; private set; }

//     [Header("References")]
//     public Transform player;

//     [Header("Terrain Settings")]
//     [Header("Terrain Identification")]
//     public string terrainTag = "Terrain";
//     public string terrainLayerName = "TerrainLayer";

//     [Range(128, 1024)]
//     public int chunkSize = 256;
//     [Range(257, 1025)]
//     public int terrainResolution = 513; // (2^n) + 1
//     public float terrainHeight = 100f;
//     public int seed = 42;

//     [Header("Biome Settings")]
//     [Tooltip("Noise scale for biome determination.")]
//     public float biomeNoiseScale = 0.001f;
//     [Tooltip("Available biome definitions.")]
//     public BiomeDefinition[] biomeDefinitions;

//     [Header("Render Distance (usado apenas no modo Infinito)")]
//     public int renderDistance = 1;

//     [Tooltip("Detail map resolution (higher value means more details).")]
//     public int detailResolution = 256;
//     public int detailResolutionPerPatch = 16;
//     public float wavingGrassStrength = 0.2f;
//     public float wavingGrassAmount = 0.5f;
//     public Color wavingGrassTint;

//     [Header("Alphamap")]
//     public int alphamapResolution = 512;

//     [Header("Chunk Limit (usado apenas no modo Finito)")]
//     public int maxChunkCount = 50;

//     [Header("Terrain object spawner Settings")]
//     public bool SpawnObjects;
//     public TerrainObjectSpawner objectSpawner;

//     // Flag para determinar se o terreno é infinito ou finito
//     [Header("Terrain Mode")]
//     [Tooltip("Se verdadeiro, o terreno é infinito; se falso, gera um número fixo de chunks.")]
//     public bool infiniteTerrain = true;

//     // Private fields for internal control
//     private Dictionary<Vector2Int, Terrain> terrainChunks = new Dictionary<Vector2Int, Terrain>();
//     private Queue<Vector2Int> chunkQueue = new Queue<Vector2Int>();
//     private bool isChunkCoroutineRunning = false;
//     private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

//     [Header("Global Layer Settings")]
//     public GlobalLayerDefinition[] globalLayerDefinitions;
//     [Header("Transition Margin Settings")]
//     public int minBlendMargin = 30;
//     public int maxBlendMargin = 70;
//     public float blendMarginNoiseScale = 0.05f;

//     [Header("Transition Curve Settings")]
//     public float transitionCurveExponent = 2f; // Valores maiores deixam a curva mais acentuada.

//     [Header("Water Settings")]
//     public bool SpawnWaterTile;
//     public GameObject waterTilePrefab;
//     public float WaterTileSize = 26.3f;
//     public float waterHeight = 50.5f;

//     [Header("Mobspawner Settings")]
//     public bool SpawnMobSpawner;
//     public GameObject MobSpawnerPrefab;

//     private void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }
//         Instance = this;

//         player = GameObject.FindGameObjectWithTag("Player").transform;
//         objectSpawner.seed = seed;
//     }

//     private void Start()
//     {
//         if (!infiniteTerrain)
//         {
//             // No modo finito, gera todos os chunks de uma vez (limitados a no máximo maxChunkCount)
//             GenerateAllChunks();
//             // Garante que a fila esteja vazia para não gerar mais nada depois
//             chunkQueue.Clear();
//         }
//     }

//     void Update()
//     {
//         // Se o modo infinito estiver desativado, não atualiza nada
//         if (!infiniteTerrain)
//             return;

//         Vector2Int currentPlayerChunk = GetPlayerChunkCoord();
//         if (currentPlayerChunk != lastPlayerChunkCoord)
//         {
//             UpdateChunks();
//             lastPlayerChunkCoord = currentPlayerChunk;
//         }
//     }

//     Vector2Int GetPlayerChunkCoord()
//     {
//         return new Vector2Int(
//             Mathf.FloorToInt(player.position.x / chunkSize),
//             Mathf.FloorToInt(player.position.z / chunkSize)
//         );
//     }

//     // Método para o modo infinito (mantém o comportamento original)
//     void UpdateChunks()
//     {
//         // Remove chunks nulos
//         List<Vector2Int> keysToClean = new List<Vector2Int>();
//         foreach (var kv in terrainChunks)
//         {
//             if (kv.Value == null)
//                 keysToClean.Add(kv.Key);
//         }
//         foreach (var key in keysToClean)
//             terrainChunks.Remove(key);

//         Vector2Int playerChunkCoord = GetPlayerChunkCoord();

//         // Enfileira chunks dentro da distância de renderização
//         for (int x = -renderDistance; x <= renderDistance; x++)
//         {
//             for (int z = -renderDistance; z <= renderDistance; z++)
//             {
//                 Vector2Int chunkCoord = new Vector2Int(playerChunkCoord.x + x, playerChunkCoord.y + z);
//                 if (!terrainChunks.ContainsKey(chunkCoord) && !chunkQueue.Contains(chunkCoord))
//                     chunkQueue.Enqueue(chunkCoord);
//             }
//         }

//         if (!isChunkCoroutineRunning && chunkQueue.Count > 0)
//         {
//             StartCoroutine(ProcessChunkQueue());
//             isChunkCoroutineRunning = true;
//         }

//         LimitChunks();
//     }

//     // Limita os chunks existentes (modo infinito)
//     void LimitChunks()
//     {
//         if (terrainChunks.Count <= maxChunkCount)
//             return;

//         List<Vector2Int> keys = new List<Vector2Int>(terrainChunks.Keys);
//         keys.Sort((a, b) =>
//         {
//             Vector2 aPos = new Vector2(a.x * chunkSize, a.y * chunkSize);
//             Vector2 bPos = new Vector2(b.x * chunkSize, b.y * chunkSize);
//             float distA = Vector2.Distance(aPos, new Vector2(player.position.x, player.position.z));
//             float distB = Vector2.Distance(bPos, new Vector2(player.position.x, player.position.z));
//             return distB.CompareTo(distA);
//         });

//         while (terrainChunks.Count > maxChunkCount && keys.Count > 0)
//         {
//             Vector2Int keyToRemove = keys[0];
//             keys.RemoveAt(0);
//             if (terrainChunks.TryGetValue(keyToRemove, out Terrain terrain))
//             {
//                 Destroy(terrain.gameObject);
//             }
//             terrainChunks.Remove(keyToRemove);
//         }
//     }

//     // Geração de todos os chunks de uma vez no modo finito
//     void GenerateAllChunks()
//     {
//         Vector2Int centerChunk = GetPlayerChunkCoord();
//         // Calcula uma grid aproximada (d x d) que não ultrapasse maxChunkCount
//         int gridDimension = Mathf.FloorToInt(Mathf.Sqrt(maxChunkCount));
//         int halfGrid = gridDimension / 2;

//         for (int x = -halfGrid; x <= halfGrid; x++)
//         {
//             for (int z = -halfGrid; z <= halfGrid; z++)
//             {
//                 Vector2Int chunkCoord = new Vector2Int(centerChunk.x + x, centerChunk.y + z);
//                 // Geração imediata sem enfileiramento nem delay
//                 CreateChunkAsync(chunkCoord);
//             }
//         }
//     }

//     IEnumerator ProcessChunkQueue()
//     {
//         while (chunkQueue.Count > 0)
//         {
//             Vector2Int coord = chunkQueue.Dequeue();
//             CreateChunkAsync(coord);
//             yield return new WaitForSeconds(0.5f);
//         }
//         isChunkCoroutineRunning = false;
//     }

//     async void CreateChunkAsync(Vector2Int coord)
//     {
//         Vector3 chunkWorldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

//         // Determina o bioma predominante para este chunk (usando a posição central)
//         BiomeDefinition centerBiome = GetBiomeAtPosition(chunkWorldPos);

//         int biomeLayerCount = (centerBiome.terrainLayerDefinitions != null ? centerBiome.terrainLayerDefinitions.Length : 0);
//         int globalLayerCount = (globalLayerDefinitions != null ? globalLayerDefinitions.Length : 0);
//         int totalLayers = biomeLayerCount + globalLayerCount;

//         var result = await Task.Run(() =>
//         {
//             float[,] heights = GenerateHeightMap(chunkWorldPos);
//             float[,,] splatmapData = null;
//             if (totalLayers > 0)
//             {
//                 splatmapData = GenerateSplatmapData(heights, centerBiome, globalLayerDefinitions);
//             }
//             return (heights, splatmapData);
//         });

//         float[,] heightsResult = result.heights;
//         float[,,] splatmapDataResult = result.splatmapData;

//         // Cria o TerrainData e configura suas propriedades através do método auxiliar.
//         TerrainData terrainData = new TerrainData();
//         ConfigureTerrainData(terrainData, heightsResult, splatmapDataResult, centerBiome, globalLayerDefinitions);

//         // Cria o GameObject do terreno e posiciona-o
//         GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
//         terrainObj.tag = terrainTag;
//         terrainObj.layer = LayerMask.NameToLayer(terrainLayerName);
//         terrainObj.transform.SetParent(transform);
//         terrainObj.transform.position = chunkWorldPos;
//         Terrain terrain = terrainObj.GetComponent<Terrain>();

//         // Adiciona o TerrainChunkInfo e define o bioma predominante
//         TerrainChunkInfo chunkInfo = terrainObj.AddComponent<TerrainChunkInfo>();
//         chunkInfo.biome = centerBiome;

//         ApplyGrassDetails(terrainData, splatmapDataResult, centerBiome, chunkInfo);

//         if (waterTilePrefab != null && SpawnWaterTile)
//         {
//             // Posiciona o tile de água no centro do chunk, com Y = waterHeight
//             Vector3 waterPosition = new Vector3(chunkWorldPos.x + chunkSize / 2, waterHeight, chunkWorldPos.z + chunkSize / 2);
//             GameObject waterTile = Instantiate(waterTilePrefab, waterPosition, Quaternion.identity, terrainObj.transform);
//             waterTile.transform.localScale = new Vector3(WaterTileSize, 1, WaterTileSize);
//         }

//         // Adiciona o chunk à lista (garante que ele esteja no dicionário)
//         if (!terrainChunks.ContainsKey(coord))
//             terrainChunks.Add(coord, terrain);

//         // Atualiza os vizinhos (cardinais e diagonais)
//         UpdateNeighborsForChunk(coord, chunkInfo);
//         ApplyBiomeTransitionBlend(ref splatmapDataResult, terrainData, chunkInfo, heightsResult, terrainHeight);

//         if (objectSpawner != null && SpawnObjects)
//         {
//             StartCoroutine(objectSpawner.SpawnObjectsOnChunkCoroutine(terrain, chunkWorldPos, terrainObj, chunkSize));
//         }

//         if (MobSpawnerPrefab != null && SpawnMobSpawner)
//         {
//             var mobSpawner = Instantiate(MobSpawnerPrefab, chunkWorldPos, Quaternion.identity, terrainObj.transform)
//                 .GetComponent<MobSpawner>();
//             mobSpawner.terrain = terrain;
//             mobSpawner.player = player;
//         }
//     }

//     // Método auxiliar para configurar os dados do TerrainData, separando a definição das Layers.
//     private void ConfigureTerrainData(
//         TerrainData terrainData,
//         float[,] heightsResult,
//         float[,,] splatmapDataResult,
//         BiomeDefinition centerBiome,
//         GlobalLayerDefinition[] globalLayers)
//     {
//         int biomeLayerCount = (centerBiome.terrainLayerDefinitions != null ? centerBiome.terrainLayerDefinitions.Length : 0);
//         int globalLayerCount = (globalLayers != null ? globalLayers.Length : 0);
//         int totalLayers = biomeLayerCount + globalLayerCount;

//         terrainData.heightmapResolution = terrainResolution;
//         terrainData.size = new Vector3(chunkSize, terrainHeight, chunkSize);
//         terrainData.alphamapResolution = alphamapResolution;
//         terrainData.wavingGrassStrength = wavingGrassStrength;
//         terrainData.wavingGrassAmount = wavingGrassAmount;
//         terrainData.wavingGrassTint = wavingGrassTint;
//         terrainData.wavingGrassSpeed = 0;

//         terrainData.SetHeights(0, 0, heightsResult);

//         if (totalLayers > 0)
//         {
//             TerrainLayer[] layers = new TerrainLayer[totalLayers];

//             // Layers do bioma central
//             for (int i = 0; i < biomeLayerCount; i++)
//             {
//                 layers[i] = centerBiome.terrainLayerDefinitions[i].terrainLayer;
//             }
//             // Layers globais
//             for (int i = 0; i < globalLayerCount; i++)
//             {
//                 layers[biomeLayerCount + i] = globalLayers[i].terrainLayer;
//             }

//             terrainData.terrainLayers = layers;
//             terrainData.SetAlphamaps(0, 0, splatmapDataResult);
//         }
//     }


//     // Expande o splatmap para incluir um novo canal (layer)
//     float[,,] ExpandSplatmapChannels(float[,,] splatmapData, int newTotalLayers)
//     {
//         int alphaRes = alphamapResolution;
//         float[,,] newSplatmapData = new float[alphaRes, alphaRes, newTotalLayers];
//         int currentTotalLayers = splatmapData.GetLength(2);
//         for (int z = 0; z < alphaRes; z++)
//         {
//             for (int x = 0; x < alphaRes; x++)
//             {
//                 for (int i = 0; i < currentTotalLayers; i++)
//                 {
//                     newSplatmapData[z, x, i] = splatmapData[z, x, i];
//                 }
//                 newSplatmapData[z, x, newTotalLayers - 1] = 0f;
//             }
//         }
//         return newSplatmapData;
//     }

//     // Gera o mapa de alturas
//     private float[,] GenerateHeightMap(Vector3 offset)
//     {
//         float[,] heights = new float[terrainResolution, terrainResolution];
//         for (int x = 0; x < terrainResolution; x++)
//         {
//             for (int z = 0; z < terrainResolution; z++)
//             {
//                 float percentX = (float)x / (terrainResolution - 1);
//                 float percentZ = (float)z / (terrainResolution - 1);
//                 float worldX = offset.x + percentX * chunkSize;
//                 float worldZ = offset.z + percentZ * chunkSize;
//                 float height = ComputeBlendedHeight(worldX, worldZ);
//                 heights[z, x] = height / terrainHeight;
//             }
//         }
//         return heights;
//     }

//     // Gera os dados da splatmap usando os pesos das layers
//     private float[,,] GenerateSplatmapData(float[,] heights, BiomeDefinition biome, GlobalLayerDefinition[] globalLayers)
//     {
//         int biomeLayerCount = (biome.terrainLayerDefinitions != null ? biome.terrainLayerDefinitions.Length : 0);
//         int globalLayerCount = (globalLayers != null ? globalLayers.Length : 0);
//         int totalLayers = biomeLayerCount + globalLayerCount;
//         float[,,] splatmapData = new float[alphamapResolution, alphamapResolution, totalLayers];

//         for (int z = 0; z < alphamapResolution; z++)
//         {
//             for (int x = 0; x < alphamapResolution; x++)
//             {
//                 float heightNormalized = heights[z, x];
//                 float worldHeight = heightNormalized * terrainHeight;
//                 float slope = CalculateSlope(heights, x, z);

//                 float totalWeight = 0f;
//                 float[] weights = new float[totalLayers];

//                 // Pesos para as layers do bioma
//                 for (int i = 0; i < biomeLayerCount; i++)
//                 {
//                     TerrainLayerDefinition def = biome.terrainLayerDefinitions[i];
//                     float weight = 1f;
//                     if (worldHeight < def.minHeight || worldHeight > def.maxHeight)
//                         weight = 0f;
//                     if (slope < def.minSlope || slope > def.maxSlope)
//                         weight = 0f;
//                     weights[i] = weight;
//                     totalWeight += weight;
//                 }

//                 // Pesos para as layers globais
//                 for (int i = 0; i < globalLayerCount; i++)
//                 {
//                     GlobalLayerDefinition globalDef = globalLayers[i];
//                     float weight = 1f;
//                     if (worldHeight < globalDef.minHeight || worldHeight > globalDef.maxHeight)
//                         weight = 0f;
//                     if (slope < globalDef.minSlope || slope > globalDef.maxSlope)
//                         weight = 0f;
//                     weights[biomeLayerCount + i] = weight;
//                     totalWeight += weight;
//                 }

//                 if (totalWeight == 0f)
//                 {
//                     if (biomeLayerCount > 0)
//                     {
//                         weights[0] = 1f;
//                         totalWeight = 1f;
//                     }
//                     else if (globalLayerCount > 0)
//                     {
//                         weights[biomeLayerCount] = 1f;
//                         totalWeight = 1f;
//                     }
//                 }

//                 for (int i = 0; i < totalLayers; i++)
//                 {
//                     splatmapData[z, x, i] = weights[i] / totalWeight;
//                 }
//             }
//         }
//         return splatmapData;
//     }

//     private BiomeDefinition GetBiomeByType(BiomeType type)
//     {
//         foreach (BiomeDefinition biome in biomeDefinitions)
//         {
//             if (biome.biomeType == type)
//                 return biome;
//         }
//         return biomeDefinitions.Length > 0 ? biomeDefinitions[0] : null;
//     }

//     private BiomeDefinition GetBiomeAtPosition(Vector3 pos)
//     {
//         float bn = Mathf.PerlinNoise((pos.x + seed) * biomeNoiseScale, (pos.z + seed) * biomeNoiseScale);
//         if (bn < 0.33f)
//             return GetBiomeByType(BiomeType.Desert);
//         else if (bn < 0.66f)
//             return GetBiomeByType(BiomeType.Forest);
//         else
//             return GetBiomeByType(BiomeType.Tundra);
//     }

//     float CalculateHeight(float worldX, float worldZ, BiomeDefinition biome)
//     {
//         float y = Mathf.PerlinNoise((worldX + seed) * biome.highFrequencyScale, (worldZ + seed) * biome.highFrequencyScale) * biome.highFrequencyAmplitude;
//         y += Mathf.PerlinNoise((worldX + seed) * biome.lowFrequencyScale, (worldZ + seed) * biome.lowFrequencyScale) * biome.lowFrequencyAmplitude;
//         return y;
//     }

//     float CalculateSlope(float[,] heights, int x, int z)
//     {
//         int xLeft = Mathf.Max(x - 1, 0);
//         int xRight = Mathf.Min(x + 1, heights.GetLength(1) - 1);
//         int zDown = Mathf.Max(z - 1, 0);
//         int zUp = Mathf.Min(z + 1, heights.GetLength(0) - 1);

//         float heightL = heights[z, xLeft] * terrainHeight;
//         float heightR = heights[z, xRight] * terrainHeight;
//         float heightD = heights[zDown, x] * terrainHeight;
//         float heightU = heights[zUp, x] * terrainHeight;

//         float cellSize = chunkSize / (float)(terrainResolution - 1);
//         float dX = (heightR - heightL) / (2f * cellSize);
//         float dZ = (heightU - heightD) / (2f * cellSize);
//         float gradient = Mathf.Sqrt(dX * dX + dZ * dZ);
//         return Mathf.Atan(gradient) * Mathf.Rad2Deg;
//     }


//     private float ComputeBlendedHeight(float worldX, float worldZ)
//     {
//         float bn = Mathf.PerlinNoise((worldX + seed) * biomeNoiseScale, (worldZ + seed) * biomeNoiseScale);

//         float wDesert = 1f - Mathf.InverseLerp(0.2f, 0.3f, bn);
//         float wForest = 1f - Mathf.Abs(bn - 0.5f) / 0.2f;
//         float wTundra = Mathf.InverseLerp(0.7f, 0.8f, bn);

//         wDesert = Mathf.Max(0, wDesert);
//         wForest = Mathf.Max(0, wForest);
//         wTundra = Mathf.Max(0, wTundra);

//         float total = wDesert + wForest + wTundra;
//         wDesert /= total;
//         wForest /= total;
//         wTundra /= total;

//         BiomeDefinition desert = GetBiomeByType(BiomeType.Desert);
//         BiomeDefinition forest = GetBiomeByType(BiomeType.Forest);
//         BiomeDefinition tundra = GetBiomeByType(BiomeType.Tundra);

//         float hDesert = CalculateHeight(worldX, worldZ, desert);
//         float hForest = CalculateHeight(worldX, worldZ, forest);
//         float hTundra = CalculateHeight(worldX, worldZ, tundra);

//         return wDesert * hDesert + wForest * hForest + wTundra * hTundra;
//     }

// }

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum BorderDirection { North, East, South, West, NorthEast, SouthEast, SouthWest, NorthWest }

[RequireComponent(typeof(TerrainObjectSpawner))]
public partial class InfiniteTerrain : MonoBehaviour
{
    public static InfiniteTerrain Instance { get; private set; }

    [Header("References")]
    public Transform player;

    [Header("Terrain Settings")]
    [Header("Terrain Identification")]
    public string terrainTag = "Terrain";
    public string terrainLayerName = "TerrainLayer";

    [Range(128, 1024)]
    public int chunkSize = 256;
    [Range(257, 1025)]
    public int terrainResolution = 513; // (2^n) + 1
    public float terrainHeight = 100f;
    public int seed = 42;

    [Header("Biome Settings")]
    [Tooltip("Noise scale for biome determination.")]
    public float biomeNoiseScale = 0.001f;
    [Tooltip("Available biome definitions.")]
    public BiomeDefinition[] biomeDefinitions;

    [Header("Render Distance (usado apenas no modo Infinito)")]
    public int renderDistance = 1;

    [Tooltip("Detail map resolution (higher value means more details).")]
    public int detailResolution = 256;
    public int detailResolutionPerPatch = 16;
    public float wavingGrassStrength = 0.2f;
    public float wavingGrassAmount = 0.5f;
    public Color wavingGrassTint;

    [Header("Alphamap")]
    public int alphamapResolution = 512;

    [Header("Chunk Limit (usado apenas no modo Finito)")]
    public int maxChunkCount = 50;

    [Header("Terrain object spawner Settings")]
    public bool SpawnObjects;
    public TerrainObjectSpawner objectSpawner;

    // Flag para determinar se o terreno é infinito ou finito
    [Header("Terrain Mode")]
    [Tooltip("Se verdadeiro, o terreno é infinito; se falso, gera um número fixo de chunks.")]
    public bool infiniteTerrain = true;

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

    [Header("Water Settings")]
    public bool SpawnWaterTile;
    public GameObject waterTilePrefab;
    public float WaterTileSize = 26.3f;
    public float waterHeight = 50.5f;

    [Header("Mobspawner Settings")]
    public bool SpawnMobSpawner;
    public GameObject MobSpawnerPrefab;

    // Offset gerado a partir da seed para alterar as posições amostradas no Perlin Noise
    private Vector2 noiseOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        player = GameObject.FindGameObjectWithTag("Player").transform;
        objectSpawner.seed = seed;

        // Inicializa a seed e gera um offset aleatório para o ruído
        UnityEngine.Random.InitState(seed);
        float offsetX = UnityEngine.Random.Range(-10000f, 10000f);
        float offsetY = UnityEngine.Random.Range(-10000f, 10000f);
        noiseOffset = new Vector2(offsetX, offsetY);
    }

    private void Start()
    {
        if (!infiniteTerrain)
        {
            // No modo finito, gera todos os chunks de uma vez (limitados a no máximo maxChunkCount)
            GenerateAllChunks();
            // Garante que a fila esteja vazia para não gerar mais nada depois
            chunkQueue.Clear();
        }
    }

    void Update()
    {
        // Se o modo infinito estiver desativado, não atualiza nada
        if (!infiniteTerrain)
            return;

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

    // Método para o modo infinito (mantém o comportamento original)
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

    // Limita os chunks existentes (modo infinito)
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

    // Geração de todos os chunks de uma vez no modo finito
    void GenerateAllChunks()
    {
        Vector2Int centerChunk = GetPlayerChunkCoord();
        // Calcula uma grid aproximada (d x d) que não ultrapasse maxChunkCount
        int gridDimension = Mathf.FloorToInt(Mathf.Sqrt(maxChunkCount));
        int halfGrid = gridDimension / 2;

        for (int x = -halfGrid; x <= halfGrid; x++)
        {
            for (int z = -halfGrid; z <= halfGrid; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(centerChunk.x + x, centerChunk.y + z);
                // Geração imediata sem enfileiramento nem delay
                CreateChunkAsync(chunkCoord);
            }
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

        // Cria o TerrainData e configura suas propriedades através do método auxiliar.
        TerrainData terrainData = new TerrainData();
        ConfigureTerrainData(terrainData, heightsResult, splatmapDataResult, centerBiome, globalLayerDefinitions);

        // Cria o GameObject do terreno e posiciona-o
        GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.tag = terrainTag;
        terrainObj.layer = LayerMask.NameToLayer(terrainLayerName);
        terrainObj.transform.SetParent(transform);
        terrainObj.transform.position = chunkWorldPos;
        Terrain terrain = terrainObj.GetComponent<Terrain>();

        // Adiciona o TerrainChunkInfo e define o bioma predominante
        TerrainChunkInfo chunkInfo = terrainObj.AddComponent<TerrainChunkInfo>();
        chunkInfo.biome = centerBiome;

        ApplyGrassDetails(terrainData, splatmapDataResult, centerBiome, chunkInfo);

        if (waterTilePrefab != null && SpawnWaterTile)
        {
            // Posiciona o tile de água no centro do chunk, com Y = waterHeight
            Vector3 waterPosition = new Vector3(chunkWorldPos.x + chunkSize / 2, waterHeight, chunkWorldPos.z + chunkSize / 2);
            GameObject waterTile = Instantiate(waterTilePrefab, waterPosition, Quaternion.identity, terrainObj.transform);
            waterTile.transform.localScale = new Vector3(WaterTileSize, 1, WaterTileSize);
        }

        // Adiciona o chunk à lista (garante que ele esteja no dicionário)
        if (!terrainChunks.ContainsKey(coord))
            terrainChunks.Add(coord, terrain);

        // Atualiza os vizinhos (cardinais e diagonais)
        UpdateNeighborsForChunk(coord, chunkInfo);
        ApplyBiomeTransitionBlend(ref splatmapDataResult, terrainData, chunkInfo, heightsResult, terrainHeight);

        if (objectSpawner != null && SpawnObjects)
        {
            StartCoroutine(objectSpawner.SpawnObjectsOnChunkCoroutine(terrain, chunkWorldPos, terrainObj, chunkSize));
        }

        if (MobSpawnerPrefab != null && SpawnMobSpawner)
        {
            var mobSpawner = Instantiate(MobSpawnerPrefab, chunkWorldPos, Quaternion.identity, terrainObj.transform)
                .GetComponent<MobSpawner>();
            mobSpawner.terrain = terrain;
            mobSpawner.player = player;
        }
    }

    // Método auxiliar para configurar os dados do TerrainData, separando a definição das Layers.
    private void ConfigureTerrainData(
        TerrainData terrainData,
        float[,] heightsResult,
        float[,,] splatmapDataResult,
        BiomeDefinition centerBiome,
        GlobalLayerDefinition[] globalLayers)
    {
        int biomeLayerCount = (centerBiome.terrainLayerDefinitions != null ? centerBiome.terrainLayerDefinitions.Length : 0);
        int globalLayerCount = (globalLayers != null ? globalLayers.Length : 0);
        int totalLayers = biomeLayerCount + globalLayerCount;

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
                layers[biomeLayerCount + i] = globalLayers[i].terrainLayer;
            }

            terrainData.terrainLayers = layers;
            terrainData.SetAlphamaps(0, 0, splatmapDataResult);
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
        // Utiliza o noiseOffset gerado a partir da seed
        float bn = Mathf.PerlinNoise((pos.x + noiseOffset.x) * biomeNoiseScale, (pos.z + noiseOffset.y) * biomeNoiseScale);
        if (bn < 0.33f)
            return GetBiomeByType(BiomeType.Desert);
        else if (bn < 0.66f)
            return GetBiomeByType(BiomeType.Forest);
        else
            return GetBiomeByType(BiomeType.Tundra);
    }

    float CalculateHeight(float worldX, float worldZ, BiomeDefinition biome)
    {
        // Aplica o offset aqui também
        float y = Mathf.PerlinNoise((worldX + noiseOffset.x) * biome.highFrequencyScale, (worldZ + noiseOffset.y) * biome.highFrequencyScale) * biome.highFrequencyAmplitude;
        y += Mathf.PerlinNoise((worldX + noiseOffset.x) * biome.lowFrequencyScale, (worldZ + noiseOffset.y) * biome.lowFrequencyScale) * biome.lowFrequencyAmplitude;
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

    private float ComputeBlendedHeight(float worldX, float worldZ)
    {
        // Utiliza o offset gerado a partir da seed
        float bn = Mathf.PerlinNoise((worldX + noiseOffset.x) * biomeNoiseScale, (worldZ + noiseOffset.y) * biomeNoiseScale);

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
}
