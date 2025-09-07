using Godot;
using System;
using FirstArrival.Scripts.Utility;

[GlobalClass]
public partial class GridObjectStat : GridObjectNode
{
	[Export] public Enums.Stat Stat { get;private set; }
	public int CurrentValue { get; protected set; }
	
	[Export] int minValue = 0;
	[Export] int maxValue = 0;
	public (int min, int max) MinMaxValue {
		get
		{
			return (minValue, maxValue);
		} protected set
		{
			minValue = value.min;
			maxValue = value.max;
		}
	}
	
	[Export] private bool signalOnMinValue = false;
	[Export] private bool signalOnMaxValue = false;
	[Signal]
	public delegate void CurrentValueChangedEventHandler(int value);
	[Signal]
	public delegate void CurrentValueMinEventHandler(int value);
	[Signal]
	public delegate void CurrentValueMaxEventHandler(int value);
	protected override void Setup()
	{
		CurrentValue = MinMaxValue.max;
		EmitSignal(SignalName.CurrentValueChanged, this, parentGridObject);
	}

	public void AddValue(int value)
	{
		CurrentValue = ((CurrentValue + value) >= MinMaxValue.max) ? MinMaxValue.max :CurrentValue +  value;

		if (CurrentValue == maxValue && signalOnMaxValue)
		{
			EmitSignal(SignalName.CurrentValueMax, CurrentValue);
		}
	}

	public void RemoveValue(int value)
	{
		CurrentValue = ((CurrentValue - value) <= minValue) ? MinMaxValue.min : CurrentValue -  value;

		if (CurrentValue == MinMaxValue.min && signalOnMinValue)
		{
			EmitSignal(SignalName.CurrentValueMin, minValue);
		}

		// GD.Print(CurrentValue);
	}
}
