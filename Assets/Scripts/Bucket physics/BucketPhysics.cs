using UnityEngine;

/// <summary>
/// Simple custom 3D pendulum physics for a bucket on a stiff rope.
///
/// No Unity Rigidbody, Collider, Joint, SpringJoint, or ConfigurableJoint is used.
/// The bucket motion is calculated directly from a small set of physics values:
/// gravity, rope length, velocity, damping, and mass.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bucket Paint/Bucket Physics")]
public sealed class BucketPhysics : MonoBehaviour
{
    [Header("Rope")]
    [Tooltip("Horizontal position of the rope anchor. X is world X, Y is world Z.")]
    public Vector2 anchorXZ = Vector2.zero;

    [Tooltip("Height of the rope anchor above the ground.")]
    public float anchorHeight = 6f;

    [Tooltip("Fixed rope length. The rope is stiff in this version, so it does not stretch.")]
    public float ropeLength = 2.25f;

    [Header("Start Motion")]
    [Tooltip("How far the bucket starts away from vertical, in degrees.")]
    [Range(0f, 80f)]
    public float startAngle = 22f;

    [Tooltip("Horizontal direction of the starting pull, in degrees. 0 = +X, 90 = +Z.")]
    public float startDirection = 0f;

    [Tooltip("Sideways starting speed. 0 gives almost one-plane motion. Higher values create 3D oval motion.")]
    public float startSpin = 0.8f;

    [Tooltip("Simple air/friction damping. Higher values make the bucket slow down faster.")]
    [Range(0f, 2f)]
    public float damping = 0.25f;

    [Tooltip("When true, the bucket returns to the starting state every time Play Mode starts.")]
    public bool resetOnPlay = true;

    private const float Gravity = 9.81f;
    private const int InternalSteps = 8;
    private const float MaxAngleFromDown = 86f;
    private const float MaxSpeed = 16f;

    // Direction from the anchor down to the bucket attach point.
    private Vector3 ropeDir = Vector3.down;

    // Sideways velocity of the bucket attach point. This is always kept perpendicular to ropeDir.
    private Vector3 tangentVelocity = Vector3.zero;

    // Cached output values read by BucketView.
    private Vector3 endPosition;
    private Vector3 endVelocity;
    private Vector3 endAcceleration;
    private Vector3 previousEndVelocity;

    private BucketMass mass;
    private bool initialized;

    /// <summary>
    /// World position of the fixed rope anchor.
    /// </summary>
    public Vector3 AnchorPosition
    {
        get { return new Vector3(anchorXZ.x, anchorHeight, anchorXZ.y); }
    }

    /// <summary>
    /// Direction from the anchor to the bucket attach point.
    /// </summary>
    public Vector3 RopeDirection
    {
        get { return ropeDir.normalized; }
    }

    /// <summary>
    /// World position of the bucket attach point at the end of the rope.
    /// </summary>
    public Vector3 EndPosition
    {
        get { return endPosition; }
    }

    /// <summary>
    /// World velocity of the bucket attach point.
    /// </summary>
    public Vector3 EndVelocity
    {
        get { return endVelocity; }
    }

    /// <summary>
    /// Approximate world acceleration of the bucket attach point.
    /// Currently useful for debugging or future paint behavior.
    /// </summary>
    public Vector3 EndAcceleration
    {
        get { return endAcceleration; }
    }

    /// <summary>
    /// Unity calls this when the component is enabled or when scripts reload.
    /// It is useful here because the simulation needs a valid starting state before Update/FixedUpdate runs.
    /// </summary>
    private void OnEnable()
    {
        mass = GetComponent<BucketMass>();
        ResetSimulation();
    }

    /// <summary>
    /// Unity calls this once when Play Mode begins.
    /// </summary>
    private void Start()
    {
        if (Application.isPlaying && resetOnPlay)
            ResetSimulation();
    }

    /// <summary>
    /// Unity calls this on the fixed timestep.
    /// The custom physics is updated here so the simulation is not tied to rendering frame rate.
    /// </summary>
    private void FixedUpdate()
    {
        if (!Application.isPlaying) return;

        EnsureInitialized();

        if (mass == null) mass = GetComponent<BucketMass>();
        if (mass != null) mass.DrainPaint(Time.fixedDeltaTime);

        Simulate(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Unity calls this when Inspector values are edited.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();

        if (!Application.isPlaying)
            initialized = false;
    }

    /// <summary>
    /// Resets the simulation to the starting angle and starting spin.
    /// </summary>
    public void ResetSimulation()
    {
        ValidateValues();

        mass = GetComponent<BucketMass>();

        Vector3 pull = StartPullDirection();
        float angleRadians = startAngle * Mathf.Deg2Rad;

        // Combine a downward direction with a horizontal pull direction.
        // startAngle = 0 means straight down.
        // Larger startAngle tilts the rope away from vertical.
        ropeDir = (Vector3.down * Mathf.Cos(angleRadians) + pull * Mathf.Sin(angleRadians)).normalized;
        ropeDir = LimitRopeDirection(ropeDir);

        // startSpin is a sideways velocity perpendicular to the pull direction.
        // This creates a 3D oval/spherical-pendulum path instead of pure one-plane swinging.
        Vector3 spinDirection = Vector3.Cross(pull, Vector3.up);
        if (spinDirection.sqrMagnitude < 0.0001f)
            spinDirection = Vector3.forward;

        spinDirection = Vector3.ProjectOnPlane(spinDirection.normalized, ropeDir);
        if (spinDirection.sqrMagnitude < 0.0001f)
            spinDirection = Vector3.ProjectOnPlane(Vector3.forward, ropeDir);

        tangentVelocity = spinDirection.normalized * startSpin;
        previousEndVelocity = Vector3.zero;
        UpdateCachedOutput();
        endAcceleration = Vector3.zero;
        initialized = true;
    }

    /// <summary>
    /// Used by BucketView in Edit Mode so the preview follows the current Inspector values.
    /// </summary>
    public void SyncPreview()
    {
        if (Application.isPlaying) return;
        ResetSimulation();
    }

    private void Simulate(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        previousEndVelocity = endVelocity;

        // The timestep is split into smaller internal steps.
        // This reduces numerical error and makes the pendulum more stable.
        float stepTime = deltaTime / InternalSteps;
        for (int i = 0; i < InternalSteps; i++)
            StepPendulum(stepTime);

        UpdateCachedOutput();
        endAcceleration = (endVelocity - previousEndVelocity) / deltaTime;
    }

    private void StepPendulum(float deltaTime)
    {
        float massKg = GetTotalMass();
        float safeLength = Mathf.Max(0.05f, ropeLength);

        // Gravity points downward. Only the part perpendicular to the rope changes the swing direction.
        Vector3 gravityAcceleration = Vector3.down * Gravity;
        Vector3 tangentAcceleration = Vector3.ProjectOnPlane(gravityAcceleration, ropeDir);

        // Damping is modeled as a force opposite the current tangential velocity.
        // Since acceleration = force / mass, heavier buckets slow down less from the same damping value.
        float dragCoefficient = damping * 3f;
        tangentAcceleration += (-dragCoefficient * tangentVelocity) / massKg;

        // Basic integration: velocity += acceleration * time.
        tangentVelocity += tangentAcceleration * deltaTime;
        tangentVelocity = Vector3.ProjectOnPlane(tangentVelocity, ropeDir);
        tangentVelocity = Vector3.ClampMagnitude(tangentVelocity, MaxSpeed);

        // Change the rope direction according to sideways velocity.
        // For a fixed rope length, direction change is roughly velocity / length.
        Vector3 nextDir = ropeDir + (tangentVelocity / safeLength) * deltaTime;
        if (nextDir.sqrMagnitude < 0.0001f)
            nextDir = Vector3.down;

        ropeDir = LimitRopeDirection(nextDir.normalized);

        // After changing the rope direction, keep velocity tangent to the new direction.
        tangentVelocity = Vector3.ProjectOnPlane(tangentVelocity, ropeDir);
    }

    private void UpdateCachedOutput()
    {
        ropeDir = ropeDir.normalized;
        endPosition = AnchorPosition + ropeDir * ropeLength;
        endVelocity = tangentVelocity;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
            ResetSimulation();
    }

    private float GetTotalMass()
    {
        if (mass == null) mass = GetComponent<BucketMass>();
        return mass != null ? mass.TotalMass : 2f;
    }

    private Vector3 StartPullDirection()
    {
        Quaternion yaw = Quaternion.Euler(0f, startDirection, 0f);
        return (yaw * Vector3.right).normalized;
    }

    private Vector3 LimitRopeDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.down;

        direction.Normalize();

        float minimumDownAmount = Mathf.Cos(MaxAngleFromDown * Mathf.Deg2Rad);
        float currentDownAmount = Vector3.Dot(direction, Vector3.down);

        if (currentDownAmount >= minimumDownAmount)
            return direction;

        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.down);
        if (horizontal.sqrMagnitude < 0.0001f)
            horizontal = Vector3.right;

        horizontal.Normalize();
        float horizontalAmount = Mathf.Sqrt(Mathf.Max(0f, 1f - minimumDownAmount * minimumDownAmount));

        return (Vector3.down * minimumDownAmount + horizontal * horizontalAmount).normalized;
    }

    private void ValidateValues()
    {
        anchorHeight = Mathf.Max(0.2f, anchorHeight);
        ropeLength = Mathf.Max(0.2f, ropeLength);
        startAngle = Mathf.Clamp(startAngle, 0f, 80f);
        startSpin = Mathf.Max(0f, startSpin);
        damping = Mathf.Clamp(damping, 0f, 2f);
    }
}
