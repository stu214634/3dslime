using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class SlimeSettings : ScriptableObject
{
	[Header("Simulation Settings")]
	[Min(1)] public int stepsPerFrame = 1;
	[Header("World Dimensions")]
	public int width = 1280;
	public int height = 720;
	public int numAgents = 100;
	public Simulation.SpawnMode spawnMode;

	[Header("Trail Settings")]
	public float trailWeight = 1;
	public float decayRate = 1;
	public float diffuseRate = 1;
	
	[Header("Health & Evolution")]
	[Range(0f, 2f)] public float starvationRate = 0.1f;

	public SpeciesSettings[] speciesSettings;

	[System.Serializable]
	public struct SpeciesSettings
	{
		[Header("Movement Settings")]
		public float moveSpeed;
		public float turnSpeed;

		[Header("Sensor Settings")]
		public float sensorAngleDegrees;
		public float sensorOffsetDst;
		[Min(1)] public int sensorSize;

		[Header("Display settings")]
		public Color colour;
	}
}
