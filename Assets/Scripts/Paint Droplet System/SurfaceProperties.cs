using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceProperties", menuName = "Paint/SurfaceProperties")]
public class SurfaceProperties : ScriptableObject
{
    public enum SurfaceType { Paper, Cloth, Wood }

    public SurfaceType type;
    [Range(0f, 1f)] public float porosity = 0.5f;
    public float permeability = 0.001f;     
    public float spreadingFactor = 1f;
    public float absorptionRate = 0.1f;      
    public float surfaceTensionCoeff = 0.03f;
    public float roughness = 0.5f;

    public static SurfaceProperties Paper()
    {
        SurfaceProperties sp = CreateInstance<SurfaceProperties>();
        sp.type = SurfaceType.Paper;
        sp.porosity = 0.4f;
        sp.permeability = 0.005f;
        sp.spreadingFactor = 1.2f;
        sp.absorptionRate = 0.15f;
        sp.surfaceTensionCoeff = 0.03f;
        sp.roughness = 0.2f;
        return sp;
    }

    public static SurfaceProperties Cloth()
    {
        SurfaceProperties sp = CreateInstance<SurfaceProperties>();
        sp.type = SurfaceType.Cloth;
        sp.porosity = 0.8f;
        sp.permeability = 0.02f;
        sp.spreadingFactor = 0.8f; 
        sp.absorptionRate = 0.3f;
        sp.surfaceTensionCoeff = 0.04f;
        sp.roughness = 0.8f;
        return sp;
    }

    public static SurfaceProperties Wood()
    {
        SurfaceProperties sp = CreateInstance<SurfaceProperties>();
        sp.type = SurfaceType.Wood;
        sp.porosity = 0.2f;
        sp.permeability = 0.0005f;
        sp.spreadingFactor = 1.5f;
        sp.absorptionRate = 0.05f;
        sp.surfaceTensionCoeff = 0.035f;
        sp.roughness = 0.4f;
        return sp;
    }
}