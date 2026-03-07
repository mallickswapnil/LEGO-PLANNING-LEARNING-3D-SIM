using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ThreeDBrickSim
{
    private const string GreenLegoBrickAssetPath = "Assets/greenPLAEXLong.fbx";
    private const float GreenBrickSpacingPadding = 0.75f;

    private void SpawnGreenLegoBrick()
    {
        GameObject prefabToSpawn = ResolveGreenLegoBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnGreenLegoBrick: Could not load prefab at '{GreenLegoBrickAssetPath}'.");
            return;
        }

        GameObject spawnedBrick = Instantiate(prefabToSpawn, greenLegoBrickPosition, Quaternion.identity);
        spawnedBrick.name = "GreenLegoBrick";
        ApplySpacingFromYellowBrick(spawnedBrick.transform);
        EnsureBrickRigidbody(spawnedBrick);
    }

    private GameObject ResolveGreenLegoBrickPrefab()
    {
        if (greenLegoBrickPrefab != null)
        {
            return greenLegoBrickPrefab;
        }

#if UNITY_EDITOR
        greenLegoBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GreenLegoBrickAssetPath);
        return greenLegoBrickPrefab;
#else
        return null;
#endif
    }

    private static void ApplySpacingFromYellowBrick(Transform greenBrickTransform)
    {
        if (greenBrickTransform == null)
        {
            return;
        }

        GameObject yellowBrickObject = GameObject.Find("YellowLegoBrick");
        if (yellowBrickObject == null)
        {
            return;
        }

        Bounds yellowBounds = GetWorldBounds(yellowBrickObject.transform);
        Bounds greenBounds = GetWorldBounds(greenBrickTransform);

        float requiredDistanceX = yellowBounds.extents.x + greenBounds.extents.x + GreenBrickSpacingPadding;
        float currentDistanceX = Mathf.Abs(greenBounds.center.x - yellowBounds.center.x);
        if (currentDistanceX >= requiredDistanceX)
        {
            return;
        }

        float direction = Mathf.Sign(greenBounds.center.x - yellowBounds.center.x);
        if (Mathf.Approximately(direction, 0f))
        {
            direction = 1f;
        }

        Vector3 position = greenBrickTransform.position;
        position.x = yellowBounds.center.x + (requiredDistanceX * direction);
        greenBrickTransform.position = position;
    }
}
