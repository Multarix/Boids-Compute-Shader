#[compute]
#version 460


struct Boid {
	vec2 Position;
	vec2 Velocity;
	float FlockID;
	float SpatialBinID;
};

// 8x8 is more annoying than just saying 64 in the x group.
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

// These layouts are probably bad, but I don't give a damn.
layout(set = 0, binding = 0, std430) restrict readonly buffer BoidBuffer {
	float boid[][16];
} BoidBufferLookup;

layout(set = 0, binding = 1, std430) restrict buffer BoidUpdateBuffer {
	float boid[];
} BoidBufferUpdate;


// Spatial Hashing
layout(set = 0, binding = 2, std430) restrict readonly buffer BoidHashLocationBuffer {
	int tile[][64];
} BoidHashLookup;

// This always starts out as all -1's...
layout(set = 0, binding = 3, std430) restrict buffer BoidHashUpdateBuffer {
	int tile[][64];
} BoidHashUpdate;

// This always starts out as all 0's...
layout(set = 0, binding = 4, std430) restrict buffer BoidHashSizeBuffer {
	int tile[];
} BoidHashSizeLookup;



layout(set = 0, binding = 5, std430) restrict readonly buffer globalBuffer {
	float VisualRange;
	float SeperationDistance;
	float SeperationWeight;
	float AlignmentWeight;
	float CohesionWeight;
	float MoveSpeed;
	float TotalBoids;
	float BoundryWidth;
	float BoundryEnabled;
	float Screen_X;
	float Screen_Y;
	float BoundryTurn;
	float DeltaTime;
} Global;



int TotalRows = int(ceil(Global.Screen_Y / 60));
int TotalColumns = int(ceil(Global.Screen_X / 60));
int totalTiles = TotalRows * TotalColumns;



// Look, this just loads the boid from the buffers into a format we can actually use.
Boid createBoid(int boidID){
	Boid boid;
	boid.Position = vec2(BoidBufferLookup.boid[boidID][3], BoidBufferLookup.boid[boidID][7]);
	boid.Velocity = vec2(BoidBufferLookup.boid[boidID][12], BoidBufferLookup.boid[boidID][13]);
	boid.FlockID = BoidBufferLookup.boid[boidID][14];
	boid.SpatialBinID = BoidBufferLookup.boid[boidID][15];
	
	return boid;
}



int[9] getRelevantBins(int binID){
	int relevantBins[9] = {-1, -1, -1, -1, binID, -1, -1, -1, -1};
	
	
	bool LEFT = (binID % TotalColumns == 0);
	bool RIGHT = (binID % TotalColumns == TotalColumns - 1);
	bool TOP = (binID < TotalColumns);
	bool BOTTOM = (binID > totalTiles - TotalColumns);
	
	
	if(!TOP){
		relevantBins[1] = binID - TotalColumns;
		
		if(!LEFT){
			relevantBins[0] = binID - TotalColumns - 1;
		}
		
		if(!RIGHT){
			relevantBins[2] = binID - TotalColumns + 1;
		}
	}
	
	if(!BOTTOM){
		relevantBins[7] = binID + TotalColumns;
		if(!LEFT){
			relevantBins[6] = binID + TotalColumns - 1;
		}
		
		if(!RIGHT){
			relevantBins[8] = binID + TotalColumns + 1;
		}
	}
	
	if(!LEFT){
		relevantBins[3] = binID - 1;
	}
	
	if(!RIGHT){
		relevantBins[5] = binID + 1;
	}
	
	return relevantBins;
}



vec2 calculateVelocity(Boid thisBoid, int boidID){
	vec2 newVelocity = thisBoid.Velocity;

	// If outside the boundry, we're going to prioritize getting back in by ignoring all other boids.
	if(thisBoid.SpatialBinID != -1.0){
		float seperationRangeSquared = Global.SeperationDistance * Global.SeperationDistance;
		int nearbyBoids = 0;
		
		vec2 seperationVector = vec2(0.0);
		vec2 alignmentVector = vec2(0.0);
		vec2 cohesionVector = vec2(0.0);
		
		int[] relevantBins = getRelevantBins(int(thisBoid.SpatialBinID));
		
		for(int i = 0; i < 9; i++){
			if(relevantBins[i] == -1) continue; // Skip bins if unneeded
			int[64] bin = BoidHashLookup.tile[relevantBins[i]];
			
			for(int b = 0; b < 64; b++){
				int otherBoidID = bin[b];
				if(otherBoidID == -1) break; // Skip empty slots
				if(otherBoidID == boidID) continue; // Skip self
				
				Boid otherBoid = createBoid(otherBoidID);
				if(otherBoid.FlockID != thisBoid.FlockID) continue; // Skip other flocks, maybe make them actively avoid other flocks later idk.
				
				float distanceToOtherBoid = distance(thisBoid.Position, otherBoid.Position);
		
				if(distanceToOtherBoid < Global.VisualRange){
					float distanceSquared = distanceToOtherBoid * distanceToOtherBoid;
			
					if(distanceSquared < seperationRangeSquared){
						seperationVector += thisBoid.Position - otherBoid.Position;
					} else {
						alignmentVector += otherBoid.Velocity;
						cohesionVector += otherBoid.Position;
						nearbyBoids = nearbyBoids + 1;
					}
				}
			}
		}
		
		if(nearbyBoids > 0){
			vec2 averagedPosition = cohesionVector /= nearbyBoids;
			vec2 averagedVelocity = alignmentVector /= nearbyBoids;
			
			newVelocity = (newVelocity + (averagedPosition - thisBoid.Position) * Global.CohesionWeight + (averagedVelocity - thisBoid.Velocity) * Global.AlignmentWeight);
			newVelocity = newVelocity + (seperationVector * Global.SeperationWeight);
		}
	}
	
	return newVelocity;
}



vec2 returnToBoundry(Boid thisBoid, vec2 velocity){
	float topBoundryLine = Global.BoundryWidth;
	float leftBoundryLine = Global.BoundryWidth;
	float rightBoundryLine = Global.Screen_X - Global.BoundryWidth;
	float bottomBoundryLine = Global.Screen_Y - Global.BoundryWidth;
	
	// Outside the top boundry
	if(thisBoid.Position.y < topBoundryLine){
		velocity.y += Global.BoundryTurn;
	}
	
	
	// Outside the right boundry
	if(thisBoid.Position.x > rightBoundryLine){
		velocity.x -= Global.BoundryTurn;
	}
	
	
	// Outside the bottom boundry
	if(thisBoid.Position.y > bottomBoundryLine){
		velocity.y -= Global.BoundryTurn;
	}
	
	
	// Outside the left boundry
	if(thisBoid.Position.x < leftBoundryLine){
		velocity.x += Global.BoundryTurn;
	}
	
	return velocity;
}



vec2 containSpeed(vec2 velocity){
		// Make sure the boid is going at least 1 unit per frame
	if(length(velocity) < 1.0){
		velocity = normalize(velocity);
	}

	// Limit the boids speed
	if(length(velocity) > 2.0){
		velocity = normalize(velocity) * 2;
	}
	
	return velocity;
}



vec2 wrapScreen(vec2 position){
	if(position.x < 0.0){
		position.x += Global.Screen_X;
	}
			
	if(position.x > Global.Screen_X){
		position.x -= Global.Screen_X;
	}
			
	if(position.y < 0.0){
		position.y += Global.Screen_Y;
	}
			
	if(position.y > Global.Screen_Y){
		position.y -= Global.Screen_Y;
	}
		
	return position;
}



bool isOutOfBounds(vec2 position){
	if(position.x < 0.0) return true;
	if(position.x > Global.Screen_X) return true;
	if(position.y < 0.0) return true;
	if(position.y > Global.Screen_Y) return true;
	
	return false;
}



// Pretty Colors!
vec3 getColor(vec2 vector){
	float r = (vector.x + 1) / 2;
	float b = (vector.y + 1) / 2;
	float g = (r + b) / 2;
	
	return normalize(vec3(r, 1 - g,  b));
}



// Negative angle + 90 degrees (cause how the mesh is rotated)
float getAngle(vec2 vector){
	float angle = atan(vector.y, vector.x);
	
	return -angle;
}



void addToHash(int boidID, int binID){
	int index = atomicAdd(BoidHashSizeLookup.tile[binID], 1);
	if(index < 64){ // Setting an arbitrary limit of 64 boids per bin, all others are ignored
		BoidHashUpdate.tile[binID][index] = boidID;
	}
}



// Make the boid fit the buffer
void compileBoid(int boidID, vec2 newPosition, vec2 newVelocity){
	// Check if the boid is out of bounds, if it is, set the bin to -1
	float SpatialBinID = -1.0;
	
	if(!isOutOfBounds(newPosition)){
		float Row = floor(newPosition.y / 60);
		float Column = floor(newPosition.x / 60);
		
		SpatialBinID = (Row * TotalColumns) + Column;
	}
	addToHash(boidID, int(SpatialBinID));
	
	int trueID = boidID * 16;

	vec2 velocityNormal = normalize(newVelocity);
	vec3 color = getColor(velocityNormal);
	
	float rotation = getAngle(velocityNormal);
	
	// Imma be honest, idk how this cr/ sr stuff works
	// But it's what the Godot Engine source does to set the rotation in a Transform2D...
	float cr = cos(rotation);
	float sr = sin(rotation);
	
	BoidBufferUpdate.boid[trueID]		= cr;
	BoidBufferUpdate.boid[trueID + 1]	= sr;
	// 2 is always 0.0
	BoidBufferUpdate.boid[trueID + 3]	= newPosition.x;
	BoidBufferUpdate.boid[trueID + 4]	= -sr;
	BoidBufferUpdate.boid[trueID + 5]	= cr;
	// 6 is always 0.0
	BoidBufferUpdate.boid[trueID + 7]	= newPosition.y;
	BoidBufferUpdate.boid[trueID + 8]	= color.r;
	BoidBufferUpdate.boid[trueID + 9]	= color.g;
	BoidBufferUpdate.boid[trueID + 10]	= color.b;
	// 11 is always 1.0
	BoidBufferUpdate.boid[trueID + 12]	= newVelocity.x;
	BoidBufferUpdate.boid[trueID + 13]	= newVelocity.y;
	// 14 is the flock ID, don't change it
	BoidBufferUpdate.boid[trueID + 15]	= SpatialBinID;
}



void main() {
	// I was lazy and decided to just use x invocations.
	int boidID = int(gl_GlobalInvocationID.x);
	
	Boid thisBoid = createBoid(boidID);
	vec2 newVelocity = calculateVelocity(thisBoid, boidID);
		
	// Check if out of bounds, if so, return turn towards the boundry.
	if(Global.BoundryEnabled == 1){
		newVelocity = returnToBoundry(thisBoid, newVelocity);
	}
	
	// Fix up the boid speed.
	newVelocity = containSpeed(newVelocity);
	newVelocity = newVelocity * Global.MoveSpeed;
	
	// Calculate the new position
	vec2 newPosition = thisBoid.Position + newVelocity;
	
	// Wrap the boid's position if the boundry is disabled.
	if(Global.BoundryEnabled == 0){
		newPosition = wrapScreen(newPosition);
	}
	
	// Update the boid in the buffer
	compileBoid(boidID, newPosition, newVelocity);
}