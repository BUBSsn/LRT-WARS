using Godot;
using System;

public partial class GameOverDialog : CanvasLayer
{
	private Panel _backdrop;
	private Panel _card;
	private Label _titleLabel;
	private Label _bodyLabel;
	private Button _newGameButton;
	private Button _exitButton;

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;
		BuildUI();
	}

	private void BuildUI()
	{
		// Backdrop – full screen dark overlay
		_backdrop = new Panel();
		_backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_backdrop.MouseFilter = Control.MouseFilterEnum.Stop;

		var backdropStyle = new StyleBoxFlat();
		backdropStyle.BgColor = new Color(0.15f, 0.0f, 0.0f, 0.80f);
		_backdrop.AddThemeStyleboxOverride("panel", backdropStyle);
		AddChild(_backdrop);

		// Card – centered dialog box with red theme
		_card = new Panel();
		_card.SetAnchorsPreset(Control.LayoutPreset.Center);
		_card.CustomMinimumSize = new Vector2(520, 280);
		_card.Position = new Vector2(-260, -140);

		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = new Color(0.12f, 0.06f, 0.06f, 0.98f);
		cardStyle.BorderColor = new Color(0.85f, 0.15f, 0.10f, 1.0f);
		cardStyle.BorderWidthBottom = 3;
		cardStyle.BorderWidthLeft = 3;
		cardStyle.BorderWidthRight = 3;
		cardStyle.BorderWidthTop = 3;
		cardStyle.CornerRadiusTopLeft = 12;
		cardStyle.CornerRadiusTopRight = 12;
		cardStyle.CornerRadiusBottomLeft = 12;
		cardStyle.CornerRadiusBottomRight = 12;
		_card.AddThemeStyleboxOverride("panel", cardStyle);
		_backdrop.AddChild(_card);

		// Layout container
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 16);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 30);
		margin.AddThemeConstantOverride("margin_right", 30);
		margin.AddThemeConstantOverride("margin_top", 28);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddChild(vbox);
		_card.AddChild(margin);

		// Title
		_titleLabel = new Label();
		_titleLabel.Text = "💀 GAME OVER 💀";
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.2f, 0.15f));
		_titleLabel.AddThemeFontSizeOverride("font_size", 26);
		vbox.AddChild(_titleLabel);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		// Body text
		_bodyLabel = new Label();
		_bodyLabel.Text = "The commuters have had enough!\nRage has reached maximum — the station is in chaos.\n\nYour shift is over, station master.";
		_bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_bodyLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.7f));
		_bodyLabel.AddThemeFontSizeOverride("font_size", 15);
		_bodyLabel.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(_bodyLabel);

		// Button row
		var hbox = new HBoxContainer();
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(hbox);

		// New Game button
		_newGameButton = new Button();
		_newGameButton.Text = "🔄 New Game";
		_newGameButton.CustomMinimumSize = new Vector2(180, 46);
		StyleButton(_newGameButton, new Color(0.12f, 0.55f, 0.18f), new Color(0.18f, 0.75f, 0.25f));
		_newGameButton.Pressed += OnNewGamePressed;
		hbox.AddChild(_newGameButton);

		// Exit button
		_exitButton = new Button();
		_exitButton.Text = "🚪 Exit";
		_exitButton.CustomMinimumSize = new Vector2(140, 46);
		StyleButton(_exitButton, new Color(0.55f, 0.12f, 0.12f), new Color(0.75f, 0.18f, 0.18f));
		_exitButton.Pressed += OnExitPressed;
		hbox.AddChild(_exitButton);
	}

	private static void StyleButton(Button btn, Color normalBg, Color hoverBg)
	{
		var normal = new StyleBoxFlat();
		normal.BgColor = normalBg;
		normal.CornerRadiusTopLeft = 8;
		normal.CornerRadiusTopRight = 8;
		normal.CornerRadiusBottomLeft = 8;
		normal.CornerRadiusBottomRight = 8;

		var hover = new StyleBoxFlat();
		hover.BgColor = hoverBg;
		hover.CornerRadiusTopLeft = 8;
		hover.CornerRadiusTopRight = 8;
		hover.CornerRadiusBottomLeft = 8;
		hover.CornerRadiusBottomRight = 8;

		var pressed = new StyleBoxFlat();
		pressed.BgColor = normalBg.Darkened(0.2f);
		pressed.CornerRadiusTopLeft = 8;
		pressed.CornerRadiusTopRight = 8;
		pressed.CornerRadiusBottomLeft = 8;
		pressed.CornerRadiusBottomRight = 8;

		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", pressed);
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeFontSizeOverride("font_size", 15);
	}

	private void OnNewGamePressed()
	{
		_newGameButton.Disabled = true;
		_exitButton.Disabled = true;
		GetTree().Paused = false;
		if (SimulationManager.Instance != null)
		{
			SimulationManager.Instance.ResetSimulation();
		}
		QueueFree();
	}

	private void OnExitPressed()
	{
		GetTree().Quit();
	}
}
