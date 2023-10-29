#[compute]
#version 460

struct Boid {
	vec2 Position;
	float Rotation;
	vec2 Velocity;
	vec4 Color;
};

// 8x8 is more annoying than just saying 64 in the x group.
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

// X1 X2 ?? Position.X | -X2 X1 ?? Position.Y | (R G B A)
layout(set = 0, binding = 0, std430) restrict readonly buffer BoidBuffer {
	float boid[][12];
} BoidBufferLookup;

layout(set = 0, binding = 1, std430) restrict buffer BoidUpdateBuffer {
	float boid[];
} BoidBufferUpdate;


layout(set = 0, binding = 2, std430) restrict readonly buffer BoidVelocityBuffer {
	vec2 boid[];
} BoidVelocityLookup;

layout(set = 0, binding = 3, std430) restrict buffer BoidVelocityUpdateBuffer {
	vec2 boid[];
} BoidVelocityUpdate;


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

float GetAngle(vec2 vector){
	float angle = atan(vector.y, vector.x);
	return -angle + radians(90);
}

vec3 GetColor(vec2 vector){
	float r = (vector.x + 1) / 2;
	float b = (vector.y + 1) / 2;
	float g = (r + b) / 2;
	return normalize(vec3(r, 1 - g,  b));
}

void CompileBoid(int boidID, vec2 newPosition, vec2 newVelocity, Boid thisBoid){
	int trueID = boidID * 12;

	vec2 velocityNormal = normalize(newVelocity);
	vec3 color = GetColor(velocityNormal);
	
	float rotation = GetAngle(velocityNormal);
	float cr = cos(rotation);
	float sr = sin(rotation);
	
	BoidBufferUpdate.boid[trueID]		= cr;
	BoidBufferUpdate.boid[trueID + 1]	= sr;
	BoidBufferUpdate.boid[trueID + 2]	= rotation;
	BoidBufferUpdate.boid[trueID + 3]	= newPosition.x;
	BoidBufferUpdate.boid[trueID + 4]	= -sr;
	BoidBufferUpdate.boid[trueID + 5]	= cr;
	BoidBufferUpdate.boid[trueID + 6]	= 0;
	BoidBufferUpdate.boid[trueID + 7]	= newPosition.y;
	BoidBufferUpdate.boid[trueID + 8]	= color.r;
	BoidBufferUpdate.boid[trueID + 9]	= color.g;
	BoidBufferUpdate.boid[trueID + 10]	= color.b;
	BoidBufferUpdate.boid[trueID + 11]	= 1.0;
	
	BoidVelocityUpdate.boid[boidID] = newVelocity;
}


Boid CreateBoid(int boidID){
	Boid boid;
	boid.Position = vec2(BoidBufferLookup.boid[boidID][3], BoidBufferLookup.boid[boidID][7]);
	boid.Rotation = atan(
		vec2(BoidBufferLookup.boid[boidID][0], BoidBufferLookup.boid[boidID][1]).y,
		vec2(BoidBufferLookup.boid[boidID][4],BoidBufferLookup.boid[boidID][5]).x
	);
	boid.Velocity = BoidVelocityLookup.boid[boidID];
	boid.Color = vec4(BoidBufferLookup.boid[boidID][8], BoidBufferLookup.boid[boidID][9], BoidBufferLookup.boid[boidID][10], BoidBufferLookup.boid[boidID][11]);
	return boid;
}




void main() {
	int boidID = int(gl_GlobalInvocationID.x);
	
	Boid thisBoid = CreateBoid(boidID);
	
	float seperationRangeSquared = Global.SeperationDistance * Global.SeperationDistance;
	int nearbyBoids = 0;
	
	vec2 seperationVector = vec2(0.0);
	vec2 alignmentVector = vec2(0.0);
	vec2 cohesionVector = vec2(0.0);
	
	vec2 newVelocity = thisBoid.Velocity;
	vec2 newPosition = vec2(0.0);
	
	
	// Loop through all of them just once
	for(int i = 0; i < Global.TotalBoids; i++){
		if(i == boidID) continue; // Skip self (this boid)
		
		Boid otherBoid = CreateBoid(i);
		
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
	
	
	if(length(newVelocity) < 1.0){
		newVelocity = normalize(newVelocity);
	}

	// Limit the boids speed
	if(length(newVelocity) > 2.0){
		newVelocity = normalize(newVelocity) * 2;
	}
	
	newVelocity = newVelocity * Global.MoveSpeed;
	newPosition = thisBoid.Position + newVelocity;
	
	CompileBoid(boidID, newPosition, newVelocity, thisBoid);
}