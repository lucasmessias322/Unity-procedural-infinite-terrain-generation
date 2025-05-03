
using UnityEngine;

public enum BiomeType
{
    Desert,
    Forest,
    Tundra
}

[CreateAssetMenu(fileName = "NovoBioma", menuName = "Terrain/Biome Definition")]
public class BiomeDefinition : ScriptableObject
{
    public BiomeType biomeType;



    // Camadas de textura para o terreno deste bioma
    public TerrainLayerDefinition[] terrainLayerDefinitions;

    // **Configuração específica de grama para este bioma**
    public GrassDetailDefinition grassDetailDefinition;
}
