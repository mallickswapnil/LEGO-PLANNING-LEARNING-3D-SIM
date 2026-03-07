using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ThreeDBrickSim
{
    private const string OrangeLegoBrickAssetPath = "Assets/orangeLEGOBrick.fbx";
    private const bool OverrideSpawnedOrangeBrickMaterial = false;

    private void SpawnOrangeLegoBrick()
    {
        GameObject prefabToSpawn = ResolveOrangeLegoBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnOrangeLegoBrick: Could not load prefab at '{OrangeLegoBrickAssetPath}'.");
            return;
        }

        GameObject spawnedBrick = Instantiate(prefabToSpawn, orangeLegoBrickPosition, Quaternion.identity);
        spawnedBrick.name = "OrangeLegoBrick";

        if (OverrideSpawnedOrangeBrickMaterial && legoBrickMaterial != null)
        {
            Renderer[] renderers = spawnedBrick.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = legoBrickMaterial;
            }
        }

        EnsureBrickRigidbody(spawnedBrick);
    }

    private GameObject ResolveOrangeLegoBrickPrefab()
    {
        if (orangeLegoBrickPrefab != null)
        {
            return orangeLegoBrickPrefab;
        }

#if UNITY_EDITOR
        orangeLegoBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrangeLegoBrickAssetPath);
        return orangeLegoBrickPrefab;
#else
        return null;
#endif
    }
}
