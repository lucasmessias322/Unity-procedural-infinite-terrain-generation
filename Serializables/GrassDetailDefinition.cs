
using UnityEngine;

[System.Serializable]
public class GrassDetailDefinition
{
    [Tooltip("Índice da TerrainLayer na qual a grama será espalhada.")]
    public int targetLayerIndex = 0;
    [Tooltip("Modo de renderização da grama: Mesh (prefab) ou Billboard 2D.")]
    public GrassRenderMode grassRenderMode = GrassRenderMode.Mesh;
    [Tooltip("Prefab da grama a ser espalhada (usado se o modo for Mesh).")]
    public GameObject grassPrefab;
    [Tooltip("Texture da grama a ser espalhada (usada se o modo for Billboard2D).")]
    public Texture2D grassTexture;
    [Tooltip("Densidade máxima da grama (valor inteiro, por exemplo, 10).")]
    public int grassMapDensity = 100;

    public float grassPrototypeDensity = 1;
    [Tooltip("Valor mínimo de alpha para que a grama seja colocada (entre 0 e 1).")]
    public float threshold = 0.5f;

    [Header("Atributos da Grama")]
    [Tooltip("Largura mínima da grama.")]
    public float minWidth = 0.5f;
    [Tooltip("Largura máxima da grama.")]
    public float maxWidth = 1f;
    [Tooltip("Altura mínima da grama.")]
    public float minHeight = 0.5f;
    [Tooltip("Altura máxima da grama.")]
    public float maxHeight = 1f;
    [Tooltip("Espalhamento do noise para variação da grama.")]
    public float noiseSpread = 0.5f;
    [Tooltip("Fator de flexão (bend) da grama.")]
    public float bendFactor = 0.2f;
    [Tooltip("Cor saudável da grama.")]
    public Color healthyColor = Color.green;
    [Tooltip("Cor seca da grama.")]
    public Color dryColor = Color.yellow;

}
