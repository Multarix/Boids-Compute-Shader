using Godot;
using Godot.Collections;
using SettingsGUI;
using System;
namespace Boids;

public partial class BoidManager : Node2D {
	private MultiMeshInstance2D MultiMeshInstance;
	private MultiMesh Multimesh;
	private Rid MultiMeshRID;

	private Gui GUI;

	private RenderingDevice RD;
	private Rid Shader;
	private Rid UniformSet;
	private Rid Pipeline;
	
	private Rid DataBuffer;
	private Rid OutputDataBuffer;
	private Rid VelocityBuffer;
	private Rid OutputVelocityBuffer;
	private Rid GlobalBuffer;

	private Vector2 SCREEN_SIZE;
	
	[Export(PropertyHint.Range, "256,64000,64")]
	public float TOTAL_BOIDS = 6400;

	[ExportSubgroup("Boid Senses")]
	[Export]
	public float VISUAL_RANGE = 40.0f;
	[Export]
	public float SEPERATION_DISTANCE = 8.0f;
	[Export]
	public float MOVEMENT_SPEED = 2.0f;

	[ExportSubgroup("Boundry Settings")]
	[Export]
	public bool BOUNDRY_ENABLED = true;
	[Export]
	public float BOUNDRY_WIDTH = 100.0f;
	[Export]
	public float BOUNDRY_TURN = 0.2f;

	[ExportSubgroup("Initial Weights")]
	[Export]
	public float SEPERATION = 0.05f;
	[Export]
	public float ALIGNMENT = 0.05f;
	[Export]
	public float COHESION = 0.0005f;

	public float BoundryBool;


	// Arrays for our data.
	private byte[] BoidDataBytes;
	private byte[] BoidVelocityBytes;
	private float[] Globals;


	private void Setup() {
		
		// Set up our Arrays
		BoidDataBytes = new byte[(int)TOTAL_BOIDS * 4 * 12];
		BoidVelocityBytes = new byte[(int)TOTAL_BOIDS * 4 * 2];
		
		float[] BoidData;
		float[] BoidVelocity = new float[(int)TOTAL_BOIDS * 2];
		Globals = new float[12];

		// Get the size of the screen
		// this is done this way so we can change the display size later if we wanted.
		SCREEN_SIZE = GetViewportRect().Size;

		// Setup and store the multimesh as a variable.
		MultiMeshInstance = GetNode<MultiMeshInstance2D>("Multi_Mesh");
		Multimesh = MultiMeshInstance.Multimesh;
		MultiMeshRID = Multimesh.GetRid();
		Multimesh.InstanceCount = (int)TOTAL_BOIDS;


		// Loop until we reached the total number of boids
		for (int i = 0; i < (int)TOTAL_BOIDS; i++) {
			// The true index is the index of the array * 2, cause we store the x pos and y pos in the same array
			// You could modify this to use a vector2, but Godot and C# don't have methods to convert bytes to vectors.
			int trueIndex = i * 2;

			// Get a random rotation and position
			float rot = Mathf.DegToRad(GD.Randf() * 360.0f);
			Vector2 pos = new Vector2(GD.Randf() * SCREEN_SIZE.X, GD.Randf() * SCREEN_SIZE.Y);
			Transform2D transform = new Transform2D(rot, pos);

			// Because of how I made the mesh, we rotate the down direction so it's facing in the direction of its velocity.
			Vector2 vel = Vector2.Down.Rotated(rot);

			// Save the velocities to the array.
			BoidVelocity[trueIndex] = vel.X;
			BoidVelocity[trueIndex + 1] = vel.Y;

			// Apply the data to the multimesh instance. We can grab the entire buffer afterwards.
			Multimesh.SetInstanceTransform2D(i, transform);
			Multimesh.SetInstanceColor(i, Colors.White);
		}

		// Setup the location of the boundry lines.
		// The boundry lines are where the boids will start turning around if they exceed it.
		GUI.SetupBoundryLines(BOUNDRY_WIDTH, SCREEN_SIZE);

		// Because I'm lazy, I'm turning the bool into a float so I can hand it over with the other variables for the compute shader
		BoundryBool = BOUNDRY_ENABLED ? 1.0f : 0.0f;
		
		// Setup Globals
		UpdateGlobals();

		// We can now grab the Buffer from the multimesh...
		BoidData = RenderingServer.MultimeshGetBuffer(MultiMeshRID);

		// Then turn everything into bytes.
		Buffer.BlockCopy(BoidData, 0, BoidDataBytes, 0, BoidDataBytes.Length);
		Buffer.BlockCopy(BoidVelocity, 0, BoidVelocityBytes, 0, BoidVelocityBytes.Length);
	}



	// Create the rendering device and the shader.
	private void InitGPU() {
		// All this stuff is available in the godot documentation
		RD = RenderingServer.CreateLocalRenderingDevice();

		RDShaderFile ShaderFile = GD.Load<RDShaderFile>("res://code/shaders/compute.glsl");
		RDShaderSpirV ShaderBytecode = ShaderFile.GetSpirV();
		Shader = RD.ShaderCreateFromSpirV(ShaderBytecode);
	}



	// Easy way to update the globals array
	// Yes it's ugly. No I don't care.
	private void UpdateGlobals() {
		Globals[0] = VISUAL_RANGE;
		Globals[1] = SEPERATION_DISTANCE;
		Globals[2] = SEPERATION;
		Globals[3] = ALIGNMENT;
		Globals[4] = COHESION;
		Globals[5] = MOVEMENT_SPEED;
		Globals[6] = TOTAL_BOIDS;
		Globals[7] = BOUNDRY_WIDTH;
		Globals[8] = BoundryBool;
		Globals[9] = SCREEN_SIZE.X;
		Globals[10] = SCREEN_SIZE.Y;
		Globals[11] = BOUNDRY_TURN;
	}



	// Initialize the buffers and uniform sets that will be used by the compute shader.
	private void InitBuffers() {
		byte[] GlobalBytes = new byte[Globals.Length * 4];
		Buffer.BlockCopy(Globals, 0, GlobalBytes, 0, GlobalBytes.Length);
		
		byte[] OutputVelocityBytes = new byte[BoidVelocityBytes.Length];
		Buffer.BlockCopy(BoidVelocityBytes, 0, OutputVelocityBytes, 0, OutputVelocityBytes.Length);
		
		byte[] OutputDataBytes = new byte[BoidDataBytes.Length];
		Buffer.BlockCopy(BoidDataBytes, 0, OutputDataBytes, 0, OutputDataBytes.Length);
		
		
		// Restrict, Readonly
		DataBuffer = RD.StorageBufferCreate((uint)BoidDataBytes.Length, BoidDataBytes);
		RDUniform DataUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 0
		};
		DataUniform.AddId(DataBuffer);
		
		
		// Restrict
		OutputDataBuffer = RD.StorageBufferCreate((uint)OutputDataBytes.Length, OutputDataBytes);
		RDUniform OutputDataUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 1
		};
		OutputDataUniform.AddId(OutputDataBuffer);
		
		// Restrict, Readonly
		VelocityBuffer = RD.StorageBufferCreate((uint)BoidVelocityBytes.Length, BoidVelocityBytes);
		RDUniform VelocityUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 2
		};
		VelocityUniform.AddId(VelocityBuffer);
		
		
		// Restrict
		OutputVelocityBuffer = RD.StorageBufferCreate((uint)OutputVelocityBytes.Length, OutputVelocityBytes);
		RDUniform OutputVelocityUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 3
		};
		OutputVelocityUniform.AddId(OutputVelocityBuffer);


		GlobalBuffer = RD.StorageBufferCreate((uint)GlobalBytes.Length, GlobalBytes);
		RDUniform GlobalUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 4
		};
		GlobalUniform.AddId(GlobalBuffer);


		// Setup the uniform set and pipeline.
		UniformSet = RD.UniformSetCreate(new Array<RDUniform> { DataUniform, OutputDataUniform, VelocityUniform, OutputVelocityUniform, GlobalUniform }, Shader, 0);
		Pipeline = RD.ComputePipelineCreate(Shader);
	}



	// Update the buffers with the new data.
	private void UpdateBuffers() {
		// Update global variables...
		UpdateGlobals();

		byte[] GlobalBytes = new byte[Globals.Length * 4];
		Buffer.BlockCopy(Globals, 0, GlobalBytes, 0, GlobalBytes.Length);

		// Update the buffers...
		_ = RD.BufferUpdate(DataBuffer, 0, (uint)BoidDataBytes.Length, BoidDataBytes);
		_ = RD.BufferUpdate(VelocityBuffer, 0, (uint)BoidVelocityBytes.Length, BoidVelocityBytes);
		_ = RD.BufferUpdate(GlobalBuffer, 0, (uint)GlobalBytes.Length, GlobalBytes);
	}



	// Submits the data to the GPU, then waits for it to finish.
	private void SubmitToGPU() {
		uint WorkGroupSize = (uint)TOTAL_BOIDS / 64;

		// Create the compute list, and all that good stuff.
		long ComputeList = RD.ComputeListBegin();

		RD.ComputeListBindComputePipeline(ComputeList, Pipeline);
		RD.ComputeListBindUniformSet(ComputeList, UniformSet, 0);
		RD.ComputeListDispatch(ComputeList, xGroups: WorkGroupSize, yGroups: 1, zGroups: 1);
		RD.ComputeListEnd();

		// Submit and sync.
		RD.Submit();

		// I see everywhere people saying "wait a few frames..."
		// But there is no documentation for "waiting a few frames" ¯\_(ツ)_/¯
		// The best is waiting till the next frame, any longer and it just crashes.
		RD.Sync();
	}



	// Gets the results from the GPU and applies it to the multimesh.
	private void GetResultsFromGPU() {
		BoidDataBytes = RD.BufferGetData(OutputDataBuffer);
		BoidVelocityBytes = RD.BufferGetData(OutputVelocityBuffer);
		
		float[] ConvertedBoidData = new float[BoidDataBytes.Length / 4];
		Buffer.BlockCopy(BoidDataBytes, 0, ConvertedBoidData, 0, BoidDataBytes.Length);
		
		
		// GD.PrintT("Updated Set:", ConvertedBoidData.Length);
		// GD.PrintT("X:");
		// GD.PrintT(ConvertedBoidData[0], ConvertedBoidData[1], ConvertedBoidData[2], ConvertedBoidData[3]);
		// GD.PrintT("Y:");
		// GD.PrintT(ConvertedBoidData[4], ConvertedBoidData[5], ConvertedBoidData[6], ConvertedBoidData[7]);
		// GD.PrintT("Color:");
		// GD.PrintT(ConvertedBoidData[8], ConvertedBoidData[9], ConvertedBoidData[10], ConvertedBoidData[11]);
		
		
		// No need to use a for loop, Our compute shader outputs the data in a valid format for this.
		// This would be even better if we didn't have to do GPU -> CPU -> GPU. Oh well.
		// Still, this method is great! It's a lot faster than using a for loop.
		RenderingServer.MultimeshSetBuffer(MultiMeshRID, ConvertedBoidData);
	}



	// Initial setups for the GUI and what have yous.
	public override void _Ready() {
		CanvasLayer Canvas = GetNode<CanvasLayer>("CanvasLayer");
		GUI = Canvas.GetNode<Gui>("GUI");
		GUI.Setup(VISUAL_RANGE, SEPERATION_DISTANCE, MOVEMENT_SPEED, COHESION, ALIGNMENT, SEPERATION, TOTAL_BOIDS);
		Setup();
	}



	// So interestingly enough, putting everything in "_Process" rather than "_PhysicsProcess" is faster.
	public override void _Process(double delta) {
		// Setup the GPU if it's not already setup, otherwise update the buffers..
		if (RD == null) {
			InitGPU();
			InitBuffers();
		} else {
			UpdateBuffers();
		}

		// I think this is self explanatory?
		SubmitToGPU();
		GetResultsFromGPU();
	}
}