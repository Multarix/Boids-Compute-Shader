using Boids;
using Godot;
namespace SettingsGUI;

public partial class Gui : Control {
	private HSlider ViewDistance_Node;
	private Label ViewDistanceLabel;

	private HSlider SeperationDistance_Node;
	private Label SeperationDistanceLabel;

	private HSlider MoveSpeed_Node;
	private Label MoveSpeedLabel;

	private HSlider Cohesion_Node;
	private Label CohesionLabel;

	private HSlider Alignment_Node;
	private Label AlignmentLabel;

	private HSlider Seperation_Node;
	private Label SeperationLabel;

	private CheckButton BoundryButton;

	private Line2D TopLine;
	private Line2D BottomLine;
	private Line2D LeftLine;
	private Line2D RightLine;

	private Label TotalCount;

	private BoidManager Parent;


	// This kinda just sets up the GUI to the defaults... really that's all it does.
	public void Setup(float VIEW_DISTANCE, float SEPERATION_DISTANCE, float MOVEMENT_SPEED, float COHESION, float ALIGNMENT, float SEPERATION, float TOTAL_BOIDS) {
		ViewDistance_Node = GetNode<HSlider>("View_Distance");
		ViewDistanceLabel = ViewDistance_Node.GetChild<Label>(1);

		MoveSpeed_Node = GetNode<HSlider>("Move_Speed");
		MoveSpeedLabel = MoveSpeed_Node.GetChild<Label>(1);

		SeperationDistance_Node = GetNode<HSlider>("Seperation_Distance");
		SeperationDistanceLabel = SeperationDistance_Node.GetChild<Label>(1);

		Cohesion_Node = GetNode<HSlider>("Cohesion");
		CohesionLabel = Cohesion_Node.GetChild<Label>(1);

		Alignment_Node = GetNode<HSlider>("Alignment");
		AlignmentLabel = Alignment_Node.GetChild<Label>(1);

		Seperation_Node = GetNode<HSlider>("Seperation");
		SeperationLabel = Seperation_Node.GetChild<Label>(1);

		BoundryButton = GetNode<CheckButton>("Boundry_Button");
		TopLine = GetNode<Line2D>("TopLine");
		BottomLine = GetNode<Line2D>("BottomLine");
		LeftLine = GetNode<Line2D>("LeftLine");
		RightLine = GetNode<Line2D>("RightLine");

		TotalCount = GetNode<Label>("TotalBoids");

		CanvasLayer Canvas = GetParent<CanvasLayer>();
		Parent = Canvas.GetParent<BoidManager>();

		ViewDistance_Node.Value = VIEW_DISTANCE;
		SeperationDistance_Node.Value = SEPERATION_DISTANCE;
		MoveSpeed_Node.Value = MOVEMENT_SPEED;
		Cohesion_Node.Value = COHESION;
		Alignment_Node.Value = ALIGNMENT;
		Seperation_Node.Value = SEPERATION;

		ViewDistanceLabel.Text = VIEW_DISTANCE.ToString();
		SeperationDistanceLabel.Text = SEPERATION_DISTANCE.ToString();
		MoveSpeedLabel.Text = MOVEMENT_SPEED.ToString();
		CohesionLabel.Text = COHESION.ToString();
		AlignmentLabel.Text = ALIGNMENT.ToString();
		SeperationLabel.Text = SEPERATION.ToString();

		TotalCount.Text = "Total Boids: " + TOTAL_BOIDS.ToString();
	}


	// Calculates the boundry line positions and sets them to be visible or not.
	public void SetupBoundryLines(float Margin, Vector2 Screen) {
		TopLine.Points = new Vector2[] {
			new Vector2(Margin, Margin),
			new Vector2(Screen.X - Margin, Margin)
		};
		LeftLine.Points = new Vector2[] {
			new Vector2(Margin, Margin),
			new Vector2(Margin, Screen.Y - Margin)
		};
		BottomLine.Points = new Vector2[] {
			new Vector2(Margin, Screen.Y - Margin),
			new Vector2(Screen.X - Margin, Screen.Y - Margin)
		};
		RightLine.Points = new Vector2[] {
			new Vector2(Screen.X - Margin, Margin),
			new Vector2(Screen.X - Margin, Screen.Y - Margin)
		};

		bool BoundryEnabled = Parent.BOUNDRY_ENABLED;
		TopLine.Visible = BoundryEnabled;
		BottomLine.Visible = BoundryEnabled;
		LeftLine.Visible = BoundryEnabled;
		RightLine.Visible = BoundryEnabled;
	}


	// The rest of the functions do pretty much what you'd expect them to do.
	// So I won't make any further comment on them.
	public void OnViewDistanceChanged(float Value) {
		ViewDistance_Node.Value = Value;
		ViewDistanceLabel.Text = Value.ToString();
		Parent.VISUAL_RANGE = Value;
	}

	public void OnSeperationDistanceChanged(float Value) {
		SeperationDistance_Node.Value = Value;
		SeperationDistanceLabel.Text = Value.ToString();
		Parent.SEPERATION_DISTANCE = Value;
	}

	public void OnMoveSpeedChanged(float Value) {
		MoveSpeed_Node.Value = Value;
		MoveSpeedLabel.Text = Value.ToString();
		Parent.MOVEMENT_SPEED = Value;
	}

	public void OnCohesionChanged(float Value) {
		Cohesion_Node.Value = Value;
		CohesionLabel.Text = Value.ToString();
		Parent.COHESION = Value;
	}

	public void OnAlignmentChanged(float Value) {
		Alignment_Node.Value = Value;
		AlignmentLabel.Text = Value.ToString();
		Parent.ALIGNMENT = Value;
	}

	public void OnSeperationChanged(float Value) {
		Seperation_Node.Value = Value;
		SeperationLabel.Text = Value.ToString();
		Parent.SEPERATION = Value;
	}

	public void OnBoundryToggled(bool Value) {
		TopLine.Visible = Value;
		BottomLine.Visible = Value;
		LeftLine.Visible = Value;
		RightLine.Visible = Value;

		Parent.BOUNDRY_ENABLED = Value;

		Parent.BoundryBool = Value ? 1.0f : 0.0f;
	}
}
