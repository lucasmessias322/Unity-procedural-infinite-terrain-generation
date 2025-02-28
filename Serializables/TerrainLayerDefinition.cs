



using UnityEngine;

[System.Serializable]
public class TerrainLayerDefinition
{
    [Tooltip("Terrain Layer a ser usado nesta condição.")]
    public TerrainLayer terrainLayer;
    [Tooltip("Altura mínima para que esta camada seja aplicada (em unidades).")]
    public float minHeight = 0f;
    [Tooltip("Altura máxima para que esta camada seja aplicada (em unidades).")]
    public float maxHeight = 50f;
    [Tooltip("Slope mínimo para que esta camada seja aplicada (em graus).")]
    public float minSlope = 0f;
    [Tooltip("Slope máximo para que esta camada seja aplicada (em graus).")]
    public float maxSlope = 90f;

    
}
