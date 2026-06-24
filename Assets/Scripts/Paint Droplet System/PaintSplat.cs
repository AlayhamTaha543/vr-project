using UnityEngine;

public class PaintSplat : MonoBehaviour
{
    [Header("Visual")]
    public float initialScale = 0.05f;

    [Header("Physics Parameters")]
    public float spreadCoefficient = 0.2f;   
    public float viscosity = 0.1f;            
    public float spreadDuration = 1f;         

    [HideInInspector] public SurfaceProperties surface;

    private float currentRadius;
    private float maxRadius;
    private float absorptionRate;
    private float opacity = 1f;
    private float elapsedTime = 0f;
    private bool isSpreading = true;
    private bool isAbsorbing = false;

    private Vector3 flowDirection;
    private float flowSpeed;

    private MeshRenderer meshRenderer;
    private Material material;

   public void Initialize(Vector3 hitPoint, float impactSpeed, float kineticEnergy, bool splatter, SurfaceProperties surf, Vector3 surfaceNormal, float tiltAngle, Color paintColor)
{

    meshRenderer = GetComponent<MeshRenderer>();
    if (meshRenderer != null)
    {
        material = meshRenderer.material;
        material.color = paintColor;
    }
    
    surface = surf;

    
    float jitterRadius = 0.03f; 
    Vector2 randomOffset2D = Random.insideUnitCircle * jitterRadius;
    Vector3 randomOffset3D = new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);
    Vector3 surfaceOffset = randomOffset3D - Vector3.Project(randomOffset3D, surfaceNormal);
    Vector3 finalPosition = hitPoint + surfaceOffset + surfaceNormal * 0.001f; 
    transform.position = finalPosition;

    
    transform.rotation = Quaternion.FromToRotation(Vector3.forward, surfaceNormal);
    transform.Rotate(surfaceNormal, Random.Range(0f, 360f), Space.World);

    
    float D = 0.005f; 
    maxRadius = spreadCoefficient * Mathf.Sqrt((impactSpeed * D) / viscosity);

    
    maxRadius *= Random.Range(0.7f, 1.3f);

    
    maxRadius = Mathf.Clamp(maxRadius, 0.01f, 0.06f);

    
    if (splatter && impactSpeed > 3f)
    {
        int numSplatters = Random.Range(3, 8);
        for (int i = 0; i < numSplatters; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 0.04f;
            offset = Vector3.ProjectOnPlane(offset, surfaceNormal);
            GameObject smallSplat = Instantiate(gameObject, transform.position + offset, Quaternion.identity);
            smallSplat.transform.localScale *= Random.Range(0.2f, 0.5f);
            
            PaintSplat small = smallSplat.GetComponent<PaintSplat>();
            if (small != null) small.absorptionRate *= 2f;
            Destroy(smallSplat, 2f);
        }
    }

    
    absorptionRate = surface.permeability / (viscosity * 10f); 
    absorptionRate = Mathf.Clamp(absorptionRate, 0.001f, 0.1f);

    
    currentRadius = 0.005f; 
    UpdateTransform();

    
    

    
    Vector3 gravityDir = Vector3.down;
    Vector3 normal = surfaceNormal;
    Vector3 tangentGravity = Vector3.ProjectOnPlane(gravityDir, normal);

    if (tangentGravity.sqrMagnitude > 0.001f)
    {
        flowDirection = tangentGravity.normalized;
        float gravityComponent = Mathf.Sin(tiltAngle * Mathf.Deg2Rad) * 9.81f;
        flowSpeed = (gravityComponent * surface.porosity) / (viscosity * 200f);
        flowSpeed = Mathf.Clamp(flowSpeed, 0f, 0.1f);
    }
    else flowSpeed = 0f;

    isSpreading = true;
    isAbsorbing = false;
}
    private void Update()
    {
        elapsedTime += Time.deltaTime;

        
        if (isSpreading)
        {
            float progress = elapsedTime / spreadDuration;
            if (progress >= 1f)
            {
                currentRadius = maxRadius;
                isSpreading = false;
                isAbsorbing = true;
            }
            else
            {
                float growthFactor = Mathf.Sqrt(progress);
                currentRadius = Mathf.Lerp(0.005f, maxRadius, growthFactor);
            }
            UpdateTransform();
        }

        
        if (isAbsorbing && absorptionRate > 0f)
        {
            opacity -= absorptionRate * Time.deltaTime;
            if (opacity < 0f) opacity = 0f;
            if (meshRenderer != null && material != null)
            {
                Color c = material.color;
                c.a = opacity;
                material.color = c;
            }
            
        }

        
        if (flowSpeed > 0.001f && opacity > 0.1f)
        {
            transform.position += flowDirection * flowSpeed * Time.deltaTime;
        }
    }

    private void UpdateTransform()
    {
        
        float size = currentRadius * 2f;
        transform.localScale = Vector3.one * size;
    }

    private void OnDestroy()
    {
        if (material != null)
            DestroyImmediate(material);
    }
}