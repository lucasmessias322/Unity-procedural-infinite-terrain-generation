using UnityEngine;

public class TerrainChunkInfo : MonoBehaviour
{   public bool TerrainCompletedGeneration;
    public bool TerrainCompletedDecoration;
    public BiomeDefinition biome;
    public bool hasDifferentNeighbor;
    public bool hasMultipleDifferentNeighbors;
    
    // Biomas vizinhos para direções cardinais:
    public BiomeDefinition neighborBiomeNorth;
    public BiomeDefinition neighborBiomeEast;
    public BiomeDefinition neighborBiomeSouth;
    public BiomeDefinition neighborBiomeWest;
    
    // Flags para blend em direções cardinais:
    public bool blendNorth;
    public bool blendEast;
    public bool blendSouth;
    public bool blendWest;
    
    // Biomas vizinhos para direções diagonais:
    public BiomeDefinition neighborBiomeNorthEast;
    public BiomeDefinition neighborBiomeSouthEast;
    public BiomeDefinition neighborBiomeSouthWest;
    public BiomeDefinition neighborBiomeNorthWest;
    
    // Flags para blend em direções diagonais:
    public bool blendNorthEast;
    public bool blendSouthEast;
    public bool blendSouthWest;
    public bool blendNorthWest;
}
