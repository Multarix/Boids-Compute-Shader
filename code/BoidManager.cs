using Godot;
using Godot.Collections;
using System;

using SettingsGUI;
namespace Boids;

public partial class BoidManager : Node2D {
	private MultiMeshInstance2D MultiMeshInstance;
	private MultiMesh Multimesh;

	private Gui GUI;

	private RenderingDevice RD;
	private Rid Shader;
	private Rid UniformSet;
	private Rid Pipeline;

	private Rid VelocityBuffer;
	private Rid PositionsBuffer;
	private Rid OutputVelocityBuffer;
	private Rid OutputPositionsBuffer;
	private Rid GlobalBuffer;


	private Vector2 SCREEN_SIZE;


	[Export(PropertyHint.Range, "100,10000,1")]
	public float TOTAL_BOIDS = 5000.0f;

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

	private float[] BoidVelocity;
	private float[] BoidPositions;
	private float[] Globals;

	private byte[] OutputVelocityBytes;
	private byte[] OutputPositionBytes;

	private RandomNumberGenerator random = new RandomNumberGenerator();

	private void Setup() {
		BoidVelocity = new float[(int)TOTAL_BOIDS * 2];
		BoidPositions = new float[(int)TOTAL_BOIDS * 2];
		Globals = new float[12];

		SCREEN_SIZE = GetViewportRect().Size;

		MultiMeshInstance = GetNode<MultiMeshInstance2D>("Multi_Mesh");
		Multimesh = MultiMeshInstance.Multimesh;
		Multimesh.InstanceCount = (int)TOTAL_BOIDS;


		for(int i = 0; i < (int)TOTAL_BOIDS; i++) {
			int trueIndex = i * 2;
			float rot = Mathf.DegToRad(GD.Randf() * 360.0f);
			Vector2 pos = new Vector2(GD.Randf() * SCREEN_SIZE.X, GD.Randf() * SCREEN_SIZE.Y);
			Transform2D transform = new Transform2D(rot, pos);

			Vector2 vel = Vector2.Down.Rotated(rot);

			BoidPositions[trueIndex] = pos.X;
			BoidPositions[trueIndex + 1] = pos.Y;
			BoidVelocity[trueIndex] = vel.X;
			BoidVelocity[trueIndex + 1] = vel.Y;

			Multimesh.SetInstanceTransform2D(i, transform);
			Multimesh.SetInstanceColor(i, new Color(0, 1, random.RandfRange(0, 1), 1));
		}


		GUI.SetupBoundryLines(BOUNDRY_WIDTH, SCREEN_SIZE);

		BoundryBool = BOUNDRY_ENABLED ? 1.0f : 0.0f;

		UpdateGlobals();
	}


	private void InitGPU() {
		RD = RenderingServer.CreateLocalRenderingDevice();

		RDShaderFile ShaderFile = GD.Load<RDShaderFile>("res://code/shaders/compute.glsl");
		RDShaderSpirV ShaderBytecode = ShaderFile.GetSpirV();
		Shader = RD.ShaderCreateFromSpirV(ShaderBytecode);
	}


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


	private void InitBuffers() {
		byte[] VelocityBytes = new byte[BoidVelocity.Length * 4];
		Buffer.BlockCopy(BoidVelocity, 0, VelocityBytes, 0, VelocityBytes.Length);

		byte[] PositionBytes = new byte[BoidPositions.Length * 4];
		Buffer.BlockCopy(BoidPositions, 0, PositionBytes, 0, PositionBytes.Length);

		byte[] GlobalBytes = new byte[Globals.Length * 4];
		Buffer.BlockCopy(Globals, 0, GlobalBytes, 0, GlobalBytes.Length);


		float[] InitialBytes = new float[BoidVelocity.Length];
		OutputVelocityBytes = new byte[BoidVelocity.Length * 4];
		OutputPositionBytes = new byte[BoidPositions.Length * 4];

		for (int i = 0; i < InitialBytes.Length; i++) {
			InitialBytes[i] = 0.0f;
		}

		Buffer.BlockCopy(InitialBytes, 0, OutputVelocityBytes, 0, OutputVelocityBytes.Length);
		Buffer.BlockCopy(InitialBytes, 0, OutputPositionBytes, 0, OutputPositionBytes.Length);




		VelocityBuffer = RD.StorageBufferCreate((uint)VelocityBytes.Length, VelocityBytes);
		RDUniform VelocityUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 0
		};
		VelocityUniform.AddId(VelocityBuffer);


		PositionsBuffer = RD.StorageBufferCreate((uint)PositionBytes.Length, PositionBytes);
		RDUniform PositionUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 1
		};
		PositionUniform.AddId(PositionsBuffer);


		GlobalBuffer = RD.StorageBufferCreate((uint)GlobalBytes.Length, GlobalBytes);
		RDUniform GlobalUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 4
		};
		GlobalUniform.AddId(GlobalBuffer);



		// Output Buffers
		OutputVelocityBuffer = RD.StorageBufferCreate((uint)OutputVelocityBytes.Length, OutputVelocityBytes);
		RDUniform OutputVelocityUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 2
		};
		OutputVelocityUniform.AddId(OutputVelocityBuffer);


		OutputPositionsBuffer = RD.StorageBufferCreate((uint)OutputPositionBytes.Length, OutputPositionBytes);
		RDUniform OutputPositionsUniform = new RDUniform() {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 3
		};
		OutputPositionsUniform.AddId(OutputPositionsBuffer);



		UniformSet = RD.UniformSetCreate(new Array<RDUniform> { VelocityUniform, PositionUniform, OutputVelocityUniform, OutputPositionsUniform, GlobalUniform }, Shader, 0);
		Pipeline = RD.ComputePipelineCreate(Shader);
	}


	private void UpdateBuffers() {
		UpdateGlobals();


		byte[] VelocityBytes = new byte[BoidVelocity.Length * 4];
		Buffer.BlockCopy(BoidVelocity, 0, VelocityBytes, 0, VelocityBytes.Length);

		byte[] PositionBytes = new byte[BoidPositions.Length * 4];
		Buffer.BlockCopy(BoidPositions, 0, PositionBytes, 0, PositionBytes.Length);

		byte[] GlobalBytes = new byte[Globals.Length * 4];
		Buffer.BlockCopy(Globals, 0, GlobalBytes, 0, GlobalBytes.Length);


		RD.BufferUpdate(VelocityBuffer, 0, (uint)VelocityBytes.Length, VelocityBytes);
		RD.BufferUpdate(PositionsBuffer, 0, (uint)PositionBytes.Length, PositionBytes);
		RD.BufferUpdate(GlobalBuffer, 0, (uint)GlobalBytes.Length, GlobalBytes);

		RD.BufferUpdate(OutputVelocityBuffer, 0, (uint)OutputVelocityBytes.Length, OutputVelocityBytes);
		RD.BufferUpdate(OutputPositionsBuffer, 0, (uint)OutputPositionBytes.Length, OutputPositionBytes);
	}


	private void SubmitToGPU() {
		long ComputeList = RD.ComputeListBegin();

		RD.ComputeListBindComputePipeline(ComputeList, Pipeline);
		RD.ComputeListBindUniformSet(ComputeList, UniformSet, 0);
		RD.ComputeListDispatch(ComputeList, xGroups: (uint)TOTAL_BOIDS, yGroups: 1, zGroups: 1);
		RD.ComputeListEnd();

		RD.Submit();
		RD.Sync();
	}


	// Gets the results from the GPU and copies them into the appropriate arrays
	private void GetResultsFromGPU() {
		byte[] PositionBytes = RD.BufferGetData(OutputPositionsBuffer);
		byte[] VelocityBytes = RD.BufferGetData(OutputVelocityBuffer);

		Buffer.BlockCopy(PositionBytes, 0, BoidPositions, 0, PositionBytes.Length);
		Buffer.BlockCopy(VelocityBytes, 0, BoidVelocity, 0, VelocityBytes.Length);
	}


	// Updates the boids positions and rotations, (Parallel may or may not be faster?)
	private void UpdateBoidPositions() {
		for(int i = 0; i < (int)TOTAL_BOIDS; i++) {
			int arrayIndex = i * 2;

			float posX = BoidPositions[arrayIndex];
			float posY = BoidPositions[arrayIndex + 1];

			if(!BOUNDRY_ENABLED){
				posX = Mathf.Wrap(posX, 0.0f, SCREEN_SIZE.X);
				posY = Mathf.Wrap(posY, 0.0f, SCREEN_SIZE.Y);

				BoidPositions[arrayIndex] = posX;
				BoidPositions[arrayIndex + 1] = posY;
			}
			
			Vector2 pos = new Vector2(posX, posY);
			Vector2 vel = new Vector2(BoidVelocity[arrayIndex], BoidVelocity[arrayIndex + 1]);

			float lastRotation = Multimesh.GetInstanceTransform2D(i).Rotation;
			float newRotation = Vector2.Down.AngleTo(vel);

			float rot = (lastRotation + newRotation) / 2.0f;

			Transform2D transform = new Transform2D(rot, pos);
			Multimesh.SetInstanceTransform2D(i, transform);
		};

		// GD.Print("Debug | Loc:", BoidPositions[0], " ", BoidPositions[1]);
		// GD.Print("Debug | Vel:", BoidVelocity[0], " ", BoidVelocity[1]);
	}


	public override void _Ready() {
		GUI = GetNode<Gui>("GUI");
		GUI.Setup(VISUAL_RANGE, SEPERATION_DISTANCE, MOVEMENT_SPEED, COHESION, ALIGNMENT, SEPERATION);
		Setup();
	}


	public override void _Process(double delta) {
		if(RD == null) {
			InitGPU();
			InitBuffers();
		} else {
			UpdateBuffers();
		}

		SubmitToGPU();
		GetResultsFromGPU();
		UpdateBoidPositions();
	}
}