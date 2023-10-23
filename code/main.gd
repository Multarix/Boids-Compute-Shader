extends Node2D

@onready var _multiInstance = $MultiMesh;
var multiMesh: MultiMesh;


# Render Devices
var rd: RenderingDevice
var shader: RID;
var uniformSet: RID;
var pipeline: RID;

var velocityBuffer: RID;
var positionsBuffer: RID;
var outputVelocityBuffer: RID;
var outputPositionsBuffer: RID;
var globalBuffer: RID;

var SCREEN_SIZE: Vector2;

var boidVelocity := [];		# Floats
var boidPositions := [];	# Floats
var globals := [];			# Floats


var packedOutput: PackedByteArray;

@export_category("Script Settings")
@export_range(100, 6000, 1, "show_slider") var TOTAL_BOIDS: float = 6000.0;


@export_subgroup("Boid Senses")
@export var VISUAL_RANGE: float = 40.0;
@export var SEPERATION_DISTANCE: float = 8.0;
@export var MOVEMENT_SPEED: float = 2.0;


## Boundry is the distance from the edge of the screen, before the boids turn around.
@export_subgroup("Boundry")
@export var BOUNDRY_ENABLED: bool = true;
@export var BOUNDRY_WIDTH: float = 100;
@export var BOUNDRY_TURN: float = 0.2;


@export_subgroup("Weights")
@export var SEPERATION: float = 0.05;
@export var ALIGNMENT: float = 0.05;
@export var COHESION: float = 0.0005;


var boundryBool: float;


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	$GUI._setup(VISUAL_RANGE, SEPERATION_DISTANCE, MOVEMENT_SPEED, COHESION, ALIGNMENT, SEPERATION)
	setup();
# End Function



var random = RandomNumberGenerator.new();
func setup() -> void:
	SCREEN_SIZE = get_viewport_rect().size;
	
	# Set up the multimesh;
	multiMesh = _multiInstance.multimesh;
	multiMesh.set_instance_count(int(TOTAL_BOIDS));
	
	for i in range(0, TOTAL_BOIDS):
		var rot = deg_to_rad(randf() * 360);
		var pos = Vector2(randf() * SCREEN_SIZE.x, randf() * SCREEN_SIZE.y);
		var trans = Transform2D(rot, pos);
		
		var vel = Vector2.RIGHT.rotated(rot).normalized();
		
		boidVelocity.append_array([vel.x, vel.y]);
		boidPositions.append_array([pos.x, pos.y]);
		
		multiMesh.set_instance_transform_2d(i, trans);
		multiMesh.set_instance_color(i, Color(0, 1, random.randf_range(0.05, 1), 1))
	# End for
	
	$GUI._setupBoundryLines(BOUNDRY_WIDTH, SCREEN_SIZE);
	
	if(BOUNDRY_ENABLED):
		boundryBool = 1;
	else:
		boundryBool = 0;
	# End If/Else
	
	globals = [
		VISUAL_RANGE, SEPERATION_DISTANCE, SEPERATION,
		ALIGNMENT, COHESION, MOVEMENT_SPEED,
		TOTAL_BOIDS, BOUNDRY_WIDTH, boundryBool,
		SCREEN_SIZE.x, SCREEN_SIZE.y, BOUNDRY_TURN
	];

# End Function



func initGPU() -> void:
	rd = RenderingServer.create_local_rendering_device();
	
	var shader_file = load("res://code/shaders/compute.glsl");
	var shader_spirv: RDShaderSPIRV = shader_file.get_spirv();
	shader = rd.shader_create_from_spirv(shader_spirv);
# End function



func initBuffers() -> void:
	var packedVelocity = PackedFloat32Array(boidVelocity).to_byte_array();
	var packedPositions = PackedFloat32Array(boidPositions).to_byte_array();	
	var packedGlobals = PackedFloat32Array(globals).to_byte_array();
	
	var outputArray = [];
	outputArray.resize(int(TOTAL_BOIDS) * 2);
	outputArray.fill(0.0);
	
	
	packedOutput = PackedFloat32Array(outputArray).to_byte_array();
	
	
	velocityBuffer = rd.storage_buffer_create(packedVelocity.size(), packedVelocity);
	var velocityUniform := RDUniform.new();
	velocityUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER;
	velocityUniform.binding = 0;
	velocityUniform.add_id(velocityBuffer);
	
	positionsBuffer = rd.storage_buffer_create(packedPositions.size(), packedPositions);
	var positionUniform := RDUniform.new();
	positionUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER;
	positionUniform.binding = 1;
	positionUniform.add_id(positionsBuffer);
	
	
	
	outputVelocityBuffer = rd.storage_buffer_create(packedOutput.size(), packedOutput.duplicate());
	var outputVelocityUniform := RDUniform.new();
	outputVelocityUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER;
	outputVelocityUniform.binding = 2;
	outputVelocityUniform.add_id(outputVelocityBuffer);
	
	outputPositionsBuffer = rd.storage_buffer_create(packedOutput.size(), packedOutput.duplicate());
	var outputPositionUniform := RDUniform.new();
	outputPositionUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER;
	outputPositionUniform.binding = 3;
	outputPositionUniform.add_id(outputPositionsBuffer);
	
	
	
	globalBuffer = rd.storage_buffer_create(packedGlobals.size(), packedGlobals);
	var globalUniform := RDUniform.new();
	globalUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER;
	globalUniform.binding = 4;
	globalUniform.add_id(globalBuffer);
	
	uniformSet = rd.uniform_set_create([velocityUniform, positionUniform, outputVelocityUniform, outputPositionUniform, globalUniform], shader, 0);
	pipeline = rd.compute_pipeline_create(shader);
# End Function



func updateBuffers() -> void:
	globals = [
		VISUAL_RANGE, SEPERATION_DISTANCE, SEPERATION,
		ALIGNMENT, COHESION, MOVEMENT_SPEED,
		TOTAL_BOIDS, BOUNDRY_WIDTH, boundryBool,
		SCREEN_SIZE.x, SCREEN_SIZE.y, BOUNDRY_TURN
	];
	
	var updatedVelocity = PackedFloat32Array(boidVelocity).to_byte_array();
	var updatedPositions = PackedFloat32Array(boidPositions).to_byte_array();
	var updatedGlobals = PackedFloat32Array(globals).to_byte_array();
	
	rd.buffer_update(velocityBuffer, 0, updatedVelocity.size(), updatedVelocity);
	rd.buffer_update(positionsBuffer, 0, updatedPositions.size(), updatedPositions);
	rd.buffer_update(globalBuffer, 0, updatedGlobals.size(), updatedGlobals);
# End function



func _physics_process(_delta: float) -> void:
	if(rd == null):
		initGPU();
		initBuffers();
	else:
		updateBuffers();
	# End If/ Else
	
	
	# Send the data off to the GPU
	var compute_list := rd.compute_list_begin();
	rd.compute_list_bind_compute_pipeline(compute_list, pipeline);
	
	rd.compute_list_bind_uniform_set(compute_list, uniformSet, 0);
	
	rd.compute_list_dispatch(compute_list, int(TOTAL_BOIDS), 1, 1);
	rd.compute_list_end();
	
	
	# Submit to GPU and then wait till next frame, then continue
	rd.submit();
	await get_tree().physics_frame;
	rd.sync();
	
	
	var velocityBytes := rd.buffer_get_data(outputVelocityBuffer);
	var positionBytes  := rd.buffer_get_data(outputPositionsBuffer);
	
	
	boidVelocity = velocityBytes.to_float32_array();
	boidPositions = positionBytes.to_float32_array();
	
	
#	$BoidParticles.process_material.set_shader_parameter("particlePosition", positionArray);
#	$BoidParticles.process_material.set_shader_parameter("particleRotation", directionArray);
	
	
#	print("Debug: ", boidVelocity[0],	", ",	boidVelocity[1]);
#	print("Pos: ", boidPositions[0],		", ",	boidPositions[1]);
	
	for i in range(0, TOTAL_BOIDS):
		var trueID = i * 2;
		
		var dir = Vector2(boidVelocity[trueID], boidVelocity[trueID + 1]);
		var pos = Vector2(boidPositions[trueID], boidPositions[trueID + 1]);
		
		if(not BOUNDRY_ENABLED):
			pos.x = wrapf(pos.x, 0, SCREEN_SIZE.x);
			pos.y = wrapf(pos.y, 0, SCREEN_SIZE.y);
			
			boidPositions[trueID] = pos.x;
			boidPositions[trueID + 1] = pos.y;
		# End If;
		
		var oldRot = multiMesh.get_instance_transform_2d(i).get_rotation();
		var newRot = Vector2.DOWN.angle_to(dir);
		
		var rot = (oldRot + newRot) / 2;
		
		multiMesh.set_instance_transform_2d(i, Transform2D(rot, pos));
	# End For
# End Function
