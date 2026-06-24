using UnityEngine;

public class FluidRayMarching : MonoBehaviour
{
    public ComputeShader raymarching;
    public Camera cam;

    public SPH_Compute sph;

    RenderTexture target;

    [Header("Params")]
    public float viewRadius;
    public float blendStrength;
    public Color paintColor;

    public Color ambientLight;

    public Light lightSource;

    void InitRenderTexture()
    {
        if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight)
        {
            if (target != null)
                target.Release();

            cam.depthTextureMode = DepthTextureMode.Depth;

            target = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }

    private bool render = false;

    public void Begin()
    {
        InitRenderTexture();
        raymarching.SetBuffer(0, "particles", sph._particlesBuffer);
        raymarching.SetInt("numParticles", sph.ActiveParticleCount);
        raymarching.SetFloat("particleRadius", viewRadius);
        raymarching.SetFloat("blendStrength", blendStrength);
        raymarching.SetVector("paintColor", paintColor);
        raymarching.SetVector("_AmbientLight", ambientLight);
        raymarching.SetTextureFromGlobal(0, "_DepthTexture", "_CameraDepthTexture");
        render = true;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!render)
            Begin();

        if (render)
        {
            raymarching.SetVector("_Light", lightSource.transform.forward);
            raymarching.SetFloat("_NearClip", cam.nearClipPlane);

            raymarching.SetTexture(0, "Source", source);
            raymarching.SetTexture(0, "Destination", target);
            raymarching.SetVector("_CameraPos", cam.transform.position);
            raymarching.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
            raymarching.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);

            // Update numParticles each frame to respect draining
            raymarching.SetInt("numParticles", sph.ActiveParticleCount);

            int threadGroupsX = Mathf.CeilToInt(cam.pixelWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(cam.pixelHeight / 8.0f);
            raymarching.Dispatch(0, threadGroupsX, threadGroupsY, 1);

            Graphics.Blit(target, destination);
        }
    }
}
