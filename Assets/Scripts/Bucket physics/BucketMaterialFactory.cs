using UnityEngine;

/// <summary>
/// Static helper class that creates generated materials for the bucket setup.
/// It is not attached to any GameObject.
/// </summary>
public static class BucketMaterialFactory
{
    public static Material CreateTransparent(string name, Color color)
    {
        Shader shader = FindFirstShader("Standard", "Unlit/Color", "Diffuse");
        if (shader == null)
        {
            Debug.LogError("BucketMaterialFactory could not find a usable shader in this Unity project.");
            return null;
        }

        Material material = new Material(shader);
        material.name = name;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = color;
        material.enableInstancing = true;

        if (shader.name == "Standard")
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        return material;
    }

    public static Material CreateColor(string name, Color color)
    {
        Shader shader = FindFirstShader("Sprites/Default", "Unlit/Color", "Diffuse", "Standard");
        if (shader == null)
        {
            Debug.LogError("BucketMaterialFactory could not find a usable shader in this Unity project.");
            return null;
        }

        Material material = new Material(shader);
        material.name = name;
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = color;
        material.enableInstancing = true;
        return material;
    }

    private static Shader FindFirstShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
                return shader;
        }

        return null;
    }
}
