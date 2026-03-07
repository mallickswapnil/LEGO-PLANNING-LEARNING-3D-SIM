using UnityEngine;

public partial class ThreeDBrickSim
{
    private void SpawnOrangePlaexLongBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject orangePlaexPrefab = ResolveOrangePlaexLongBrickPrefab();
        if (orangePlaexPrefab == null)
        {
            Debug.LogWarning($"SpawnOrangePlaexLongBrickInventory: Could not load prefab at '{OrangePlaexLongBrickAssetPath}'.");
            return;
        }

        GameObject orangeLegoPrefab = ResolveOrangeLegoBrickPrefab();
        GameObject greenPlaexPrefab = ResolveGreenLegoBrickPrefab();

        Vector3 orangeLegoSize = ResolveInventoryBrickSize(orangeLegoPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 greenPlaexSize = ResolveInventoryBrickSize(greenPlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        Vector3 orangePlaexSize = ResolveInventoryBrickSize(orangePlaexPrefab, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));

        GameObject inventoryRoot = new GameObject("Inventory_OrangePlaexLongBricks");

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

        float orangePlaexStartX = greenPlaexRightEdge + inventorySeparationGap + (orangePlaexSize.x * 0.5f);
        float orangePlaexStartZ = normalStart.z;
        float baseY = boundsCenter.y + (orangePlaexSize.y * 0.5f);
        float stepX = orangePlaexSize.x + inventoryGapX;
        float stepZ = orangePlaexSize.z + inventoryGapZ;
        float stepY = orangePlaexSize.y + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = orangePlaexStartX + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = orangePlaexStartZ - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;

                    GameObject spawnedBrick = Instantiate(orangePlaexPrefab, brickPosition, Quaternion.identity, inventoryRoot.transform);
                    spawnedBrick.name = $"InventoryOrangePlaexLongBrick_{brickIndex}";
                    EnsureBrickRigidbody(spawnedBrick);
                }
            }
        }
    }
}
