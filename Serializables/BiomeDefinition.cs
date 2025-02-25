
using UnityEngine;

[System.Serializable]
public class BiomeDefinition
{
    public BiomeType biomeType;

    [Header("Parâmetros de Altura")]
    public float highFrequencyScale = 0.02f;
    public float highFrequencyAmplitude = 10f;
    public float lowFrequencyScale = 0.005f;
    public float lowFrequencyAmplitude = 70f;

    [Header("Camadas de Terreno")]
    [Tooltip("Layers específicas para este bioma.")]
    public TerrainLayerDefinition[] terrainLayerDefinitions;
}
