// using UnityEngine;

// public class ChunkManager : MonoBehaviour
// {
//     public Transform player;
//     public int chunkSize = 512;
//     public float activationDistance = 512f; // Distância para ativar/desativar

//     void Update()
//     {
//         UpdateChunks();
//     }

//     void UpdateChunks()
//     {
//         for (int i = 0; i < transform.childCount; i++)
//         {
//             Transform chunk = transform.GetChild(i);
//             float distance = Vector3.Distance(player.position, chunk.position);

//             if (distance > activationDistance)
//             {
//                 if (chunk.gameObject.activeSelf)
//                 {
//                     chunk.gameObject.SetActive(false);
//                 }
//             }
//             else
//             {
//                 if (!chunk.gameObject.activeSelf)
//                 {
//                     chunk.gameObject.SetActive(true);
//                 }

//                 Terrain terrain = chunk.GetComponent<Terrain>();
//                 if (terrain != null)
//                 {
//                     AdjustTerrainQuality(terrain, distance);
//                 }
//             }
//         }
//     }

//     void AdjustTerrainQuality(Terrain terrain, float distance)
//     {
//         if (distance < chunkSize * 2)
//         {
//             terrain.heightmapPixelError = 5; // Alta qualidade
//             terrain.detailObjectDistance = 100; // Renderiza grama e detalhes próximos
//             // terrain.treeDistance = 500;
//         }
//         else if (distance < chunkSize * 4)
//         {
//             terrain.heightmapPixelError = 50; // Qualidade média
//             // terrain.detailObjectDistance = 50;
//             // terrain.treeDistance = 300;
//         }
//         else
//         {
//             terrain.heightmapPixelError = 100; // Qualidade baixa (menos polígonos)
//             // terrain.detailObjectDistance = 0; // Remove grama e detalhes
//             // terrain.treeDistance = 100;
//         }
//     }
// }

using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public Transform player;
    public int chunkSize = 512;
    public float activationDistance = 512f; // Distância para ativar/desativar
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform chunk = transform.GetChild(i);
            float distance = Vector3.Distance(player.position, chunk.position);
            bool wasActive = chunk.gameObject.activeSelf;

            if (distance > activationDistance)
            {
                if (wasActive)
                {
                    chunk.gameObject.SetActive(false);

                }
            }
            else
            {
                if (!wasActive)
                {
                    chunk.gameObject.SetActive(true);
                    // Reativa os nós na área do chunk

                }

                Terrain terrain = chunk.GetComponent<Terrain>();
                if (terrain != null)
                {
                    AdjustTerrainQuality(terrain, distance);
                }
            }
        }
    }

    void AdjustTerrainQuality(Terrain terrain, float distance)
    {
        if (distance < chunkSize * 2)
        {
            terrain.heightmapPixelError = 5; // Alta qualidade
            terrain.detailObjectDistance = 100; // Renderiza grama e detalhes próximos
            // terrain.treeDistance = 500;
        }
        else if (distance < chunkSize * 4)
        {
            terrain.heightmapPixelError = 50; // Qualidade média
            // terrain.detailObjectDistance = 50;
            // terrain.treeDistance = 300;
        }
        else
        {
            terrain.heightmapPixelError = 100; // Qualidade baixa (menos polígonos)
            // terrain.detailObjectDistance = 0; // Remove grama e detalhes
            // terrain.treeDistance = 100;
        }
    }
}
