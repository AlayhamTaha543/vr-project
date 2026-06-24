using UnityEngine;
using PaintSimulation;

public class DropController : MonoBehaviour
{
    private bool warnedEmpty = false;
    [Header("Auto Drop Settings")]
    public bool autoDropOnMovement = true;
    public float speedThreshold = 0.5f;
    public float dropInterval = 0.5f;
    private float lastDropTime = -999f;

    [Header("References")]
    public BucketPhysics bucketPhysics; // Kept for speed check
    public IBucketPaintSource bucketSource; // The interface
    public SurfaceProperties currentSurface;

    [Header("Prefabs")]
    public GameObject dropPrefab;
    public GameObject splatPrefab;

    [Header("Spawn Settings")]
    public KeyCode dropKey = KeyCode.Space;
    public float randomSpread = 0.2f;
    public float tiltAngle = 0f;

    [Header("Wind Settings")]
    public float windSpeed = 2f;
    public float windAcceleration = 5f;
    public bool showWindDebug = true;

    private void Start()
    {
        // If you forgot to assign it in the Inspector, find it automatically
        if (bucketSource == null)
        {
            bucketSource = FindObjectOfType<BucketPaintAdapter>() as IBucketPaintSource;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(dropKey))
        {
            DropPaint();
        }

        if (autoDropOnMovement && bucketPhysics != null)
        {
            float currentSpeed = bucketPhysics.EndVelocity.magnitude;
            float dynamicInterval = Mathf.Lerp(1.0f, 0.1f, currentSpeed / 3f);
            if (currentSpeed > speedThreshold && Time.time > lastDropTime + dropInterval)
            {
                DropPaint();
                lastDropTime = Time.time;
            }
        }

        // Wind Logic (unchanged)
        Vector3 windDir = Vector3.zero;
        if (Input.GetKey(KeyCode.I)) windDir += Vector3.forward;
        if (Input.GetKey(KeyCode.K)) windDir += Vector3.back;
        if (Input.GetKey(KeyCode.J)) windDir += Vector3.left;
        if (Input.GetKey(KeyCode.L)) windDir += Vector3.right;
        if (windDir.sqrMagnitude > 0f)
        {
            windDir.Normalize();
            PaintDrop.WindForce = windDir * windSpeed;
            if (showWindDebug && Input.anyKeyDown)
            {
                Debug.Log($"Wind: {windDir} at {windSpeed} m/s²");
            }
        }
    }

    private void DropPaint()
    
    {
         Debug.Log("🟢 DropPaint() called!");

    // Check if bucketSource is assigned or found
    if (bucketSource == null)
    {
        Debug.LogError("❌ bucketSource is NULL! Did you add BucketPaintAdapter component to the scene?");
        return;
    }

    // Check if bucket source can release paint
    if (!bucketSource.CanReleasePaint())
    {
        // Only warn once so the console doesn't get spammed
        if (!warnedEmpty)
        {
            Debug.Log("⛔ Bucket is empty. Refill paint mass (in Inspector) to continue dropping.");
            warnedEmpty = true;
        }
        return;
    }

    warnedEmpty = false;

    // Check prefabs
    if (dropPrefab == null)
    {
        Debug.LogError("❌ dropPrefab is NULL! Drag DropPrefab into the Inspector.");
        return;
    }

    if (splatPrefab == null)
    {
        Debug.LogError("❌ splatPrefab is NULL! Drag SplatPrefab into the Inspector.");
        return;
    }

    Debug.Log("✅ All checks passed! Spawning paint...");


        // 1. Get standardized data from the interface
        PaintDropSpawnData dropData = bucketSource.GetPaintDropData();

        // 2. Apply random spread
        Vector3 randomOffset = Random.insideUnitSphere * randomSpread;
        dropData.SpawnVelocity += randomOffset;

        // 3. Notify the bucket to reduce mass
        bucketSource.NotifyPaintReleased(dropData.DropMass);

        // 4. Spawn the drop
        GameObject dropObj = Instantiate(dropPrefab, dropData.SpawnPosition, Quaternion.identity);
        Renderer dropRenderer = dropObj.GetComponent<Renderer>();
        if (dropRenderer != null)
        {
            dropRenderer.material.color = dropData.PaintColor;
        }
        PaintDrop drop = dropObj.GetComponent<PaintDrop>();
        if (drop == null) { Destroy(dropObj); return; }

        drop.Launch(dropData.SpawnPosition, dropData.SpawnVelocity);

        // 5. Handle impact event
        drop.OnImpact += (hitPos, speed, angle, energy, splatter) =>
        {
            GameObject splatObj = Instantiate(splatPrefab, Vector3.zero, Quaternion.identity);
            PaintSplat splat = splatObj.GetComponent<PaintSplat>();
            if (splat != null)
            {
                Vector3 normal = Vector3.up;
                Quaternion tiltRot = Quaternion.Euler(tiltAngle, 0f, 0f);
                normal = tiltRot * normal;

                splat.Initialize(hitPos, speed, energy, splatter, currentSurface, normal, tiltAngle,dropData.PaintColor);
            }
            Destroy(dropObj, 0.5f);
        };
    }
}