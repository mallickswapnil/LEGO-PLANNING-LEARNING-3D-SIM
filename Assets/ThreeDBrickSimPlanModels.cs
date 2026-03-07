using UnityEngine;

[System.Serializable]
public class ThreeDBrickSimPlan
{
    public ThreeDBrickSimPlanStep[] steps;
}

[System.Serializable]
public class ThreeDBrickSimPlanStep
{
    public string brickId;
    public Vector3 targetPosition;
    public Vector3 targetRotation;
    public float delay;
}
