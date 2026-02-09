using System;
using UnityEngine;

/// <summary>
/// Struct para camadas de ruído Perlin.
/// </summary>
// [Serializable]
// public struct NoiseLayer
// {
//     [Tooltip("Escala do Perlin Noise no eixo X.")]
//     public float scaleX;
//     [Tooltip("Escala do Perlin Noise no eixo Y.")]
//     public float scaleY;
//     [Tooltip("Amplitude da camada.")]
//     public float amplitude;
//     [Tooltip("Limiar de altura para aplicar esta camada (0 = sempre). Positivo para aplicar acima do valor e negativo para abaixo).")]
//     public float heightThreshold;
// }

[System.Serializable]
public class GlobalLayerDefinition
{
    public TerrainLayer terrainLayer;
    public float minHeight; // minimum height for application
    public float maxHeight; // maximum height for application
    public float minSlope;  // minimum slope
    public float maxSlope;  // maximum slope
    public float heightBlendDistance; // distance for smooth transition on height
    public float slopeBlendDistance; // distance for smooth transition on slope
}

public enum GrassRenderMode
{
    Mesh,
    Billboard2D
}

// [System.Serializable]
// public class GrassDetailDefinition
// {
//     public GrassRenderMode grassRenderMode;
//     public GameObject grassPrefab;      // Utilizado se o modo for Mesh
//     public bool useInstancing;
//     public Texture2D grassTexture;        // Utilizado se o modo for Billboard2D
//     public int targetLayerIndex;          // Índice da layer que receberá a grama
//     public float threshold;               // Threshold do splat map para ativar a grama
//     public int grassMapDensity;
//     public float minWidth;
//     public float maxWidth;
//     public float minHeight;
//     public float maxHeight;
//     public float noiseSpread;
//     public Color healthyColor;
//     public Color dryColor;
//     public int grassPrototypeDensity;

// }

[System.Serializable]
public class GrassDetailDefinition
{
    public GrassRenderMode grassRenderMode;
    public GameObject grassPrefab; // Para Mesh
    public Texture2D grassTexture; // Para Billboard2D
    public float minWidth;
    public float maxWidth;
    public float minHeight;
    public float maxHeight;
    public float noiseSpread;
    public Color healthyColor;
    public Color dryColor;
    public bool useInstancing;
    public float threshold;
    public int grassMapDensity;
    public int grassPrototypeDensity;
    public TerrainLayer targetLayer; // Novo campo para referenciar a TerrainLayer diretamente
}



