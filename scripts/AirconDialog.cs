using Godot;
using System;

/// <summary>
/// A full-screen modal dialog that appears when the train's aircon breaks.
/// It blocks gameplay and lets the player choose to either fix the aircon
/// (10-second standby penalty) or let passengers in (Rage +25).
/// </summary>
public partial class AirconDialog : CanvasLayer
{
	// Callback invoked when the player makes a choice.
	// true  = Fix the aircon (10-second standby)
	// false = Let passengers in (Rage +25)
	public Action<bool> OnChoiceMade;

	private Panel _backdrop;
	private Panel _card;
	private Label _titleLabel;
	private Label _bodyLabel;
	private Button _fixButton;
	private Button _letInButton;

	public override void _Ready()
	{
		// Ensure the dialog renders on top of everything.
		Layer = 100;

		BuildUI();
	}

	private void BuildUI()
	{
		// ── Semi-transparent dark overlay ──────────────────────────────────
		_backdrop = new Panel();
		_backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_backdrop.MouseFilter = Control.MouseFilterEnum.Stop; // Eat all input

		var backdropStyle = new StyleBoxFlat();
		backdropStyle.BgColor = new Color(0.0f, 0.0f, 0.0f, 0.65f);
		_backdrop.AddThemeStyleboxOverride("panel", backdropStyle);
		AddChild(_backdrop);

		// ── Central card ───────────────────────────────────────────────────
		_card = new Panel();
		_card.SetAnchorsPreset(Control.LayoutPreset.Center);
		_card.CustomMinimumSize = new Vector2(520, 300);
		// Centre the card manually (CanvasLayer anchors work in screen space)
		_card.Position = new Vector2(-260, -150);

		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor       = new Color(0.10f, 0.10f, 0.14f, 0.97f);
		cardStyle.BorderColor   = new Color(1.0f, 0.55f, 0.0f, 1.0f);  // orange warning border
		cardStyle.BorderWidthBottom = 3;
		cardStyle.BorderWidthLeft   = 3;
		cardStyle.BorderWidthRight  = 3;
		cardStyle.BorderWidthTop    = 3;
		cardStyle.CornerRadiusTopLeft     = 12;
		cardStyle.CornerRadiusTopRight    = 12;
		cardStyle.CornerRadiusBottomLeft  = 12;
		cardStyle.CornerRadiusBottomRight = 12;
		_card.AddThemeStyleboxOverride("panel", cardStyle);
		_backdrop.AddChild(_card);

		// ── Layout container ───────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 18);

		// Padding via MarginContainer
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left",   30);
		margin.AddThemeConstantOverride("margin_right",  30);
		margin.AddThemeConstantOverride("margin_top",    28);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddChild(vbox);
		_card.AddChild(margin);

		// ── Warning icon + title ───────────────────────────────────────────
		_titleLabel = new Label();
		_titleLabel.Text = "⚠️  AIRCON MALFUNCTION DETECTED";
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.65f, 0.0f));
		_titleLabel.AddThemeFontSizeOverride("font_size", 20);
		vbox.AddChild(_titleLabel);

		// ── Separator ─────────────────────────────────────────────────────
		var sep = new HSeparator();
		vbox.AddChild(sep);

		// ── Body text ─────────────────────────────────────────────────────
		_bodyLabel = new Label();
		_bodyLabel.Text =
			"The train's air-conditioning system has broken down!\n\n" +
			"  🔧  FIX IT  — Repair the unit before boarding.\n" +
			"         The train stays. The NEXT wave of passengers\n" +
			"         floods in while you re-balance the lines!\n\n" +
			"  🚪  LET THEM IN  — Open the doors anyway.\n" +
			"         Passengers won't be happy.  Rage  +25.";
		_bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_bodyLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.88f));
		_bodyLabel.AddThemeFontSizeOverride("font_size", 14);
		_bodyLabel.HorizontalAlignment = HorizontalAlignment.Left;
		vbox.AddChild(_bodyLabel);

		// ── Button row ────────────────────────────────────────────────────
		var hbox = new HBoxContainer();
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(hbox);

		// Fix button
		_fixButton = new Button();
		_fixButton.Text = "🔧  Fix It  (+Wave)";
		_fixButton.CustomMinimumSize = new Vector2(200, 48);
		StyleButton(_fixButton, new Color(0.12f, 0.55f, 0.18f), new Color(0.18f, 0.75f, 0.25f));
		_fixButton.Pressed += OnFixPressed;
		hbox.AddChild(_fixButton);

		// Let in button
		_letInButton = new Button();
		_letInButton.Text = "🚪  Let Them In  (Rage +25)";
		_letInButton.CustomMinimumSize = new Vector2(200, 48);
		StyleButton(_letInButton, new Color(0.60f, 0.10f, 0.10f), new Color(0.85f, 0.20f, 0.20f));
		_letInButton.Pressed += OnLetInPressed;
		hbox.AddChild(_letInButton);
	}

	private static void StyleButton(Button btn, Color normalBg, Color hoverBg)
	{
		var normal = new StyleBoxFlat();
		normal.BgColor = normalBg;
		normal.CornerRadiusTopLeft     = 8;
		normal.CornerRadiusTopRight    = 8;
		normal.CornerRadiusBottomLeft  = 8;
		normal.CornerRadiusBottomRight = 8;

		var hover = new StyleBoxFlat();
		hover.BgColor = hoverBg;
		hover.CornerRadiusTopLeft     = 8;
		hover.CornerRadiusTopRight    = 8;
		hover.CornerRadiusBottomLeft  = 8;
		hover.CornerRadiusBottomRight = 8;

		var pressed = new StyleBoxFlat();
		pressed.BgColor = normalBg.Darkened(0.2f);
		pressed.CornerRadiusTopLeft     = 8;
		pressed.CornerRadiusTopRight    = 8;
		pressed.CornerRadiusBottomLeft  = 8;
		pressed.CornerRadiusBottomRight = 8;

		btn.AddThemeStyleboxOverride("normal",  normal);
		btn.AddThemeStyleboxOverride("hover",   hover);
		btn.AddThemeStyleboxOverride("pressed", pressed);
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeFontSizeOverride("font_size", 15);
	}

	private void OnFixPressed()
	{
		_fixButton.Disabled   = true;
		_letInButton.Disabled = true;
		OnChoiceMade?.Invoke(true);
		QueueFree();
	}

	private void OnLetInPressed()
	{
		_fixButton.Disabled   = true;
		_letInButton.Disabled = true;
		OnChoiceMade?.Invoke(false);
		QueueFree();
	}
}
