using Godot;

namespace FrameRate;

public partial class Fps : Label {
	public override void _Process(double delta)	{
		Text = "FPS: " + Engine.GetFramesPerSecond().ToString();
	}
}
