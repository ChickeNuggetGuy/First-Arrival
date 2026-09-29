using System;
using System.Threading;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;
using Godot.Collections;

public partial class TutorialRegression : Node3D
{
	private TutorialManager _tutorial;
	private GameManager _game;
	private OrbitalCamera _camera;

	public override async void _Ready()
	{
		try
		{
			_game = new GameManager { currentScene = GameManager.GameScene.GlobeScene };
			typeof(Manager<GameManager>).GetProperty("Instance").SetValue(null, _game);
			var ui = new UIManager();
			typeof(Manager<UIManager>).GetProperty("Instance").SetValue(null, ui);
			_tutorial = GD.Load<PackedScene>("res://Scenes/tutorial_manager.tscn").Instantiate<TutorialManager>();
			AddChild(_tutorial);
			_tutorial.SetProcess(false);
			_camera = new OrbitalCamera { UseSmoothing = false };
			_camera.AddChild(new Camera3D { Name = "Camera3D", Position = new Vector3(0, 0, 5) });
			AddChild(_camera);
			_camera.SetProcess(false);
			_camera.SetProcessUnhandledInput(false);
			await Prepare(false);

			StoryDefinition story = GD.Load<StoryDefinition>("res://Data/Story/MainStory.tres");
			StoryEvent intro = story.Tracks[0].Events[0];
			Check(!intro.CheckTriggerConditions(), "Story must wait for controls.");
			Check(intro.SubEvents.Count == 1 && intro.SubEvents[0] is PopupStoryEvent,
				"Existing story sub-events must survive the base-resource migration.");

			Task<EventExecutionResult> run = _tutorial.TriggerEventAsync("controls_tutorial");
			Check(_tutorial.ActivePromptId == "controls_keyboard_pan", "Keyboard prompt must be first.");
			Check(!run.IsCompleted, "Tutorial must wait for player input.");
			using var started = new EventStatusCondition();
			started.Set("Source", (int)EventSource.Tutorial);
			started.Set("RequiredStatus", (int)EventStatus.Started);
			started.Set("EventId", "controls_tutorial");
			Check(started.Check(), "Started condition must pass before completion.");
			Check(!intro.CheckTriggerConditions(), "Starting the tutorial must not unlock the story.");
			typeof(UIManager).GetProperty("BlockingInput").SetValue(ui, true);
			_tutorial.ReportControlAction(TutorialControlAction.KeyboardPan, 100);
			typeof(UIManager).GetProperty("BlockingInput").SetValue(ui, false);
			GetTree().Paused = true;
			_tutorial.ReportControlAction(TutorialControlAction.KeyboardPan, 100);
			_tutorial._Process(0);
			foreach (Node child in _tutorial.GetChildren())
			{
				if (child is TutorialPromptUI prompt) Check(!prompt.Visible, "Pausing must hide the prompt.");
			}
			GetTree().Paused = false;
			_tutorial.ReportControlAction(TutorialControlAction.FocusCell);
			await Frames(2);
			Check(_tutorial.ActivePromptId == "controls_keyboard_pan", "Wrong action must not advance a prompt.");
			Input.ActionPress("cameraRight");
			_camera._Process(0.25);
			Input.ActionRelease("cameraRight");
			_tutorial._Process(0.1);
			Check(_tutorial.PromptProgress > 0 && _tutorial.PromptProgress < 0.1f,
				"A brief key press must only partially fill the progress bar.");
			float partialProgress = _tutorial.PromptProgress;
			await Tick(10);
			Check(_tutorial.PromptProgress == partialProgress, "Idle time must not count as movement.");
			Input.ActionPress("cameraRight");
			await Tick(32);
			Input.ActionRelease("cameraRight");
			await Tick(5);
			Check(_tutorial.ActivePromptId == "controls_mouse_pan", "Actual keyboard movement must complete step one.");
			Check(_tutorial.GetActionAmount(TutorialControlAction.FocusCell) == 0,
				"Actions from previous prompts must not carry over.");

			var saved = _tutorial.Save();
			_tutorial.Deinitialize();
			await Frames(2);
			Check(await run == EventExecutionResult.Cancelled, "Leaving the scene must cancel the pending event.");
			Check(_tutorial.ActivePromptId == string.Empty, "Cancellation must remove the prompt.");
			await _tutorial.Load(saved);
			await Prepare(true);
			run = _tutorial.TriggerEventAsync("controls_tutorial");
			Check(_tutorial.ActivePromptId == "controls_mouse_pan", "Load must resume at the first unfinished step.");

			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
			await Frames(2);
			Check(_tutorial.ActivePromptId == "controls_mouse_pan", "Holding the mouse without movement must not count.");
			_camera._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(4000, 0) });
			await Tick(1);
			Check(_tutorial.PromptProgress < 0.1f, "A fast mouse flick must not bypass the required duration.");
			float mouseProgress = _tutorial.PromptProgress;
			await Tick(10);
			Check(_tutorial.PromptProgress == mouseProgress, "Holding a still mouse must not fill the bar.");
			for (int i = 0; i < 32; i++)
			{
				_camera._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(2, 0) });
				await Tick(1);
			}
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
			await Tick(5);
			Check(_tutorial.ActivePromptId == "controls_context_menu", "Actual left drag must complete step two.");
			_tutorial.ReportControlAction(TutorialControlAction.OpenCellContextMenu);
			await Tick(10);
			Check(_tutorial.ActivePromptId == "controls_context_menu" && _tutorial.PromptProgress < 0.5f,
				"Opening the menu must allow time to read it before advancing.");
			GetTree().Paused = true;
			float beforePause = _tutorial.PromptProgress;
			_tutorial._Process(2);
			Check(_tutorial.PromptProgress == beforePause, "Pausing must stop the progress timer.");
			GetTree().Paused = false;
			await Tick(26);
			Check(_tutorial.ActivePromptId == "controls_focus_cell", "Opening the context menu must reveal the focus prompt.");
			Check(!intro.CheckTriggerConditions(), "Story must remain blocked before the last step.");
			_tutorial.ReportControlAction(TutorialControlAction.FocusCell);
			await Tick(10);
			Check(!run.IsCompleted && !intro.CheckTriggerConditions(), "Focus must not immediately start the story.");
			await Tick(26);
			Check(await run == EventExecutionResult.Completed, "Focus must complete the tutorial.");
			Check(intro.CheckTriggerConditions(), "Completing controls must unlock the first story event.");
			Check(_tutorial.ActivePromptId == string.Empty, "Completion must remove the prompt.");

			await _tutorial.Load(_tutorial.Save());
			await Prepare(true);
			Check(await _tutorial.TriggerEventAsync("controls_tutorial") == EventExecutionResult.Completed &&
			      _tutorial.ActivePromptId == string.Empty, "Completed tutorials must not replay after loading.");
			_game.currentScene = GameManager.GameScene.BaseScene;
			await Prepare(false);
			Check(_tutorial.IsEventCompleted("controls_tutorial"), "Entering a fresh scene must preserve campaign progress.");
			_tutorial.ResetProgress();
			Check(!_tutorial.IsEventStarted("controls_tutorial"), "A new campaign must reset tutorial progress.");
			_game.currentScene = GameManager.GameScene.GlobeScene;
			await Prepare(false);
			run = _tutorial.TriggerEventAsync("controls_tutorial");
			Check(_tutorial.ActivePromptId == "controls_keyboard_pan", "A new campaign must replay controls.");
			_tutorial.Deinitialize();
			await Frames(2);
			Check(await run == EventExecutionResult.Cancelled, "Restarted tutorial must cancel cleanly.");
			await Prepare(true);
			Check(!_tutorial.IsEventCompleted("controls_keyboard_pan"), "Saves without tutorial data must not inherit another campaign.");
			await CheckWaitEvent();
			_tutorial.Free();
			_camera.Free();
			ui.Free();
			_game.Free();
			GD.Print("PASS: controls tutorial regression (input, ordering, save/resume, cancellation, event dependencies).");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PrintErr(exception);
			GetTree().Quit(1);
		}
	}

	private async Task CheckWaitEvent()
	{
		using var condition = new EventStatusCondition();
		condition.Set("Source", (int)EventSource.Tutorial);
		condition.Set("EventId", "dependency");
		using var waitEvent = new WaitForConditionsEvent();
		waitEvent.CompletionConditions.Add(condition);
		using var cancellation = new CancellationTokenSource();
		var context = new EventExecutionContext { Owner = this, CancellationToken = cancellation.Token };
		Task<EventExecutionResult> wait = waitEvent.ExecuteCall(context: context);
		await Frames(2);
		Check(!wait.IsCompleted, "Wait event must remain pending before its dependency completes.");
		await _tutorial.Load(new Dictionary<string, Variant>
		{
			["completed_event_ids"] = new Array<string> { "dependency" }
		});
		await Frames(2);
		Check(await wait == EventExecutionResult.Completed, "Wait event must detect a dependency completed after it started.");
		condition.Set("EventId", "missing");
		wait = waitEvent.ExecuteCall(context: context);
		cancellation.Cancel();
		await Frames(2);
		Check(await wait == EventExecutionResult.Cancelled, "Wait event must support cancellation.");
	}

	private async Task Prepare(bool loadingData)
	{
		await _tutorial.SetupCall(loadingData);
		await _tutorial.ExecuteCall(loadingData);
		_game.EmitSignal(GameManager.SignalName.CoreManagersLoaded);
	}

	private async Task Tick(int count)
	{
		for (int i = 0; i < count; i++)
		{
			_camera._Process(0.1);
			_tutorial._Process(0.1);
			await Frames(1);
		}
	}

	private async Task Frames(int count)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private static void Check(bool value, string message)
	{
		if (!value) throw new InvalidOperationException(message);
	}
}
