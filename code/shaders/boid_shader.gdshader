shader_type particles;
render_mode disable_force, disable_velocity;

// Particle shader to render boids as particles rather than using a multimesh

uniform float[100000] BOID_POS;
uniform float[100000] BOID_VEL;

void start() {
	int realIndex = int(INDEX) * 2;
	vec2 pos = vec2(BOID_POS[realIndex], BOID_POS[realIndex + 1]);
	
	TRANSFORM[3].xy = vec2(float(INDEX) * 10.0, 0);
	TRANSFORM[3].xy = pos;
}

void process() {
	int realIndex = int(INDEX) * 2;
	vec2 pos = vec2(BOID_POS[realIndex], BOID_POS[realIndex + 1]);
	
//	TRANSFORM[3].xy += vec2(1, 2);
	TRANSFORM[3].xy = pos;
}
