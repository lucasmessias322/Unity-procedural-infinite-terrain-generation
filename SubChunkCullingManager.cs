using UnityEngine;
using System.Collections.Generic;

public class SubChunkCullingManager : MonoBehaviour
{
    public Camera mainCamera;
    [Tooltip("Recalcula os bounds automaticamente a cada N segundos (0 = nunca).")]
    public float recomputeBoundsInterval = 1f;
    [Tooltip("Se true, recalcula automaticamente quando detectar mudança no número de filhos.")]
    public bool autoRecomputeBounds = true;
    [Tooltip("Desenhar bounds dos subchunks para debug.")]
    public bool drawGizmos = false;

    private Plane[] frustumPlanes;

    public class SubChunkData
    {
        public Transform container;
        public Bounds bounds;
        public bool isVisible;
        public int lastChildCount;
        public float lastRecomputeTime;
    }

    // Uso Dictionary para lookup rápido
    private Dictionary<Transform, SubChunkData> subChunks = new Dictionary<Transform, SubChunkData>();

    // Registra um subchunk. FallbackSize usado caso não haja renderers ainda.
    public void RegisterSubChunk(Transform container, float fallbackSize = 1f)
    {
        if (container == null) return;
        if (subChunks.ContainsKey(container)) return;

        Bounds b = CalculateBoundsFor(container, fallbackSize);
        SubChunkData data = new SubChunkData
        {
            container = container,
            bounds = b,
            isVisible = true,
            lastChildCount = container.childCount,
            lastRecomputeTime = Time.time
        };

        subChunks[container] = data;
    }

    // Remove registro
    public void UnregisterSubChunk(Transform container)
    {
        if (container == null) return;
        subChunks.Remove(container);
    }

    // Marca para recalcular imediatamente (use após adicionar/remover objetos no container)
    public void MarkDirty(Transform container, float fallbackSize = 1f)
    {
        if (container == null) return;
        if (subChunks.TryGetValue(container, out var data))
        {
            data.bounds = CalculateBoundsFor(container, fallbackSize);
            data.lastRecomputeTime = 0f;
            data.lastChildCount = container.childCount;
        }
    }

    // Calcula bounds combinando todos os Renderers (inclui inativos)
    Bounds CalculateBoundsFor(Transform container, float fallbackSize = 1f)
    {
        var renderers = container.GetComponentsInChildren<Renderer>(true); // <--- inclui objetos inativos
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(container.position, Vector3.one * fallbackSize);
        }
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

        float now = Time.time;
        // iterate sobre uma cópia para permitir remoções seguras
        var list = new List<SubChunkData>(subChunks.Values);
        foreach (var sc in list)
        {
            if (sc.container == null)
            {
                // removemos entradas com container destruído
                subChunks.Remove(sc.container);
                continue;
            }

            bool shouldRecalc = false;
            if (autoRecomputeBounds)
            {
                if (sc.container.childCount != sc.lastChildCount) shouldRecalc = true;
                if (recomputeBoundsInterval > 0f && (now - sc.lastRecomputeTime) >= recomputeBoundsInterval) shouldRecalc = true;
            }

            if (shouldRecalc)
            {
                sc.bounds = CalculateBoundsFor(sc.container, Mathf.Max(1f, sc.bounds.size.magnitude));
                sc.lastChildCount = sc.container.childCount;
                sc.lastRecomputeTime = now;
            }

            bool visible = GeometryUtility.TestPlanesAABB(frustumPlanes, sc.bounds);
            if (visible != sc.isVisible)
            {
                sc.isVisible = visible;
                sc.container.gameObject.SetActive(visible);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.cyan;
        foreach (var kvp in subChunks)
        {
            var sc = kvp.Value;
            Gizmos.DrawWireCube(sc.bounds.center, sc.bounds.size);
        }
    }
}
