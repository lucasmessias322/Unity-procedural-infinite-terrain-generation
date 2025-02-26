using UnityEngine;
[System.Serializable]
public class ObjectSpawnDefinition
{
    [Tooltip("Prefab do objeto a ser instanciado.")]
    public GameObject prefab;

    [Tooltip("Quantidade máxima de objetos por chunk.")]
    public int maxCount = 50;

    [Tooltip("Valor de threshold do Perlin Noise para spawn.")]
    public float spawnThreshold = 0.5f;

    [Tooltip("Escala do Perlin Noise.")]
    public float noiseScale = 0.1f;

    [Tooltip("Altura mínima para que o objeto seja instanciado.")]
    public float minHeight = 20f;

    [Tooltip("Altura máxima para que o objeto seja instanciado.")]
    public float maxHeight = 200f; // Novo campo para definir a altura máxima

    [Header("Pintura do Terreno")]
    [Tooltip("Índice da terrain layer que será usada para pintar a área do objeto.")]
    public int paintLayerIndex;

    [Tooltip("Raio (em unidades) para pintar o terreno ao redor do objeto.")]
    public float paintRadius = 5f;
}

public class TerrainObjectSpawner : MonoBehaviour
{
    // Você pode passar o seed do terreno aqui, ou defini-lo como parâmetro do método
    public int seed = 42;

    [Tooltip("Definições para spawn de objetos (árvores, rochas, etc.).")]
    public ObjectSpawnDefinition[] objectSpawnDefinitions;

    public void SpawnObjectsOnChunk(Terrain terrain, Vector3 chunkWorldPos, GameObject parent, int chunkSize)
    {

        if (objectSpawnDefinitions != null && objectSpawnDefinitions.Length > 0)
        {
            foreach (ObjectSpawnDefinition spawnDef in objectSpawnDefinitions)
            {
                int spawnedCount = 0;
                int attempts = 0;
                while (spawnedCount < spawnDef.maxCount && attempts < spawnDef.maxCount * 10)
                {
                    attempts++;
                    // Posição aleatória dentro do chunk
                    float posX = Random.Range(0f, chunkSize);
                    float posZ = Random.Range(0f, chunkSize);
                    Vector3 posWorld = new Vector3(chunkWorldPos.x + posX, 0, chunkWorldPos.z + posZ);
                    // Obtém a altura do terreno na posição
                    float y = terrain.SampleHeight(posWorld);
                    posWorld.y = y;

                    // Verifica se a posição atende ao critério de altura
                    if (y >= spawnDef.minHeight && y <= spawnDef.maxHeight)
                    {
                        // Usa Perlin Noise para decidir o spawn
                        float noiseValue = Mathf.PerlinNoise((posWorld.x + seed) * spawnDef.noiseScale, (posWorld.z + seed) * spawnDef.noiseScale);
                        if (noiseValue > spawnDef.spawnThreshold)
                        {// Dentro do método de spawn, após instanciar o objeto:
                            GameObject spawnedObject = Instantiate(spawnDef.prefab, posWorld, Quaternion.identity, parent.transform);
                            RemoveGrassUnderObject(terrain, posWorld, 2f); // 2f representa o raio de exclusão, ajuste conforme necessário.

                            // Pinta o terreno na área onde o objeto foi colocado, usando a terrain layer definida.
                            PaintTerrainAtPosition(terrain, posWorld, spawnDef.paintLayerIndex, spawnDef.paintRadius);
                            spawnedCount++;
                        }
                    }
                }
            }
        }

    }

    /// <summary>
    /// Remove a grama do terrain na área onde o objeto foi instanciado.
    /// </summary>
    /// <param name="terrain">Terrain onde remover a grama.</param>
    /// <param name="objectWorldPos">Posição do objeto no mundo.</param>
    /// <param name="exclusionRadius">Raio (em unidades) para remoção da grama.</param>
    public void RemoveGrassUnderObject(Terrain terrain, Vector3 objectWorldPos, float exclusionRadius)
    {
        TerrainData terrainData = terrain.terrainData;
        int detailResolution = terrainData.detailResolution;
        int[,] detailLayer = terrainData.GetDetailLayer(0, 0, detailResolution, detailResolution, 0);

        // Converte a posição do objeto para coordenadas normalizadas relativas ao terreno.
        Vector3 terrainPos = terrain.transform.position;
        float normX = (objectWorldPos.x - terrainPos.x) / terrainData.size.x;
        float normZ = (objectWorldPos.z - terrainPos.z) / terrainData.size.z;

        int centerX = Mathf.RoundToInt(normX * detailResolution);
        int centerZ = Mathf.RoundToInt(normZ * detailResolution);
        // Converte o raio de exclusão de unidades para células do detail map.
        int radiusInCells = Mathf.RoundToInt((exclusionRadius / terrainData.size.x) * detailResolution);

        // Percorre as células do detail layer e zera as que estiverem dentro do círculo de exclusão.
        for (int z = centerZ - radiusInCells; z <= centerZ + radiusInCells; z++)
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
        }
        terrainData.SetDetailLayer(0, 0, 0, detailLayer);
    }


    public void PaintTerrainAtPosition(Terrain terrain, Vector3 worldPos, int targetLayerIndex, float paintRadius)
    {
        TerrainData terrainData = terrain.terrainData;
        int alphamapResolution = terrainData.alphamapResolution;

        // Converte a posição do mundo para coordenadas normalizadas do terreno.
        Vector3 terrainPos = terrain.transform.position;
        float normX = (worldPos.x - terrainPos.x) / terrainData.size.x;
        float normZ = (worldPos.z - terrainPos.z) / terrainData.size.z;

        int centerX = Mathf.RoundToInt(normX * alphamapResolution);
        int centerZ = Mathf.RoundToInt(normZ * alphamapResolution);

        // Converte o raio de unidades do mundo para células do alphamap.
        int radiusInCells = Mathf.RoundToInt((paintRadius / terrainData.size.x) * alphamapResolution);

        // Define os limites da área a ser alterada.
        int xStart = Mathf.Max(0, centerX - radiusInCells);
        int xEnd = Mathf.Min(alphamapResolution, centerX + radiusInCells);
        int zStart = Mathf.Max(0, centerZ - radiusInCells);
        int zEnd = Mathf.Min(alphamapResolution, centerZ + radiusInCells);

        int width = xEnd - xStart;
        int height = zEnd - zStart;

        // Obtém a parte do alphamap que será modificada.
        float[,,] alphamaps = terrainData.GetAlphamaps(xStart, zStart, width, height);
        int numLayers = terrainData.terrainLayers.Length;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int dx = (x + xStart) - centerX;
                int dz = (z + zStart) - centerZ;
                // Verifica se a célula está dentro do círculo de raio especificado.
                if (dx * dx + dz * dz <= radiusInCells * radiusInCells)
                {
                    // Define o valor 1 para o layer alvo e 0 para os demais.
                    for (int i = 0; i < numLayers; i++)
                    {
                        alphamaps[z, x, i] = (i == targetLayerIndex) ? 1f : 0f;
                    }
                }
            }
        }
        // Atualiza o alphamap do terreno com a região modificada.
        terrainData.SetAlphamaps(xStart, zStart, alphamaps);
    }


}
