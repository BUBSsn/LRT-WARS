using Godot;

public partial class GameHUD : CanvasLayer
{
	[Export]
	public ProgressBar RiotMeterBar { get; set; }

	[Export]
	public Label ScoreLabel { get; set; }

	[Export]
	public Label TimerLabel { get; set; }

	[Export]
	public Label AnnouncementLabel { get; set; }

	private StyleBoxFlat _rageFillStyle;

	public override void _Ready()
	{
		// Hide unused UI elements – only the rage bar and timer are shown.
		if (ScoreLabel != null) ScoreLabel.Visible = false;
		if (AnnouncementLabel != null) AnnouncementLabel.Visible = false;

		// Set up the fill StyleBox so we can colour it dynamically.
		if (RiotMeterBar != null)
		{
			_rageFillStyle = new StyleBoxFlat();
			_rageFillStyle.BgColor = Colors.LimeGreen;
			RiotMeterBar.AddThemeStyleboxOverride("fill", _rageFillStyle);

			// Hide the percentage text that Godot shows by default.
			RiotMeterBar.ShowPercentage = false;
		}
	}

	public override void _Process(double delta)
	{
		var sim = SimulationManager.Instance;
		if (sim == null)
		{
			return;
		}

		// --- Rage Bar (green → red) ---
		// AverageRage is the performance score (100 = perfect balance).
		// Commuter anger is the inverse: low score → high anger → red.
		if (RiotMeterBar != null && _rageFillStyle != null)
		{
			float rageLevel = 100.0f - sim.AverageRage; // 0 = calm/green, 100 = furious/red
			RiotMeterBar.Value = rageLevel;

			// Lerp hue: 0.33 (green) → 0.0 (red)
			float t = rageLevel / 100.0f;
			float hue = Mathf.Lerp(0.33f, 0.0f, t);
			_rageFillStyle.BgColor = Color.FromHsv(hue, 0.85f, 0.95f);
		}

		// --- Timer: only show when the train hasn't arrived yet ---
		if (TimerLabel != null)
		{
			switch (sim.CurrentState)
			{
				case TrainRoundState.WaitingForTrain:
					TimerLabel.Visible = true;
					TimerLabel.Text = $"TRAIN IN {sim.RoundTimeRemaining:0.0}s";
					break;
				case TrainRoundState.Arriving:
					TimerLabel.Visible = true;
					TimerLabel.Text = $"ARRIVING {sim.RoundTimeRemaining:0.0}s";
					break;
				default:
					TimerLabel.Visible = false;
					break;
			}
		}
	}

	// Kept for compatibility – announcements are no longer displayed.
	public void ShowAnnouncement(string text, Color baseColor) { }
}
