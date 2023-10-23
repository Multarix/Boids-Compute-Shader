# Boid Simulation using Godot Engine

A simulation of the [Boids](https://en.wikipedia.org/wiki/Boids) algorithm using the Godot Engine, in conjunction with a GLSL compute shader.
Based on the original concept by Craig Reynolds.

Using a compute shader, it allows for large numbers of boids to be simulated in real time - Though further optimisation is still entirely possible within the godot engine.
Using my PC specs, I was able to simulate around 7000 boids at a steady 75fps consistently, your milage will likely vary.

The exact rule implimentation roughly followed the steps found in the paper found [here](https://vanhunteradams.com/Pico/Animal_Movement/Boids-algorithm.html).