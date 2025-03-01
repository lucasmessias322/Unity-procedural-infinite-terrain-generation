using UnityEngine;
public class TerrainChunkInfo : MonoBehaviour
{
    public BiomeDefinition biome;
    public bool hasDifferentNeighbor;

    public bool hasMultipleDifferentNeighbors;
    

    // Biomas vizinhos para cada direção:
    public BiomeDefinition neighborBiomeNorth;
    public BiomeDefinition neighborBiomeEast;
    public BiomeDefinition neighborBiomeSouth;
    public BiomeDefinition neighborBiomeWest;

    

    // Flags para blend em cada direção:
    public bool blendNorth;
    public bool blendEast;
    public bool blendSouth;
    public bool blendWest;
}
