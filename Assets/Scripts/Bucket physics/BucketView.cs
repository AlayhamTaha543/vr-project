using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates and updates the visible bucket, rope, handle, paper, camera, and light.
/// This script does not calculate physics. It reads BucketPhysics and moves the visuals to match it.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Bucket Paint/Bucket View")]
public sealed class BucketView : MonoBehaviour
{
    [Header("Bucket Size")]
    public float bucketHeight = 1.05f;
    public float topWidth = 0.96f;
    public float bottomWidth = 0.66f;
    public float wallThickness = 0.035f;
    public float holeDiameter = 0.15f;

    [Header("Paper")]
    public Vector2 paperSize = new Vector2(6f, 4f);
    public Vector2 paperOffset = Vector2.zero;

    [Header("Camera")]
    public bool controlCamera = true;
    
    public Vector3 cameraOffset = new Vector3(0f, 3f, -6f);

    //public Vector3 cameraOffset = new Vector3(0f, 1.35f, -5.6f);

    private const int BucketSegments = 48;
    private const float HandleHeight = 0.28f;
    private const float RopeWidth = 0.035f;
    private const float HandleWidth = 0.025f;

    private static readonly Color BucketColor = new Color(0.70f, 0.90f, 1.00f, 0.28f);
    private static readonly Color RimColor = new Color(0.88f, 0.96f, 1.00f, 0.65f);
    private static readonly Color RopeColor = new Color(0.35f, 0.22f, 0.10f, 1f);
    private static readonly Color HandleColor = new Color(0.80f, 0.80f, 0.80f, 1f);
    private static readonly Color PaperColor = new Color(0.92f, 0.90f, 0.84f, 1f);

    private BucketPhysics physics;

    private Transform anchor;
    private Transform bucketRoot;
    private Transform attachPoint;

    private LineRenderer ropeLine;
    private LineRenderer handleLine;

    private MeshFilter bucketFilter;
    private MeshFilter topRimFilter;
    private MeshFilter holeRimFilter;
    private MeshFilter paperFilter;

    private MeshRenderer bucketRenderer;
    private MeshRenderer topRimRenderer;
    private MeshRenderer holeRimRenderer;
    private MeshRenderer paperRenderer;

    private Material bucketMaterial;
    private Material rimMaterial;
    private Material ropeMaterial;
    private Material handleMaterial;
    private Material paperMaterial;

    private bool refreshQueued;
    private bool isRefreshing;

    // Keeps bucket yaw stable so the bucket does not flip when swing velocity changes direction.
    private Vector3 forwardReference = Vector3.forward;
    private bool forwardReferenceReady;

    private float TopRadius { get { return topWidth * 0.5f; } }
    private float BottomRadius { get { return bottomWidth * 0.5f; } }
    private float HoleRadius { get { return holeDiameter * 0.5f; } }

    private void OnEnable()
    {
        forwardReferenceReady = false;
        QueueRefresh();
    }

    private void OnValidate()
    {
        ValidateValues();
        QueueRefresh();
    }

    private void Update()
    {
        if (refreshQueued)
        {
            refreshQueued = false;
            RefreshAll();
        }
        else
        {
            RefreshTransforms();
        }
    }

    private void QueueRefresh()
    {
        refreshQueued = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= EditorRefresh;
            EditorApplication.delayCall += EditorRefresh;
        }
#endif
    }

#if UNITY_EDITOR
    private void EditorRefresh()
    {
        if (this == null) return;
        if (!isActiveAndEnabled) return;
        if (Application.isPlaying) return;

        refreshQueued = false;
        RefreshAll();
    }
#endif

    private void RefreshAll()
    {
        if (isRefreshing) return;
        isRefreshing = true;

        ValidateValues();
        FindOrAddRootComponents();
        CreateChildren();
        CreateMaterials();
        ApplyMaterials();
        RebuildMeshes();
        RefreshTransforms();
        SetupCameraAndLight();

        isRefreshing = false;
    }

    private void FindOrAddRootComponents()
    {
        physics = GetComponent<BucketPhysics>();
        if (physics == null) physics = gameObject.AddComponent<BucketPhysics>();

        BucketMass mass = GetComponent<BucketMass>();
        if (mass == null) gameObject.AddComponent<BucketMass>();
    }

    private void CreateChildren()
    {
        anchor = Child(transform, "anchor");
        bucketRoot = Child(transform, "bucketRoot");
        attachPoint = Child(bucketRoot, "attachPoint");

        Transform rope = Child(transform, "rope");
        ropeLine = Add<LineRenderer>(rope.gameObject);

        Transform handle = Child(bucketRoot, "handle");
        handleLine = Add<LineRenderer>(handle.gameObject);

        Transform bucketMesh = Child(bucketRoot, "bucketMesh");
        bucketFilter = Add<MeshFilter>(bucketMesh.gameObject);
        bucketRenderer = Add<MeshRenderer>(bucketMesh.gameObject);

        Transform topRim = Child(bucketRoot, "topRim");
        topRimFilter = Add<MeshFilter>(topRim.gameObject);
        topRimRenderer = Add<MeshRenderer>(topRim.gameObject);

        Transform holeRim = Child(bucketRoot, "holeRim");
        holeRimFilter = Add<MeshFilter>(holeRim.gameObject);
        holeRimRenderer = Add<MeshRenderer>(holeRim.gameObject);

        Transform paper = Child(transform, "paper");
        paperFilter = Add<MeshFilter>(paper.gameObject);
        paperRenderer = Add<MeshRenderer>(paper.gameObject);
    }

    private void CreateMaterials()
    {
        if (bucketMaterial == null) bucketMaterial = BucketMaterialFactory.CreateTransparent("bucketMaterial", BucketColor);
        if (rimMaterial == null) rimMaterial = BucketMaterialFactory.CreateTransparent("rimMaterial", RimColor);
        if (ropeMaterial == null) ropeMaterial = BucketMaterialFactory.CreateColor("ropeMaterial", RopeColor);
        if (handleMaterial == null) handleMaterial = BucketMaterialFactory.CreateColor("handleMaterial", HandleColor);
        if (paperMaterial == null) paperMaterial = BucketMaterialFactory.CreateColor("paperMaterial", PaperColor);
    }

    private void ApplyMaterials()
    {
        if (bucketMaterial != null) bucketMaterial.color = BucketColor;
        if (rimMaterial != null) rimMaterial.color = RimColor;
        if (ropeMaterial != null) ropeMaterial.color = RopeColor;
        if (handleMaterial != null) handleMaterial.color = HandleColor;
        if (paperMaterial != null) paperMaterial.color = PaperColor;

        if (bucketRenderer != null) bucketRenderer.sharedMaterial = bucketMaterial;
        if (topRimRenderer != null) topRimRenderer.sharedMaterial = rimMaterial;
        if (holeRimRenderer != null) holeRimRenderer.sharedMaterial = rimMaterial;
        if (paperRenderer != null) paperRenderer.sharedMaterial = paperMaterial;

        SetupLine(ropeLine, ropeMaterial, RopeWidth, true);
        SetupLine(handleLine, handleMaterial, HandleWidth, false);
    }

    private void RebuildMeshes()
    {
        if (bucketFilter != null)
        {
            bucketFilter.sharedMesh = BucketMeshBuilder.BuildBucket(
                bucketHeight,
                topWidth,
                bottomWidth,
                wallThickness,
                holeDiameter,
                BucketSegments);
        }

        if (topRimFilter != null)
            topRimFilter.sharedMesh = BucketMeshBuilder.BuildTorus(TopRadius, wallThickness * 0.55f, BucketSegments, 8);

        if (holeRimFilter != null)
            holeRimFilter.sharedMesh = BucketMeshBuilder.BuildTorus(HoleRadius, wallThickness * 0.38f, BucketSegments, 8);

        if (paperFilter != null)
            paperFilter.sharedMesh = BucketMeshBuilder.BuildPaper(paperSize.x, paperSize.y);
    }

    private void RefreshTransforms()
    {
        if (physics == null) physics = GetComponent<BucketPhysics>();
        if (physics == null) return;

        if (!Application.isPlaying)
            physics.SyncPreview();

        if (anchor == null || bucketRoot == null || attachPoint == null || ropeLine == null || handleLine == null)
        {
            QueueRefresh();
            return;
        }

        Vector3 anchorPosition = physics.AnchorPosition;
        Vector3 ropeEnd = physics.EndPosition;
        Vector3 ropeDirection = physics.RopeDirection;

        anchor.position = anchorPosition;

        float attachY = bucketHeight * 0.5f + HandleHeight;
        Vector3 attachLocal = new Vector3(0f, attachY, 0f);
        attachPoint.localPosition = attachLocal;
        attachPoint.localRotation = Quaternion.identity;
        attachPoint.localScale = Vector3.one;

        Quaternion bucketRotation = BucketRotationFromRope(ropeDirection);

        bucketRoot.rotation = bucketRotation;
        bucketRoot.position = ropeEnd - bucketRotation * attachLocal;
        bucketRoot.localScale = Vector3.one;

        if (bucketFilter != null)
        {
            bucketFilter.transform.localPosition = Vector3.zero;
            bucketFilter.transform.localRotation = Quaternion.identity;
            bucketFilter.transform.localScale = Vector3.one;
        }

        if (topRimFilter != null)
        {
            topRimFilter.transform.localPosition = new Vector3(0f, bucketHeight * 0.5f, 0f);
            topRimFilter.transform.localRotation = Quaternion.identity;
            topRimFilter.transform.localScale = Vector3.one;
        }

        if (holeRimFilter != null)
        {
            holeRimFilter.transform.localPosition = new Vector3(0f, -bucketHeight * 0.5f, 0f);
            holeRimFilter.transform.localRotation = Quaternion.identity;
            holeRimFilter.transform.localScale = Vector3.one;
        }

        if (paperFilter != null)
        {
            paperFilter.transform.position = new Vector3(
                physics.anchorXZ.x + paperOffset.x,
                0f,
                physics.anchorXZ.y + paperOffset.y);

            paperFilter.transform.rotation = Quaternion.identity;
            paperFilter.transform.localScale = Vector3.one;
        }

        UpdateRope(anchorPosition, ropeEnd);
        UpdateHandle();

        if (controlCamera)
            PositionCamera();
    }

    private Quaternion BucketRotationFromRope(Vector3 ropeDirection)
    {
        Vector3 desiredUp = -ropeDirection.normalized;

        if (!forwardReferenceReady || forwardReference.sqrMagnitude < 0.0001f)
        {
            forwardReference = Vector3.ProjectOnPlane(transform.forward, desiredUp);
            if (forwardReference.sqrMagnitude < 0.0001f)
                forwardReference = Vector3.ProjectOnPlane(Vector3.forward, desiredUp);
            if (forwardReference.sqrMagnitude < 0.0001f)
                forwardReference = Vector3.ProjectOnPlane(Vector3.right, desiredUp);

            forwardReference.Normalize();
            forwardReferenceReady = true;
        }

        Vector3 desiredForward = Vector3.ProjectOnPlane(forwardReference, desiredUp);
        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(Vector3.forward, desiredUp);
        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(Vector3.right, desiredUp);

        desiredForward.Normalize();
        forwardReference = desiredForward;

        return Quaternion.LookRotation(desiredForward, desiredUp);
    }

    private void UpdateRope(Vector3 start, Vector3 end)
    {
        if (ropeLine == null) return;

        ropeLine.useWorldSpace = true;
        ropeLine.positionCount = 2;
        ropeLine.SetPosition(0, start);
        ropeLine.SetPosition(1, end);
    }

    private void UpdateHandle()
    {
        if (handleLine == null) return;

        const int pointCount = 17;
        handleLine.useWorldSpace = false;
        handleLine.positionCount = pointCount;

        float radius = TopRadius * 0.82f;
        float topY = bucketHeight * 0.5f;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float x = Mathf.Lerp(-radius, radius, t);
            float y = topY + Mathf.Sin(t * Mathf.PI) * HandleHeight;
            handleLine.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private static void SetupLine(LineRenderer line, Material material, float width, bool useWorldSpace)
    {
        if (line == null) return;

        if (material != null)
            line.sharedMaterial = material;

        line.widthMultiplier = width;
        line.useWorldSpace = useWorldSpace;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 5;
        line.numCornerVertices = 5;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void SetupCameraAndLight()
    {
        if (controlCamera)
            PositionCamera();

        if (FindObjectOfType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }
    }

    private void PositionCamera()
    {
        if (physics == null) physics = GetComponent<BucketPhysics>();
        if (physics == null) return;

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        Vector3 target = physics.AnchorPosition + Vector3.down * Mathf.Min(physics.ropeLength * 0.65f, physics.anchorHeight * 0.55f);
        camera.transform.position = target + cameraOffset;
        camera.transform.LookAt(target);
        camera.nearClipPlane = 0.03f;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        GameObject go = new GameObject(name);
        child = go.transform;
        child.SetParent(parent, false);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static T Add<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private void ValidateValues()
    {
        bucketHeight = Mathf.Max(0.1f, bucketHeight);
        topWidth = Mathf.Max(0.1f, topWidth);
        bottomWidth = Mathf.Max(0.1f, bottomWidth);

        float smallestRadius = Mathf.Min(TopRadius, BottomRadius);
        wallThickness = Mathf.Clamp(wallThickness, 0.005f, smallestRadius * 0.45f);

        float innerBottomRadius = Mathf.Max(0.01f, BottomRadius - wallThickness);
        float maxHoleDiameter = Mathf.Max(0.005f, (innerBottomRadius - 0.01f) * 2f);
        holeDiameter = Mathf.Clamp(holeDiameter, 0.005f, maxHoleDiameter);

        paperSize.x = Mathf.Max(0.2f, paperSize.x);
        paperSize.y = Mathf.Max(0.2f, paperSize.y);
    }




    public Vector3 GetHoleWorldPosition()
    {
        if (physics == null) physics = GetComponent<BucketPhysics>();
        if (physics == null) return Vector3.zero;

        Vector3 attachPoint = physics.EndPosition;
        float handleHeight = 0.28f;
        float distance = bucketHeight + handleHeight;

        return attachPoint + physics.RopeDirection * distance;
    }

    public Vector3 GetHoleWorldVelocity()
    {
        
        if (physics == null) return Vector3.zero;
        return physics.EndVelocity;
    }
}
