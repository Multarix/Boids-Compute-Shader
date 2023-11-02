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
	private Rid GlobalBuffer;

	private Rid HashLookupBuffer;
	private Rid HashUpdateBuffer;
	private Rid HashSizeBuffer;

	private Vector2 SCREEN_SIZE;
	
	[Export(PropertyHint.Range, "256,96000,64")]
	public float TOTAL_BOIDS = 64000;
	[Export(PropertyHint.Range, "1,10,1")]
	public int NUMBER_of_FLOCKS = 1;
	[Export(PropertyHint.Range, "1,3,0.1")]
	public float BOID_SIZE = 1.5f;

	[ExportSubgroup("Boid Senses")]
	[Export]
	public float VISUAL_RANGE = 60.0f;
	[Export]
	public float SEPERATION_DISTANCE = 8.0f;
	[Export]
	public float MOVEMENT_SPEED = 1.25f;

	[ExportSubgroup("Boundry Settings")]
	[Export]
	public bool BOUNDRY_ENABLED = true;
	[Export]
	public float BOUNDRY_WIDTH = -5.0f;
	[Export]
	public float BOUNDRY_TURN = 0.25f;

	[ExportSubgroup("Initial Weights")]
	[Export]
	public float SEPERATION = 0.05f;
	[Export]
	public float ALIGNMENT = 0.05f;
	[Export]
	public float COHESION = 0.0005f;

	public float BoundryBool;
	
	private int TotalCells;

	// Arrays for our data.
	private byte[] BoidDataBytes;
	private float[] Globals;

	// Arrays for our spatial hashing.
	private byte[] HashLookup;
	private byte[] HashUpdate;
	private byte[] HashSize;


	private RandomNumberGenerator RNG = new RandomNumberGenerator();

	private void BuildMesh() {
		Vector3[] points = new Vector3[]{
			new Vector3(-1 * BOID_SIZE, -1 * BOID_SIZE, 0),
			new Vector3(-1 * BOID_SIZE, 1 * BOID_SIZE, 0),
			new Vector3(2 * BOID_SIZE, 0, 0),
		};

		ArrayMesh arrayMesh = new ArrayMesh();
		Godot.Collections.Array arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = points;

		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		Multimesh.Mesh = arrayMesh;
	}


	private void Setup() {
		// Get the size of the screen, and the total cells
		SCREEN_SIZE = GetViewportRect().Size;
		int TotalColumns = (int)Math.Ceiling(SCREEN_SIZE.X / 60.0);
		int TotalRows = (int)Math.Ceiling(SCREEN_SIZE.Y / 60.0);
		TotalCells = TotalColumns * TotalRows;

		RNG.Randomize();

		// Initialize the arrays
		BoidDataBytes = new byte[(int)TOTAL_BOIDS * 4 * 16]; // Each boid is 16 floats, 1 float = 4 bytes.
		Globals = new float[13]; // 13 floats for the globals.

		// 64 is the max number of boids per cell, we're just going to store their ID.
		HashLookup = new byte[TotalCells * 4 * 64]; 
		HashUpdate = new byte[TotalCells * 4 * 64];

		HashSize = new byte[TotalCells * 4];


		int[] HashSizeInts = new int[TotalCells];
		System.Array.Fill(HashSizeInts, 0);
		Buffer.BlockCopy(HashSizeInts, 0, HashSize, 0, HashSize.Length);
		
		int[] HashLookupInts = new int[TotalCells * 64];
		System.Array.Fill(HashLookupInts, -1);
		Buffer.BlockCopy(HashLookupInts, 0, HashLookup, 0, HashLookup.Length);
		Buffer.BlockCopy(HashLookupInts, 0, HashUpdate, 0, HashUpdate.Length);
		

		// Setup and store the multimesh as a variable.
		MultiMeshInstance = GetNode<MultiMeshInstance2D>("Multi_Mesh");
		Multimesh = MultiMeshInstance.Multimesh;
		MultiMeshRID = Multimesh.GetRid();
		Multimesh.InstanceCount = (int)TOTAL_BOIDS;

		BuildMesh();

		// This can 100% be Parallel.For'd, but I'd have to make the buffer like in the compute shader... Faster, but this only happens once.
		// Loop until we reached the total number of boids
		for (int i = 0; i < (int)TOTAL_BOIDS; i++) {
			// Get a random rotation and position
			float rot = Mathf.DegToRad(GD.Randf() * 360.0f);
			Vector2 pos = new Vector2(GD.Randf() * SCREEN_SIZE.X, GD.Randf() * SCREEN_SIZE.Y);
			Transform2D transform = new Transform2D(rot, pos);

			float Row = (float)Math.Floor(pos.Y / 60.0f);
			float Column = (float)Math.Floor(pos.X / 60.0f);
		
			float SpatialBinID = (Row * TotalColumns) + Column;

			// Because of how I made the mesh, we rotate the down direction so it's facing in the direction of its velocity.
			Vector2 vel = Vector2.Right.Rotated(rot);

			float FlockID = (float)RNG.RandiRange(0, NUMBER_of_FLOCKS - 1);

			// Apply the data to the multimesh instance. We can grab the entire buffer afterwards.
			Multimesh.SetInstanceTransform2D(i, transform);
			Multimesh.SetInstanceColor(i, Colors.White);
			Multimesh.SetInstanceCustomData(i, new Color(vel.X, vel.Y, FlockID, SpatialBinID));
		}
		
		// Don't care about the spatial bin on the first frame, less headache.

		// Setup the location of the boundry lines.
		// The boundry lines are where the boids will start turning around if they exceed it.
		GUI.SetupBoundryLines(BOUNDRY_WIDTH, SCREEN_SIZE);

		// Because I'm lazy, I'm turning the bool into a float so I can hand it over with the other variables for the compute shader
		BoundryBool = BOUNDRY_ENABLED ? 1.0f : 0.0f;
		
		// Setup Globals
		UpdateGlobals();

		// We can now grab the Buffer from the multimesh...
		float[] BoidData = RenderingServer.MultimeshGetBuffer(MultiMeshRID);

		// Then turn everything into bytes.
		Buffer.BlockCopy(BoidData, 0, BoidDataBytes, 0, BoidDataBytes.Length);

		Buffer.BlockCopy(HashLookupInts, 0, HashLookup, 0, HashLookup.Length);
		
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
		Globals[12] = (float)GetPhysicsProcessDeltaTime();
	}



	// Initialize the buffers and uniform sets that will be used by the compute shader.
	private void InitBuffers() {
		byte[] GlobalBytes = new byte[Globals.Length * 4];
		Buffer.BlockCopy(Globals, 0, GlobalBytes, 0, GlobalBytes.Length);
		
		
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
		GlobalBuffer = RD.StorageBufferCreate((uint)GlobalBytes.Length, GlobalBytes);
		RDUniform GlobalUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 5
		};
		GlobalUniform.AddId(GlobalBuffer);


		// Spatial Hashing
		// Restrict, readonly
		HashLookupBuffer = RD.StorageBufferCreate((uint)HashLookup.Length, HashLookup);
		RDUniform HashLookupUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 2
		};
		HashLookupUniform.AddId(HashLookupBuffer);
		
		// Restrict
		HashUpdateBuffer = RD.StorageBufferCreate((uint)HashUpdate.Length, HashUpdate);
		RDUniform HashUpdateUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 3
		};
		HashUpdateUniform.AddId(HashUpdateBuffer);
		
		// Restrict
		HashSizeBuffer = RD.StorageBufferCreate((uint)HashSize.Length, HashSize);
		RDUniform HashSizeUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 4
		};
		HashSizeUniform.AddId(HashSizeBuffer);
		


		// Setup the uniform set and pipeline.
		UniformSet = RD.UniformSetCreate(new Array<RDUniform> { DataUniform, OutputDataUniform, GlobalUniform, HashLookupUniform, HashUpdateUniform, HashSizeUniform }, Shader, 0);
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
		_ = RD.BufferUpdate(GlobalBuffer, 0, (uint)GlobalBytes.Length, GlobalBytes);

		_ = RD.BufferUpdate(HashLookupBuffer, 0, (uint)HashLookup.Length, HashLookup);
		_ = RD.BufferUpdate(HashUpdateBuffer, 0, (uint)HashUpdate.Length, HashUpdate);
		_ = RD.BufferUpdate(HashSizeBuffer, 0, (uint)HashSize.Length, HashSize);
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
		HashLookup = RD.BufferGetData(HashUpdateBuffer);
		
		float[] ConvertedBoidData = new float[BoidDataBytes.Length / 4];
		Buffer.BlockCopy(BoidDataBytes, 0, ConvertedBoidData, 0, BoidDataBytes.Length);


		//int id = 0;
		//GD.PrintT("Updated Set:", ConvertedBoidData.Length);
		//GD.PrintT("X:");
		//GD.PrintT(ConvertedBoidData[(id * 16) + 0], ConvertedBoidData[(id * 16) + 1], ConvertedBoidData[(id * 16) + 2], ConvertedBoidData[(id * 16) + 3]);
		//GD.PrintT("Y:");
		//GD.PrintT(ConvertedBoidData[(id * 16) + 4], ConvertedBoidData[(id * 16) + 5], ConvertedBoidData[(id * 16) + 6], ConvertedBoidData[(id * 16) + 7]);
		//GD.PrintT("Color:");
		//GD.PrintT(ConvertedBoidData[(id * 16) + 8], ConvertedBoidData[(id * 16) + 9], ConvertedBoidData[(id * 16) + 10], ConvertedBoidData[(id * 16) + 11]);
		//GD.PrintT("Custom:");
		//GD.PrintT(ConvertedBoidData[(id * 16) + 12], ConvertedBoidData[(id * 16) + 13], ConvertedBoidData[(id * 16) + 14], ConvertedBoidData[(id * 16) + 15]);

		//int[] ConvertedHashData = new int[HashUpdate.Length / 4];
		//Buffer.BlockCopy(HashUpdate, 0, ConvertedHashData, 0, HashUpdate.Length);

		//int[] CellData = new int[64];
		//for(int i = 0; i < 64; i++) {
		//	ConvertedHashData[i] = CellData[i];
		//}

		//GD.PrintT("HashUpdate:");
		//GD.PrintT(CellData[0]);


		// No need to use a for loop, Our compute shader outputs the data in a valid format for this.
		// This would be even better if we didn't have to do GPU -> CPU -> GPU. Oh well.
		// Still, this method is great! It's a lot faster than using a for loop.
		RenderingServer.MultimeshSetBuffer(MultiMeshRID, ConvertedBoidData);
	}



	// Clean up the GPU
	public void FinGPU() {
		if(RD == null) return;

		RD.FreeRid(Pipeline);
		RD.FreeRid(UniformSet);
		RD.FreeRid(DataBuffer);
		RD.FreeRid(OutputDataBuffer);
		RD.FreeRid(GlobalBuffer);
		RD.FreeRid(Shader);
		RD.Free();
		RD = null;
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



	// Handle quitting, and freeing the Rids.
	public override void _Notification(int what) {
		if(what == NotificationWMCloseRequest) FinGPU();
	}
}