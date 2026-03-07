using UnityEngine;

public partial class ThreeDBrickSim
{
    private void SpawnGreenPlaexLongBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject greenPrefab = ResolveGreenLegoBrickPrefab();
        if (greenPrefab == null)
        {
            Debug.LogWarning($"SpawnGreenPlaexLongBrickInventory: Could not load prefab at '{GreenLegoBrickAssetPath}'.");
            return;
        }

        GameObject orangePrefab = ResolveOrangeLegoBrickPrefab();
        Vector3 greenBrickSize = ResolveInventoryBrickSize(greenPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 orangeBrickSize = ResolveInventoryBrickSize(orangePrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));

        GameObject inventoryRoot = new GameObject("Inventory_GreenPlaexLongBricks");

        Vector3 normalStart = GetNormalInventoryStartPosition();
        float normalStepX = NormalBrickLength + inventoryGapX;
        float normalRightEdge = normalStart.x + ((inventoryColumns - 1) * normalStepX) + (NormalBrickLength * 0.5f);

        float legoStepX = LegoBrickLength + inventoryGapX;
        float legoStartX = normalRightEdge + inventorySeparationGap + (LegoBrickLength * 0.5f);
        float legoRightEdge = legoStartX + ((inventoryColumns - 1) * legoStepX) + (LegoBrickLength * 0.5f);

        float orangeStepX = orangeBrickSize.x + inventoryGapX;
        float orangeStartX = legoRightEdge + inventorySeparationGap + (orangeBrickSize.x * 0.5f);
        float orangeRightEdge = orangeStartX + ((inventoryColumns - 1) * orangeStepX) + (orangeBrickSize.x * 0.5f);

        float greenStartX = orangeRightEdge + inventorySeparationGap + (greenBrickSize.x * 0.5f);
        float greenStartZ = normalStart.z;
        float baseY = boundsCenter.y + (greenBrickSize.y * 0.5f);
        float stepX = greenBrickSize.x + inventoryGapX;
        float stepZ = greenBrickSize.z + inventoryGapZ;
        float stepY = greenBrickSize.y + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = greenStartX + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = greenStartZ - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;

                    GameObject spawnedBrick = Instantiate(greenPrefab, brickPosition, Quaternion.identity, inventoryRoot.transform);
                    spawnedBrick.name = $"InventoryGreenPlaexLongBrick_{brickIndex}";
                    EnsureBrickRigidbody(spawnedBrick);
                }
            }
        }
    }

    private void EnsureBoundsCanFitInventories()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0)
        {
            return;
        }

        GameObject orangePrefab = ResolveOrangeLegoBrickPrefab();
        GameObject greenPrefab = ResolveGreenLegoBrickPrefab();
        GameObject orangePlaexPrefab = ResolveOrangePlaexLongBrickPrefab();
        GameObject yellowPlaexPrefab = ResolveYellowPlaexLongBrickPrefab();
        GameObject yellowSidePrefab = ResolveYellowLegoBrickPrefab();

        Vector3 orangeSize = ResolveInventoryBrickSize(orangePrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 greenSize = ResolveInventoryBrickSize(greenPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 orangePlaexSize = ResolveInventoryBrickSize(orangePlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 yellowPlaexSize = ResolveInventoryBrickSize(yellowPlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 yellowSideSize = ResolveInventoryBrickSize(yellowSidePrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));

        float normalWidthX = (inventoryColumns * NormalBrickLength) + ((inventoryColumns - 1) * inventoryGapX);
        float legoWidthX = (inventoryColumns * LegoBrickLength) + ((inventoryColumns - 1) * inventoryGapX);
        float orangeWidthX = (inventoryColumns * orangeSize.x) + ((inventoryColumns - 1) * inventoryGapX);
        float greenWidthX = (inventoryColumns * greenSize.x) + ((inventoryColumns - 1) * inventoryGapX);
        float orangePlaexWidthX = (inventoryColumns * orangePlaexSize.x) + ((inventoryColumns - 1) * inventoryGapX);
        float yellowPlaexWidthX = (inventoryColumns * yellowPlaexSize.x) + ((inventoryColumns - 1) * inventoryGapX);
        float yellowSideWidthX = (inventoryColumns * yellowSideSize.x) + ((inventoryColumns - 1) * inventoryGapX);

        float requiredLength =
            normalWidthX +
            legoWidthX +
            orangeWidthX +
            greenWidthX +
            orangePlaexWidthX +
            yellowPlaexWidthX +
            yellowSideWidthX +
            (6f * inventorySeparationGap) +
            (2f * wallThickness) +
            (2f * inventoryCornerInset);

        float normalDepthZ = (inventoryRows * NormalBrickWidth) + ((inventoryRows - 1) * inventoryGapZ);
        float legoDepthZ = (inventoryRows * LegoBrickWidth) + ((inventoryRows - 1) * inventoryGapZ);
        float orangeDepthZ = (inventoryRows * orangeSize.z) + ((inventoryRows - 1) * inventoryGapZ);
        float greenDepthZ = (inventoryRows * greenSize.z) + ((inventoryRows - 1) * inventoryGapZ);
        float orangePlaexDepthZ = (inventoryRows * orangePlaexSize.z) + ((inventoryRows - 1) * inventoryGapZ);
        float yellowPlaexDepthZ = (inventoryRows * yellowPlaexSize.z) + ((inventoryRows - 1) * inventoryGapZ);
        float yellowSideDepthZ = (inventoryRows * yellowSideSize.z) + ((inventoryRows - 1) * inventoryGapZ);

        float maxRequiredDepth = Mathf.Max(normalDepthZ, legoDepthZ, orangeDepthZ, greenDepthZ, orangePlaexDepthZ, yellowPlaexDepthZ, yellowSideDepthZ);
        float requiredWidth = maxRequiredDepth + (2f * wallThickness) + (2f * inventoryCornerInset);

        if (boundsLength < requiredLength)
        {
            boundsLength = requiredLength + 1f;
        }

        if (boundsWidth < requiredWidth)
        {
            boundsWidth = requiredWidth + 1f;
        }
    }

    private Vector3 ResolveInventoryBrickSize(GameObject prefab, Vector3 fallback)
    {
        if (prefab == null)
        {
            return fallback;
        }

        GameObject preview = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        try
        {
            Bounds bounds = GetWorldBounds(preview.transform);
            Vector3 size = bounds.size;

            if (size.x <= 0f)
            {
                size.x = fallback.x;
            }

            if (size.y <= 0f)
            {
                size.y = fallback.y;
            }

            if (size.z <= 0f)
            {
                size.z = fallback.z;
            }

            return size;
        }
        finally
        {
            Destroy(preview);
        }
    }
}
