using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseLayers", menuName = "Terrain/Noise Layers", order = 0)]
public class NoiseLayersSO : ScriptableObject
{
    public List<NoiseLayer> noiseLayers = new List<NoiseLayer>();
}

[Serializable]
public struct NoiseLayer
{
    public bool enabled;      // <<–– novo
    [Tooltip("Escala do Perlin Noise no eixo X.")]
    public float scaleX;
    [Tooltip("Escala do Perlin Noise no eixo Y.")]
    public float scaleY;
    [Tooltip("Amplitude da camada.")]
    public float amplitude;
    [Tooltip("Limiar de altura para aplicar esta camada (0 = sempre). Positivo para aplicar acima do valor e negativo para abaixo).")]
    public float heightThreshold;
    public float blendDistance;   // ⭐ blend por layer


    // --- NOVOS campos para octaves ---
    [Header("Octaves")]
    public int octaves;          // ex: 4
    public float lacunarity;     // ex: 2.0
    public float persistence;    // ex: 0.5

    
}