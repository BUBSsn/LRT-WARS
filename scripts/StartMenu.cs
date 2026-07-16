using Godot;
using System;

public partial class StartMenu : Control
{
	[Export]
	public TextureButton PlayButton { get; set; }

	[Export]
	public TextureButton HowToPlayButton { get; set; }

	[Export]
	public TextureButton MusicToggleButton { get; set; }

	[Export]
	public TextureButton VfxToggleButton { get; set; }

	private AudioStreamPlayer _clickAudioPlayer;
	private AudioStreamPlayer _musicAudioPlayer;

	private bool _musicMuted = false;
	private bool _vfxMuted = false;

	private VSlider _musicSlider;
	private VSlider _vfxSlider;

	public override void _Ready()
	{
		GD.Print("StartMenu: Initializing...");

		// Fallback dynamic node resolution in case Inspector exports didn't map correctly
		PlayButton ??= GetNodeOrNull<TextureButton>("PlayButton");
		HowToPlayButton ??= GetNodeOrNull<TextureButton>("HowToPlayButton");
		MusicToggleButton ??= GetNodeOrNull<TextureButton>("TopLeftContainer/MusicToggle");
		VfxToggleButton ??= GetNodeOrNull<TextureButton>("TopLeftContainer/VfxToggle");

		// Adjust parent container height to accommodate sliders
		var topLeftContainer = GetNodeOrNull<Control>("TopLeftContainer");
		if (topLeftContainer != null)
		{
			topLeftContainer.OffsetBottom = 250.0f;
		}

		// Set up audio player for button click sounds (VFX)
		_clickAudioPlayer = new AudioStreamPlayer();
		_clickAudioPlayer.Name = "ClickAudioPlayer";
		_clickAudioPlayer.VolumeDb = Mathf.LinearToDb(0.5f); // Default to 50% volume
		if (ResourceLoader.Exists("res://click.mp3"))
		{
			var stream = GD.Load<AudioStream>("res://click.mp3");
			_clickAudioPlayer.Stream = stream;
			GD.Print("StartMenu: click.mp3 loaded successfully.");
		}
		else
		{
			GD.PrintErr("StartMenu ERROR: click.mp3 not found at res://click.mp3");
		}
		AddChild(_clickAudioPlayer);

		// Set up background music player (Music)
		_musicAudioPlayer = new AudioStreamPlayer();
		_musicAudioPlayer.Name = "MusicAudioPlayer";
		if (ResourceLoader.Exists("res://Chiptuned Light.mp3"))
		{
			var stream = GD.Load<AudioStream>("res://Chiptuned Light.mp3");
			stream.Set("loop", true); // Enable looping dynamically
			_musicAudioPlayer.Stream = stream;
			_musicAudioPlayer.Autoplay = true;

			// Looping fallback hook
			_musicAudioPlayer.Finished += () => {
				if (!_musicMuted)
				{
					_musicAudioPlayer.Play();
				}
			};

			GD.Print("StartMenu: Chiptuned Light.mp3 loaded successfully.");
		}
		else
		{
			GD.PrintErr("StartMenu ERROR: Chiptuned Light.mp3 not found at res://Chiptuned Light.mp3");
		}
		AddChild(_musicAudioPlayer);

		// Start playing music immediately on load
		if (_musicAudioPlayer.Stream != null)
		{
			_musicAudioPlayer.VolumeDb = Mathf.LinearToDb(0.5f); // Default to 50% volume
			_musicAudioPlayer.Play();
			GD.Print("StartMenu: Playing background music.");
		}

		// Connect Play button
		if (PlayButton != null)
		{
			PlayButton.Pressed += OnPlayPressed;
			SetupButtonEffects(PlayButton);
			GD.Print("StartMenu: PlayButton successfully connected.");
		}
		else
		{
			GD.PrintErr("StartMenu ERROR: PlayButton node is NULL!");
		}

		// Connect and configure other buttons
		if (HowToPlayButton != null)
		{
			HowToPlayButton.Pressed += OnHowToPlayPressed;
			SetupButtonEffects(HowToPlayButton);
		}
		if (MusicToggleButton != null)
		{
			MusicToggleButton.Pressed += OnMusicTogglePressed;
			SetupButtonEffects(MusicToggleButton);
		}
		if (VfxToggleButton != null)
		{
			VfxToggleButton.Pressed += OnVfxTogglePressed;
			SetupButtonEffects(VfxToggleButton);
		}

		// Setup the sliders programmatically
		SetupSliders();

		// Create a text-based Quit Button in the Start Menu
		Button quitButton = new Button();
		quitButton.Name = "StartMenuQuitButton";
		quitButton.Text = "QUIT";
		quitButton.Size = new Vector2(140f, 36f);
		quitButton.FocusMode = Control.FocusModeEnum.None;

		// Center the button horizontally, place it below the How To Play button (which ends at offset_bottom = 170)
		quitButton.SetAnchorsPreset(Control.LayoutPreset.Center);
		quitButton.OffsetLeft = -70.0f;
		quitButton.OffsetRight = 70.0f;
		quitButton.OffsetTop = 190.0f;
		quitButton.OffsetBottom = 226.0f;

		// Premium styling matching the theme
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

		quitButton.AddThemeStyleboxOverride("normal", normalStyle);
		quitButton.AddThemeStyleboxOverride("hover", hoverStyle);
		quitButton.AddThemeStyleboxOverride("pressed", pressedStyle);
		quitButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		quitButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
		quitButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.4f));
		quitButton.AddThemeColorOverride("font_pressed_color", new Color(0.8f, 0.65f, 0.1f));
		quitButton.AddThemeFontSizeOverride("font_size", 16);

		// Dynamic pivot offset for scaling animation
		quitButton.PivotOffset = quitButton.Size / 2f;
		quitButton.Resized += () => {
			quitButton.PivotOffset = quitButton.Size / 2f;
		};

		quitButton.Pressed += () => {
			PlayClickSound();
			
			// Click animation
			Color targetColor = Colors.White;
			Color clickColor = targetColor * 0.7f;
			var tween = CreateTween();
			tween.TweenProperty(quitButton, "scale", new Vector2(0.92f, 0.92f), 0.05f);
			tween.Parallel().TweenProperty(quitButton, "self_modulate", clickColor, 0.05f);
			tween.TweenProperty(quitButton, "scale", Vector2.One, 0.05f);
			tween.Parallel().TweenProperty(quitButton, "self_modulate", targetColor, 0.05f);

			// Quit execution
			GetTree().Quit();
		};

		AddChild(quitButton);

		// Initially hide the main game HUD if available
		var hud = GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hud != null)
		{
			hud.Visible = false;
			GD.Print("StartMenu: Game HUD hidden on startup.");
		}
	}

	private void SetupSliders()
	{
		// Load textures
		var thumbTexture = GD.Load<Texture2D>("res://thumb slider.png");
		var trackTexture = GD.Load<Texture2D>("res://Vertical Bar.png");

		// Create scaled versions of the textures using nearest neighbor scaling to keep pixel art crisp
		ImageTexture scaledThumb = null;
		if (thumbTexture != null)
		{
			var img = thumbTexture.GetImage();
			img.Resize(24, 19, Image.Interpolation.Nearest);
			scaledThumb = ImageTexture.CreateFromImage(img);
		}

		ImageTexture scaledTrack = null;
		if (trackTexture != null)
		{
			var img = trackTexture.GetImage();
			img.Resize(12, 120, Image.Interpolation.Nearest);
			scaledTrack = ImageTexture.CreateFromImage(img);
		}

		// 1. Setup Vfx Slider
		if (VfxToggleButton != null)
		{
			var parent = VfxToggleButton.GetParent();
			if (parent != null)
			{
				int index = VfxToggleButton.GetIndex();
				parent.RemoveChild(VfxToggleButton);

				var vBox = new VBoxContainer();
				vBox.Name = "VfxContainer";
				vBox.Alignment = BoxContainer.AlignmentMode.Begin;
				vBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				vBox.AddThemeConstantOverride("separation", 5);

				parent.AddChild(vBox);
				parent.MoveChild(vBox, index);

				vBox.AddChild(VfxToggleButton);

				_vfxSlider = new VSlider();
				_vfxSlider.Name = "VfxSlider";
				_vfxSlider.CustomMinimumSize = new Vector2(50, 120);
				_vfxSlider.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				_vfxSlider.Visible = false;
				_vfxSlider.MaxValue = 1.0;
				_vfxSlider.Step = 0.01;

				ApplyCustomSliderStyles(_vfxSlider, scaledThumb, scaledTrack);
				_vfxSlider.ValueChanged += OnVfxSliderValueChanged;
				_vfxSlider.Value = 0.5; // Default VFX volume (50%)

				vBox.AddChild(_vfxSlider);
			}
		}

		// 2. Setup Music Slider
		if (MusicToggleButton != null)
		{
			var parent = MusicToggleButton.GetParent();
			if (parent != null)
			{
				int index = MusicToggleButton.GetIndex();
				parent.RemoveChild(MusicToggleButton);

				var vBox = new VBoxContainer();
				vBox.Name = "MusicContainer";
				vBox.Alignment = BoxContainer.AlignmentMode.Begin;
				vBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				vBox.AddThemeConstantOverride("separation", 5);

				parent.AddChild(vBox);
				parent.MoveChild(vBox, index);

				vBox.AddChild(MusicToggleButton);

				_musicSlider = new VSlider();
				_musicSlider.Name = "MusicSlider";
				_musicSlider.CustomMinimumSize = new Vector2(50, 120);
				_musicSlider.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				_musicSlider.Visible = false;
				_musicSlider.MaxValue = 1.0;
				_musicSlider.Step = 0.01;

				ApplyCustomSliderStyles(_musicSlider, scaledThumb, scaledTrack);
				_musicSlider.ValueChanged += OnMusicSliderValueChanged;
				_musicSlider.Value = 0.5; // Default background music volume (50%)

				vBox.AddChild(_musicSlider);
			}
		}
	}

	private void ApplyCustomSliderStyles(VSlider slider, Texture2D thumb, Texture2D track)
	{
		slider.TextureFilter = TextureFilterEnum.Nearest;

		// Set the bounding width of the slider track area
		slider.AddThemeConstantOverride("slider_width", 12);

		if (thumb != null)
		{
			slider.AddThemeIconOverride("grabber", thumb);
			slider.AddThemeIconOverride("grabber_highlight", thumb);
		}

		if (track != null)
		{
			var styleBox = new StyleBoxTexture();
			styleBox.Texture = track;
			styleBox.TextureMarginTop = 10;
			styleBox.TextureMarginBottom = 10;
			
			// Set expand margins so the StyleBoxTexture renders with a proper visible width
			styleBox.ExpandMarginLeft = 6;
			styleBox.ExpandMarginRight = 6;
			
			slider.AddThemeStyleboxOverride("slider", styleBox);

			// Hide the default progress/grabber area highlight colors so the vertical bar is fully visible
			var emptyStyle = new StyleBoxEmpty();
			slider.AddThemeStyleboxOverride("grabber_area", emptyStyle);
			slider.AddThemeStyleboxOverride("grabber_area_highlight", emptyStyle);
		}
	}

	private void OnMusicSliderValueChanged(double value)
	{
		_musicMuted = (value <= 0.001f);
		float db = Mathf.LinearToDb((float)value);

		if (_musicAudioPlayer != null)
		{
			_musicAudioPlayer.VolumeDb = _musicMuted ? -80.0f : db;
		}

		int musicBusIndex = AudioServer.GetBusIndex("Music");
		if (musicBusIndex != -1)
		{
			AudioServer.SetBusVolumeDb(musicBusIndex, db);
			AudioServer.SetBusMute(musicBusIndex, _musicMuted);
		}

		GD.Print($"StartMenu: Music volume updated to {value} ({db} dB)");
	}

	private void OnVfxSliderValueChanged(double value)
	{
		_vfxMuted = (value <= 0.001f);
		float db = Mathf.LinearToDb((float)value);

		if (_clickAudioPlayer != null)
		{
			_clickAudioPlayer.VolumeDb = _vfxMuted ? -80.0f : db;
		}

		int sfxBusIndex = AudioServer.GetBusIndex("SFX");
		if (sfxBusIndex != -1)
		{
			AudioServer.SetBusVolumeDb(sfxBusIndex, db);
			AudioServer.SetBusMute(sfxBusIndex, _vfxMuted);
		}

		int vfxBusIndex = AudioServer.GetBusIndex("VFX");
		if (vfxBusIndex != -1)
		{
			AudioServer.SetBusVolumeDb(vfxBusIndex, db);
			AudioServer.SetBusMute(vfxBusIndex, _vfxMuted);
		}

		GD.Print($"StartMenu: VFX volume updated to {value} ({db} dB)");
	}

	private void SetupButtonEffects(TextureButton button)
	{
		if (button == null) return;

		// Set default pivot to center for scaling animation
		button.PivotOffset = button.Size / 2f;
		
		// Connect resize signal to dynamically update pivot
		button.Resized += () => {
			button.PivotOffset = button.Size / 2f;
		};

		// Wire up click visual and audio feedback
		button.Pressed += () => {
			PlayClickSound();
			AnimateButtonClick(button);
		};
	}

	private void PlayClickSound()
	{
		if (!_vfxMuted && _clickAudioPlayer != null && _clickAudioPlayer.Stream != null)
		{
			_clickAudioPlayer.Play();
		}
	}

	private void AnimateButtonClick(TextureButton button)
	{
		// Force correct pivot at the moment of click
		button.PivotOffset = button.Size / 2f;

		Color targetColor = Colors.White;
		Color clickColor = targetColor * 0.7f;

		// Create a quick scale down & darken, then scale back up & restore color
		var tween = CreateTween();
		tween.TweenProperty(button, "scale", new Vector2(0.92f, 0.92f), 0.05f);
		tween.Parallel().TweenProperty(button, "self_modulate", clickColor, 0.05f);
		tween.TweenProperty(button, "scale", Vector2.One, 0.05f);
		tween.Parallel().TweenProperty(button, "self_modulate", targetColor, 0.05f);
	}

	private async void OnPlayPressed()
	{
		GD.Print("StartMenu: PLAY Button pressed!");

		// Wait slightly to let the click animation and sound play out (0.12 seconds)
		await ToSignal(GetTree().CreateTimer(0.12f), "timeout");

		if (SimulationManager.Instance != null)
		{
			SimulationManager.Instance.StartGameFromMenu();
			GD.Print("StartMenu: StartGameFromMenu() called on SimulationManager.");
		}
		else
		{
			GD.PrintErr("StartMenu ERROR: SimulationManager.Instance is NULL!");
		}

		// Show HUD when entering game
		var hud = GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("HUD");
		if (hud != null)
		{
			hud.Visible = true;
			GD.Print("StartMenu: Game HUD set to visible.");
		}

		// Hide the Main Menu
		Visible = false;
		GD.Print("StartMenu: Hiding Main Menu control.");
	}

	private void OnHowToPlayPressed()
	{
		// No function for now
		GD.Print("How To Play pressed - no function for now.");
	}

	private void OnMusicTogglePressed()
	{
		if (_musicSlider != null)
		{
			_musicSlider.Visible = !_musicSlider.Visible;
			GD.Print($"StartMenu: Music Slider visibility toggled to {_musicSlider.Visible}");
		}
	}

	private void OnVfxTogglePressed()
	{
		if (_vfxSlider != null)
		{
			_vfxSlider.Visible = !_vfxSlider.Visible;
			GD.Print($"StartMenu: VFX Slider visibility toggled to {_vfxSlider.Visible}");
		}
	}
}
