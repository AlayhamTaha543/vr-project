using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
public struct Particle
{
    public float lambda;
    public float density;
    public Vector3 predictedPosition;
    public Vector3 velocity;
    public Vector3 position;
    public float _pad;
}

public class SPH_Compute : MonoBehaviour
{
    [Header("Container Reference")]
    [Tooltip("Drag the BucketSetup GameObject here (it has the BucketFluidConnector)")]
    public GameObject containerObject;

    private IFluidContainer container;
    private IFluidMassSource massSource;

    [Header("General")]
    [Tooltip("Show individual SPH particles as spheres. Disable to use FluidRayMarching volumetric rendering instead.")]
    public bool showSpheres = true;
    public Vector3Int numToSpawn = new Vector3Int(10, 10, 10);
    public Vector3 spawnBoxCenter = new Vector3(0, 3, 0);
    public Vector3 spawnBox = new Vector3(4, 2, 1.5f);
    public float particleRadius = 0.1f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 0.08f;
    public Material material;

    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    [Header("Fluid Constants")]
    public float boundDamping = -0.3f;
    public float particleMass = 1f;
    public float restingDensity = 1f;

    [Header("PBF Solver")]
    public int solverIterations = 3;

    [Header("Tensile Instability Correction")]
    public float sCorrK = 0.0001f;
    public float sCorrN = 4f;
    public float deltaQ = 0.03f;

    [Header("Non-Newtonian Viscosity")]
    public float consistencyIndex = 1f;
    public float powerLawIndex = 0.6f;
    public float gammaDotMin = 0.01f;

    [Header("Surface Adhesion")]
    public float kAdhesion = 50f;
    public float rInt = 0.2f;

    [Header("Time")]
    public float timestep = 0.007f;
    public Transform sphere;

    [Header("Compute")]
    public ComputeShader shader;
    public Particle[] particles;

    public ComputeBuffer _particlesBuffer { get; private set; }
    public int ActiveParticleCount { get; private set; }

    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _deltaPositionsBuffer;
    private Vector3[] _zeroDeltaPositions;
    private readonly uint[] _indirectArgs = new uint[5];

    private int totalSpawned;
    private float initialPaintMass;
    private bool initialized;
    private bool missingRenderReferenceLogged;

    private int predictPositionKernel;
    private int computeLambdaKernel;
    private int computeDeltaPositionKernel;
    private int applyDeltaPositionKernel;
    private int finalizeKernel;
    private int applyViscosityKernel;

    private void Awake()
    {
        if (containerObject == null)
        {
            BucketFluidConnector found = FindObjectOfType<BucketFluidConnector>();
            if (found != null)
                containerObject = found.gameObject;
        }

        if (containerObject != null)
        {
            container = containerObject.GetComponent<IFluidContainer>();
            massSource = containerObject.GetComponent<IFluidMassSource>();

            if (container == null)
                UnityEngine.Debug.LogError("SPH_Compute: containerObject has no component implementing IFluidContainer! Add BucketFluidConnector to it.");
        }
        else
        {
            UnityEngine.Debug.LogError("SPH_Compute: no BucketFluidConnector found in scene! Create one or drag BucketSetup into the containerObject field.");
        }
    }

    private void Start()
    {
        Vector3 spawnCenter = spawnBoxCenter;
        if (container != null)
        {
            spawnCenter = container.GetContainerCenter() + container.GetContainerUp() * container.GetHeight() * 0.5f;
        }

        SpawnParticlesInBox(spawnCenter);

        if (shader == null)
        {
            UnityEngine.Debug.LogError("[SPH_Compute] No ComputeShader assigned! Drag SPH_PBF.compute into the 'shader' field.");
            return;
        }

        InitializeComputeBuffers();
        SetupIndirectArgs();

        if (massSource != null)
        {
            initialPaintMass = Mathf.Max(massSource.InitialMass, 0.001f);
        }
        else
        {
            initialPaintMass = totalSpawned * particleMass;
        }

        initialized = true;
    }

    private void SetupIndirectArgs()
    {
        WriteIndirectArgs(totalSpawned);
        _argsBuffer = new ComputeBuffer(1, _indirectArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(_indirectArgs);
    }

    private void UpdateIndirectArgs()
    {
        if (_argsBuffer == null) return;

        WriteIndirectArgs(ActiveParticleCount);
        _argsBuffer.SetData(_indirectArgs);
    }

    private void WriteIndirectArgs(int instanceCount)
    {
        _indirectArgs[0] = particleMesh != null ? particleMesh.GetIndexCount(0) : 0;
        _indirectArgs[1] = (uint)Mathf.Max(instanceCount, 0);
        _indirectArgs[2] = particleMesh != null ? particleMesh.GetIndexStart(0) : 0;
        _indirectArgs[3] = particleMesh != null ? particleMesh.GetBaseVertex(0) : 0;
        _indirectArgs[4] = 0;
    }

    private void UpdateParticleMaterial()
    {
        if (material == null || _particlesBuffer == null) return;

        // DrawMeshInstancedIndirect needs the material's procedural-instancing
        // variant and the particle buffer bound before the draw call.
        material.enableInstancing = true;
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);
    }

    private void InitializeComputeBuffers()
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Particle));
        _particlesBuffer = new ComputeBuffer(totalSpawned, stride);
        _particlesBuffer.SetData(particles);

        _deltaPositionsBuffer = new ComputeBuffer(totalSpawned, 12);
        _zeroDeltaPositions = new Vector3[totalSpawned];

        predictPositionKernel = shader.FindKernel("PredictPosition");
        computeLambdaKernel = shader.FindKernel("ComputeLambda");
        computeDeltaPositionKernel = shader.FindKernel("ComputeDeltaPosition");
        applyDeltaPositionKernel = shader.FindKernel("ApplyDeltaPosition");
        finalizeKernel = shader.FindKernel("FinalizeVelocityAndPosition");
        applyViscosityKernel = shader.FindKernel("ApplyViscosity");

        shader.SetInt("particleLength", totalSpawned);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping", boundDamping);

        // Send all radius powers — shader defaults (radius=2, radius2=4) are wrong for Unity units
        float r = particleRadius;
        shader.SetFloat("radius", r);
        shader.SetFloat("radius2", r * r);
        shader.SetFloat("radius3", r * r * r);
        shader.SetFloat("radius4", r * r * r * r);

        shader.SetFloat("pi", Mathf.PI);
        shader.SetFloat("kAdhesion", kAdhesion);
        shader.SetFloat("rInt", rInt);
        shader.SetFloat("sCorrK", sCorrK);
        shader.SetFloat("sCorrN", sCorrN);
        shader.SetFloat("deltaQ", deltaQ);
        shader.SetFloat("K", consistencyIndex);
        shader.SetFloat("n", powerLawIndex);
        shader.SetFloat("gammaDotMin", gammaDotMin);

        int[] kernels = {
        predictPositionKernel, computeLambdaKernel,
        computeDeltaPositionKernel, applyDeltaPositionKernel,
        finalizeKernel, applyViscosityKernel
    };
        foreach (int k in kernels)
            shader.SetBuffer(k, "_particles", _particlesBuffer);

        shader.SetBuffer(computeDeltaPositionKernel, "_deltaPositions", _deltaPositionsBuffer);
        shader.SetBuffer(applyDeltaPositionKernel, "_deltaPositions", _deltaPositionsBuffer);
    }
    private void SpawnParticlesInBox(Vector3 center)
    {
        List<Particle> spawnedParticles = new List<Particle>();

        // Get bucket orientation at spawn time.
        // If no container is connected yet, fall back to world-up.
        Vector3 bucketUp = container != null ? container.GetContainerUp() : Vector3.up;
        float topR = container != null ? container.GetTopRadius() : 0.48f;
        float botR = container != null ? container.GetBottomRadius() : 0.33f;
        float height = container != null ? container.GetHeight() : 1.05f;

        // Build two axes perpendicular to bucketUp for the horizontal grid sweep.
        Vector3 right = Vector3.Cross(bucketUp, Vector3.forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(bucketUp, Vector3.right);
        right = right.normalized;
        Vector3 forward = Vector3.Cross(bucketUp, right).normalized;

        float spacing = particleRadius * 2.2f;   // 10 % gap between particles at rest
        float yStart = particleRadius * 2f;     // start one diameter above floor
        float yEnd = height * 0.7f;           // fill to 70 % height (leaves headroom)

        for (float ly = yStart; ly < yEnd; ly += spacing)
        {
            // Wall radius at this height, shrunk inward so no particle touches the wall on spawn
            float t = ly / height;
            float wallR = Mathf.Lerp(botR, topR, t);
            float safeR = wallR - particleRadius * 3f;    // 3-radius safety margin from wall
            if (safeR <= 0f) continue;

            for (float lx = -safeR; lx <= safeR; lx += spacing)
            {
                for (float lz = -safeR; lz <= safeR; lz += spacing)
                {
                    // Only spawn if inside the safe cylinder at this height
                    if (Mathf.Sqrt(lx * lx + lz * lz) > safeR) continue;

                    // World-space position: center + ly along bucketUp + lx/lz in the horizontal plane
                    Vector3 worldPos = center
                        + bucketUp * ly
                        + right * lx
                        + forward * lz;

                    // Tiny random jitter (< 5 % of spacing) to break grid symmetry
                    worldPos += UnityEngine.Random.insideUnitSphere * (spacing * 0.04f);

                    spawnedParticles.Add(new Particle { position = worldPos });
                }
            }
        }

        totalSpawned = spawnedParticles.Count;

        if (totalSpawned == 0)
        {
            UnityEngine.Debug.LogWarning("[SPH_Compute] Bucket-aware spawn produced 0 particles. Falling back to single particle at center.");
            spawnedParticles.Add(new Particle { position = center });
            totalSpawned = 1;
        }

        particles = spawnedParticles.ToArray();
        ActiveParticleCount = totalSpawned;

        UnityEngine.Debug.Log($"[SPH_Compute] Spawned {totalSpawned} particles inside bucket " +
                  $"(r={botR:F2}-{topR:F2}m, h={height:F2}m, spacing={spacing:F3}m)");
    }
    private void FixedUpdate()
    {
        if (!initialized) return;

        shader.SetFloat("timestep", timestep);

        Vector3 bucketVelocityValue = Vector3.zero;

        if (container != null)
        {
            shader.SetVector("bucketCenter", container.GetContainerCenter());
            shader.SetVector("bucketUp", container.GetContainerUp());
            shader.SetFloat("bucketTopRadius", container.GetTopRadius());
            shader.SetFloat("bucketBottomRadius", container.GetBottomRadius());
            shader.SetFloat("bucketHeight", container.GetHeight());

            bucketVelocityValue = container.GetContainerVelocity();
            shader.SetVector("bucketVelocity", bucketVelocityValue);

            shader.SetVector("bucketAcceleration", container.GetContainerAcceleration());
        }

        if (sphere != null)
        {
            shader.SetVector("spherePos", sphere.transform.position);
            shader.SetFloat("sphereRadius", sphere.transform.localScale.x / 2);
        }
        else
        {
            shader.SetVector("spherePos", new Vector3(1e6f, 1e6f, 1e6f));
            shader.SetFloat("sphereRadius", 0f);
        }

        if (massSource != null)
        {
            ActiveParticleCount = Mathf.Clamp(Mathf.RoundToInt(massSource.MassRatio * totalSpawned), 1, totalSpawned);
        }
        else
        {
            ActiveParticleCount = totalSpawned;
        }

        shader.SetInt("particleLength", ActiveParticleCount);
        int threadGroups = Mathf.CeilToInt(ActiveParticleCount / 100.0f);

        shader.Dispatch(predictPositionKernel, threadGroups, 1, 1);

        for (int iter = 0; iter < solverIterations; iter++)
        {
            _deltaPositionsBuffer.SetData(_zeroDeltaPositions);
            shader.Dispatch(computeLambdaKernel, threadGroups, 1, 1);
            shader.Dispatch(computeDeltaPositionKernel, threadGroups, 1, 1);
            shader.Dispatch(applyDeltaPositionKernel, threadGroups, 1, 1);
        }

        shader.Dispatch(finalizeKernel, threadGroups, 1, 1);
        shader.Dispatch(applyViscosityKernel, threadGroups, 1, 1);

        UpdateParticleMaterial();
        UpdateIndirectArgs();
    }

    private bool debugLogged;

    private void Update()
    {
        if (!initialized) return;

        if (!debugLogged)
        {
            UnityEngine.Debug.Log($"[SPH_Compute] showSpheres={showSpheres}, container={container != null}, mesh={particleMesh?.name ?? "null"}, mat={material?.name ?? "null"}, args={_argsBuffer != null}, particles={ActiveParticleCount}, total={totalSpawned}");
            debugLogged = true;
        }

        if (Input.GetKeyDown(KeyCode.P) && _particlesBuffer != null)
        {
            Particle[] data = new Particle[Mathf.Min(5, totalSpawned)];
            _particlesBuffer.GetData(data);
            for (int i = 0; i < data.Length; i++)
                UnityEngine.Debug.Log($"[SPH] Particle {i}: pos={data[i].position}, vel={data[i].velocity}");
        }

        if (!showSpheres) return;

        if (particleMesh == null || material == null || _argsBuffer == null || _particlesBuffer == null)
        {
            if (!missingRenderReferenceLogged)
            {
                UnityEngine.Debug.LogWarning("[SPH_Compute] Cannot draw particles yet. Check particleMesh, material, compute buffers, and indirect args buffer.", this);
                missingRenderReferenceLogged = true;
            }
            return;
        }

        UpdateParticleMaterial();

        Bounds renderBounds = GetRenderBounds();
        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            material,
            renderBounds,
            _argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows: false,
            layer: gameObject.layer);
    }

    private Bounds GetRenderBounds()
    {
        if (container != null)
        {
            Vector3 center = container.GetContainerCenter() + container.GetContainerUp() * container.GetHeight() * 0.5f;
            float maxDiameter = Mathf.Max(container.GetTopRadius(), container.GetBottomRadius()) * 2f;
            float size = Mathf.Max(maxDiameter, container.GetHeight()) + particleRenderSize * 4f;
            return new Bounds(center, Vector3.one * Mathf.Max(size, 2f));
        }

        Vector3 fallbackSize = spawnBox + Vector3.one * Mathf.Max(particleRenderSize * 4f, 1f);
        return new Bounds(spawnBoxCenter, fallbackSize);
    }

    private void OnDestroy()
    {
        _particlesBuffer?.Release();
        _argsBuffer?.Release();
        _deltaPositionsBuffer?.Release();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(spawnBoxCenter, spawnBox);
        }

        if (container != null)
        {
            Gizmos.color = Color.blue;
            Vector3 center = container.GetContainerCenter();
            Vector3 up = container.GetContainerUp();
            DrawWireFrustumGizmo(center, up, container.GetBottomRadius(), container.GetTopRadius(), container.GetHeight());
        }
    }

    private void DrawWireFrustumGizmo(Vector3 baseCenter, Vector3 axis, float bottomRadius, float topRadius, float height, int segments = 24)
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
            if (i % 4 == 0) Gizmos.DrawLine(curBase, curTop);

            prevBase = curBase;
            prevTop = curTop;
        }
    }
}
