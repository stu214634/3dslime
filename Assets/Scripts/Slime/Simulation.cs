using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class Simulation : MonoBehaviour
{
	public enum SpawnMode { Random, Point, InwardSphere, RandomSphere }

	const int updateKernel = 0;
	const int diffuseKernel = 1;
	const int colourKernel = 2;

	public ComputeShader compute;
	public ComputeShader drawAgentsCS;

	public SlimeSettings settings;

	[Header("Display Settings")]
	public bool showAgentsOnly;
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture volumeTexture; // 3D texture for volume rendering
	public Material volumeMaterial; // Reference to Raymarch material

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	Texture2D colourMapTexture;

	[Header("Debug")]
	public bool debugMode;

	protected virtual void Start()
	{
		Init();
		// Assign volume material to renderer
		var renderer = transform.GetComponentInChildren<MeshRenderer>();
		if (renderer != null && volumeMaterial != null) {
			renderer.material = volumeMaterial;
		}
	}


	void Init()
	{
		// Create 3D render textures (width, height, depth)
		ComputeHelper.CreateRenderTexture3D(ref trailMap, settings.width, settings.height, settings.depth, filterMode, format);
		ComputeHelper.CreateRenderTexture3D(ref diffusedTrailMap, settings.width, settings.height, settings.depth, filterMode, format);
		ComputeHelper.CreateRenderTexture3D(ref volumeTexture, settings.width, settings.height, settings.depth, filterMode, format); // Create 3D volume texture
		
		// Initialize volume material if not set
		if (volumeMaterial == null) {
			volumeMaterial = new Material(Shader.Find("Custom/Raymarch"));
		}

		// Assign textures to kernels
		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseKernel, "DiffusedTrailMap", diffusedTrailMap);
		
		Debug.Log($"[SIMULATION] Kernel indices - Update: {updateKernel}, Diffuse: {diffuseKernel}");
		Debug.Log($"[SIMULATION] Texture bindings set for kernels");
		
		Debug.Log($"[SIMULATION] Textures created - TrailMap: {trailMap.width}x{trailMap.height}x{trailMap.volumeDepth}, Format: {trailMap.graphicsFormat}");
		Debug.Log($"[SIMULATION] DiffusedTrailMap: {diffusedTrailMap.width}x{diffusedTrailMap.height}x{diffusedTrailMap.volumeDepth}");
		Debug.Log($"[SIMULATION] VolumeTexture: {volumeTexture.width}x{volumeTexture.height}x{volumeTexture.volumeDepth}");
		// Connect textures to volume material
		if (volumeMaterial != null) {
			volumeMaterial.SetTexture("_VolumeTex", volumeTexture);
		}

		// Create agents with initial positions and angles
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			Vector3 centre = new Vector3(settings.width / 2, settings.height / 2, settings.depth / 2);
			Vector3 startPos = Vector3.zero;
			float randomAngle = Random.value * Mathf.PI * 2;
			float angle = 0;

			if (settings.spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.Random)
			{
				startPos = new Vector3(Random.Range(0, settings.width), Random.Range(0, settings.height), Random.Range(0, settings.depth));
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.InwardSphere)
			{
				startPos = centre + Random.insideUnitSphere * settings.depth * 0.5f;
				Vector3 dir = (centre - startPos).normalized;
				angle = Mathf.Atan2(dir.y, dir.x);
			}
			else if (settings.spawnMode == SpawnMode.RandomSphere)
			{
				startPos = centre + Random.insideUnitSphere * settings.depth * 0.15f;
				angle = randomAngle;
			}

			Vector3Int speciesMask;
			int speciesIndex = 0;
			int numSpecies = settings.speciesSettings.Length;

			if (numSpecies == 1)
			{
				speciesMask = Vector3Int.one;
			}
			else
			{
				int species = Random.Range(1, numSpecies + 1);
				speciesIndex = species - 1;
				speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
			}

			float randomPitch = (Random.value - 0.5f) * Mathf.PI; // Random pitch angle ±90 degrees
			agents[i] = new Agent() { position = startPos, angle = angle, pitchAngle = randomPitch, speciesMask = speciesMask, speciesIndex = speciesIndex };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
		compute.SetInt("numAgents", settings.numAgents);
		// Agent drawing removed for volume rendering


		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);
		compute.SetInt("depth", settings.depth);


	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	void SaveTextureSlices()
	{
		if (volumeTexture == null) {
			Debug.LogError("[TEXTURE_DEBUG] VolumeTexture is null!");
			return;
		}
		
		Debug.Log($"[TEXTURE_DEBUG] Starting texture slice save - Volume: {volumeTexture.width}x{volumeTexture.height}x{volumeTexture.volumeDepth}");
		Debug.Log($"[TEXTURE_DEBUG] Volume texture format: {volumeTexture.graphicsFormat}, Created: {volumeTexture.IsCreated()}");
		
		string directoryPath = Application.dataPath + "/DebugOutput/";
		if (!System.IO.Directory.Exists(directoryPath)) {
			System.IO.Directory.CreateDirectory(directoryPath);
		}
		
		int depth = volumeTexture.volumeDepth;
		
		// For 3D textures, we need to extract each slice properly
		for (int z = 0; z < depth; z++) {
			// Create a 2D render texture to copy the slice into
			RenderTexture sliceRT = new RenderTexture(volumeTexture.width, volumeTexture.height, 0, volumeTexture.graphicsFormat);
			sliceRT.Create();
			
			// Copy the specific slice from the 3D texture to the 2D render texture
			Graphics.CopyTexture(volumeTexture, z, sliceRT, 0);
			
			// Now read from the 2D render texture
			RenderTexture.active = sliceRT;
			Texture2D slice = new Texture2D(volumeTexture.width, volumeTexture.height, TextureFormat.RGBAFloat, false);
			slice.ReadPixels(new Rect(0, 0, volumeTexture.width, volumeTexture.height), 0, 0);
			slice.Apply();
			RenderTexture.active = null;
			
			// Check for non-zero pixels in this slice
			Color[] pixels = slice.GetPixels();
			int nonZeroPixels = 0;
			float maxValue = 0f;
			for (int i = 0; i < pixels.Length; i++) {
				if (pixels[i].r > 0 || pixels[i].g > 0 || pixels[i].b > 0 || pixels[i].a > 0) {
					nonZeroPixels++;
					maxValue = Mathf.Max(maxValue, Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, Mathf.Max(pixels[i].b, pixels[i].a))));
				}
			}
			Debug.Log($"[TEXTURE_DEBUG] Slice {z} - Non-zero pixels: {nonZeroPixels}/{pixels.Length}, Max value: {maxValue}");
			
			byte[] bytes = slice.EncodeToPNG();
			string filePath = directoryPath + $"texture_slice_{z}.png";
			System.IO.File.WriteAllBytes(filePath, bytes);
			
			Object.DestroyImmediate(slice);
			sliceRT.Release();
			Object.DestroyImmediate(sliceRT);
		}
		
		Debug.Log($"[TEXTURE_DEBUG] Saved {depth} texture slices to {directoryPath}");
	}

	void LateUpdate()
	{
		// Volume rendering handles display - no need for agent drawing or colour kernel
		// NOTE: Volume texture is already updated in RunSimulation(), no need to copy again
		// Copy3DTexture(diffusedTrailMap, volumeTexture); // REMOVED - redundant copy
		
		// Save texture slices if debug mode is enabled
		if (debugMode) {
			SaveTextureSlices();
			debugMode = false; // Reset after saving
		}
	}
	
	void Copy3DTexture(RenderTexture source, RenderTexture target)
	{
		// Use a simple compute shader or Graphics.CopyTexture for 3D textures
		Graphics.CopyTexture(source, target);
		Debug.Log($"[SIMULATION] Copied 3D texture from {source.name} to {target.name}");
	}

	void RunSimulation()
	{
		var speciesSettings = settings.speciesSettings;
		ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
		compute.SetBuffer(updateKernel, "speciesSettings", settingsBuffer);
		compute.SetBuffer(colourKernel, "speciesSettings", settingsBuffer);

		// Assign settings
		compute.SetFloat("deltaTime", Time.fixedDeltaTime);
		compute.SetFloat("time", Time.fixedTime);

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);
		compute.SetInt("numSpecies", speciesSettings.Length);

		Debug.Log($"[SIMULATION] Running simulation - Agents: {settings.numAgents}, Dimensions: {settings.width}x{settings.height}x{settings.depth}");
		Debug.Log($"[SIMULATION] TrailWeight: {settings.trailWeight}, DecayRate: {settings.decayRate}, DiffuseRate: {settings.diffuseRate}");

		// Verify texture bindings before dispatch
		Debug.Log($"[SIMULATION] TrailMap: {(trailMap != null ? "Valid" : "NULL")}, DiffusedTrailMap: {(diffusedTrailMap != null ? "Valid" : "NULL")}");
		Debug.Log($"[SIMULATION] VolumeTexture: {(volumeTexture != null ? "Valid" : "NULL")}");

		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		Debug.Log($"[SIMULATION] Update kernel dispatched for {settings.numAgents} agents");
		
		ComputeHelper.Dispatch(compute, settings.width, settings.height, settings.depth, kernelIndex: diffuseKernel);
		Debug.Log($"[SIMULATION] Diffuse kernel dispatched for {settings.width}x{settings.height}x{settings.depth}");

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
		Debug.Log("[SIMULATION] Copied diffused trail map to trail map");
		
		// CRITICAL: Copy diffused trail map to volume texture for raymarching
		Graphics.CopyTexture(diffusedTrailMap, volumeTexture);
		Debug.Log("[SIMULATION] CRITICAL: Copied diffused trail map to volume texture for raymarching");
	}

	void OnDestroy()
	{

		ComputeHelper.Release(agentBuffer, settingsBuffer);
	}

	public struct Agent
	{
		public Vector3 position;
		public float angle;
		public float pitchAngle;  // New: vertical angle for 3D sensing
		public Vector3Int speciesMask;
		int unusedSpeciesChannel;
		public int speciesIndex;
	}


}
