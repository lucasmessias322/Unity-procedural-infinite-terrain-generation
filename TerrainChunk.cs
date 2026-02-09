using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Representa um chunk de terreno (dados + runtime state).
/// Mantido leve — apenas campos públicos como no code original para compatibilidade com o pool.
/// </summary>
public class TerrainChunk
{
    // Runtime references
    public GameObject gameObject;
    public Terrain terrain;
    public TerrainCollider terrainCollider;
    public TerrainData terrainData;

    // Coordenação / pooling
    public Vector2Int chunkCoord = new Vector2Int(int.MinValue, int.MinValue);

    // Geração (background -> main thread)
    public CancellationTokenSource generationToken;
    public float[,] pendingHeights;
    public int pendingResolution;
    public float[,,] pendingAlphamaps;
    public int pendingAlphamapRes;
    public List<int[,]> pendingDetailMaps;

    // Water / spawning
    public bool needsWater;
    public Vector3 waterPosition;
    public GameObject waterTile;

    // Cached biome / ready flag
    public BiomeType biomeType;
    public bool isReady;

    /// <summary>
    /// Cancela geração em andamento e limpa pendings básicos.
    /// Chamado por InfiniteTerrain quando precisa cancelar/regenerar.
    /// </summary>
    public void CancelGeneration()
    {
        if (generationToken != null)
        {
            try { generationToken.Cancel(); }
            catch { /* swallow */ }
            generationToken = null;
        }

        // Limpa resultados pendentes (serão re-gerados se necessário)
        pendingHeights = null;
        pendingAlphamaps = null;
        pendingDetailMaps = null;
    }

    /// <summary>
    /// Reseta o estado do chunk para voltar ao pool.
    /// Centraliza limpeza que antes estava espalhada em RecycleChunk().
    /// OBS: não desativa o gameObject aqui — o gerenciador de pool faz isso.
    /// </summary>
    public void ResetForPool()
    {
        CancelGeneration();

        // Destrói tile de água se existir
        if (waterTile != null)
        {
            try { UnityEngine.Object.Destroy(waterTile); }
            catch { }
            waterTile = null;
        }

        needsWater = false;
        pendingDetailMaps = null;
        chunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        isReady = false;
    }
}
