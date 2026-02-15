using Godot;
using System;

public interface IDelayedAction
{
	int TurnsRemaining { get; set; }
	bool OnTurnTick();
	void OnDelayComplete();
}