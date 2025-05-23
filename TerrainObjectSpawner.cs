

// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [System.Serializable]
// public class ObjectSpawnDefinition
// {
//     [Tooltip("Lista de prefabs do objeto a ser instanciado.")]
//     public GameObject[] prefabs;

//     [Tooltip("Quantidade máxima de objetos por chunk.")]
//     public int maxCount = 50;

//     [Tooltip("Valor de threshold do Perlin Noise para spawn.")]
//     public float spawnThreshold = 0.5f;

//     [Tooltip("Escala do Perlin Noise.")]
//     public float noiseScale = 0.1f;

//     [Tooltip("Altura mínima para que o objeto seja instanciado.")]
//     public float minHeight = 20f;

//     [Tooltip("Altura máxima para que o objeto seja instanciado.")]
//     public float maxHeight = 200f;

//     [Header("Pintura do Terreno")]
//     [Tooltip("Índice da terrain layer que será usada para pintar a área do objeto.")]
//     public int paintLayerIndex;

//     [Tooltip("Raio (em unidades) para pintar o terreno ao redor do objeto.")]
//     public float paintRadius = 5f;

//     [Tooltip("Distância mínima entre os objetos instanciados.")]
//     public float minDistance = 1f; // Modifique conforme a necessidade

//     [Header("Remover Grass")]
//     [Tooltip("Se verdadeiro, remove a grass na região onde o objeto é instanciado.")]
//     public bool RemoveGrassOnObjectPossition;
//     [Tooltip("Raio (em unidades) para remover a grass ao redor do objeto. Se <= 0, a remoção não será aplicada.")]
//     public float grassRemovalRadius = 2f;

//     [Header("Raridade do Objeto")]
//     [Tooltip("Nível de raridade do objeto. Valores maiores tornam o objeto mais raro (mínimo 1).")]
//     public int rarityLevel = 1;
// }

// // Estruturas para armazenar modificações do terreno
// public struct GrassModification
// {
//     public Vector3 position;
//     public float exclusionRadius;
// }

// public struct PaintModification
// {
//     public Vector3 position;
//     public int paintLayerIndex;
//     public float paintRadius;
// }

// public class TerrainObjectSpawner : MonoBehaviour
// {
//     // Seed do terreno (pode ser passado como parâmetro também)
//     public int seed = 42;

//     [Tooltip("Definições para spawn de objetos (árvores, rochas, etc.).")]
//     public ObjectSpawnDefinition[] objectSpawnDefinitions;

//     /// <summary>
//     /// Coroutine para distribuir o processamento de spawn de objetos por múltiplos frames.
//     /// O uso de corrotinas (ou Job System, se desejado) evita picos de processamento e quedas na taxa de frames.
//     /// </summary>
//     public IEnumerator SpawnObjectsOnChunkCoroutine(Terrain terrain, Vector3 chunkWorldPos, GameObject chunk, int chunkSize)
//     {
//         if (terrain == null)
//         {
//             Debug.LogError("Terrain is null");
//             yield break;
//         }

//         GameObject objectsParent = new GameObject("SpawnedObjects_" + chunkWorldPos);
//         objectsParent.transform.SetParent(chunk.transform);

//         // Cache de referências para evitar chamadas repetitivas
//         TerrainData terrainData = terrain.terrainData;
//         Vector3 terrainPos = terrain.transform.position;

//         // Listas para acumular as modificações de terreno em batch
//         List<GrassModification> grassModifications = new List<GrassModification>();
//         List<PaintModification> paintModifications = new List<PaintModification>();

//         if (objectSpawnDefinitions != null && objectSpawnDefinitions.Length > 0)
//         {
//             foreach (ObjectSpawnDefinition spawnDef in objectSpawnDefinitions)
//             {
//                 // Pré-cálculo do mapa de ruído para o chunk utilizando o noiseScale desta definição.
//                 int noiseResolution = chunkSize + 1;
//                 float[,] noiseMap = new float[noiseResolution, noiseResolution];
//                 for (int z = 0; z < noiseResolution; z++)
//                 {
//                     for (int x = 0; x < noiseResolution; x++)
//                     {
//                         float worldX = chunkWorldPos.x + x;
//                         float worldZ = chunkWorldPos.z + z;
//                         noiseMap[x, z] = Mathf.PerlinNoise((worldX + seed) * spawnDef.noiseScale, (worldZ + seed) * spawnDef.noiseScale);
//                     }
//                     // Distribui o processamento: espera um frame após processar cada linha
//                     yield return null;
//                 }

//                 // Lista para armazenar as posições dos objetos já spawnados para essa definição
//                 List<Vector3> spawnedPositions = new List<Vector3>();

//                 int spawnedCount = 0;
//                 int attempts = 0;
//                 while (spawnedCount < spawnDef.maxCount && attempts < spawnDef.maxCount * 10)
//                 {


//                     attempts++;
//                     // Gera uma posição aleatória dentro do chunk
//                     float posX = Random.Range(0f, chunkSize);
//                     float posZ = Random.Range(0f, chunkSize);
//                     Vector3 posWorld = new Vector3(chunkWorldPos.x + posX, 0, chunkWorldPos.z + posZ);



//                     // Obtém a altura do terreno na posição
//                     float y = terrain.SampleHeight(posWorld);
//                     posWorld.y = y;

//                     // Verifica se a altura está dentro dos limites definidos
//                     if (y >= spawnDef.minHeight && y <= spawnDef.maxHeight)
//                     {
//                         // Amostra o valor do Perlin Noise a partir do mapa pré-calculado.
//                         int ix = Mathf.Clamp(Mathf.RoundToInt(posX), 0, chunkSize);
//                         int iz = Mathf.Clamp(Mathf.RoundToInt(posZ), 0, chunkSize);
//                         float noiseValue = noiseMap[ix, iz];

//                         // Aplica o threshold e a raridade (chance de 1/rarityLevel)
//                         if (noiseValue > spawnDef.spawnThreshold && Random.Range(0, spawnDef.rarityLevel) == 0)
//                         {
//                             // Verifica se a posição está suficientemente distante dos objetos já spawnados
//                             bool tooClose = false;
//                             foreach (Vector3 pos in spawnedPositions)
//                             {
//                                 if (Vector3.Distance(pos, posWorld) < spawnDef.minDistance)
//                                 {
//                                     tooClose = true;
//                                     break;
//                                 }
//                             }
//                             if (tooClose)
//                                 continue;

//                             if (spawnDef.prefabs != null && spawnDef.prefabs.Length > 0)
//                             {
//                                 // Seleciona aleatoriamente um prefab da lista
//                                 GameObject selectedPrefab = spawnDef.prefabs[Random.Range(0, spawnDef.prefabs.Length)];
//                                 GameObject spawnedObject = Instantiate(selectedPrefab, posWorld, Quaternion.identity, objectsParent.transform);
//                                 spawnedObject.transform.localScale = selectedPrefab.transform.localScale;

//                                 spawnedPositions.Add(posWorld);



//                                 // Remove a grass somente se estiver habilitado e se o raio definido for maior que zero
//                                 if (spawnDef.RemoveGrassOnObjectPossition && spawnDef.grassRemovalRadius > 0)
//                                 {
//                                     grassModifications.Add(new GrassModification { position = posWorld, exclusionRadius = spawnDef.grassRemovalRadius });
//                                 }

//                                 // Adiciona a modificação de pintura apenas se paintRadius for maior que 0
//                                 if (spawnDef.paintRadius > 0)
//                                 {
//                                     paintModifications.Add(new PaintModification
//                                     {
//                                         position = posWorld,
//                                         paintLayerIndex = spawnDef.paintLayerIndex,
//                                         paintRadius = spawnDef.paintRadius
//                                     });
//                                 }

//                                 spawnedCount++;
//                             }
//                         }
//                     }

//                     // Distribui o processamento a cada 100 iterações para evitar travamentos
//                     if (attempts % 10 == 0)
//                         yield return null;
//                 }

//                 // Aguarda um frame entre processamentos de diferentes definições
//                 yield return null;
//             }
//         }

//         // Após spawnar todos os objetos do chunk, aplica as modificações no terreno em batch
//         BatchRemoveGrass(terrain, grassModifications);
//         BatchPaintTerrain(terrain, paintModifications);
//     }

//     /// <summary>
//     /// Aplica em batch as remoções de grama acumuladas.
//     /// </summary>
//     public void BatchRemoveGrass(Terrain terrain, List<GrassModification> mods)
//     {
//         TerrainData terrainData = terrain.terrainData;
//         int detailResolution = terrainData.detailResolution;
//         int[,] detailLayer = terrainData.GetDetailLayer(0, 0, detailResolution, detailResolution, 0);
//         Vector3 terrainPos = terrain.transform.position;

//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * detailResolution);
//             int centerZ = Mathf.RoundToInt(normZ * detailResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.exclusionRadius / terrainData.size.x) * detailResolution);

//             for (int z = centerZ - radiusInCells; z <= centerZ + radiusInCells; z++)
//             {
//                 for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
//                 {
//                     if (x >= 0 && x < detailResolution && z >= 0 && z < detailResolution)
//                     {
//                         int dx = x - centerX;
//                         int dz = z - centerZ;
//                         if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                         {
//                             detailLayer[z, x] = 0;
//                         }
//                     }
//                 }
//             }
//         }
//         terrainData.SetDetailLayer(0, 0, 0, detailLayer);
//     }

//     /// <summary>
//     /// Aplica em batch as pinturas de terreno acumuladas.
//     /// Calcula a área afetada por todas as modificações e atualiza os alphamaps de uma só vez.
//     /// </summary>
//     public void BatchPaintTerrain(Terrain terrain, List<PaintModification> mods)
//     {
//         if (mods.Count == 0)
//             return;

//         TerrainData terrainData = terrain.terrainData;
//         int alphamapResolution = terrainData.alphamapResolution;
//         Vector3 terrainPos = terrain.transform.position;

//         // Calcula os limites (em coordenadas de alphamap) de todas as modificações
//         int minX = alphamapResolution;
//         int minZ = alphamapResolution;
//         int maxX = 0;
//         int maxZ = 0;

//         // Armazena dados auxiliares para cada modificação
//         var modData = new List<(PaintModification mod, int centerX, int centerZ, int radiusInCells)>();
//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * alphamapResolution);
//             int centerZ = Mathf.RoundToInt(normZ * alphamapResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.paintRadius / terrainData.size.x) * alphamapResolution);
//             modData.Add((mod, centerX, centerZ, radiusInCells));

//             minX = Mathf.Min(minX, centerX - radiusInCells);
//             minZ = Mathf.Min(minZ, centerZ - radiusInCells);
//             maxX = Mathf.Max(maxX, centerX + radiusInCells);
//             maxZ = Mathf.Max(maxZ, centerZ + radiusInCells);
//         }

//         // Limita a área dentro dos bounds válidos do alphamap
//         minX = Mathf.Max(0, minX);
//         minZ = Mathf.Max(0, minZ);
//         maxX = Mathf.Min(alphamapResolution, maxX);
//         maxZ = Mathf.Min(alphamapResolution, maxZ);
//         int width = maxX - minX;
//         int height = maxZ - minZ;

//         // Obtém os alphamaps da área afetada
//         float[,,] alphamaps = terrainData.GetAlphamaps(minX, minZ, width, height);
//         int numLayers = terrainData.terrainLayers.Length;

//         // Para cada modificação, atualiza os alphamaps na região correspondente
//         foreach (var (mod, centerX, centerZ, radiusInCells) in modData)
//         {
//             for (int z = 0; z < height; z++)
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     int globalX = x + minX;
//                     int globalZ = z + minZ;
//                     int dx = globalX - centerX;
//                     int dz = globalZ - centerZ;
//                     if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                     {
//                         for (int i = 0; i < numLayers; i++)
//                         {
//                             alphamaps[z, x, i] = (i == mod.paintLayerIndex) ? 1f : 0f;
//                         }
//                     }
//                 }
//             }
//         }
//         terrainData.SetAlphamaps(minX, minZ, alphamaps);
//     }
// }

// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Threading;
// using System.Threading.Tasks;

// [System.Serializable]
// public class ObjectSpawnDefinition
// {
//     [Tooltip("Lista de prefabs do objeto a ser instanciado.")]
//     public GameObject[] prefabs;

//     [Tooltip("Quantidade máxima de objetos por chunk.")]
//     public int maxCount = 50;

//     [Tooltip("Valor de threshold do Perlin Noise para spawn.")]
//     public float spawnThreshold = 0.5f;

//     [Tooltip("Escala do Perlin Noise.")]
//     public float noiseScale = 0.1f;

//     [Tooltip("Altura mínima para que o objeto seja instanciado.")]
//     public float minHeight = 20f;

//     [Tooltip("Altura máxima para que o objeto seja instanciado.")]
//     public float maxHeight = 200f;

//     [Header("Pintura do Terreno")]
//     [Tooltip("Índice da terrain layer que será usada para pintar a área do objeto.")]
//     public int paintLayerIndex;

//     [Tooltip("Raio (em unidades) para pintar o terreno ao redor do objeto.")]
//     public float paintRadius = 5f;

//     [Tooltip("Distância mínima entre os objetos instanciados.")]
//     public float minDistance = 1f; // Modifique conforme a necessidade

//     [Header("Remover Grass")]
//     [Tooltip("Se verdadeiro, remove a grass na região onde o objeto é instanciado.")]
//     public bool RemoveGrassOnObjectPossition;
//     [Tooltip("Raio (em unidades) para remover a grass ao redor do objeto. Se <= 0, a remoção não será aplicada.")]
//     public float grassRemovalRadius = 2f;

//     [Header("Raridade do Objeto")]
//     [Tooltip("Nível de raridade do objeto. Valores maiores tornam o objeto mais raro (mínimo 1).")]
//     public int rarityLevel = 1;

//     [Tooltip("Valor que define quantas unidades o objeto deve entrar no terreno (afundar).")]
//     public float verticalSink = 0.2f;

// }

// // Estruturas para armazenar modificações do terreno
// public struct GrassModification
// {
//     public Vector3 position;
//     public float exclusionRadius;
// }

// public struct PaintModification
// {
//     public Vector3 position;
//     public int paintLayerIndex;
//     public float paintRadius;
// }

// public class TerrainObjectSpawner : MonoBehaviour
// {
//     // Seed do terreno (pode ser passado como parâmetro também)
//     [HideInInspector] public int seed = 42;

//     [Tooltip("Definições para spawn de objetos (árvores, rochas, etc.).")]
//     public ObjectSpawnDefinition[] objectSpawnDefinitions;



//     public IEnumerator SpawnObjectsOnChunkCoroutine(Terrain terrain, Vector3 chunkWorldPos, GameObject chunk, int chunkSize)
//     {
//         if (terrain == null)
//         {
//             Debug.LogError("Terrain is null");
//             yield break;
//         }

//         GameObject objectsParent = new GameObject("SpawnedObjects_" + chunkWorldPos);
//         objectsParent.transform.SetParent(chunk.transform);

//         // Cache de referências para evitar chamadas repetitivas
//         TerrainData terrainData = terrain.terrainData;
//         Vector3 terrainPos = terrain.transform.position;

//         // Listas para acumular as modificações de terreno em batch
//         List<GrassModification> grassModifications = new List<GrassModification>();
//         List<PaintModification> paintModifications = new List<PaintModification>();

//         // Cria uma instância local do System.Random com seed derivada (por exemplo, combinando a seed geral com a posição do chunk)
//         int chunkSeed = seed + (int)(chunkWorldPos.x * 73856093) + (int)(chunkWorldPos.z * 19349663);
//         System.Random prng = new System.Random(chunkSeed);

//         if (objectSpawnDefinitions != null && objectSpawnDefinitions.Length > 0)
//         {
//             foreach (ObjectSpawnDefinition spawnDef in objectSpawnDefinitions)
//             {
//                 // Pré-cálculo do mapa de ruído para o chunk utilizando multithreading.
//                 int noiseResolution = chunkSize + 1;
//                 float[,] noiseMap = new float[noiseResolution, noiseResolution];
//                 bool noiseDone = false;
//                 Thread noiseThread = new Thread(() =>
//                 {
//                     for (int z = 0; z < noiseResolution; z++)
//                     {
//                         for (int x = 0; x < noiseResolution; x++)
//                         {
//                             float worldX = chunkWorldPos.x + x;
//                             float worldZ = chunkWorldPos.z + z;
//                             noiseMap[x, z] = Mathf.PerlinNoise((worldX + seed) * spawnDef.noiseScale, (worldZ + seed) * spawnDef.noiseScale);
//                         }
//                     }
//                     noiseDone = true;
//                 });
//                 noiseThread.Start();
//                 // Espera até que o cálculo do noiseMap seja concluído
//                 yield return new WaitUntil(() => noiseDone);

//                 // Lista para armazenar as posições dos objetos já spawnados para essa definição
//                 List<Vector3> spawnedPositions = new List<Vector3>();

//                 int spawnedCount = 0;
//                 int attempts = 0;
//                 while (spawnedCount < spawnDef.maxCount && attempts < spawnDef.maxCount * 10)
//                 {
//                     attempts++;

//                     // Gera uma posição aleatória dentro do chunk utilizando o prng local
//                     float posX = (float)prng.NextDouble() * chunkSize;
//                     float posZ = (float)prng.NextDouble() * chunkSize;
//                     Vector3 posWorld = new Vector3(chunkWorldPos.x + posX, 0, chunkWorldPos.z + posZ);

//                     // Obtém a altura do terreno na posição (chamada na thread principal)
//                     float y = terrain.SampleHeight(posWorld);
//                     posWorld.y = y;

//                     // Verifica se a altura está dentro dos limites definidos
//                     if (y >= spawnDef.minHeight && y <= spawnDef.maxHeight)
//                     {
//                         // Amostra o valor do Perlin Noise a partir do mapa pré-calculado.
//                         int ix = Mathf.Clamp(Mathf.RoundToInt(posX), 0, chunkSize);
//                         int iz = Mathf.Clamp(Mathf.RoundToInt(posZ), 0, chunkSize);
//                         float noiseValue = noiseMap[ix, iz];

//                         // Aplica o threshold e a raridade (chance de 1/rarityLevel) utilizando o prng local
//                         if (noiseValue > spawnDef.spawnThreshold && prng.Next(0, spawnDef.rarityLevel) == 0)
//                         {
//                             // Verifica se a posição está suficientemente distante dos objetos já spawnados
//                             bool tooClose = false;
//                             foreach (Vector3 pos in spawnedPositions)
//                             {
//                                 if (Vector3.Distance(pos, posWorld) < spawnDef.minDistance)
//                                 {
//                                     tooClose = true;
//                                     break;
//                                 }
//                             }
//                             if (tooClose)
//                                 continue;

//                             if (spawnDef.prefabs != null && spawnDef.prefabs.Length > 0)
//                             {

//                                 GameObject selectedPrefab = spawnDef.prefabs[prng.Next(0, spawnDef.prefabs.Length)];
//                                 GameObject spawnedObject = Instantiate(selectedPrefab, posWorld, Quaternion.identity, objectsParent.transform);
//                                 spawnedObject.transform.localScale = selectedPrefab.transform.localScale;
//                                 spawnedPositions.Add(posWorld);

//                                 // Aguarda um frame para garantir que os colliders já foram calculados
//                                 yield return null;

//                                 // Obtém todos os colliders (inclusive de filhos) que não sejam triggers
//                                 Collider[] colliders = spawnedObject.GetComponentsInChildren<Collider>();
//                                 if (colliders != null && colliders.Length > 0)
//                                 {
//                                     float minY = float.MaxValue;
//                                     foreach (Collider col in colliders)
//                                     {
//                                         if (!col.isTrigger)
//                                         {
//                                             // Usa os bounds em world space para determinar o ponto mais baixo
//                                             minY = Mathf.Min(minY, col.bounds.min.y);
//                                         }
//                                     }
//                                     if (minY != float.MaxValue)
//                                     {
//                                         // Calcula o offset entre o pivô do objeto e o ponto mais baixo dos colliders
//                                         float offset = spawnedObject.transform.position.y - minY;
//                                         // Reposiciona o objeto para que o ponto mais baixo fique na altura do terreno,
//                                         // subtraindo o valor de verticalSink para afundar o objeto conforme definido.
//                                         spawnedObject.transform.position = new Vector3(
//                                             spawnedObject.transform.position.x,
//                                             posWorld.y + offset - spawnDef.verticalSink,
//                                             spawnedObject.transform.position.z);
//                                     }
//                                 }



//                                 // Remove a grass somente se estiver habilitado e se o raio definido for maior que zero
//                                 if (spawnDef.RemoveGrassOnObjectPossition && spawnDef.grassRemovalRadius > 0)
//                                 {
//                                     grassModifications.Add(new GrassModification { position = posWorld, exclusionRadius = spawnDef.grassRemovalRadius });
//                                 }

//                                 // Adiciona a modificação de pintura apenas se paintRadius for maior que 0
//                                 if (spawnDef.paintRadius > 0)
//                                 {
//                                     paintModifications.Add(new PaintModification
//                                     {
//                                         position = posWorld,
//                                         paintLayerIndex = spawnDef.paintLayerIndex,
//                                         paintRadius = spawnDef.paintRadius
//                                     });
//                                 }

//                                 spawnedCount++;
//                             }
//                         }
//                     }

//                     // Distribui o processamento a cada 10 iterações para evitar travamentos
//                     if (attempts % 10 == 0)
//                         yield return null;
//                 }

//                 // Aguarda um frame entre processamentos de diferentes definições
//                 yield return null;
//             }
//         }

//         // Após spawnar todos os objetos do chunk, aplica as modificações no terreno em batch
//         BatchRemoveGrass(terrain, grassModifications);
//         BatchPaintTerrain(terrain, paintModifications);
//     }

//     /// <summary>
//     /// Aplica em batch as remoções de grama acumuladas, usando Parallel.For para distribuir o processamento.
//     /// </summary>
//     public void BatchRemoveGrass(Terrain terrain, List<GrassModification> mods)
//     {
//         TerrainData terrainData = terrain.terrainData;
//         int detailResolution = terrainData.detailResolution;
//         int[,] detailLayer = terrainData.GetDetailLayer(0, 0, detailResolution, detailResolution, 0);
//         Vector3 terrainPos = terrain.transform.position;

//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * detailResolution);
//             int centerZ = Mathf.RoundToInt(normZ * detailResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.exclusionRadius / terrainData.size.x) * detailResolution);

//             Parallel.For(centerZ - radiusInCells, centerZ + radiusInCells + 1, z =>
//             {
//                 for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
//                 {
//                     if (x >= 0 && x < detailResolution && z >= 0 && z < detailResolution)
//                     {
//                         int dx = x - centerX;
//                         int dz = z - centerZ;
//                         if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                         {
//                             detailLayer[z, x] = 0;
//                         }
//                     }
//                 }
//             });
//         }
//         terrainData.SetDetailLayer(0, 0, 0, detailLayer);
//     }

//     /// <summary>
//     /// Aplica em batch as pinturas de terreno acumuladas, utilizando Parallel.For para acelerar o processamento.
//     /// </summary>
//     public void BatchPaintTerrain(Terrain terrain, List<PaintModification> mods)
//     {
//         if (mods.Count == 0)
//             return;

//         TerrainData terrainData = terrain.terrainData;
//         int alphamapResolution = terrainData.alphamapResolution;
//         Vector3 terrainPos = terrain.transform.position;

//         // Calcula os limites (em coordenadas de alphamap) de todas as modificações
//         int minX = alphamapResolution;
//         int minZ = alphamapResolution;
//         int maxX = 0;
//         int maxZ = 0;

//         // Armazena dados auxiliares para cada modificação
//         var modData = new List<(PaintModification mod, int centerX, int centerZ, int radiusInCells)>();
//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * alphamapResolution);
//             int centerZ = Mathf.RoundToInt(normZ * alphamapResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.paintRadius / terrainData.size.x) * alphamapResolution);
//             modData.Add((mod, centerX, centerZ, radiusInCells));

//             minX = Mathf.Min(minX, centerX - radiusInCells);
//             minZ = Mathf.Min(minZ, centerZ - radiusInCells);
//             maxX = Mathf.Max(maxX, centerX + radiusInCells);
//             maxZ = Mathf.Max(maxZ, centerZ + radiusInCells);
//         }

//         // Limita a área dentro dos bounds válidos do alphamap
//         minX = Mathf.Max(0, minX);
//         minZ = Mathf.Max(0, minZ);
//         maxX = Mathf.Min(alphamapResolution, maxX);
//         maxZ = Mathf.Min(alphamapResolution, maxZ);
//         int width = maxX - minX;
//         int height = maxZ - minZ;

//         // Obtém os alphamaps da área afetada
//         float[,,] alphamaps = terrainData.GetAlphamaps(minX, minZ, width, height);
//         int numLayers = terrainData.terrainLayers.Length;

//         foreach (var (mod, centerX, centerZ, radiusInCells) in modData)
//         {
//             Parallel.For(0, height, z =>
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     int globalX = x + minX;
//                     int globalZ = z + minZ;
//                     int dx = globalX - centerX;
//                     int dz = globalZ - centerZ;
//                     if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                     {
//                         for (int i = 0; i < numLayers; i++)
//                         {
//                             alphamaps[z, x, i] = (i == mod.paintLayerIndex) ? 1f : 0f;
//                         }
//                     }
//                 }
//             });
//         }
//         terrainData.SetAlphamaps(minX, minZ, alphamaps);
//     }
// }


// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Threading;
// using System.Threading.Tasks;

// [System.Serializable]
// public class ObjectSpawnDefinition
// {
//     [Tooltip("Lista de prefabs do objeto a ser instanciado.")]
//     public GameObject[] prefabs;

//     [Tooltip("Quantidade máxima de objetos por chunk.")]
//     public int maxCount = 50;

//     [Tooltip("Valor de threshold do Perlin Noise para spawn.")]
//     public float spawnThreshold = 0.5f;

//     [Tooltip("Escala do Perlin Noise.")]
//     public float noiseScale = 0.1f;

//     [Tooltip("Altura mínima para que o objeto seja instanciado.")]
//     public float minHeight = 20f;

//     [Tooltip("Altura máxima para que o objeto seja instanciado.")]
//     public float maxHeight = 200f;

//     [Header("Pintura do Terreno")]
//     [Tooltip("Índice da terrain layer que será usada para pintar a área do objeto.")]
//     public int paintLayerIndex;

//     [Tooltip("Raio (em unidades) para pintar o terreno ao redor do objeto.")]
//     public float paintRadius = 5f;

//     [Tooltip("Distância mínima entre os objetos instanciados.")]
//     public float minDistance = 1f; // Modifique conforme a necessidade

//     [Header("Remover Grass")]
//     [Tooltip("Se verdadeiro, remove a grass na região onde o objeto é instanciado.")]
//     public bool RemoveGrassOnObjectPossition;
//     [Tooltip("Raio (em unidades) para remover a grass ao redor do objeto. Se <= 0, a remoção não será aplicada.")]
//     public float grassRemovalRadius = 2f;

//     [Header("Raridade do Objeto")]
//     [Tooltip("Nível de raridade do objeto. Valores maiores tornam o objeto mais raro (mínimo 1).")]
//     public int rarityLevel = 1;

//     [Tooltip("Valor que define quantas unidades o objeto deve entrar no terreno (afundar).")]
//     public float verticalSink = 0.2f;

//     [Header("Bioma")]
//     [Tooltip("Bioma permitido para spawnar este objeto.")]
//     public BiomeDefinition allowedBiome;
// }

// // Estruturas para armazenar modificações do terreno
// public struct GrassModification
// {
//     public Vector3 position;
//     public float exclusionRadius;
// }

// public struct PaintModification
// {
//     public Vector3 position;
//     public int paintLayerIndex;
//     public float paintRadius;
// }

// public class TerrainObjectSpawner : MonoBehaviour
// {
//     // Seed do terreno (pode ser passado como parâmetro também)
//     [HideInInspector] public int seed = 42;

//     [Tooltip("Definições para spawn de objetos (árvores, rochas, etc.).")]
//     public ObjectSpawnDefinition[] objectSpawnDefinitions;

//     public IEnumerator SpawnObjectsOnChunkCoroutine(Terrain terrain, Vector3 chunkWorldPos, GameObject chunk, int chunkSize)
//     {
//         if (terrain == null)
//         {
//             Debug.LogError("Terrain is null");
//             yield break;
//         }

//         // Obtém o componente TerrainChunkInfo para verificar o bioma do chunk
//         TerrainChunkInfo chunkInfo = chunk.GetComponent<TerrainChunkInfo>();
//         if(chunkInfo == null)
//         {
//             Debug.LogError("TerrainChunkInfo não encontrado no chunk");
//             yield break;
//         }

//         GameObject objectsParent = new GameObject("SpawnedObjects_" + chunkWorldPos);
//         objectsParent.transform.SetParent(chunk.transform);

//         // Cache de referências para evitar chamadas repetitivas
//         TerrainData terrainData = terrain.terrainData;
//         Vector3 terrainPos = terrain.transform.position;

//         // Listas para acumular as modificações de terreno em batch
//         List<GrassModification> grassModifications = new List<GrassModification>();
//         List<PaintModification> paintModifications = new List<PaintModification>();

//         // Cria uma instância local do System.Random com seed derivada (por exemplo, combinando a seed geral com a posição do chunk)
//         int chunkSeed = seed + (int)(chunkWorldPos.x * 73856093) + (int)(chunkWorldPos.z * 19349663);
//         System.Random prng = new System.Random(chunkSeed);

//         if (objectSpawnDefinitions != null && objectSpawnDefinitions.Length > 0)
//         {
//             foreach (ObjectSpawnDefinition spawnDef in objectSpawnDefinitions)
//             {
//                 // Verifica se a definição é compatível com o bioma do chunk
//                 if (spawnDef.allowedBiome != null && spawnDef.allowedBiome != chunkInfo.biome)
//                     continue; // Pula se o bioma não for compatível

//                 // Ajusta o threshold se houver vizinhos diferentes (área de transição)
//                 float effectiveSpawnThreshold = spawnDef.spawnThreshold;
//                 if (chunkInfo.hasDifferentNeighbor)
//                     effectiveSpawnThreshold += 0.1f; // ajuste conforme desejado

//                 // Pré-cálculo do mapa de ruído para o chunk utilizando multithreading.
//                 int noiseResolution = chunkSize + 1;
//                 float[,] noiseMap = new float[noiseResolution, noiseResolution];
//                 bool noiseDone = false;
//                 Thread noiseThread = new Thread(() =>
//                 {
//                     for (int z = 0; z < noiseResolution; z++)
//                     {
//                         for (int x = 0; x < noiseResolution; x++)
//                         {
//                             float worldX = chunkWorldPos.x + x;
//                             float worldZ = chunkWorldPos.z + z;
//                             noiseMap[x, z] = Mathf.PerlinNoise((worldX + seed) * spawnDef.noiseScale, (worldZ + seed) * spawnDef.noiseScale);
//                         }
//                     }
//                     noiseDone = true;
//                 });
//                 noiseThread.Start();
//                 // Espera até que o cálculo do noiseMap seja concluído
//                 yield return new WaitUntil(() => noiseDone);

//                 // Lista para armazenar as posições dos objetos já spawnados para essa definição
//                 List<Vector3> spawnedPositions = new List<Vector3>();

//                 int spawnedCount = 0;
//                 int attempts = 0;
//                 while (spawnedCount < spawnDef.maxCount && attempts < spawnDef.maxCount * 10)
//                 {
//                     attempts++;

//                     // Gera uma posição aleatória dentro do chunk utilizando o prng local
//                     float posX = (float)prng.NextDouble() * chunkSize;
//                     float posZ = (float)prng.NextDouble() * chunkSize;
//                     Vector3 posWorld = new Vector3(chunkWorldPos.x + posX, 0, chunkWorldPos.z + posZ);

//                     // Obtém a altura do terreno na posição (chamada na thread principal)
//                     float y = terrain.SampleHeight(posWorld);
//                     posWorld.y = y;

//                     // Verifica se a altura está dentro dos limites definidos
//                     if (y >= spawnDef.minHeight && y <= spawnDef.maxHeight)
//                     {
//                         // Amostra o valor do Perlin Noise a partir do mapa pré-calculado.
//                         int ix = Mathf.Clamp(Mathf.RoundToInt(posX), 0, chunkSize);
//                         int iz = Mathf.Clamp(Mathf.RoundToInt(posZ), 0, chunkSize);
//                         float noiseValue = noiseMap[ix, iz];

//                         // Aplica o threshold e a raridade (chance de 1/rarityLevel) utilizando o prng local
//                         if (noiseValue > effectiveSpawnThreshold && prng.Next(0, spawnDef.rarityLevel) == 0)
//                         {
//                             // Verifica se a posição está suficientemente distante dos objetos já spawnados
//                             bool tooClose = false;
//                             foreach (Vector3 pos in spawnedPositions)
//                             {
//                                 if (Vector3.Distance(pos, posWorld) < spawnDef.minDistance)
//                                 {
//                                     tooClose = true;
//                                     break;
//                                 }
//                             }
//                             if (tooClose)
//                                 continue;

//                             if (spawnDef.prefabs != null && spawnDef.prefabs.Length > 0)
//                             {
//                                 GameObject selectedPrefab = spawnDef.prefabs[prng.Next(0, spawnDef.prefabs.Length)];
//                                 GameObject spawnedObject = Instantiate(selectedPrefab, posWorld, Quaternion.identity, objectsParent.transform);
//                                 spawnedObject.transform.localScale = selectedPrefab.transform.localScale;
//                                 spawnedPositions.Add(posWorld);

//                                 // Aguarda um frame para garantir que os colliders já foram calculados
//                                 yield return null;

//                                 // Obtém todos os colliders (inclusive de filhos) que não sejam triggers
//                                 Collider[] colliders = spawnedObject.GetComponentsInChildren<Collider>();
//                                 if (colliders != null && colliders.Length > 0)
//                                 {
//                                     float minY = float.MaxValue;
//                                     foreach (Collider col in colliders)
//                                     {
//                                         if (!col.isTrigger)
//                                         {
//                                             // Usa os bounds em world space para determinar o ponto mais baixo
//                                             minY = Mathf.Min(minY, col.bounds.min.y);
//                                         }
//                                     }
//                                     if (minY != float.MaxValue)
//                                     {
//                                         // Calcula o offset entre o pivô do objeto e o ponto mais baixo dos colliders
//                                         float offset = spawnedObject.transform.position.y - minY;
//                                         // Reposiciona o objeto para que o ponto mais baixo fique na altura do terreno,
//                                         // subtraindo o valor de verticalSink para afundar o objeto conforme definido.
//                                         spawnedObject.transform.position = new Vector3(
//                                             spawnedObject.transform.position.x,
//                                             posWorld.y + offset - spawnDef.verticalSink,
//                                             spawnedObject.transform.position.z);
//                                     }
//                                 }

//                                 // Remove a grass somente se estiver habilitado e se o raio definido for maior que zero
//                                 if (spawnDef.RemoveGrassOnObjectPossition && spawnDef.grassRemovalRadius > 0)
//                                 {
//                                     grassModifications.Add(new GrassModification { position = posWorld, exclusionRadius = spawnDef.grassRemovalRadius });
//                                 }

//                                 // Adiciona a modificação de pintura apenas se paintRadius for maior que 0
//                                 if (spawnDef.paintRadius > 0)
//                                 {
//                                     paintModifications.Add(new PaintModification
//                                     {
//                                         position = posWorld,
//                                         paintLayerIndex = spawnDef.paintLayerIndex,
//                                         paintRadius = spawnDef.paintRadius
//                                     });
//                                 }

//                                 spawnedCount++;
//                             }
//                         }
//                     }

//                     // Distribui o processamento a cada 10 iterações para evitar travamentos
//                     if (attempts % 10 == 0)
//                         yield return null;
//                 }

//                 // Aguarda um frame entre processamentos de diferentes definições
//                 yield return null;
//             }
//         }

//         // Após spawnar todos os objetos do chunk, aplica as modificações no terreno em batch
//         BatchRemoveGrass(terrain, grassModifications);
//         BatchPaintTerrain(terrain, paintModifications);
//     }

//     /// <summary>
//     /// Aplica em batch as remoções de grama acumuladas, usando Parallel.For para distribuir o processamento.
//     /// </summary>
//     public void BatchRemoveGrass(Terrain terrain, List<GrassModification> mods)
//     {
//         TerrainData terrainData = terrain.terrainData;
//         int detailResolution = terrainData.detailResolution;
//         int[,] detailLayer = terrainData.GetDetailLayer(0, 0, detailResolution, detailResolution, 0);
//         Vector3 terrainPos = terrain.transform.position;

//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * detailResolution);
//             int centerZ = Mathf.RoundToInt(normZ * detailResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.exclusionRadius / terrainData.size.x) * detailResolution);

//             Parallel.For(centerZ - radiusInCells, centerZ + radiusInCells + 1, z =>
//             {
//                 for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
//                 {
//                     if (x >= 0 && x < detailResolution && z >= 0 && z < detailResolution)
//                     {
//                         int dx = x - centerX;
//                         int dz = z - centerZ;
//                         if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                         {
//                             detailLayer[z, x] = 0;
//                         }
//                     }
//                 }
//             });
//         }
//         terrainData.SetDetailLayer(0, 0, 0, detailLayer);
//     }

//     /// <summary>
//     /// Aplica em batch as pinturas de terreno acumuladas, utilizando Parallel.For para acelerar o processamento.
//     /// </summary>
//     public void BatchPaintTerrain(Terrain terrain, List<PaintModification> mods)
//     {
//         if (mods.Count == 0)
//             return;

//         TerrainData terrainData = terrain.terrainData;
//         int alphamapResolution = terrainData.alphamapResolution;
//         Vector3 terrainPos = terrain.transform.position;

//         // Calcula os limites (em coordenadas de alphamap) de todas as modificações
//         int minX = alphamapResolution;
//         int minZ = alphamapResolution;
//         int maxX = 0;
//         int maxZ = 0;

//         // Armazena dados auxiliares para cada modificação
//         var modData = new List<(PaintModification mod, int centerX, int centerZ, int radiusInCells)>();
//         foreach (var mod in mods)
//         {
//             float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
//             float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
//             int centerX = Mathf.RoundToInt(normX * alphamapResolution);
//             int centerZ = Mathf.RoundToInt(normZ * alphamapResolution);
//             int radiusInCells = Mathf.RoundToInt((mod.paintRadius / terrainData.size.x) * alphamapResolution);
//             modData.Add((mod, centerX, centerZ, radiusInCells));

//             minX = Mathf.Min(minX, centerX - radiusInCells);
//             minZ = Mathf.Min(minZ, centerZ - radiusInCells);
//             maxX = Mathf.Max(maxX, centerX + radiusInCells);
//             maxZ = Mathf.Max(maxZ, centerZ + radiusInCells);
//         }

//         // Limita a área dentro dos bounds válidos do alphamap
//         minX = Mathf.Max(0, minX);
//         minZ = Mathf.Max(0, minZ);
//         maxX = Mathf.Min(alphamapResolution, maxX);
//         maxZ = Mathf.Min(alphamapResolution, maxZ);
//         int width = maxX - minX;
//         int height = maxZ - minZ;

//         // Obtém os alphamaps da área afetada
//         float[,,] alphamaps = terrainData.GetAlphamaps(minX, minZ, width, height);
//         int numLayers = terrainData.terrainLayers.Length;

//         foreach (var (mod, centerX, centerZ, radiusInCells) in modData)
//         {
//             Parallel.For(0, height, z =>
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     int globalX = x + minX;
//                     int globalZ = z + minZ;
//                     int dx = globalX - centerX;
//                     int dz = globalZ - centerZ;
//                     if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
//                     {
//                         for (int i = 0; i < numLayers; i++)
//                         {
//                             alphamaps[z, x, i] = (i == mod.paintLayerIndex) ? 1f : 0f;
//                         }
//                     }
//                 }
//             });
//         }
//         terrainData.SetAlphamaps(minX, minZ, alphamaps);
//     }
// }

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

[System.Serializable]
public class ObjectSpawnDefinition
{
    [Tooltip("Lista de prefabs do objeto a ser instanciado.")]
    public GameObject[] prefabs;

    [Tooltip("Quantidade máxima de objetos por chunk.")]
    public int maxCount = 50;

    [Tooltip("Valor de threshold do Perlin Noise para spawn.")]
    public float spawnThreshold = 0.5f;

    [Tooltip("Escala do Perlin Noise.")]
    public float noiseScale = 0.1f;

    [Tooltip("Altura mínima para que o objeto seja instanciado.")]
    public float minHeight = 20f;

    [Tooltip("Altura máxima para que o objeto seja instanciado.")]
    public float maxHeight = 200f;

    [Header("Pintura do Terreno")]
    [Tooltip("Índice da terrain layer que será usada para pintar a área do objeto.")]
    public int paintLayerIndex;

    [Tooltip("Raio (em unidades) para pintar o terreno ao redor do objeto.")]
    public float paintRadius = 5f;

    [Tooltip("Distância mínima entre os objetos instanciados.")]
    public float minDistance = 1f; // Modifique conforme a necessidade

    [Header("Remover Grass")]
    [Tooltip("Se verdadeiro, remove a grass na região onde o objeto é instanciado.")]
    public bool RemoveGrassOnObjectPossition;
    [Tooltip("Raio (em unidades) para remover a grass ao redor do objeto. Se <= 0, a remoção não será aplicada.")]
    public float grassRemovalRadius = 2f;

    [Header("Raridade do Objeto")]
    [Tooltip("Nível de raridade do objeto. Valores maiores tornam o objeto mais raro (mínimo 1).")]
    public int rarityLevel = 1;

    [Tooltip("Valor que define quantas unidades o objeto deve entrar no terreno (afundar).")]
    public float verticalSink = 0.2f;

    [Header("Bioma")]
    [Tooltip("Permite spawn no bioma: Forest.")]
    public bool allowForest = true;
    [Tooltip("Permite spawn no bioma: Desert.")]
    public bool allowDesert = false;
    [Tooltip("Permite spawn no bioma: Tundra.")]
    public bool allowTundra = false;
}

// Estruturas para armazenar modificações do terreno
public struct GrassModification
{
    public Vector3 position;
    public float exclusionRadius;
}

public struct PaintModification
{
    public Vector3 position;
    public int paintLayerIndex;
    public float paintRadius;
}

public class TerrainObjectSpawner : MonoBehaviour
{
    // Seed do terreno (pode ser passado como parâmetro também)
    [HideInInspector] public int seed = 42;

    [Tooltip("Definições para spawn de objetos (árvores, rochas, etc.).")]
    public ObjectSpawnDefinition[] objectSpawnDefinitions;

    public IEnumerator SpawnObjectsOnChunkCoroutine(Terrain terrain, Vector3 chunkWorldPos, GameObject chunk, int chunkSize)
    {
        if (terrain == null)
        {
            Debug.LogError("Terrain is null");
            yield break;
        }

        // Obtém o componente TerrainChunkInfo para verificar o bioma do chunk
        TerrainChunkInfo chunkInfo = chunk.GetComponent<TerrainChunkInfo>();
        if (chunkInfo == null)
        {
            Debug.LogError("TerrainChunkInfo não encontrado no chunk");
            yield break;
        }

        GameObject objectsParent = new GameObject("SpawnedObjects_" + chunkWorldPos);
        objectsParent.transform.SetParent(chunk.transform);

        // Cache de referências para evitar chamadas repetitivas
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // Listas para acumular as modificações de terreno em batch
        List<GrassModification> grassModifications = new List<GrassModification>();
        List<PaintModification> paintModifications = new List<PaintModification>();

        // Cria uma instância local do System.Random com seed derivada
        int chunkSeed = seed + (int)(chunkWorldPos.x * 73856093) + (int)(chunkWorldPos.z * 19349663);
        System.Random prng = new System.Random(chunkSeed);

        if (objectSpawnDefinitions != null && objectSpawnDefinitions.Length > 0)
        {
            foreach (ObjectSpawnDefinition spawnDef in objectSpawnDefinitions)
            {
                // Verifica se a definição é compatível com o bioma do chunk usando os booleans
                if (!IsAllowedBiome(chunkInfo.biome, spawnDef))
                    continue; // Pula se o bioma do chunk não estiver permitido

                // Ajusta o threshold se houver vizinhos diferentes (área de transição)
                float effectiveSpawnThreshold = spawnDef.spawnThreshold;
                if (chunkInfo.hasDifferentNeighbor)
                    effectiveSpawnThreshold += 0.1f; // ajuste conforme desejado

                // Pré-cálculo do mapa de ruído para o chunk utilizando multithreading.
                int noiseResolution = chunkSize + 1;
                float[,] noiseMap = new float[noiseResolution, noiseResolution];
                bool noiseDone = false;
                Thread noiseThread = new Thread(() =>
                {
                    for (int z = 0; z < noiseResolution; z++)
                    {
                        for (int x = 0; x < noiseResolution; x++)
                        {
                            float worldX = chunkWorldPos.x + x;
                            float worldZ = chunkWorldPos.z + z;
                            noiseMap[x, z] = Mathf.PerlinNoise((worldX + seed) * spawnDef.noiseScale, (worldZ + seed) * spawnDef.noiseScale);
                        }
                    }
                    noiseDone = true;
                });
                noiseThread.Start();
                // Espera até que o cálculo do noiseMap seja concluído
                yield return new WaitUntil(() => noiseDone);

                // Lista para armazenar as posições dos objetos já spawnados para essa definição
                List<Vector3> spawnedPositions = new List<Vector3>();

                int spawnedCount = 0;
                int attempts = 0;
                while (spawnedCount < spawnDef.maxCount && attempts < spawnDef.maxCount * 10)
                {
                    attempts++;

                    // Gera uma posição aleatória dentro do chunk utilizando o prng local
                    float posX = (float)prng.NextDouble() * chunkSize;
                    float posZ = (float)prng.NextDouble() * chunkSize;
                    Vector3 posWorld = new Vector3(chunkWorldPos.x + posX, 0, chunkWorldPos.z + posZ);

                    // Obtém a altura do terreno na posição (chamada na thread principal)
                    float y = terrain.SampleHeight(posWorld);
                    posWorld.y = y;

                    // Verifica se a altura está dentro dos limites definidos
                    if (y >= spawnDef.minHeight && y <= spawnDef.maxHeight)
                    {
                        // Amostra o valor do Perlin Noise a partir do mapa pré-calculado.
                        int ix = Mathf.Clamp(Mathf.RoundToInt(posX), 0, chunkSize);
                        int iz = Mathf.Clamp(Mathf.RoundToInt(posZ), 0, chunkSize);
                        float noiseValue = noiseMap[ix, iz];

                        // Aplica o threshold e a raridade (chance de 1/rarityLevel) utilizando o prng local
                        if (noiseValue > effectiveSpawnThreshold && prng.Next(0, spawnDef.rarityLevel) == 0)
                        {
                            // Verifica se a posição está suficientemente distante dos objetos já spawnados
                            bool tooClose = false;
                            foreach (Vector3 pos in spawnedPositions)
                            {
                                if (Vector3.Distance(pos, posWorld) < spawnDef.minDistance)
                                {
                                    tooClose = true;
                                    break;
                                }
                            }
                            if (tooClose)
                                continue;

                            if (spawnDef.prefabs != null && spawnDef.prefabs.Length > 0)
                            {
                                GameObject selectedPrefab = spawnDef.prefabs[prng.Next(0, spawnDef.prefabs.Length)];
                                GameObject spawnedObject = Instantiate(selectedPrefab, posWorld, Quaternion.identity, objectsParent.transform);
                                spawnedObject.transform.localScale = selectedPrefab.transform.localScale;
                                spawnedPositions.Add(posWorld);

                                // Aguarda um frame para garantir que os colliders já foram calculados
                                yield return null;

                                // Obtém todos os colliders (inclusive de filhos) que não sejam triggers
                                Collider[] colliders = spawnedObject.GetComponentsInChildren<Collider>();
                                if (colliders != null && colliders.Length > 0)
                                {
                                    float minY = float.MaxValue;
                                    foreach (Collider col in colliders)
                                    {
                                        if (!col.isTrigger)
                                        {
                                            // Usa os bounds em world space para determinar o ponto mais baixo
                                            minY = Mathf.Min(minY, col.bounds.min.y);
                                        }
                                    }
                                    if (minY != float.MaxValue)
                                    {
                                        // Calcula o offset entre o pivô do objeto e o ponto mais baixo dos colliders
                                        float offset = spawnedObject.transform.position.y - minY;
                                        // Reposiciona o objeto para que o ponto mais baixo fique na altura do terreno,
                                        // subtraindo o valor de verticalSink para afundar o objeto conforme definido.
                                        spawnedObject.transform.position = new Vector3(
                                            spawnedObject.transform.position.x,
                                            posWorld.y + offset - spawnDef.verticalSink,
                                            spawnedObject.transform.position.z);
                                    }
                                }

                                // Remove a grass somente se estiver habilitado e se o raio definido for maior que zero
                                if (spawnDef.RemoveGrassOnObjectPossition && spawnDef.grassRemovalRadius > 0)
                                {
                                    grassModifications.Add(new GrassModification { position = posWorld, exclusionRadius = spawnDef.grassRemovalRadius });
                                }

                                // Adiciona a modificação de pintura apenas se paintRadius for maior que 0
                                if (spawnDef.paintRadius > 0)
                                {
                                    paintModifications.Add(new PaintModification
                                    {
                                        position = posWorld,
                                        paintLayerIndex = spawnDef.paintLayerIndex,
                                        paintRadius = spawnDef.paintRadius
                                    });
                                }

                                spawnedCount++;
                            }
                        }
                    }

                    // Distribui o processamento a cada 10 iterações para evitar travamentos
                    if (attempts % 10 == 0)
                        yield return null;
                }

                // Aguarda um frame entre processamentos de diferentes definições
                yield return null;
            }
        }

        // Após spawnar todos os objetos do chunk, aplica as modificações no terreno em batch
        BatchRemoveGrass(terrain, grassModifications);
        BatchPaintTerrain(terrain, paintModifications);

        // Define que o chunk terminou de receber decorações
        chunkInfo.TerrainCompletedDecoration = true;
    }

    /// <summary>
    /// Verifica se o bioma do chunk é permitido para spawnar este objeto, utilizando os booleans.
    /// Agora utilizando biomeType.
    /// </summary>
    private bool IsAllowedBiome(BiomeDefinition biome, ObjectSpawnDefinition spawnDef)
    {
        switch (biome.biomeType)
        {
            case BiomeType.Forest:
                return spawnDef.allowForest;
            case BiomeType.Desert:
                return spawnDef.allowDesert;
            case BiomeType.Tundra:
                return spawnDef.allowTundra;
            default:
                return false;
        }
    }

    /// <summary>
    /// Aplica em batch as remoções de grama acumuladas, usando Parallel.For para distribuir o processamento.
    /// </summary>
    public void BatchRemoveGrass(Terrain terrain, List<GrassModification> mods)
    {
        TerrainData terrainData = terrain.terrainData;
        int detailResolution = terrainData.detailResolution;
        int[,] detailLayer = terrainData.GetDetailLayer(0, 0, detailResolution, detailResolution, 0);
        Vector3 terrainPos = terrain.transform.position;

        foreach (var mod in mods)
        {
            float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
            float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
            int centerX = Mathf.RoundToInt(normX * detailResolution);
            int centerZ = Mathf.RoundToInt(normZ * detailResolution);
            int radiusInCells = Mathf.RoundToInt((mod.exclusionRadius / terrainData.size.x) * detailResolution);

            Parallel.For(centerZ - radiusInCells, centerZ + radiusInCells + 1, z =>
            {
                for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
                {
                    if (x >= 0 && x < detailResolution && z >= 0 && z < detailResolution)
                    {
                        int dx = x - centerX;
                        int dz = z - centerZ;
                        if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
                        {
                            detailLayer[z, x] = 0;
                        }
                    }
                }
            });
        }
        terrainData.SetDetailLayer(0, 0, 0, detailLayer);
    }

    /// <summary>
    /// Aplica em batch as pinturas de terreno acumuladas, utilizando Parallel.For para acelerar o processamento.
    /// </summary>
    public void BatchPaintTerrain(Terrain terrain, List<PaintModification> mods)
    {
        if (mods.Count == 0)
            return;

        TerrainData terrainData = terrain.terrainData;
        int alphamapResolution = terrainData.alphamapResolution;
        Vector3 terrainPos = terrain.transform.position;

        // Calcula os limites (em coordenadas de alphamap) de todas as modificações
        int minX = alphamapResolution;
        int minZ = alphamapResolution;
        int maxX = 0;
        int maxZ = 0;

        // Armazena dados auxiliares para cada modificação
        var modData = new List<(PaintModification mod, int centerX, int centerZ, int radiusInCells)>();
        foreach (var mod in mods)
        {
            float normX = (mod.position.x - terrainPos.x) / terrainData.size.x;
            float normZ = (mod.position.z - terrainPos.z) / terrainData.size.z;
            int centerX = Mathf.RoundToInt(normX * alphamapResolution);
            int centerZ = Mathf.RoundToInt(normZ * alphamapResolution);
            int radiusInCells = Mathf.RoundToInt((mod.paintRadius / terrainData.size.x) * alphamapResolution);
            modData.Add((mod, centerX, centerZ, radiusInCells));

            minX = Mathf.Min(minX, centerX - radiusInCells);
            minZ = Mathf.Min(minZ, centerZ - radiusInCells);
            maxX = Mathf.Max(maxX, centerX + radiusInCells);
            maxZ = Mathf.Max(maxZ, centerZ + radiusInCells);
        }

        // Limita a área dentro dos bounds válidos do alphamap
        minX = Mathf.Max(0, minX);
        minZ = Mathf.Max(0, minZ);
        maxX = Mathf.Min(alphamapResolution, maxX);
        maxZ = Mathf.Min(alphamapResolution, maxZ);
        int width = maxX - minX;
        int height = maxZ - minZ;

        // Obtém os alphamaps da área afetada
        float[,,] alphamaps = terrainData.GetAlphamaps(minX, minZ, width, height);
        int numLayers = terrainData.terrainLayers.Length;

        foreach (var (mod, centerX, centerZ, radiusInCells) in modData)
        {
            Parallel.For(0, height, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    int globalX = x + minX;
                    int globalZ = z + minZ;
                    int dx = globalX - centerX;
                    int dz = globalZ - centerZ;
                    if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
                    {
                        for (int i = 0; i < numLayers; i++)
                        {
                            alphamaps[z, x, i] = (i == mod.paintLayerIndex) ? 1f : 0f;
                        }
                    }
                }
            });
        }
        terrainData.SetAlphamaps(minX, minZ, alphamaps);
    }
}
