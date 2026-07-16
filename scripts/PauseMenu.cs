using Godot;
using System;

public partial class PauseMenu : CanvasLayer
{
	private ColorRect _backgroundOverlay;
	private VBoxContainer _buttonContainer;
	private AudioStreamPlayer _clickAudioPlayer;

	private TextureButton _resumeButton;
	private TextureButton _newGameButton;
	private VBoxContainer _optionsPanel;
	private HSlider _musicSlider;
	private HSlider _sfxSlider;
	private TextureButton _menuButton;
	private Button _quitButton;

	public override void _Ready()
	{
		// We want the pause menu to render on top of HUD and other elements
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always; // Run even when tree is paused

		// 1. Create a dark translucent overlay background
		_backgroundOverlay = new ColorRect();
		_backgroundOverlay.Name = "BackgroundOverlay";
		_backgroundOverlay.Color = new Color(0f, 0f, 0f, 0.65f);
		
		// Set full anchors to cover the entire screen
		_backgroundOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_backgroundOverlay);

		// 2. Create the HBoxContainer for centering the two columns (Left: Buttons, Right: Sliders)
		HBoxContainer columnsContainer = new HBoxContainer();
		columnsContainer.Name = "ColumnsContainer";
		columnsContainer.Alignment = BoxContainer.AlignmentMode.Center;
		columnsContainer.AddThemeConstantOverride("separation", 80); // Slider is somewhere a bit to the right
		
		// Anchor container to the center of the screen
		columnsContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
		columnsContainer.GrowHorizontal = Control.GrowDirection.Both;
		columnsContainer.GrowVertical = Control.GrowDirection.Both;
		AddChild(columnsContainer);

		// 3. Left column VBoxContainer for buttons
		_buttonContainer = new VBoxContainer();
		_buttonContainer.Name = "ButtonContainer";
		_buttonContainer.Alignment = BoxContainer.AlignmentMode.Center;
		_buttonContainer.AddThemeConstantOverride("separation", 15);
		columnsContainer.AddChild(_buttonContainer);

		// 4. Right column VBoxContainer for volume controls
		_optionsPanel = new VBoxContainer();
		_optionsPanel.Name = "OptionsPanel";
		_optionsPanel.Alignment = BoxContainer.AlignmentMode.Center;
		_optionsPanel.AddThemeConstantOverride("separation", 15);
		_optionsPanel.CustomMinimumSize = new Vector2(240f, 150f);
		columnsContainer.AddChild(_optionsPanel);

		// 5. Populate left column buttons
		_resumeButton = CreatePauseButton("ResumeButton", "res://Resume.png", OnResumePressed);
		_newGameButton = CreatePauseButton("NewGameButton", "res://NewGame.png", OnNewGamePressed);
		_menuButton = CreatePauseButton("MenuButton", "res://menu.png", OnMainMenuPressed);
		_quitButton = CreateTextButton("QuitButton", "QUIT", OnQuitPressed);

		// 6. Populate right column options panel
		SetupOptionsPanel();

		// 7. Setup Click Audio Player (matching StartMenu SFX/VFX)
		_clickAudioPlayer = new AudioStreamPlayer();
		_clickAudioPlayer.Name = "ClickAudioPlayer";
		_clickAudioPlayer.VolumeDb = Mathf.LinearToDb(0.5f);
		
		// Assign to VFX or SFX bus so controls muting/volume works
		int sfxBusIndex = AudioServer.GetBusIndex("SFX");
		if (sfxBusIndex != -1)
		{
			_clickAudioPlayer.Bus = "SFX";
		}
		else
		{
			int vfxBusIndex = AudioServer.GetBusIndex("VFX");
			if (vfxBusIndex != -1)
			{
				_clickAudioPlayer.Bus = "VFX";
			}
		}

		if (ResourceLoader.Exists("res://click.mp3"))
		{
			_clickAudioPlayer.Stream = GD.Load<AudioStream>("res://click.mp3");
		}
		AddChild(_clickAudioPlayer);

		// Hide by default on startup
		Visible = false;
	}

	private TextureButton CreatePauseButton(string name, string texturePath, Action onPressedAction)
	{
		var button = new TextureButton();
		button.Name = name;
		button.IgnoreTextureSize = true;
		button.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
		button.CustomMinimumSize = new Vector2(240f, 65f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		if (ResourceLoader.Exists(texturePath))
		{
			button.TextureNormal = GD.Load<Texture2D>(texturePath);
		}
		else
		{
			GD.PrintErr($"PauseMenu: Failed to load button texture {texturePath}");
		}

		// Connect effects
		button.PivotOffset = button.CustomMinimumSize / 2f;
		button.Resized += () => {
			button.PivotOffset = button.Size / 2f;
		};

		button.Pressed += () => {
			PlayClickSound();
			AnimateButtonClick(button);
			onPressedAction?.Invoke();
		};

		// Add subtle hover effect (increase modulation scale/brightness slightly)
		button.MouseEntered += () => {
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", new Vector2(1.05f, 1.05f), 0.1f);
			tween.Parallel().TweenProperty(button, "self_modulate", new Color(1.1f, 1.1f, 1.1f), 0.1f);
		};
		button.MouseExited += () => {
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", Vector2.One, 0.1f);
			tween.Parallel().TweenProperty(button, "self_modulate", Colors.White, 0.1f);
		};

		_buttonContainer.AddChild(button);
		return button;
	}

	private Button CreateTextButton(string name, string text, Action onPressedAction)
	{
		Button button = new Button();
		button.Name = name;
		button.Text = text;
		button.CustomMinimumSize = new Vector2(160f, 38f); // Smaller Quit Button
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		button.FocusMode = Control.FocusModeEnum.None;

		// Premium styling matching the gold/slate theme
		StyleBoxFlat normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
		normalStyle.BorderColor = new Color(1.0f, 0.85f, 0.2f); // Gold border matching theme
		normalStyle.BorderWidthLeft = 2;
		normalStyle.BorderWidthRight = 2;
		normalStyle.BorderWidthTop = 2;
		normalStyle.BorderWidthBottom = 2;
		normalStyle.CornerRadiusTopLeft = 4;
		normalStyle.CornerRadiusTopRight = 4;
		normalStyle.CornerRadiusBottomLeft = 4;
		normalStyle.CornerRadiusBottomRight = 4;

		StyleBoxFlat hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
		hoverStyle.BgColor = new Color(0.2f, 0.2f, 0.26f, 0.95f);
		hoverStyle.BorderColor = new Color(1.0f, 0.9f, 0.4f);

		StyleBoxFlat pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
		pressedStyle.BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
		pressedStyle.BorderColor = new Color(0.8f, 0.65f, 0.1f);

		button.AddThemeStyleboxOverride("normal", normalStyle);
		button.AddThemeStyleboxOverride("hover", hoverStyle);
		button.AddThemeStyleboxOverride("pressed", pressedStyle);
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		button.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.4f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.8f, 0.65f, 0.1f));
		button.AddThemeFontSizeOverride("font_size", 14);

		button.PivotOffset = button.CustomMinimumSize / 2f;
		button.Resized += () => {
			button.PivotOffset = button.Size / 2f;
		};

		button.Pressed += () => {
			PlayClickSound();
			
			// Click animation
			button.PivotOffset = button.Size / 2f;
			Color targetColor = Colors.White;
			Color clickColor = targetColor * 0.7f;
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", new Vector2(0.92f, 0.92f), 0.05f);
			tween.Parallel().TweenProperty(button, "self_modulate", clickColor, 0.05f);
			tween.TweenProperty(button, "scale", Vector2.One, 0.05f);
			tween.Parallel().TweenProperty(button, "self_modulate", targetColor, 0.05f);

			onPressedAction?.Invoke();
		};

		// Add subtle hover effect (increase modulation scale/brightness slightly)
		button.MouseEntered += () => {
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", new Vector2(1.05f, 1.05f), 0.1f);
			tween.Parallel().TweenProperty(button, "self_modulate", new Color(1.1f, 1.1f, 1.1f), 0.1f);
		};
		button.MouseExited += () => {
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", Vector2.One, 0.1f);
			tween.Parallel().TweenProperty(button, "self_modulate", Colors.White, 0.1f);
		};

		_buttonContainer.AddChild(button);
		return button;
	}

	private void SetupOptionsPanel()
	{
		// 1. Music volume controls
		Label musicLabel = new Label();
		musicLabel.Text = "MUSIC VOLUME";
		musicLabel.HorizontalAlignment = HorizontalAlignment.Center;
		musicLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
		musicLabel.AddThemeFontSizeOverride("font_size", 14);
		_optionsPanel.AddChild(musicLabel);

		_musicSlider = new HSlider();
		_musicSlider.Name = "MusicSlider";
		_musicSlider.CustomMinimumSize = new Vector2(240f, 24f);
		_musicSlider.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		_musicSlider.MaxValue = 1.0;
		_musicSlider.Step = 0.01;
		_musicSlider.FocusMode = Control.FocusModeEnum.None;
		_musicSlider.ValueChanged += OnMusicSliderValueChanged;
		_optionsPanel.AddChild(_musicSlider);

		// Space separator
		Control spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 15);
		_optionsPanel.AddChild(spacer);

		// 2. SFX volume controls
		Label sfxLabel = new Label();
		sfxLabel.Text = "SFX VOLUME";
		sfxLabel.HorizontalAlignment = HorizontalAlignment.Center;
		sfxLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
		sfxLabel.AddThemeFontSizeOverride("font_size", 14);
		_optionsPanel.AddChild(sfxLabel);

		_sfxSlider = new HSlider();
		_sfxSlider.Name = "SfxSlider";
		_sfxSlider.CustomMinimumSize = new Vector2(240f, 24f);
		_sfxSlider.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		_sfxSlider.MaxValue = 1.0;
		_sfxSlider.Step = 0.01;
		_sfxSlider.FocusMode = Control.FocusModeEnum.None;
		_sfxSlider.ValueChanged += OnSfxSliderValueChanged;
		_optionsPanel.AddChild(_sfxSlider);

		// Style HSliders dynamically to fit theme
		StyleBoxFlat sliderTrack = new StyleBoxFlat();
		sliderTrack.BgColor = new Color(0.12f, 0.12f, 0.16f, 1.0f);
		sliderTrack.BorderColor = new Color(1.0f, 0.85f, 0.2f, 0.4f);
		sliderTrack.BorderWidthLeft = 1;
		sliderTrack.BorderWidthRight = 1;
		sliderTrack.BorderWidthTop = 1;
		sliderTrack.BorderWidthBottom = 1;
		sliderTrack.ContentMarginTop = 4;
		sliderTrack.ContentMarginBottom = 4;
		sliderTrack.CornerRadiusTopLeft = 2;
		sliderTrack.CornerRadiusTopRight = 2;
		sliderTrack.CornerRadiusBottomLeft = 2;
		sliderTrack.CornerRadiusBottomRight = 2;

		_musicSlider.AddThemeStyleboxOverride("slider", sliderTrack);
		_sfxSlider.AddThemeStyleboxOverride("slider", sliderTrack);
	}

	private void PlayClickSound()
	{
		if (_clickAudioPlayer != null && _clickAudioPlayer.Stream != null)
		{
			_clickAudioPlayer.Play();
		}
	}

	private void AnimateButtonClick(TextureButton button)
	{
		button.PivotOffset = button.Size / 2f;
		Color targetColor = Colors.White;
		Color clickColor = targetColor * 0.7f;

		var tween = CreateTween();
		tween.TweenProperty(button, "scale", new Vector2(0.92f, 0.92f), 0.05f);
		tween.Parallel().TweenProperty(button, "self_modulate", clickColor, 0.05f);
		tween.TweenProperty(button, "scale", Vector2.One, 0.05f);
		tween.Parallel().TweenProperty(button, "self_modulate", targetColor, 0.05f);
	}

	public override void _Input(InputEvent @event)
	{
		// Toggle pause using the Escape key, only if the main game is active
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Escape)
			{
				var sim = SimulationManager.Instance;
				if (sim != null && sim.IsGameActive)
				{
					TogglePauseState();
					GetViewport().SetInputAsHandled();
				}
			}
		}
	}

	public void TogglePauseState()
	{
		bool newPaused = !GetTree().Paused;
		GetTree().Paused = newPaused;
		Visible = newPaused;
		if (newPaused)
		{
			SyncAudioSliders();
		}
		GD.Print($"PauseMenu: Game pause status toggled to {newPaused}");
	}

	private void SyncAudioSliders()
	{
		if (_musicSlider == null || _sfxSlider == null) return;

		// Sync Music with active player in StartMenu
		var musicPlayer = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("StartMenuLayer/StartMenu/MusicAudioPlayer");
		if (musicPlayer != null)
		{
			float db = musicPlayer.VolumeDb;
			_musicSlider.Value = db <= -79.0f ? 0f : Mathf.DbToLinear(db);
		}

		// Sync SFX with active players in StartMenu and PauseMenu
		var startClickPlayer = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("StartMenuLayer/StartMenu/ClickAudioPlayer");
		if (startClickPlayer != null)
		{
			float db = startClickPlayer.VolumeDb;
			_sfxSlider.Value = db <= -79.0f ? 0f : Mathf.DbToLinear(db);
		}
	}

	private void OnMusicSliderValueChanged(double value)
	{
		float db = Mathf.LinearToDb((float)value);
		bool muted = (value <= 0.001f);
		float finalDb = muted ? -80.0f : db;

		// Update actual music player in StartMenu
		var musicPlayer = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("StartMenuLayer/StartMenu/MusicAudioPlayer");
		if (musicPlayer != null)
		{
			musicPlayer.VolumeDb = finalDb;
		}
		GD.Print($"PauseMenu: Music volume set to {value} ({finalDb} dB)");
	}

	private void OnSfxSliderValueChanged(double value)
	{
		float db = Mathf.LinearToDb((float)value);
		bool muted = (value <= 0.001f);
		float finalDb = muted ? -80.0f : db;

		// Update click audio player in StartMenu
		var startClickPlayer = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("StartMenuLayer/StartMenu/ClickAudioPlayer");
		if (startClickPlayer != null)
		{
			startClickPlayer.VolumeDb = finalDb;
		}

		// Update click audio player in PauseMenu
		if (_clickAudioPlayer != null)
		{
			_clickAudioPlayer.VolumeDb = finalDb;
		}
		GD.Print($"PauseMenu: SFX volume set to {value} ({finalDb} dB)");
	}

	private void OnResumePressed()
	{
		GD.Print("PauseMenu: Resume pressed!");
		GetTree().Paused = false;
		Visible = false;
	}

	private async void OnNewGamePressed()
	{
		GD.Print("PauseMenu: New Game pressed!");
		// Give click animation a fraction of a second to play
		await ToSignal(GetTree().CreateTimer(0.12f), "timeout");

		GetTree().Paused = false;
		Visible = false;

		if (SimulationManager.Instance != null)
		{
			SimulationManager.Instance.ResetSimulation();
		}
	}

	private async void OnMainMenuPressed()
	{
		GD.Print("PauseMenu: Main Menu pressed!");
		// Give click animation a fraction of a second to play
		await ToSignal(GetTree().CreateTimer(0.12f), "timeout");

		GetTree().Paused = false;
		Visible = false;

		if (SimulationManager.Instance != null)
		{
			SimulationManager.Instance.StopGameAndReturnToMenu();
		}

		// Hide HUD
		var hud = GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hud != null)
		{
			hud.Visible = false;
		}

		// Show Start Menu
		var startMenuLayer = GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("StartMenuLayer");
		if (startMenuLayer != null)
		{
			var startMenu = startMenuLayer.GetNodeOrNull<Control>("StartMenu");
			if (startMenu != null)
			{
				startMenu.Visible = true;
			}
		}
	}

	private async void OnQuitPressed()
	{
		GD.Print("PauseMenu: Quit pressed!");
		// Give click animation a fraction of a second to play
		await ToSignal(GetTree().CreateTimer(0.12f), "timeout");
		GetTree().Quit();
	}
}
