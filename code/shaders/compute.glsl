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


layout(set = 0, binding = 4, std430) restrict readonly buffer globalBuffer {
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
} Global;


bool IsOutOfBounds(vec2 position){
	if(position.x < 0.0) return true;
	if(position.x > Global.Screen_X) return true;
	if(position.y < 0.0) return true;
	if(position.y > Global.Screen_Y) return true;
	
	return false;
}


// Negative angle + 90 degrees (cause how the mesh is rotated)
float GetAngle(vec2 vector){
	float angle = atan(vector.y, vector.x);
	return -angle + radians(90);
}


// Pretty Colors!
vec3 GetColor(vec2 vector){
	float r = (vector.x + 1) / 2;
	float b = (vector.y + 1) / 2;
	float g = (r + b) / 2;
	return normalize(vec3(r, 1 - g,  b));
}


// Make the boid fit the buffer
void CompileBoid(int boidID, vec2 newPosition, vec2 newVelocity, Boid thisBoid){
	// Check if the boid is out of bounds, if it is, set the bin to -1
	float SpatialBinID = -1.0;
	
	if(!IsOutOfBounds(newPosition)){
		float TotalRows = ceil(Global.Screen_Y / Global.VisualRange);
		float TotalColumns = ceil(Global.Screen_X / Global.VisualRange);
		
		float Row = floor(thisBoid.Position.y / Global.VisualRange);
		float Column = floor(thisBoid.Position.x / Global.VisualRange);
		
		SpatialBinID = (Row * TotalColumns) + Column;
	}
	
	int trueID = boidID * 16;

	vec2 velocityNormal = normalize(newVelocity);
	vec3 color = GetColor(velocityNormal);
	
	float rotation = GetAngle(velocityNormal);
	
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


// Look, this just loads the boid from the buffers into a format we can actually use.
Boid CreateBoid(int boidID){
	Boid boid;
	boid.Position = vec2(BoidBufferLookup.boid[boidID][3], BoidBufferLookup.boid[boidID][7]);
	boid.Velocity = vec2(BoidBufferLookup.boid[boidID][12], BoidBufferLookup.boid[boidID][13]);
	boid.FlockID = BoidBufferLookup.boid[boidID][14];
	boid.SpatialBinID = BoidBufferLookup.boid[boidID][15];
	return boid;
}


void main() {
	// I was lazy and decided to just use x invocations.
	int boidID = int(gl_GlobalInvocationID.x);
	
	Boid thisBoid = CreateBoid(boidID);
	
	float seperationRangeSquared = Global.SeperationDistance * Global.SeperationDistance;
	int nearbyBoids = 0;
	
	vec2 seperationVector = vec2(0.0);
	vec2 alignmentVector = vec2(0.0);
	vec2 cohesionVector = vec2(0.0);
	
	vec2 newVelocity = thisBoid.Velocity;
	vec2 newPosition = vec2(0.0);
	
	
	// Loop through all other boids just once... except if we're outside the boundry, then we prioritize getting back in.
	if(thisBoid.SpatialBinID != -1.0){
		for(int i = 0; i < Global.TotalBoids; i++){
			if(i == boidID) continue; // Skip self
			
			Boid otherBoid = CreateBoid(i);
			if(otherBoid.FlockID != thisBoid.FlockID) continue; // Skip other flocks, maybe make them actively avoid other flocks later idk.
			// if(otherBoid.SpatialBinID != thisBoid.SpatialBinID) continue; // Skip boids that aren't in the same bin... There is a better way to do this. I'll add it later.
			
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
	
	
	if(Global.BoundryEnabled == 1){
		float topBoundryLine = Global.BoundryWidth;
		float leftBoundryLine = Global.BoundryWidth;
		float rightBoundryLine = Global.Screen_X - Global.BoundryWidth;
		float bottomBoundryLine = Global.Screen_Y - Global.BoundryWidth;
		
		// Outside the top boundry
		if(thisBoid.Position.y < topBoundryLine){
			newVelocity.y += Global.BoundryTurn;
		}
		
		
		// Outside the right boundry
		if(thisBoid.Position.x > rightBoundryLine){
			newVelocity.x -= Global.BoundryTurn;
		}
		
		
		// Outside the bottom boundry
		if(thisBoid.Position.y > bottomBoundryLine){
			newVelocity.y -= Global.BoundryTurn;
		}
		
		
		// Outside the left boundry
		if(thisBoid.Position.x < leftBoundryLine){
			newVelocity.x += Global.BoundryTurn;
		}
	}
	
	
	// Make sure the boid is going at least 1 unit per frame
	if(length(newVelocity) < 1.0){
		newVelocity = normalize(newVelocity);
	}

	// Limit the boids speed
	if(length(newVelocity) > 2.0){
		newVelocity = normalize(newVelocity) * 2;
	}
	
	newVelocity = newVelocity * Global.MoveSpeed;
	newPosition = thisBoid.Position + newVelocity;
	
	if(Global.BoundryEnabled == 0){
		if(newPosition.x < 0.0){
			newPosition.x += Global.Screen_X;
		}
			
		if(newPosition.x > Global.Screen_X){
			newPosition.x -= Global.Screen_X;
		}
			
		if(newPosition.y < 0.0){
			newPosition.y += Global.Screen_Y;
		}
			
		if(newPosition.y > Global.Screen_Y){
			newPosition.y -= Global.Screen_Y;
		}
	}

	
	CompileBoid(boidID, newPosition, newVelocity, thisBoid);
}