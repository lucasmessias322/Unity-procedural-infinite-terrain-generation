using UnityEngine;

[System.Serializable]
public class GlobalLayerDefinition
{
    public TerrainLayer terrainLayer;
    public float minHeight; // minimum height for application
    public float maxHeight; // maximum height for application
    public float minSlope;  // minimum slope
    public float maxSlope;  // maximum slope
    // Additional settings can be added here...
    
}