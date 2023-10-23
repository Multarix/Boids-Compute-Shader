#[compute]
#version 460

struct Boid {
	highp vec2 velocity;
	highp vec2 position;
};


layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;


layout(set = 0, binding = 0, std430) restrict readonly buffer velocityBuffer {
	highp float boid[];
} VelocityLookup;

layout(set = 0, binding = 1, std430) restrict readonly buffer positionBuffer {
	highp float boid[];
} PositionLookup;


layout(set = 0, binding = 2, std430) restrict buffer outVelocityBuffer {
	highp float boid[];
} OutputVelocity;

layout(set = 0, binding = 3, std430) restrict buffer outPositionBuffer {
	highp float boid[];
} OutputPosition;



layout(set = 0, binding = 4, std430) restrict readonly buffer globalBuffer {
	highp float VisualRange;
	highp float SeperationDistance;
	highp float SeperationWeight;
	highp float AlignmentWeight;
	highp float CohesionWeight;
	highp float MoveSpeed;
	highp float TotalBoids;
	highp float BoundryWidth;
	highp float BoundryEnabled;
	highp float Screen_X;
	highp float Screen_Y;
	highp float BoundryTurn;
} Global;





Boid createBoid(int boidID){
	int trueID = boidID * 2;
	
	Boid boid;
	boid.velocity = vec2(VelocityLookup.boid[trueID], VelocityLookup.boid[trueID + 1]);
	boid.position = vec2(PositionLookup.boid[trueID], PositionLookup.boid[trueID + 1]);
	return boid;
}



void main() {
	int boidID = int(gl_GlobalInvocationID.x);
	
	Boid thisBoid = createBoid(boidID);
	
	// double xPosAvg, yPosAvg, xVelAvg, yVelAvg, totalNearby, seperationX, seperationY = 0.0;
	
	highp float seperationRangeSquared = Global.SeperationDistance * Global.SeperationDistance;
	int nearbyBoids = 0;
	
	highp vec2 seperationVector = vec2(0.0);
	highp vec2 alignmentVector = vec2(0.0);
	highp vec2 cohesionVector = vec2(0.0);
	
	highp vec2 newVelocity = thisBoid.velocity;
	highp vec2 newPosition = vec2(0.0);
	
	
	for(int i = 0; i < Global.TotalBoids; i++){
		if(i == boidID) continue; // Skip self (this boid)
		
		Boid otherBoid = createBoid(i);
		
		highp float distanceToOtherBoid = distance(thisBoid.position, otherBoid.position);
	
		if(distanceToOtherBoid < Global.VisualRange){
			highp float distanceSquared = distanceToOtherBoid * distanceToOtherBoid;
		
			if(distanceSquared < seperationRangeSquared){
				seperationVector += thisBoid.position - otherBoid.position;
			} else {
				alignmentVector += otherBoid.velocity;
				cohesionVector += otherBoid.position;
				nearbyBoids = nearbyBoids + 1;
			}
		}
	}
	
	if(nearbyBoids > 0){
		highp vec2 averagedPosition = cohesionVector /= nearbyBoids;
		highp vec2 averagedVelocity = alignmentVector /= nearbyBoids;
		
		newVelocity = (newVelocity + (averagedPosition - thisBoid.position) * Global.CohesionWeight + (averagedVelocity - thisBoid.velocity) * Global.AlignmentWeight);
		newVelocity = newVelocity + (seperationVector * Global.SeperationWeight);
	}
	
	
	if(Global.BoundryEnabled == 1){
		highp float topBoundryLine = Global.BoundryWidth;
		highp float leftBoundryLine = Global.BoundryWidth;
		highp float rightBoundryLine = Global.Screen_X - Global.BoundryWidth;
		highp float bottomBoundryLine = Global.Screen_Y - Global.BoundryWidth;
		
		float GlobalBoundryTurn = 0.2;
		// Outside the top boundry
		if(thisBoid.position.y < topBoundryLine){
			newVelocity.y += GlobalBoundryTurn;
		}
		
		
		// Outside the right boundry
		if(thisBoid.position.x > rightBoundryLine){
			newVelocity.x -= GlobalBoundryTurn;
		}
		
		
		// Outside the bottom boundry
		if(thisBoid.position.y > bottomBoundryLine){
			newVelocity.y -= GlobalBoundryTurn;
		}
		
		
		// Outside the left boundry
		if(thisBoid.position.x < leftBoundryLine){
			newVelocity.x += GlobalBoundryTurn;
		}
	}
	
	
	if(length(newVelocity) < 1.0){
		newVelocity = normalize(newVelocity);
	}

	// Limit the boids speed
	if(length(newVelocity) > 2.0){
		newVelocity = normalize(newVelocity) * 2.0;
	}
	
	newVelocity = newVelocity * Global.MoveSpeed;
	newPosition = thisBoid.position + newVelocity;
	
	int trueSaveID = boidID * 2;
	OutputVelocity.boid[trueSaveID] = newVelocity.x;
	OutputVelocity.boid[trueSaveID + 1] = newVelocity.y;
	
	// For Debuggin:
	// OutputVelocity.boid[trueSaveID] = nearbyBoids;
	// OutputVelocity.boid[trueSaveID + 1] = TotalBoids;
	
	OutputPosition.boid[trueSaveID] = newPosition.x;
	OutputPosition.boid[trueSaveID + 1] = newPosition.y;
}