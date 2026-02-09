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

    // Terrain layers
    public GlobalLayerDefinition[] terrainLayerDefinitions;

    // Grass
    public GrassDetailDefinition[] grassDetailDefinitions;

    [Header("Biome Radius (Valheim-style)")]
    public float minRadius = 0f;
    public float maxRadius = -1f; // -1 = infinito
}
