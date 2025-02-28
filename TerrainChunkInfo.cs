
using UnityEngine;
public class TerrainChunkInfo : MonoBehaviour
{
    public BiomeDefinition biome;
    public bool hasDifferentNeighbor;
    public BiomeDefinition neighborBiome;

    // Flags para blend em cada direção:
    public bool blendNorth;
    public bool blendEast;
    public bool blendSouth;
    public bool blendWest;


}
