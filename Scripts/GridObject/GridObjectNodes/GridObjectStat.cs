using Godot;
using System;
using FirstArrival.Scripts.Utility;

[GlobalClass,Tool]
public partial class GridObjectStat : GridObjectNode
{
	[Export] public Enums.Stat Stat { get; private set; }
	[Export] public float CurrentValue { get; protected set; }

	[Export] int minValue = 0;
	[Export] int maxValue = 0;

	public (int min, int max) MinMaxValue
	{
		get => (minValue, maxValue);
		protected set
		{
			minValue = value.min;
			maxValue = value.max;
		}
	}

	[Export] private bool signalOnMinValue = false;
	[Export] private bool signalOnMaxValue = false;

	[Export] public Enums.StatTurnBehavior turnBehavior = Enums.StatTurnBehavior.None;
	 public float incrementValue = 0;
	 public float decrementValue = 0;
	[Signal] public delegate void CurrentValueChangedEventHandler(int value, GridObject gridObject);
	[Signal] public delegate void CurrentValueMinEventHandler(int value, GridObject gridObject);
	[Signal] public delegate void CurrentValueMaxEventHandler(int value, GridObject gridObject);

	
	public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
	{
		Godot.Collections.Array<Godot.Collections.Dictionary> properties = [];

		if (turnBehavior.HasFlag(Enums.StatTurnBehavior.Decrement))
		{
			properties.Add(new Godot.Collections.Dictionary()
			{
				{ "name", $"decrementValue" },
				{ "type", (int)Variant.Type.Float },
				{ "hint_string", "Decrement" },
				
			});
		}
		
		if (turnBehavior.HasFlag(Enums.StatTurnBehavior.Increment))
		{
			properties.Add(new Godot.Collections.Dictionary()
			{
				{ "name", $"incrementValue" },
				{ "type", (int)Variant.Type.Float },
				{ "hint_string", "Increment" },
			});
		}

		return properties;
	}

	protected override void Setup()
	{
		CurrentValue = MinMaxValue.max;
		EmitSignal(SignalName.CurrentValueChanged, CurrentValue,parentGridObject);
	}

	public void AddValue(float value)
	{
		float old = CurrentValue;
		CurrentValue = Mathf.Clamp(CurrentValue + value, minValue, maxValue);

		if (CurrentValue != old)
		{
			EmitSignal(SignalName.CurrentValueChanged, CurrentValue);

			if (CurrentValue >= maxValue && signalOnMaxValue)
			{
				EmitSignal(SignalName.CurrentValueMax, CurrentValue, parentGridObject);
			}
		}
	}

	public void RemoveValue(float value)
	{
		float old = CurrentValue;
		CurrentValue = Mathf.Clamp(CurrentValue - value, minValue, maxValue);

		if (CurrentValue != old)
		{
			EmitSignal(SignalName.CurrentValueChanged, CurrentValue,parentGridObject);

			if (CurrentValue <= minValue && signalOnMinValue)
			{
				EmitSignal(SignalName.CurrentValueMin, CurrentValue,parentGridObject);
			}
		}
		
	}

	public void SetValue(float value)
	{
		float old = CurrentValue;
		CurrentValue = Mathf.Clamp(CurrentValue + value, minValue, maxValue);
		
		if (CurrentValue <= minValue && signalOnMinValue)
		{
			EmitSignal(SignalName.CurrentValueMin, CurrentValue,parentGridObject);
		}
		
		if (CurrentValue >= maxValue && signalOnMaxValue)
		{
			EmitSignal(SignalName.CurrentValueMax, CurrentValue, parentGridObject);
		}
	}
	
	
	public void OnTurnEnded()
	{
		switch (turnBehavior)
		{
			case Enums.StatTurnBehavior.None:
				break;
			case Enums.StatTurnBehavior.ResetToMax:
				SetValue(maxValue);
				break;
			case Enums.StatTurnBehavior.ResetToMin:
				SetValue(minValue);
				break;
			case Enums.StatTurnBehavior.Increment:
				if (incrementValue > 0 && incrementValue < 1)
				{
					//Percentage Increment
					AddValue(CurrentValue * incrementValue);
				}
				else
				{
					AddValue(incrementValue);
				}

				break;
			case Enums.StatTurnBehavior.Decrement:
				if (decrementValue > 0 && decrementValue < 1)
				{
					//Percentage decrement
					RemoveValue(CurrentValue * decrementValue);
				}
				else
				{
					RemoveValue(incrementValue);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}