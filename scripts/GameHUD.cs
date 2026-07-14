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

	private string _announcementText = "BALANCE THE 5 LINES BEFORE THE TRAIN DEPARTS";
	private Color _announcementColor = Colors.DarkSlateGray;
	private int _announcementTicks = 0;

	public override void _Process(double delta)
	{
		var sim = SimulationManager.Instance;
		if (sim == null)
		{
			return;
		}

		if (RiotMeterBar != null)
		{
			RiotMeterBar.Value = sim.BalanceMeter;
		}

		if (ScoreLabel != null)
		{
			ScoreLabel.Text = $"Round {sim.CurrentRound} | Score {sim.CurrentRoundScore:0} | Total {sim.CumulativeScore:0}";
		}

		if (TimerLabel != null)
		{
			string stateText = sim.CurrentState switch
			{
				TrainRoundState.Boarding => "BOARDING",
				TrainRoundState.Scoring => "SCORING",
				TrainRoundState.Transition => "TRANSITION",
				_ => "WAITING"
			};

			TimerLabel.Text = $"{stateText} {sim.RoundTimeRemaining:0.0}s";
		}

		if (AnnouncementLabel != null)
		{
			if (_announcementTicks > 0)
			{
				_announcementTicks--;
			}
			else
			{
				_announcementText = sim.RoundResultText.Length > 0
					? sim.RoundResultText
					: "BALANCE THE 5 LINES BEFORE THE TRAIN DEPARTS";
				_announcementColor = Colors.DarkSlateGray;
			}

			AnnouncementLabel.Text = _announcementText;
			AnnouncementLabel.Modulate = _announcementColor;
		}
	}

	public void ShowAnnouncement(string text, Color baseColor)
	{
		_announcementText = text;
		_announcementColor = baseColor;
		_announcementTicks = 120;

		if (AnnouncementLabel != null)
		{
			AnnouncementLabel.Text = _announcementText;
			AnnouncementLabel.Modulate = _announcementColor;
		}
	}
}
