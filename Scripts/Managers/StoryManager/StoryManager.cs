using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class StoryManager : Manager<StoryManager>
{
    private sealed class StoryEventRegistration
    {
        public StoryEventRegistration(StoryEvent storyEvent, StoryTrackDefinition track)
        {
            Event = storyEvent;
            Track = track;
        }

        public StoryEvent Event { get; }
        public StoryTrackDefinition Track { get; }
    }

    [Signal]
    public delegate void StoryEventStartedEventHandler(string eventId);

    [Signal]
    public delegate void StoryEventCompletedEventHandler(string eventId);

    [Signal]
    public delegate void StoryEventExecutionStoppedEventHandler(
        string eventId,
        StoryEventExecutionResult result
    );

    [Export]
    public StoryDefinition Story { get; private set; }

    [Export(PropertyHint.Range, "0.05,10.0,0.05")]
    public double AutomaticCheckIntervalSeconds { get; private set; } = 0.25;

    private readonly HashSet<string> _completedEventIds = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, StoryEventRegistration>
        _eventsById = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, StoryTrackDefinition>
        _tracksById = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, string> _linearCurrentEventIds =
        new(StringComparer.Ordinal);

    private double _automaticCheckTimer;
    private bool _isExecuting;
    private bool _readyForChecks;
    private bool _deinitializing;
    private GameManager _loadingCoordinator;

    public override string GetManagerName() => "StoryManager";

    public override void _Process(double delta)
    {
        if (!_readyForChecks || _deinitializing || _isExecuting)
            return;

        _automaticCheckTimer -= delta;
        if (_automaticCheckTimer > 0.0)
            return;

        _automaticCheckTimer = Math.Max(AutomaticCheckIntervalSeconds, 0.05);
        _ = EvaluateAutomaticEventsAsync();
    }

    protected override Task _Setup(bool loadingData)
    {
        _deinitializing = false;
        _readyForChecks = false;
        _automaticCheckTimer = 0.0;

        DisconnectLoadingCoordinator();
        _loadingCoordinator = GameManager.Instance;

        if (!loadingData)
        {
            _completedEventIds.Clear();
            _linearCurrentEventIds.Clear();
        }

        BuildStoryIndex();

        if (_loadingCoordinator != null)
            _loadingCoordinator.CoreManagersLoaded += OnCoreManagersLoaded;

        return Task.CompletedTask;
    }

    protected override Task _Execute(bool loadingData)
    {
        // Story events can await player interaction. They must not be part of the
        // manager-loading task, or a popup could keep the loading screen open forever.
        if (_loadingCoordinator == null)
            _readyForChecks = true;

        return Task.CompletedTask;
    }

    public override void Deinitialize()
    {
        _deinitializing = true;
        _readyForChecks = false;
        DisconnectLoadingCoordinator();
    }

    public override void _ExitTree()
    {
        DisconnectLoadingCoordinator();
        base._ExitTree();
    }

    public bool IsEventCompleted(string eventId)
    {
        return !string.IsNullOrWhiteSpace(eventId) && _completedEventIds.Contains(eventId);
    }

    public async Task<StoryEventExecutionResult> TriggerEventAsync(
        string eventId,
        bool ignoreConditions = false
    )
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return StoryEventExecutionResult.Failed;

        if (_eventsById.Count == 0)
            BuildStoryIndex();

        if (!_eventsById.TryGetValue(eventId, out StoryEventRegistration registration))
        {
            GD.PushWarning($"Story event '{eventId}' was not found in the loaded story.");
            return StoryEventExecutionResult.Failed;
        }

        if (IsEventCompleted(eventId))
            return StoryEventExecutionResult.Completed;

        if (_isExecuting)
            return StoryEventExecutionResult.Blocked;

        if (
            registration.Track.Mode == StoryTrackMode.Linear
            && GetCurrentLinearEvent(registration.Track) != registration.Event
        )
        {
            return StoryEventExecutionResult.Blocked;
        }

        return await ExecuteStoryEventAsync(registration, ignoreConditions);
    }

    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        Array<string> completedEventIds = new();
        var sortedCompletedEventIds = new List<string>(_completedEventIds);
        sortedCompletedEventIds.Sort(StringComparer.Ordinal);
        foreach (string eventId in sortedCompletedEventIds)
            completedEventIds.Add(eventId);

        Godot.Collections.Dictionary<string, Variant> linearTrackPositions = new();
        foreach (KeyValuePair<string, StoryTrackDefinition> entry in _tracksById)
        {
            if (entry.Value.Mode != StoryTrackMode.Linear)
                continue;

            StoryEvent currentEvent = GetCurrentLinearEvent(entry.Value);
            linearTrackPositions[entry.Key] = currentEvent?.EventId ?? string.Empty;
        }

        return new Godot.Collections.Dictionary<string, Variant>
        {
            ["story_id"] = Story?.StoryId ?? string.Empty,
            ["completed_event_ids"] = completedEventIds,
            ["linear_track_positions"] = linearTrackPositions,
        };
    }

    public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        _completedEventIds.Clear();
        _linearCurrentEventIds.Clear();

        if (data == null)
            return Task.CompletedTask;

        if (data.TryGetValue("completed_event_ids", out Variant completedVariant))
        {
            Array<string> completedEventIds = completedVariant.AsGodotArray<string>();
            foreach (string eventId in completedEventIds)
            {
                if (!string.IsNullOrWhiteSpace(eventId))
                    _completedEventIds.Add(eventId);
            }
        }

        if (data.TryGetValue("linear_track_positions", out Variant positionsVariant))
        {
            Godot.Collections.Dictionary<string, Variant> positions = positionsVariant.AsGodotDictionary<
                string,
                Variant
            >();

            foreach (KeyValuePair<string, Variant> entry in positions)
            {
                string eventId = entry.Value.AsString();
                if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(eventId))
                    _linearCurrentEventIds[entry.Key] = eventId;
            }
        }

        return Task.CompletedTask;
    }

    private void OnCoreManagersLoaded()
    {
        _readyForChecks = true;
        _automaticCheckTimer = 0.0;
    }

    private void DisconnectLoadingCoordinator()
    {
        if (_loadingCoordinator != null)
            _loadingCoordinator.CoreManagersLoaded -= OnCoreManagersLoaded;

        _loadingCoordinator = null;
    }

    private void BuildStoryIndex()
    {
        _eventsById.Clear();
        _tracksById.Clear();

        if (Story == null)
        {
            GD.PushWarning("StoryManager has no StoryDefinition assigned.");
            return;
        }

        foreach (StoryTrackDefinition track in Story.Tracks)
        {
            if (track == null)
                continue;

            if (string.IsNullOrWhiteSpace(track.TrackId))
            {
                GD.PushError("A story track has an empty TrackId and will be ignored.");
                continue;
            }

            if (!_tracksById.TryAdd(track.TrackId, track))
            {
                GD.PushError($"Duplicate story TrackId '{track.TrackId}'. The later track is ignored.");
                continue;
            }

            foreach (StoryEvent storyEvent in track.Events)
            {
                if (storyEvent == null)
                    continue;

                if (string.IsNullOrWhiteSpace(storyEvent.EventId))
                {
                    GD.PushError($"A story event in track '{track.TrackId}' has an empty EventId.");
                    continue;
                }

                if (!_eventsById.TryAdd(storyEvent.EventId, new StoryEventRegistration(storyEvent, track)))
                {
                    GD.PushError(
                        $"Duplicate story EventId '{storyEvent.EventId}'. Event IDs must be unique across all tracks."
                    );
                }
            }
        }

        foreach (StoryTrackDefinition track in _tracksById.Values)
        {
            if (track.Mode != StoryTrackMode.Linear)
                continue;

            _linearCurrentEventIds.TryGetValue(track.TrackId, out string preferredEventId);
            ResolveLinearPosition(track, preferredEventId);
        }
    }

    private async Task EvaluateAutomaticEventsAsync()
    {
        if (_isExecuting || _deinitializing || Story == null)
            return;

        foreach (StoryTrackDefinition track in Story.Tracks)
        {
            if (track == null || !_tracksById.ContainsKey(track.TrackId))
                continue;

            StoryEvent candidate = GetAutomaticCandidate(track);
            if (candidate == null || !_eventsById.TryGetValue(candidate.EventId, out var registration))
                continue;

            await ExecuteStoryEventAsync(registration, false);

            // Execute at most one top-level event per check. This avoids a sequence of
            // newly eligible popups being created in a single frame.
            return;
        }
    }

    private StoryEvent GetAutomaticCandidate(StoryTrackDefinition track)
    {
        switch (track.Mode)
        {
            case StoryTrackMode.Linear:
            {
                StoryEvent currentEvent = GetCurrentLinearEvent(track);
                return IsEligibleAutomaticEvent(currentEvent) ? currentEvent : null;
            }

            case StoryTrackMode.Independent:
                foreach (StoryEvent storyEvent in track.Events)
                {
                    if (IsEligibleAutomaticEvent(storyEvent))
                        return storyEvent;
                }
                return null;

            case StoryTrackMode.RandomPool:
            {
                var candidates = new List<StoryEvent>();
                foreach (StoryEvent storyEvent in track.Events)
                {
                    if (IsEligibleAutomaticEvent(storyEvent))
                        candidates.Add(storyEvent);
                }

                if (candidates.Count == 0)
                    return null;

                int selectedIndex = (int)GD.RandRange(0, candidates.Count - 1);
                return candidates[selectedIndex];
            }

            default:
                return null;
        }
    }

    private bool IsEligibleAutomaticEvent(StoryEvent storyEvent)
    {
        return storyEvent != null
            && !IsEventCompleted(storyEvent.EventId)
            && storyEvent.CanAutomaticallyTrigger();
    }

    private async Task<StoryEventExecutionResult> ExecuteStoryEventAsync(
        StoryEventRegistration registration,
        bool ignoreConditions
    )
    {
        if (_isExecuting)
            return StoryEventExecutionResult.Blocked;

        StoryEvent storyEvent = registration.Event;
        if (!ignoreConditions && !storyEvent.CheckTriggerConditions())
            return StoryEventExecutionResult.Blocked;

        _isExecuting = true;
        SetIsBusy(true);
        EmitSignal(SignalName.StoryEventStarted, storyEvent.EventId);

        StoryEventExecutionResult result;
        try
        {
            result = await storyEvent.ExecuteCall(ignoreConditions);
        }
        catch (Exception exception)
        {
            GD.PushError($"Story event '{storyEvent.EventId}' failed: {exception}");
            result = StoryEventExecutionResult.Failed;
        }
        finally
        {
            _isExecuting = false;
            SetIsBusy(false);
        }

        if (result == StoryEventExecutionResult.Completed)
        {
            _completedEventIds.Add(storyEvent.EventId);

            if (registration.Track.Mode == StoryTrackMode.Linear)
                AdvanceLinearTrack(registration.Track, storyEvent);

            EmitSignal(SignalName.StoryEventCompleted, storyEvent.EventId);
        }
        else
        {
            EmitSignal(
                SignalName.StoryEventExecutionStopped,
                storyEvent.EventId,
                (int)result
            );
        }

        return result;
    }

    private StoryEvent GetCurrentLinearEvent(StoryTrackDefinition track)
    {
        _linearCurrentEventIds.TryGetValue(track.TrackId, out string preferredEventId);
        return ResolveLinearPosition(track, preferredEventId);
    }

    private StoryEvent ResolveLinearPosition(
        StoryTrackDefinition track,
        string preferredEventId
    )
    {
        int startIndex = 0;
        if (!string.IsNullOrWhiteSpace(preferredEventId))
        {
            for (int index = 0; index < track.Events.Count; index++)
            {
                StoryEvent storyEvent = track.Events[index];
                if (storyEvent?.EventId != preferredEventId)
                    continue;

                if (!IsEventCompleted(preferredEventId))
                {
                    _linearCurrentEventIds[track.TrackId] = preferredEventId;
                    return storyEvent;
                }

                startIndex = index + 1;
                break;
            }
        }

        for (int index = startIndex; index < track.Events.Count; index++)
        {
            StoryEvent storyEvent = track.Events[index];
            if (storyEvent == null || IsEventCompleted(storyEvent.EventId))
                continue;

            _linearCurrentEventIds[track.TrackId] = storyEvent.EventId;
            return storyEvent;
        }

        _linearCurrentEventIds.Remove(track.TrackId);
        return null;
    }

    private void AdvanceLinearTrack(StoryTrackDefinition track, StoryEvent completedEvent)
    {
        int completedIndex = track.Events.IndexOf(completedEvent);
        int nextIndex = Math.Max(completedIndex + 1, 0);

        for (int index = nextIndex; index < track.Events.Count; index++)
        {
            StoryEvent nextEvent = track.Events[index];
            if (nextEvent == null || IsEventCompleted(nextEvent.EventId))
                continue;

            _linearCurrentEventIds[track.TrackId] = nextEvent.EventId;
            return;
        }

        _linearCurrentEventIds.Remove(track.TrackId);
    }
}
