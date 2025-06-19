using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class Simulation : MonoBehaviour
{
	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

	const int updateKernel = 0;
	const int diffuseMapKernel = 1;
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
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;
	public Material slimeMaterial;
	
	// Public accessor for the slime texture
	public RenderTexture SlimeTexture => displayTexture;

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	Texture2D colourMapTexture;

	[Header("Debug")]
	public bool debugMode;

	protected virtual void Start()
	{
		Init();
		// Assign slime material to renderer
		var renderer = transform.GetComponentInChildren<MeshRenderer>();
		if (renderer != null && slimeMaterial != null) {
			renderer.material = slimeMaterial;
		}
	}

	void Init()
	{
		// Create 2D render textures (width, height only)
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);
		
		// Initialize slime material if not set
		if (slimeMaterial == null) {
			slimeMaterial = new Material(Shader.Find("Custom/Slime2D"));
		}

		// Assign textures to kernels
		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);
		compute.SetTexture(colourKernel, "ColourMap", displayTexture);
		compute.SetTexture(colourKernel, "TrailMap", trailMap);
		
		Debug.Log($"[SIMULATION] Kernel indices - Update: {updateKernel}, Diffuse: {diffuseMapKernel}");
		Debug.Log($"[SIMULATION] Texture bindings set for kernels");
		
		Debug.Log($"[SIMULATION] Textures created - TrailMap: {trailMap.width}x{trailMap.height}, Format: {trailMap.graphicsFormat}");
		Debug.Log($"[SIMULATION] DiffusedTrailMap: {diffusedTrailMap.width}x{diffusedTrailMap.height}");
		Debug.Log($"[SIMULATION] DisplayTexture: {displayTexture.width}x{displayTexture.height}");
		
		// Connect texture to slime material
		if (slimeMaterial != null) {
			slimeMaterial.SetTexture("_SlimeTexture", displayTexture);
		}

		// Create agents with initial 2D positions and angles
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
			Vector2 startPos = Vector2.zero;
			float randomAngle = Random.value * Mathf.PI * 2;
			float angle = 0;

			if (settings.spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.Random)
			{
				startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.5f;
				angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
			}
			else if (settings.spawnMode == SpawnMode.RandomCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.15f;
				angle = randomAngle;
			}

			Vector4 speciesMask;
			int speciesIndex = 0;
			int numSpecies = settings.speciesSettings.Length;

			if (numSpecies == 1)
			{
				speciesMask = Vector4.one;
			}
			else
			{
				int species = Random.Range(1, numSpecies + 1);
				speciesIndex = species - 1;
				speciesMask = new Vector4((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0, (species == 4) ? 1 : 0);
			}

					agents[i] = new Agent() { 
			position = startPos, 
			angle = angle, 
			speciesMask = speciesMask, 
			speciesIndex = speciesIndex,
			health = 1.0f,
			neuralWeights = new Vector3(
				Random.Range(-0.5f, 0.5f), // Forward sensor weight
				Random.Range(-0.5f, 0.5f), // Left sensor weight  
				Random.Range(-0.5f, 0.5f)  // Right sensor weight
			)
		};
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
		compute.SetInt("numAgents", settings.numAgents);
		
		if (drawAgentsCS != null)
		{
			drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
			drawAgentsCS.SetInt("numAgents", settings.numAgents);
		}

		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);
	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}



	void LateUpdate()
	{
		if (showAgentsOnly)
		{
			ComputeHelper.ClearRenderTexture(displayTexture);
			
			if (drawAgentsCS != null)
			{
				drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
				ComputeHelper.Dispatch(drawAgentsCS, settings.numAgents, 1, 1, 0);
			}
		}
		else
		{
			ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: colourKernel);
		}
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
		compute.SetFloat("starvationRate", settings.starvationRate);
		compute.SetInt("numSpecies", speciesSettings.Length);

		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
	}

	void OnDestroy()
	{
		ComputeHelper.Release(agentBuffer, settingsBuffer);
	}

	[System.Serializable]
	public struct Agent
	{
		public Vector2 position;
		public float angle;
		public Vector4 speciesMask;
		public int speciesIndex;
		public float health;
		public Vector3 neuralWeights; // 3 sensor inputs -> 1 steering output
	}
}
