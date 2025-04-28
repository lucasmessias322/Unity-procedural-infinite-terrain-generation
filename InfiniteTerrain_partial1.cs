using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public partial class InfiniteTerrain
{

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

    // transiçao de biomas
    private void ApplyBiomeTransitionBlend(ref float[,,] splatmapData, TerrainData terrainData, TerrainChunkInfo chunkInfo, float[,] heights, float terrainHeight)
    {
        // Se houver bordas com vizinhos de bioma diferente, aplica o blend somente neste chunk
        if (chunkInfo.hasDifferentNeighbor && splatmapData != null)
        {
            // Converte as layers atuais para uma lista para facilitar a verificação
            List<TerrainLayer> layerList = new List<TerrainLayer>(terrainData.terrainLayers);
            int currentTotalLayers = layerList.Count;

            // Blend para as bordas cardinais
            if (chunkInfo.blendNorth)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeNorth.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.North);
            }

            if (chunkInfo.blendEast)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeEast.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.East);
            }

            if (chunkInfo.blendSouth)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeSouth.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.South);
            }

            if (chunkInfo.blendWest)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeWest.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.West);
            }

            // Blend para as bordas diagonais
            if (chunkInfo.blendNorthEast)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeNorthEast.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeNorthEast.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeNorthEast.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.NorthEast);
            }

            if (chunkInfo.blendSouthEast)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeSouthEast.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeSouthEast.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeSouthEast.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.SouthEast);
            }

            if (chunkInfo.blendSouthWest)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeSouthWest.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeSouthWest.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeSouthWest.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.SouthWest);
            }

            if (chunkInfo.blendNorthWest)
            {
                TerrainLayer neighborLayer = chunkInfo.neighborBiomeNorthWest.terrainLayerDefinitions[0].terrainLayer;
                if (!layerList.Contains(neighborLayer))
                {
                    layerList.Add(neighborLayer);
                    currentTotalLayers = layerList.Count;
                    splatmapData = ExpandSplatmapChannels(splatmapData, currentTotalLayers);
                }
                int neighborLayerIndex = layerList.IndexOf(neighborLayer);
                float minTransitionHeight = chunkInfo.neighborBiomeNorthWest.terrainLayerDefinitions[0].minHeight;
                float maxTransitionHeight = chunkInfo.neighborBiomeNorthWest.terrainLayerDefinitions[0].maxHeight;
                BlendBorderWithSide(ref splatmapData, currentTotalLayers, neighborLayerIndex, heights, terrainHeight, minTransitionHeight, maxTransitionHeight, BorderDirection.NorthWest);
            }

            terrainData.terrainLayers = layerList.ToArray();
            terrainData.SetAlphamaps(0, 0, splatmapData);
        }
    }

    void UpdateNeighborsForChunk(Vector2Int coord, TerrainChunkInfo chunkInfo)
    {
        // Atualiza os vizinhos cardinais
        UpdateNeighbors(coord, chunkInfo);

        // Atualiza os vizinhos diagonais
        Vector2Int[] diagDirections = new Vector2Int[]
        {
            new Vector2Int(1, 1),    // Nordeste
            new Vector2Int(1, -1),   // Sudeste
            new Vector2Int(-1, -1),  // Sudoeste
            new Vector2Int(-1, 1)    // Noroeste
        };

        foreach (Vector2Int direction in diagDirections)
        {
            Vector2Int neighborCoord = coord + direction;
            if (terrainChunks.TryGetValue(neighborCoord, out Terrain neighborTerrain))
            {
                TerrainChunkInfo neighborInfo = neighborTerrain.GetComponent<TerrainChunkInfo>();
                if (neighborInfo != null)
                {
                    UpdateDiagonalNeighbor(chunkInfo, direction, neighborInfo);
                }
            }
        }

        // Atualiza a flag geral incluindo diagonais
        chunkInfo.hasDifferentNeighbor = chunkInfo.blendNorth || chunkInfo.blendEast || chunkInfo.blendSouth || chunkInfo.blendWest ||
                                          chunkInfo.blendNorthEast || chunkInfo.blendSouthEast || chunkInfo.blendSouthWest || chunkInfo.blendNorthWest;

        int differentCount = 0;
        if (chunkInfo.blendNorth) differentCount++;
        if (chunkInfo.blendEast) differentCount++;
        if (chunkInfo.blendSouth) differentCount++;
        if (chunkInfo.blendWest) differentCount++;
        if (chunkInfo.blendNorthEast) differentCount++;
        if (chunkInfo.blendSouthEast) differentCount++;
        if (chunkInfo.blendSouthWest) differentCount++;
        if (chunkInfo.blendNorthWest) differentCount++;
        chunkInfo.hasMultipleDifferentNeighbors = (differentCount > 1);
    }

    void UpdateNeighbors(Vector2Int coord, TerrainChunkInfo chunkInfo)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Norte
            new Vector2Int(1, 0),   // Leste
            new Vector2Int(0, -1),  // Sul
            new Vector2Int(-1, 0)   // Oeste
        };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborCoord = coord + direction;
            if (terrainChunks.TryGetValue(neighborCoord, out Terrain neighborTerrain))
            {
                TerrainChunkInfo neighborInfo = neighborTerrain.GetComponent<TerrainChunkInfo>();
                if (neighborInfo != null)
                {
                    if (direction == new Vector2Int(0, 1)) // Norte
                    {
                        chunkInfo.neighborBiomeNorth = neighborInfo.biome;
                        chunkInfo.blendNorth = (neighborInfo.biome != chunkInfo.biome);
                    }
                    else if (direction == new Vector2Int(1, 0)) // Leste
                    {
                        chunkInfo.neighborBiomeEast = neighborInfo.biome;
                        chunkInfo.blendEast = (neighborInfo.biome != chunkInfo.biome);
                    }
                    else if (direction == new Vector2Int(0, -1)) // Sul
                    {
                        chunkInfo.neighborBiomeSouth = neighborInfo.biome;
                        chunkInfo.blendSouth = (neighborInfo.biome != chunkInfo.biome);
                    }
                    else if (direction == new Vector2Int(-1, 0)) // Oeste
                    {
                        chunkInfo.neighborBiomeWest = neighborInfo.biome;
                        chunkInfo.blendWest = (neighborInfo.biome != chunkInfo.biome);
                    }
                }
            }
        }

        // Atualiza flags dos vizinhos cardinais
        chunkInfo.hasDifferentNeighbor = chunkInfo.blendNorth || chunkInfo.blendEast || chunkInfo.blendSouth || chunkInfo.blendWest;
        int differentCount = 0;
        if (chunkInfo.blendNorth) differentCount++;
        if (chunkInfo.blendEast) differentCount++;
        if (chunkInfo.blendSouth) differentCount++;
        if (chunkInfo.blendWest) differentCount++;
        chunkInfo.hasMultipleDifferentNeighbors = (differentCount > 1);
    }

    void UpdateDiagonalNeighbor(TerrainChunkInfo chunkInfo, Vector2Int direction, TerrainChunkInfo neighborInfo)
    {
        if (direction == new Vector2Int(1, 1)) // Nordeste
        {
            chunkInfo.neighborBiomeNorthEast = neighborInfo.biome;
            chunkInfo.blendNorthEast = (neighborInfo.biome != chunkInfo.biome);
        }
        else if (direction == new Vector2Int(1, -1)) // Sudeste
        {
            chunkInfo.neighborBiomeSouthEast = neighborInfo.biome;
            chunkInfo.blendSouthEast = (neighborInfo.biome != chunkInfo.biome);
        }
        else if (direction == new Vector2Int(-1, -1)) // Sudoeste
        {
            chunkInfo.neighborBiomeSouthWest = neighborInfo.biome;
            chunkInfo.blendSouthWest = (neighborInfo.biome != chunkInfo.biome);
        }
        else if (direction == new Vector2Int(-1, 1)) // Noroeste
        {
            chunkInfo.neighborBiomeNorthWest = neighborInfo.biome;
            chunkInfo.blendNorthWest = (neighborInfo.biome != chunkInfo.biome);
        }
    }

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

                // Condições para bordas cardinais
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
                // Condições para bordas diagonais
                else if (direction == BorderDirection.NorthEast && x >= width - effectiveBlendMargin && z >= height - effectiveBlendMargin)
                {
                    t = Mathf.Min((width - x - 1), (height - z - 1)) / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.SouthEast && x >= width - effectiveBlendMargin && z < effectiveBlendMargin)
                {
                    t = Mathf.Min((width - x - 1), z) / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.SouthWest && x < effectiveBlendMargin && z < effectiveBlendMargin)
                {
                    t = Mathf.Min(x, z) / (float)effectiveBlendMargin;
                    blendFactor = 1f - Mathf.Pow(t, transitionCurveExponent);
                }
                else if (direction == BorderDirection.NorthWest && x < effectiveBlendMargin && z >= height - effectiveBlendMargin)
                {
                    t = Mathf.Min(x, (height - z - 1)) / (float)effectiveBlendMargin;
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



    void ApplyGrassDetails(TerrainData terrainData, float[,,] splatmapData, BiomeDefinition biome, TerrainChunkInfo chunkInfo)
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

}

