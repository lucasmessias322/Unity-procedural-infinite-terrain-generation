
using UnityEngine;

// [System.Serializable]
// public class BiomeDefinition
// {
//     public BiomeType biomeType;

//     [Header("Parâmetros de Altura")]
//     public float highFrequencyScale = 0.02f;
//     public float highFrequencyAmplitude = 10f;
//     public float lowFrequencyScale = 0.005f;
//     public float lowFrequencyAmplitude = 70f;

//     [Header("Camadas de Terreno")]
//     [Tooltip("Layers específicas para este bioma.")]
//     public TerrainLayerDefinition[] terrainLayerDefinitions;
// }

public enum BiomeType
{
    Deserto,
    Floresta,
    Tundra
}

[CreateAssetMenu(fileName = "NovoBioma", menuName = "Terrain/Biome Definition")]
public class BiomeDefinition : ScriptableObject
{
    public BiomeType biomeType;

    // Configurações de ruído e altura do bioma
    public float highFrequencyScale;
    public float highFrequencyAmplitude;
    public float lowFrequencyScale;
    public float lowFrequencyAmplitude;

    // Camadas de textura para o terreno deste bioma
    public TerrainLayerDefinition[] terrainLayerDefinitions;

    // **Configuração específica de grama para este bioma**
    public GrassDetailDefinition grassDetailDefinition;
}
