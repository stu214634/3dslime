using UnityEngine;

/// <summary>
/// Simple script to apply the 2D slime material to any mesh.
/// Attach this to any GameObject with a MeshRenderer to apply slime simulation.
/// </summary>
public class SlimeMaterialApplicator : MonoBehaviour
{
    [Header("Slime Material Settings")]
    public Material slime2DMaterial;
    public Simulation slimeSimulation;
    
    [Header("Material Properties")]
    [Range(0.5f, 10f)]
    public float intensity = 2.0f;
    [Range(0f, 5f)]
    public float emissionStrength = 1.0f;
    [Range(0f, 1f)]
    public float metallic = 0f;
    [Range(0f, 1f)]
    public float smoothness = 0.5f;
    
    [Header("Texture Mapping")]
    [Range(0.1f, 5f)]
    public float textureScale = 1.0f;
    [Tooltip("0 = UV Mapping (uses mesh UVs), 1 = World Space (global mapping)")]
    [Range(0f, 1f)]
    public float mappingMode = 0f;
    
    [Header("Species Colors")]
    [ColorUsage(true, true)]
    public Color speciesColor1 = Color.red;
    [ColorUsage(true, true)]
    public Color speciesColor2 = Color.green;
    [ColorUsage(true, true)]
    public Color speciesColor3 = Color.blue;
    
    private MeshRenderer meshRenderer;
    private Material materialInstance;
    
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("SlimeMaterialApplicator requires a MeshRenderer component!");
            enabled = false;
            return;
        }
        
        // Create material instance to avoid modifying the shared material
        if (slime2DMaterial != null)
        {
            materialInstance = new Material(slime2DMaterial);
            meshRenderer.material = materialInstance;
            
            // Apply initial settings
            UpdateMaterialProperties();
        }
        else
        {
            Debug.LogWarning("No Slime2D Material assigned! Please assign the material in the inspector.");
        }
        
        // Find simulation if not assigned
        if (slimeSimulation == null)
        {
            slimeSimulation = FindObjectOfType<Simulation>();
            if (slimeSimulation == null)
            {
                Debug.LogWarning("No Simulation found in scene! The slime texture will not update.");
            }
        }
    }
    
    void Update()
    {
        // Update material properties if they've changed
        UpdateMaterialProperties();
        
        // Update the slime texture from simulation
        if (slimeSimulation != null && materialInstance != null)
        {
            // Get the slime texture directly from the simulation using the public property
            RenderTexture slimeTexture = slimeSimulation.SlimeTexture;
            if (slimeTexture != null)
            {
                materialInstance.SetTexture("_SlimeTexture", slimeTexture);
            }
        }
    }
    
    void UpdateMaterialProperties()
    {
        if (materialInstance != null)
        {
            materialInstance.SetFloat("_Intensity", intensity);
            materialInstance.SetFloat("_EmissionStrength", emissionStrength);
            materialInstance.SetFloat("_Metallic", metallic);
            materialInstance.SetFloat("_Smoothness", smoothness);
            materialInstance.SetFloat("_TextureScale", textureScale);
            materialInstance.SetFloat("_MappingMode", mappingMode);
            materialInstance.SetColor("_SpeciesColor1", speciesColor1);
            materialInstance.SetColor("_SpeciesColor2", speciesColor2);
            materialInstance.SetColor("_SpeciesColor3", speciesColor3);
        }
    }
    
    void OnDestroy()
    {
        // Clean up material instance
        if (materialInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(materialInstance);
            }
            else
            {
                DestroyImmediate(materialInstance);
            }
        }
    }
    
    /// <summary>
    /// Apply the slime material to any GameObject with a MeshRenderer
    /// </summary>
    /// <param name="targetObject">GameObject to apply slime material to</param>
    public void ApplyToMesh(GameObject targetObject)
    {
        MeshRenderer renderer = targetObject.GetComponent<MeshRenderer>();
        if (renderer != null && slime2DMaterial != null)
        {
            Material instance = new Material(slime2DMaterial);
            renderer.material = instance;
            
            // Apply current settings
            instance.SetFloat("_Intensity", intensity);
            instance.SetFloat("_EmissionStrength", emissionStrength);
            instance.SetFloat("_Metallic", metallic);
            instance.SetFloat("_Smoothness", smoothness);
            instance.SetFloat("_TextureScale", textureScale);
            instance.SetFloat("_MappingMode", mappingMode);
            instance.SetColor("_SpeciesColor1", speciesColor1);
            instance.SetColor("_SpeciesColor2", speciesColor2);
            instance.SetColor("_SpeciesColor3", speciesColor3);
            
            Debug.Log($"Applied slime material to {targetObject.name}");
        }
        else
        {
            Debug.LogWarning($"Cannot apply slime material to {targetObject.name} - missing MeshRenderer or Material!");
        }
    }
} 