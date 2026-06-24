using UnityEngine;
using PaintSimulation;

[RequireComponent(typeof(BucketView))]
public class BucketPaintAdapter : MonoBehaviour, IBucketPaintSource
{
    [Header("Paint Drop Settings")]
    public float dropMass = 0.05f; // Mass removed per drop
    public Color paintColor = Color.green;

    private BucketView _view;
    private BucketMass _mass;

    private void Awake()
    {
        _view = GetComponent<BucketView>();
        _mass = GetComponent<BucketMass>();
    }

    public bool CanReleasePaint()
    {
        // Check if there is paint left to drop
        if (_mass == null || _mass.paintMass <= 0f) return false;
        return true;
    }

    public PaintDropSpawnData GetPaintDropData()
    {
        return new PaintDropSpawnData
        {
            SpawnPosition = _view.GetHoleWorldPosition(),
            SpawnVelocity = _view.GetHoleWorldVelocity(),
            DropMass = dropMass,
            PaintColor = paintColor
        };
    }

    public void NotifyPaintReleased(float massReleased)
    {
        // Tell the bucket to reduce its paint mass
        if (_mass != null)
        {
            _mass.RemovePaint(massReleased);
        }
    }
}