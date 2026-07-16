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

	[Export]
	public Label ClockLabel { get; set; }

	private StyleBoxFlat _rageFillStyle;
	private PauseMenu _pauseMenu;

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

		// Instantiate and add the PauseMenu dynamically to the current scene
		_pauseMenu = new PauseMenu();
		_pauseMenu.Name = "PauseMenu";
		GetTree().CurrentScene.CallDeferred("add_child", _pauseMenu);

		// Create a beautiful in-game menu button in the top-left area
		Button menuButton = new Button();
		menuButton.Name = "InGameMenuButton";
		menuButton.Text = "MENU";
		menuButton.Position = new Vector2(16.0f, 28.0f);
		menuButton.Size = new Vector2(90.0f, 32.0f);
		menuButton.FocusMode = Control.FocusModeEnum.None;

		// Create a beautiful premium theme stylebox for normal state
		StyleBoxFlat normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
		normalStyle.BorderColor = new Color(1.0f, 0.85f, 0.2f); // Gold border matching ClockLabel
		normalStyle.BorderWidthLeft = 2;
		normalStyle.BorderWidthRight = 2;
		normalStyle.BorderWidthTop = 2;
		normalStyle.BorderWidthBottom = 2;
		normalStyle.CornerRadiusTopLeft = 4;
		normalStyle.CornerRadiusTopRight = 4;
		normalStyle.CornerRadiusBottomLeft = 4;
		normalStyle.CornerRadiusBottomRight = 4;

		// Stylebox for hover state
		StyleBoxFlat hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
		hoverStyle.BgColor = new Color(0.2f, 0.2f, 0.26f, 0.95f);
		hoverStyle.BorderColor = new Color(1.0f, 0.9f, 0.4f); // Brighter gold

		// Stylebox for pressed state
		StyleBoxFlat pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
		pressedStyle.BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
		pressedStyle.BorderColor = new Color(0.8f, 0.65f, 0.1f); // Darker gold

		menuButton.AddThemeStyleboxOverride("normal", normalStyle);
		menuButton.AddThemeStyleboxOverride("hover", hoverStyle);
		menuButton.AddThemeStyleboxOverride("pressed", pressedStyle);
		menuButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		menuButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
		menuButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.4f));
		menuButton.AddThemeColorOverride("font_pressed_color", new Color(0.8f, 0.65f, 0.1f));
		menuButton.AddThemeFontSizeOverride("font_size", 14);

		menuButton.Pressed += () => {
			if (_pauseMenu != null)
			{
				_pauseMenu.TogglePauseState();
			}
		};

		AddChild(menuButton);

		// Create a temporary test win button next to it
		Button testWinButton = new Button();
		testWinButton.Name = "TestWinButton";
		testWinButton.Text = "TEST WIN";
		testWinButton.Position = new Vector2(120.0f, 28.0f);
		testWinButton.Size = new Vector2(90.0f, 32.0f);
		testWinButton.FocusMode = Control.FocusModeEnum.None;

		StyleBoxFlat testStyle = (StyleBoxFlat)normalStyle.Duplicate();
		testStyle.BgColor = new Color(0.12f, 0.22f, 0.12f, 0.85f); // Greenish tint
		testStyle.BorderColor = new Color(0.2f, 0.8f, 0.2f); // Green border

		testWinButton.AddThemeStyleboxOverride("normal", testStyle);
		testWinButton.AddThemeStyleboxOverride("hover", hoverStyle);
		testWinButton.AddThemeStyleboxOverride("pressed", pressedStyle);
		testWinButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		testWinButton.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 0.2f));
		testWinButton.AddThemeColorOverride("font_hover_color", new Color(0.3f, 0.9f, 0.3f));
		testWinButton.AddThemeColorOverride("font_pressed_color", new Color(0.1f, 0.6f, 0.1f));
		testWinButton.AddThemeFontSizeOverride("font_size", 14);

		testWinButton.Pressed += () => {
			var simMgr = SimulationManager.Instance;
			if (simMgr != null)
			{
				simMgr.SetTimeTo2PM();
			}
		};

		AddChild(testWinButton);
	}

	public override void _Process(double delta)
	{
		var sim = SimulationManager.Instance;
		if (sim == null)
		{
			return;
		}

		if (ClockLabel == null)
		{
			ClockLabel = GetNodeOrNull<Label>("ClockLabel");
			if (ClockLabel == null)
			{
				ClockLabel = new Label();
				ClockLabel.Name = "ClockLabel";
				ClockLabel.Position = new Vector2(680.0f, 24.0f);
				ClockLabel.Size = new Vector2(100.0f, 24.0f);
				ClockLabel.HorizontalAlignment = HorizontalAlignment.Right;
				ClockLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f)); // vibrant gold/yellow
				ClockLabel.AddThemeFontSizeOverride("font_size", 18);
				AddChild(ClockLabel);
			}
		}

		if (ClockLabel != null)
		{
			ClockLabel.Text = sim.CurrentGameTime;
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
			if (sim.IsFixingStandby)
			{
				TimerLabel.Visible = true;
				int pendingCount = 0;
				foreach (var p in sim.Passengers)
				{
					if (GodotObject.IsInstanceValid(p) && p.LaneIndex == -1)
					{
						pendingCount++;
					}
				}
				TimerLabel.Text = $"🔧 REPAIRING AIRCON... ({pendingCount + sim.PendingPassengerSpawns} IN CONCOURSE)";
			}
			else if (sim.IsStandbyBufferActive)
			{
				TimerLabel.Visible = true;
				TimerLabel.Text = $"⏳ ARRANGING: {sim.StandbyBufferTimer:0.0}s";
			}
			else
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
	}

	// Kept for compatibility – announcements are no longer displayed.
	public void ShowAnnouncement(string text, Color baseColor) { }
}
