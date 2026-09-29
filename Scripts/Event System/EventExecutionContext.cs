using System;
using System.Threading;
using Godot;

public sealed class EventExecutionContext
{
	public Node Owner { get; init; }
	public CancellationToken CancellationToken { get; init; }
	public Func<string, bool> IsCompleted { get; init; }
	public Action<string> Started { get; init; }
	public Action<string> Completed { get; init; }
}
