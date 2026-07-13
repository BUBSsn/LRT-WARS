using Godot;
using System;

public partial class GameHUD : CanvasLayer
{
	[Export] 
	public ProgressBar RiotMeterBar { get; set; }

	[Export] 
	public Label BudgetLabel { get; set; }

	[Export] 
	public Label TimerLabel { get; set; }

	public override void _Process(double delta)
	{
		var sim = SimulationManager.Instance;
		if (sim == null) return;

		if (RiotMeterBar != null)
		{
			RiotMeterBar.Value = sim.GlobalCommuterRage;
		}

		if (BudgetLabel != null)
		{
			BudgetLabel.Text = $"Budget: ${sim.DailyBudget:F0}";
		}

		if (TimerLabel != null)
		{
			TimerLabel.Text = $"Shift: {sim.ShiftTimer:F1}s";
		}
	}
}
