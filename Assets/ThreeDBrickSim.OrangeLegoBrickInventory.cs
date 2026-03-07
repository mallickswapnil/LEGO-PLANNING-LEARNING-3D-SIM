using UnityEngine;

public partial class ThreeDBrickSim
{
    private void SpawnOrangeLegoBrickInventory()
    {
        if (inventoryRows <= 0 || inventoryColumns <= 0 || inventoryLayers <= 0)
        {
            return;
        }

        GameObject prefabToSpawn = ResolveOrangeLegoBrickPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"SpawnOrangeLegoBrickInventory: Could not load prefab at '{OrangeLegoBrickAssetPath}'.");
            return;
        }

        Vector3 brickSize = ResolveInventoryBrickSize(prefabToSpawn, new Vector3(LegoBrickLength, LegoBrickHeight, LegoBrickWidth));
        GameObject inventoryRoot = new GameObject("Inventory_OrangeLegoBricks");

        Vector3 normalStart = GetNormalInventoryStartPosition();
        float normalStepX = NormalBrickLength + inventoryGapX;
        float normalRightEdge = normalStart.x + ((inventoryColumns - 1) * normalStepX) + (NormalBrickLength * 0.5f);

        float legoStepX = LegoBrickLength + inventoryGapX;
        float legoStartX = normalRightEdge + inventorySeparationGap + (LegoBrickLength * 0.5f);
        float legoRightEdge = legoStartX + ((inventoryColumns - 1) * legoStepX) + (LegoBrickLength * 0.5f);

        float orangeStartX = legoRightEdge + inventorySeparationGap + (brickSize.x * 0.5f);
        float orangeStartZ = normalStart.z;
        float baseY = boundsCenter.y + (brickSize.y * 0.5f);
        float stepX = brickSize.x + inventoryGapX;
        float stepZ = brickSize.z + inventoryGapZ;
        float stepY = brickSize.y + inventoryGapY;

        for (int layer = 0; layer < inventoryLayers; layer++)
        {
            for (int row = 0; row < inventoryRows; row++)
            {
                for (int col = 0; col < inventoryColumns; col++)
                {
                    float x = orangeStartX + (col * stepX);
                    float y = baseY + (layer * stepY);
                    float z = orangeStartZ - (row * stepZ);
                    Vector3 brickPosition = new Vector3(x, y, z);
                    int brickIndex = (layer * inventoryRows * inventoryColumns) + (row * inventoryColumns) + col + 1;

                    GameObject spawnedBrick = Instantiate(prefabToSpawn, brickPosition, Quaternion.identity, inventoryRoot.transform);
                    spawnedBrick.name = $"InventoryOrangeLegoBrick_{brickIndex}";
                    EnsureBrickRigidbody(spawnedBrick);
                }
            }
        }
    }

}
