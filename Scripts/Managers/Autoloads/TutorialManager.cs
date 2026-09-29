using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class TutorialManager : Manager<TutorialManager>
{
	[Signal] public delegate void TutorialEventStartedEventHandler(string eventId);
	[Signal] public delegate void TutorialEventCompletedEventHandler(string eventId);

	[Export] protected Godot.Collections.Dictionary<GameManager.GameScene, Array<Event>> _events = new();

	private readonly HashSet<string> _startedEventIds = new(StringComparer.Ordinal);
	private readonly HashSet<string> _completedEventIds = new(StringComparer.Ordinal);
	private readonly System.Collections.Generic.Dictionary<TutorialControlAction, float> _actionAmounts = new();
	private readonly System.Collections.Generic.Dictionary<TutorialControlAction, float> _actionDurations = new();
	private readonly System.Collections.Generic.Dictionary<TutorialControlAction, float> _timeSinceActions = new();
	private float _completionPause;
	public float PromptProgress { get; private set; }
	private CancellationTokenSource _executionCancellation;
	private GameManager _loadingCoordinator;
	private TutorialPromptEvent _activePrompt;
	private TutorialPromptUI _promptUI;
	private bool _readyForChecks;
	private bool _isExecuting;
	private bool _loadedProgress;
	private double _checkTimer;

	public override string GetManagerName() => "TutorialManager";
	public string ActivePromptId => _activePrompt?.EventId ?? string.Empty;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		base._Ready();
	}

	protected override Task _Setup(bool loadingData)
	{
		StopExecution();
		DisconnectLoadingCoordinator();
		if (loadingData && !_loadedProgress) ClearProgress();
		_loadedProgress = false;
		_executionCancellation = new CancellationTokenSource();
		_loadingCoordinator = GameManager.Instance;
		if (_loadingCoordinator != null)
			_loadingCoordinator.CoreManagersLoaded += OnCoreManagersLoaded;
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		if (_loadingCoordinator == null) _readyForChecks = true;
		return Task.CompletedTask;
	}

	private void OnCoreManagersLoaded()
	{
		_readyForChecks = true;
		_checkTimer = 0.0;
	}

	public override void _Process(double delta)
	{
		bool inputAvailable = _readyForChecks &&
			GameManager.Instance?.loadingState == GameManager.LoadingState.NONE &&
			UIManager.Instance?.BlockingInput != true && !GetTree().Paused;
		if (GodotObject.IsInstanceValid(_promptUI)) _promptUI.Visible = inputAvailable;
		if (!inputAvailable) return;
		UpdatePromptProgress((float)Math.Min(delta, 0.1));
		if (_isExecuting) return;
		_checkTimer -= delta;
		if (_checkTimer > 0.0) return;
		_checkTimer = 0.25;

		if (!_events.TryGetValue(GameManager.Instance.currentScene, out Array<Event> events)) return;
		foreach (Event tutorialEvent in events)
		{
			if (tutorialEvent == null || IsEventCompleted(tutorialEvent.EventId)) continue;
			if (tutorialEvent.CanAutomaticallyTrigger()) _ = RunEventAsync(tutorialEvent);
			break;
		}
	}

	public async Task<EventExecutionResult> TriggerEventAsync(string eventId)
	{
		if (!_readyForChecks || _isExecuting || GameManager.Instance == null ||
		    GameManager.Instance.loadingState != GameManager.LoadingState.NONE)
			return EventExecutionResult.Blocked;
		if (IsEventCompleted(eventId)) return EventExecutionResult.Completed;
		if (!_events.TryGetValue(GameManager.Instance.currentScene, out Array<Event> events))
			return EventExecutionResult.Failed;
		foreach (Event tutorialEvent in events)
		{
			if (tutorialEvent == null || IsEventCompleted(tutorialEvent.EventId)) continue;
			return tutorialEvent.EventId == eventId
				? await RunEventAsync(tutorialEvent)
				: EventExecutionResult.Blocked;
		}
		return EventExecutionResult.Failed;
	}

	private async Task<EventExecutionResult> RunEventAsync(Event tutorialEvent)
	{
		CancellationToken token = _executionCancellation.Token;
		_isExecuting = true;
		SetIsBusy(true);
		try
		{
			return await tutorialEvent.ExecuteCall(context: new EventExecutionContext
			{
				Owner = this,
				CancellationToken = token,
				IsCompleted = IsEventCompleted,
				Started = RecordStarted,
				Completed = RecordCompleted
			});
		}
		catch (Exception exception)
		{
			GD.PushError($"Tutorial event '{tutorialEvent.EventId}' failed: {exception}");
			return EventExecutionResult.Failed;
		}
		finally
		{
			if (!token.IsCancellationRequested)
			{
				_isExecuting = false;
				SetIsBusy(false);
			}
		}
	}

	public bool BeginPrompt(TutorialPromptEvent prompt)
	{
		if (!_readyForChecks || _activePrompt != null) return false;
		_activePrompt = prompt;
		_actionAmounts.Clear();
		_actionDurations.Clear();
		_timeSinceActions.Clear();
		_completionPause = 0.0f;
		PromptProgress = 0.0f;
		_promptUI = new TutorialPromptUI();
		AddChild(_promptUI);
		_promptUI.SetPrompt(prompt.EventName, prompt.EventDescription);
		return true;
	}

	public void EndPrompt(TutorialPromptEvent prompt)
	{
		if (_activePrompt != prompt) return;
		_activePrompt = null;
		_actionAmounts.Clear();
		_actionDurations.Clear();
		_timeSinceActions.Clear();
		_completionPause = 0.0f;
		PromptProgress = 0.0f;
		if (GodotObject.IsInstanceValid(_promptUI))
		{
			_promptUI.Hide();
			_promptUI.QueueFree();
		}
		_promptUI = null;
	}

	public void ReportControlAction(TutorialControlAction action, float amount = 1.0f, float activeSeconds = 0.0f)
	{
		if (_activePrompt == null || !_readyForChecks ||
		    GameManager.Instance?.loadingState != GameManager.LoadingState.NONE ||
		    UIManager.Instance?.BlockingInput == true || GetTree().Paused ||
		    !float.IsFinite(amount) || amount <= 0.0f) return;
		_actionAmounts[action] = GetActionAmount(action) + amount;
		if (float.IsFinite(activeSeconds) && activeSeconds > 0.0f)
			_actionDurations[action] = GetActionDuration(action) + Math.Min(activeSeconds, 0.1f);
		_timeSinceActions.TryAdd(action, 0.0f);
	}

	public float GetActionDuration(TutorialControlAction action) =>
		_actionDurations.TryGetValue(action, out float duration) ? duration : 0.0f;

	public float GetTimeSinceAction(TutorialControlAction action) =>
		_timeSinceActions.TryGetValue(action, out float elapsed) ? elapsed : 0.0f;

	public bool IsPromptComplete(TutorialPromptEvent prompt) =>
		_activePrompt == prompt && PromptProgress >= 1.0f &&
		_completionPause >= Math.Max(prompt.CompletionPauseSeconds, 0.0f);

	private void UpdatePromptProgress(float delta)
	{
		if (_activePrompt == null) return;
		foreach (TutorialControlAction action in new List<TutorialControlAction>(_timeSinceActions.Keys))
			_timeSinceActions[action] += delta;

		PromptProgress = 1.0f;
		foreach (EventCondition condition in _activePrompt.CompletionConditions)
		{
			float progress = condition is PlayerActionCondition actionCondition
				? actionCondition.GetProgress()
				: condition?.Check() == true ? 1.0f : 0.0f;
			PromptProgress = Math.Min(PromptProgress, progress);
		}
		_completionPause = PromptProgress >= 1.0f ? _completionPause + delta : 0.0f;
		_promptUI?.SetProgress(PromptProgress);
	}

	public float GetActionAmount(TutorialControlAction action) =>
		_actionAmounts.TryGetValue(action, out float amount) ? amount : 0.0f;

	public bool IsEventStarted(string eventId) =>
		!string.IsNullOrWhiteSpace(eventId) && _startedEventIds.Contains(eventId);

	public bool IsEventCompleted(string eventId) =>
		!string.IsNullOrWhiteSpace(eventId) && _completedEventIds.Contains(eventId);

	private void RecordStarted(string eventId)
	{
		if (!string.IsNullOrWhiteSpace(eventId) && _startedEventIds.Add(eventId))
			EmitSignal(SignalName.TutorialEventStarted, eventId);
	}

	private void RecordCompleted(string eventId)
	{
		if (!string.IsNullOrWhiteSpace(eventId) && _completedEventIds.Add(eventId))
			EmitSignal(SignalName.TutorialEventCompleted, eventId);
	}

	public override Godot.Collections.Dictionary<string, Variant> Save() => new()
	{
		["started_event_ids"] = SortedIds(_startedEventIds),
		["completed_event_ids"] = SortedIds(_completedEventIds)
	};

	private static Array<string> SortedIds(HashSet<string> ids)
	{
		var sorted = new List<string>(ids);
		sorted.Sort(StringComparer.Ordinal);
		return new Array<string>(sorted);
	}

	public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		StopExecution();
		ClearProgress();
		_loadedProgress = true;
		LoadIds(data, "started_event_ids", _startedEventIds);
		LoadIds(data, "completed_event_ids", _completedEventIds);
		_startedEventIds.UnionWith(_completedEventIds);
		return Task.CompletedTask;
	}

	private static void LoadIds(Godot.Collections.Dictionary<string, Variant> data,
		string key, HashSet<string> destination)
	{
		if (data == null || !data.TryGetValue(key, out Variant value)) return;
		foreach (string eventId in value.AsGodotArray<string>())
		{
			if (!string.IsNullOrWhiteSpace(eventId)) destination.Add(eventId);
		}
	}

	public void ResetProgress()
	{
		StopExecution();
		ClearProgress();
		_loadedProgress = false;
	}

	private void ClearProgress()
	{
		_startedEventIds.Clear();
		_completedEventIds.Clear();
	}

	private void StopExecution()
	{
		_readyForChecks = false;
		_executionCancellation?.Cancel();
		_executionCancellation?.Dispose();
		_executionCancellation = null;
		if (_activePrompt != null) EndPrompt(_activePrompt);
		_isExecuting = false;
		SetIsBusy(false);
	}

	private void DisconnectLoadingCoordinator()
	{
		if (GodotObject.IsInstanceValid(_loadingCoordinator))
			_loadingCoordinator.CoreManagersLoaded -= OnCoreManagersLoaded;
		_loadingCoordinator = null;
	}

	public override void Deinitialize()
	{
		StopExecution();
		DisconnectLoadingCoordinator();
	}

	public override void _ExitTree()
	{
		Deinitialize();
		base._ExitTree();
	}
}
