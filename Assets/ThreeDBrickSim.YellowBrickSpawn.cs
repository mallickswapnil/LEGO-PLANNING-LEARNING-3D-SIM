using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ThreeDBrickSim
{
    private const string YellowLegoBrickAssetPath = "Assets/yellowPLAEXSide.fbx";
    private const float YellowBrickSpacingPadding = 0.75f;

    private void SpawnYellowLegoBrick()
    {
        GameObject prefabToSpawn = ResolveYellowLegoBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnYellowLegoBrick: Could not load prefab at '{YellowLegoBrickAssetPath}'.");
            return;
        }

        GameObject spawnedBrick = Instantiate(prefabToSpawn, yellowLegoBrickPosition, Quaternion.identity);
        spawnedBrick.name = "YellowLegoBrick";
        ApplySpacingFromOrangeBrick(spawnedBrick.transform);
        EnsureBrickRigidbody(spawnedBrick);
    }

    private GameObject ResolveYellowLegoBrickPrefab()
    {
        if (yellowLegoBrickPrefab != null)
        {
            return yellowLegoBrickPrefab;
        }

#if UNITY_EDITOR
        yellowLegoBrickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(YellowLegoBrickAssetPath);
        return yellowLegoBrickPrefab;
#else
        return null;
#endif
    }

    private static void ApplySpacingFromOrangeBrick(Transform yellowBrickTransform)
    {
        if (yellowBrickTransform == null)
        {
            return;
        }

        GameObject orangeBrickObject = GameObject.Find("OrangeLegoBrick");
        if (orangeBrickObject == null)
        {
            return;
        }

        Bounds orangeBounds = GetWorldBounds(orangeBrickObject.transform);
        Bounds yellowBounds = GetWorldBounds(yellowBrickTransform);

        float requiredDistanceX = orangeBounds.extents.x + yellowBounds.extents.x + YellowBrickSpacingPadding;
        float currentDistanceX = Mathf.Abs(yellowBounds.center.x - orangeBounds.center.x);
        if (currentDistanceX >= requiredDistanceX)
        {
            return;
        }

        float direction = Mathf.Sign(yellowBounds.center.x - orangeBounds.center.x);
        if (Mathf.Approximately(direction, 0f))
        {
            direction = 1f;
        }

        Vector3 position = yellowBrickTransform.position;
        position.x = orangeBounds.center.x + (requiredDistanceX * direction);
        yellowBrickTransform.position = position;
    }

    private static Bounds GetWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds;
        }

        return new Bounds(root.position, Vector3.zero);
    }
}
