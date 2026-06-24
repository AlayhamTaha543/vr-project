using UnityEngine;

/// <summary>
/// Connector component that bridges BucketPhysics/View/Mass with fluid simulation systems.
/// Implements abstraction interfaces to decouple SPH and other systems from bucket internals.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bucket Paint/Bucket Fluid Connector")]
public class BucketFluidConnector : MonoBehaviour, IFluidContainer, IFluidMassSource, IContainerMotion
{
    [Header("Component References")]
    [Tooltip("Reference to the BucketPhysics component. Leave null to auto-find on this GameObject.")]
    [SerializeField] private BucketPhysics physics;

    [Tooltip("Reference to the BucketView component. Leave null to auto-find on this GameObject.")]
    [SerializeField] private BucketView view;

    [Tooltip("Reference to the BucketMass component. Leave null to auto-find on this GameObject.")]
    [SerializeField] private BucketMass mass;

    [Header("Settings")]
    [Tooltip("Automatically find components on this GameObject if not manually assigned.")]
    [SerializeField] private bool autoFindComponents = true;

    [Tooltip("Handle height above bucket top (matches SPH_Compute constant).")]
    [SerializeField] private float handleHeight = 0.28f;

    [Tooltip("Speed threshold for IsMoving property (m/s).")]
    [SerializeField] private float movementThreshold = 0.01f;

    [Header("Debug Visualization")]
    [Tooltip("Draw container bounds in Scene view.")]
    [SerializeField] private bool showContainerGizmo = true;

    [Tooltip("Draw velocity vector in Scene view.")]
    [SerializeField] private bool showVelocityGizmo = true;

    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 1f, 0.5f);

    // Cached values
    private Vector3 previousVelocity;
    private Vector3 cachedAcceleration;
    private float initialMass;
    private bool initialized;

    // Public accessors for components (useful for debugging/editor)
    public BucketPhysics Physics => physics;
    public BucketView View => view;
    public BucketMass Mass => mass;

    #region Unity Lifecycle

    private void Awake()
    {
        if (autoFindComponents)
        {
            FindComponents();
        }

        ValidateComponents();
    }

    private void Start()
    {
        if (mass != null)
        {
            initialMass = Mathf.Max(mass.paintMass, 0.001f);
        }
        else
        {
            initialMass = 1f;
            Debug.LogWarning($"[BucketFluidConnector] No BucketMass found on {gameObject.name}. Using default initial mass.", this);
        }

        previousVelocity = GetContainerVelocity();
        initialized = true;
    }

    private void FixedUpdate()
    {
        // Update acceleration calculation every physics frame
        Vector3 currentVel = GetContainerVelocity();
        cachedAcceleration = (currentVel - previousVelocity) / Mathf.Max(Time.fixedDeltaTime, 1e-6f);
        previousVelocity = currentVel;
    }

    #endregion

    #region Component Management

    /// <summary>
    /// Attempts to find required components on this GameObject.
    /// </summary>
    public void FindComponents()
    {
        if (physics == null) physics = GetComponent<BucketPhysics>();
        if (view == null) view = GetComponent<BucketView>();
        if (mass == null) mass = GetComponent<BucketMass>();
    }

    /// <summary>
    /// Validates that required components are present.
    /// </summary>
    private void ValidateComponents()
    {
        if (physics == null)
        {
            Debug.LogError($"[BucketFluidConnector] BucketPhysics component is missing on {gameObject.name}! Fluid simulation will not work correctly.", this);
        }

        if (view == null)
        {
            Debug.LogError($"[BucketFluidConnector] BucketView component is missing on {gameObject.name}! Container geometry will be invalid.", this);
        }

        if (mass == null)
        {
            Debug.LogWarning($"[BucketFluidConnector] BucketMass component is missing on {gameObject.name}. Mass tracking will not work.", this);
        }
    }

    /// <summary>
    /// Checks if all required components are present and valid.
    /// </summary>
    public bool IsValid()
    {
        return physics != null && view != null;
    }

    #endregion

    #region IFluidContainer Implementation

    public Vector3 GetContainerCenter()
    {
        if (physics == null || view == null)
        {
            Debug.LogWarning("[BucketFluidConnector] Cannot get container center: missing components.", this);
            return Vector3.zero;
        }

        // Calculate bottom-center of bucket in world space
        // From attach point, continue along rope direction by (handle height + bucket height)
        float dist = handleHeight + view.bucketHeight;
        return physics.EndPosition + physics.RopeDirection * dist;
    }

    public Vector3 GetContainerUp()
    {
        if (physics == null)
        {
            return Vector3.up;
        }

        // Bucket's local up axis = opposite of rope direction
        return -physics.RopeDirection;
    }

    public Vector3 GetContainerVelocity()
    {
        if (physics == null)
        {
            return Vector3.zero;
        }

        return physics.EndVelocity;
    }

    public Vector3 GetContainerAcceleration()
    {
        return cachedAcceleration;
    }

    public float GetTopRadius()
    {
        if (view == null)
        {
            return 0.5f;
        }

        return view.topWidth * 0.5f;
    }

    public float GetBottomRadius()
    {
        if (view == null)
        {
            return 0.33f;
        }

        return view.bottomWidth * 0.5f;
    }

    public float GetHeight()
    {
        if (view == null)
        {
            return 1f;
        }

        return view.bucketHeight;
    }

    public bool IsInsideContainer(Vector3 worldPosition)
    {
        if (!IsValid())
        {
            return false;
        }

        Vector3 center = GetContainerCenter();
        Vector3 up = GetContainerUp();
        float height = GetHeight();

        // Convert to container's local coordinates
        Vector3 localPos = worldPosition - center;
        float localY = Vector3.Dot(localPos, up);

        // Check if within height bounds
        if (localY < 0f || localY > height)
        {
            return false;
        }

        // Get radius at this height (lerp between bottom and top)
        float t = localY / height;
        float radiusAtHeight = Mathf.Lerp(GetBottomRadius(), GetTopRadius(), t);

        // Check horizontal distance
        Vector3 horizontal = localPos - up * localY;
        return horizontal.magnitude <= radiusAtHeight;
    }

    #endregion

    #region IFluidMassSource Implementation

    public float InitialMass
    {
        get
        {
            if (!initialized)
            {
                return mass != null ? mass.paintMass : 0f;
            }
            return initialMass;
        }
    }

    public float CurrentMass
    {
        get
        {
            return mass != null ? mass.paintMass : 0f;
        }
    }

    public float MassRatio
    {
        get
        {
            if (InitialMass <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(CurrentMass / InitialMass);
        }
    }

    #endregion

    #region IContainerMotion Implementation

    public float CurrentSpeed
    {
        get
        {
            return GetContainerVelocity().magnitude;
        }
    }

    public float CurrentAngularSpeed
    {
        get
        {
            if (physics == null)
            {
                return 0f;
            }

            // Approximate angular speed from tangential velocity and rope length
            float ropeLength = physics.ropeLength;
            if (ropeLength < 0.01f) return 0f;

            return CurrentSpeed / ropeLength;
        }
    }

    public bool IsMoving
    {
        get
        {
            return CurrentSpeed > movementThreshold;
        }
    }

    public bool IsSpeedAboveThreshold(float threshold)
    {
        return CurrentSpeed > threshold;
    }

    #endregion

    #region Debug Gizmos

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && autoFindComponents)
        {
            FindComponents();
        }

        if (!IsValid())
        {
            return;
        }

        if (showContainerGizmo)
        {
            DrawContainerGizmo();
        }

        if (showVelocityGizmo && Application.isPlaying)
        {
            DrawVelocityGizmo();
        }
    }

    private void DrawContainerGizmo()
    {
        Gizmos.color = gizmoColor;

        Vector3 center = GetContainerCenter();
        Vector3 up = GetContainerUp();
        float height = GetHeight();
        float bottomRadius = GetBottomRadius();
        float topRadius = GetTopRadius();

        DrawWireFrustum(center, up, bottomRadius, topRadius, height, 24);
    }

    private void DrawWireFrustum(Vector3 baseCenter, Vector3 axis, float bottomRadius, float topRadius, float height, int segments = 24)
    {
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
        Vector3 topCenter = baseCenter + axis * height;

        Vector3 prevBase = baseCenter + rot * new Vector3(bottomRadius, 0, 0);
        Vector3 prevTop = topCenter + rot * new Vector3(topRadius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 baseOffset = rot * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * bottomRadius;
            Vector3 topOffset = rot * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * topRadius;

            Vector3 curBase = baseCenter + baseOffset;
            Vector3 curTop = topCenter + topOffset;

            Gizmos.DrawLine(prevBase, curBase);
            Gizmos.DrawLine(prevTop, curTop);

            if (i % 4 == 0)
            {
                Gizmos.DrawLine(curBase, curTop);
            }

            prevBase = curBase;
            prevTop = curTop;
        }
    }

    private void DrawVelocityGizmo()
    {
        Vector3 velocity = GetContainerVelocity();
        if (velocity.sqrMagnitude < 0.001f) return;

        Vector3 center = GetContainerCenter();
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(center, velocity);

        // Draw arrow head
        Vector3 endPoint = center + velocity;
        Vector3 direction = velocity.normalized;
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * 0.1f;
        Vector3 back = -direction * 0.2f;

        Gizmos.DrawLine(endPoint, endPoint + back + right);
        Gizmos.DrawLine(endPoint, endPoint + back - right);
    }

    #endregion
}
