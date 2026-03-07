using UnityEngine;

public partial class ThreeDBrickSim
{
    private void SpawnYellowPlaexSideBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject yellowSidePrefab = ResolveYellowLegoBrickPrefab();
        if (yellowSidePrefab == null)
        {
            Debug.LogWarning($"SpawnYellowPlaexSideBrickInventory: Could not load prefab at '{YellowLegoBrickAssetPath}'.");
            return;
        }

        GameObject orangeLegoPrefab = ResolveOrangeLegoBrickPrefab();
        GameObject greenPlaexPrefab = ResolveGreenLegoBrickPrefab();
        GameObject orangePlaexPrefab = ResolveOrangePlaexLongBrickPrefab();
        GameObject yellowPlaexPrefab = ResolveYellowPlaexLongBrickPrefab();

        Vector3 orangeLegoSize = ResolveInventoryBrickSize(orangeLegoPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 greenPlaexSize = ResolveInventoryBrickSize(greenPlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 orangePlaexSize = ResolveInventoryBrickSize(orangePlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 yellowPlaexSize = ResolveInventoryBrickSize(yellowPlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 yellowSideSize = ResolveInventoryBrickSize(yellowSidePrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));

        GameObject inventoryRoot = new GameObject("Inventory_YellowPlaexSideBricks");

        Vector3 normalStart = GetNormalInventoryStartPosition();
        float normalStepX = NormalBrickLength + inventoryGapX;
        float normalRightEdge = normalStart.x + ((inventoryColumns - 1) * normalStepX) + (NormalBrickLength * 0.5f);

        float legoStepX = LegoBrickLength + inventoryGapX;
        float legoStartX = normalRightEdge + inventorySeparationGap + (LegoBrickLength * 0.5f);
        float legoRightEdge = legoStartX + ((inventoryColumns - 1) * legoStepX) + (LegoBrickLength * 0.5f);

        float orangeLegoStepX = orangeLegoSize.x + inventoryGapX;
        float orangeLegoStartX = legoRightEdge + inventorySeparationGap + (orangeLegoSize.x * 0.5f);
        float orangeLegoRightEdge = orangeLegoStartX + ((inventoryColumns - 1) * orangeLegoStepX) + (orangeLegoSize.x * 0.5f);

        float greenPlaexStepX = greenPlaexSize.x + inventoryGapX;
        float greenPlaexStartX = orangeLegoRightEdge + inventorySeparationGap + (greenPlaexSize.x * 0.5f);
        float greenPlaexRightEdge = greenPlaexStartX + ((inventoryColumns - 1) * greenPlaexStepX) + (greenPlaexSize.x * 0.5f);

        float orangePlaexStepX = orangePlaexSize.x + inventoryGapX;
        float orangePlaexStartX = greenPlaexRightEdge + inventorySeparationGap + (orangePlaexSize.x * 0.5f);
        float orangePlaexRightEdge = orangePlaexStartX + ((inventoryColumns - 1) * orangePlaexStepX) + (orangePlaexSize.x * 0.5f);

        float yellowPlaexStepX = yellowPlaexSize.x + inventoryGapX;
        float yellowPlaexStartX = orangePlaexRightEdge + inventorySeparationGap + (yellowPlaexSize.x * 0.5f);
        float yellowPlaexRightEdge = yellowPlaexStartX + ((inventoryColumns - 1) * yellowPlaexStepX) + (yellowPlaexSize.x * 0.5f);

        float yellowSideStartX = yellowPlaexRightEdge + inventorySeparationGap + (yellowSideSize.x * 0.5f);
        float yellowSideStartZ = normalStart.z;
        float baseY = boundsCenter.y + (yellowSideSize.y * 0.5f);
        float stepX = yellowSideSize.x + inventoryGapX;
        float stepZ = yellowSideSize.z + inventoryGapZ;
        float stepY = yellowSideSize.y + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = yellowSideStartX + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = yellowSideStartZ - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;

                    GameObject spawnedBrick = Instantiate(yellowSidePrefab, brickPosition, Quaternion.identity, inventoryRoot.transform);
                    spawnedBrick.name = $"InventoryYellowPlaexSideBrick_{brickIndex}";
                    EnsureBrickRigidbody(spawnedBrick);
                }
            }
        }
    }
}
