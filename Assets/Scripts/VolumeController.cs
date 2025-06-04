using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class VolumeController : MonoBehaviour
{
    public ComputeShader computeShader;
    public int resolution = 64;
    
    private RenderTexture _volumeTexture;
    private Material _material;
    private int _kernelHandle;
    private float _time;

    void Start()
    {
        // Create 3D render texture
        _volumeTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = resolution,
            enableRandomWrite = true
        };
        _volumeTexture.Create();

        // Set up material
        _material = GetComponent<MeshRenderer>().material;
        _material.SetTexture("_VolumeTex", _volumeTexture);

        // Set up compute shader
        _kernelHandle = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(_kernelHandle, "Result", _volumeTexture);
    }

    void Update()
    {
        // Update time parameter
        _time += Time.deltaTime;
        computeShader.SetFloat("_TimeDelta", _time);

        // Dispatch compute shader
        int groups = Mathf.CeilToInt(resolution / 8f);
        int groupsZ = Mathf.CeilToInt(resolution / 4f); // Z uses 4 threads per group
        computeShader.Dispatch(_kernelHandle, groups, groups, groupsZ);
    }

    void OnDestroy()
    {
        if (_volumeTexture != null) 
            _volumeTexture.Release();
    }
}