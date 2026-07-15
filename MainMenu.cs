using Godot;
using System;

public partial class MainMenu : Control
{
	[Export]
	public string PlayLevelScene { get; set; } = "res://main_world.tscn";

	// 1. ADDED [Export]: You can now drag your SubMenu node directly into the 
	// Inspector on the right! This prevents "Node not found" errors forever.
	[Export]
	private Control _subMenuButtons;

	public override void _Ready()
	{
		// 2. FALLBACK: If you didn't drag it into the Inspector, we'll try to find it by name.
		if (_subMenuButtons == null)
		{
			_subMenuButtons = GetNodeOrNull<Control>("SubMenu");
		}

		// 3. DEBUGGER WARNING: If both fail, print a helpful message immediately when the game starts
		if (_subMenuButtons == null)
		{
			GD.PrintErr("CRITICAL ERROR: 'SubMenu' node could not be found! Please drag it into the MainMenu Inspector slot.");
		}
	}

	// --- 1. THE HAMBURGER BUTTON ---
	public void _on_hamburger_button_pressed()
	{
		if (_subMenuButtons != null)
		{
			// Toggle visibility
			_subMenuButtons.Visible = !_subMenuButtons.Visible;
		}
		else
		{
			GD.PrintErr("Error: Cannot toggle SubMenu because it is not connected/found!");
		}
	}

	// --- 2. SUB-MENU BUTTONS ---
	public void _on_resume_button_pressed()
	{
		if (_subMenuButtons != null)
		{
			_subMenuButtons.Visible = false;
		}
		GetTree().ChangeSceneToFile(PlayLevelScene);
	}

	public void _on_new_game_button_pressed()
	{
		if (SimulationManager.Instance != null)
		{
			SimulationManager.Instance.ResetSimulation();
		}
		GetTree().ChangeSceneToFile(PlayLevelScene);
	}

	public void _on_quit_button_pressed()
	{
		GetTree().Quit();
	}
}
