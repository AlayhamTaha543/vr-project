using UnityEngine;

/// <summary>
/// Stores the mass of the empty bucket and the optional future paint mass.
/// This script does not create visible paint. It only provides mass values for BucketPhysics.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bucket Paint/Bucket Mass")]
public sealed class BucketMass : MonoBehaviour
{
    [Header("Mass (kg)")]
    [Tooltip("Mass of the empty bucket, before any paint is added.")]
    public float emptyMass = 2f;

    [Tooltip("Future paint mass. Keep this at 0 while the bucket is empty.")]
    public float paintMass = 0f;

    [Tooltip("How many kilograms of paint are removed per second. Keep this at 0 until you add paint later.")]
    public float paintDrainRate = 0f;

    /// <summary>
    /// Total current mass used by the physics script.
    /// The tiny minimum prevents division by zero in physics formulas.
    /// </summary>
    public float TotalMass
    {
        get { return Mathf.Max(0.01f, emptyMass + paintMass); }
    }

    /// <summary>
    /// Removes paint mass over time. This is called by BucketPhysics while the scene is playing.
    /// </summary>
    public void DrainPaint(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        if (paintDrainRate <= 0f) return;
        if (paintMass <= 0f) return;

        paintMass = Mathf.Max(0f, paintMass - paintDrainRate * deltaTime);
    }

    /// <summary>
    /// Future paint scripts can call this when paint is added to the bucket.
    /// </summary>
    public void AddPaint(float amount)
    {
        if (amount <= 0f) return;
        paintMass += amount;
        ValidateValues();
    }

    /// <summary>
    /// Future paint scripts can call this when paint leaves the bucket.
    /// </summary>
    public void RemovePaint(float amount)
    {
        if (amount <= 0f) return;
        paintMass = Mathf.Max(0f, paintMass - amount);
        ValidateValues();
    }

    /// <summary>
    /// Future paint scripts can call this when they need to directly set the current paint mass.
    /// </summary>
    public void SetPaint(float amount)
    {
        paintMass = Mathf.Max(0f, amount);
        ValidateValues();
    }

    /// <summary>
    /// Unity calls this when Inspector values change.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }

    /// <summary>
    /// Keeps mass values physically valid.
    /// </summary>
    private void ValidateValues()
    {
        emptyMass = Mathf.Max(0.01f, emptyMass);
        paintMass = Mathf.Max(0f, paintMass);
        paintDrainRate = Mathf.Max(0f, paintDrainRate);
    }
}
