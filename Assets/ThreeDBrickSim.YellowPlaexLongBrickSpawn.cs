using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ThreeDBrickSim
{
    private const string YellowPlaexLongBrickAssetPath = "Assets/yellowPLAEXLong.fbx";
    private const float YellowPlaexLongBrickSpacingPadding = 0.75f;

    private void SpawnYellowPlaexLongBrick()
    {
        GameObject prefabToSpawn = ResolveYellowPlaexLongBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnYellowPlaexLongBrick: Could not load prefab at '{YellowPlaexLongBrickAssetPath}'.");
            return;
        }

        GameObject spawnedBrick = Instantiate(prefabToSpawn, yellowPlaexLongBrickPosition, Quaternion.identity);
        spawnedBrick.name = "YellowPlaexLongBrick";
        ApplySpacingFromAllSpawnedBricks(spawnedBrick.transform);
        EnsureBrickRigidbody(spawnedBrick);
    }

    private GameObject ResolveYellowPlaexLongBrickPrefab()
    {
        if (yellowPlaexLongBrickPrefab != null)
        {
            return yellowPlaexLongBrickPrefab;
        }

#if UNITY_EDITOR
        yellowPlaexLongBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(YellowPlaexLongBrickAssetPath);
        return yellowPlaexLongBrickPrefab;
#else
        return null;
#endif
    }

    private static void ApplySpacingFromAllSpawnedBricks(Transform yellowPlaexLongBrickTransform)
    {
        if (yellowPlaexLongBrickTransform == null)
        {
            return;
        }

        string[] anchorBrickNames =
        {
            "OrangeLegoBrick",
            "YellowLegoBrick",
            "GreenLegoBrick",
            "OrangePlaexLongBrick"
        };

        Bounds yellowBounds = GetWorldBounds(yellowPlaexLongBrickTransform);
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

        float targetCenterX = farthestRightX + yellowBounds.extents.x + YellowPlaexLongBrickSpacingPadding;
        Vector3 position = yellowPlaexLongBrickTransform.position;
        position.x = targetCenterX;
        yellowPlaexLongBrickTransform.position = position;
    }
}
