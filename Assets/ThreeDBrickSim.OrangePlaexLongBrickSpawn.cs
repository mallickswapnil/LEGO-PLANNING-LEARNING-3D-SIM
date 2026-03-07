using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ThreeDBrickSim
{
    private const string OrangePlaexLongBrickAssetPath = "Assets/orangePLAEXLong.fbx";
    private const float OrangePlaexLongBrickSpacingPadding = 0.75f;

    private void SpawnOrangePlaexLongBrick()
    {
        GameObject prefabToSpawn = ResolveOrangePlaexLongBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnOrangePlaexLongBrick: Could not load prefab at '{OrangePlaexLongBrickAssetPath}'.");
            return;
        }

        GameObject spawnedBrick = Instantiate(prefabToSpawn, orangePlaexLongBrickPosition, Quaternion.identity);
        spawnedBrick.name = "OrangePlaexLongBrick";
        ApplySpacingFromExistingSpawnedBricks(spawnedBrick.transform);
        EnsureBrickRigidbody(spawnedBrick);
    }

    private GameObject ResolveOrangePlaexLongBrickPrefab()
    {
        if (orangePlaexLongBrickPrefab != null)
        {
            return orangePlaexLongBrickPrefab;
        }

#if UNITY_EDITOR
        orangePlaexLongBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrangePlaexLongBrickAssetPath);
        return orangePlaexLongBrickPrefab;
#else
        return null;
#endif
    }

    private static void ApplySpacingFromExistingSpawnedBricks(Transform orangePlaexLongBrickTransform)
    {
        if (orangePlaexLongBrickTransform == null)
        {
            return;
        }

        string[] anchorBrickNames =
        {
            "OrangeLegoBrick",
            "YellowLegoBrick",
            "GreenLegoBrick"
        };

        Bounds orangeBounds = GetWorldBounds(orangePlaexLongBrickTransform);
        float farthestRightX = float.NegativeInfinity;
        bool foundAnyAnchor = false;

        for (int i = 0; i < anchorBrickNames.Length; i++)
        {
            GameObject anchor = GameObject.Find(anchorBrickNames[i]);
            if (anchor == null)
            {
                continue;
            }

            Bounds anchorBounds = GetWorldBounds(anchor.transform);
            if (anchorBounds.max.x > farthestRightX)
            {
                farthestRightX = anchorBounds.max.x;
            }

            foundAnyAnchor = true;
        }

        if (!foundAnyAnchor)
        {
            return;
        }

        float targetCenterX = farthestRightX + orangeBounds.extents.x + OrangePlaexLongBrickSpacingPadding;

        Vector3 position = orangePlaexLongBrickTransform.position;
        position.x = targetCenterX;
        orangePlaexLongBrickTransform.position = position;
    }
}
