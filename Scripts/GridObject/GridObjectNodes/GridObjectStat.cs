using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Utility;

[GlobalClass,Tool]
public partial class GridObjectStat : GridObjectNode
{
	private const int FatalWoundGuaranteedDamage = 11;
	private const int HealthRestoredPerFatalWound = 3;
	private const float PenaltyPerFatalWound = 0.1f;
	private const float MaximumFatalWoundPenalty = 0.9f;

	private static readonly Enums.BodyPart[] WoundableBodyParts =
	[
		Enums.BodyPart.Head,
		Enums.BodyPart.Torso,
		Enums.BodyPart.LeftArm,
		Enums.BodyPart.RightArm,
		Enums.BodyPart.LeftLeg,
		Enums.BodyPart.RightLeg
	];

	private readonly Dictionary<Enums.BodyPart, int> _fatalWounds = new();

	[Export] public Enums.Stat Stat { get; private set; }
	[Export] public float CurrentValue { get; protected set; } = -1;
	[Export] public bool CanReceiveFatalWounds { get; set; }
	[Export] public Enums.BodyPart WeaponHoldingArm { get; set; } =
		Enums.BodyPart.RightArm;

	[Export] int minValue = -1;
	[Export] int maxValue = -1;

	public (int min, int max) MinMaxValue
	{
		get => (minValue, maxValue);
		protected set
		{
			minValue = value.min;
			maxValue = value.max;
		}
	}

	[Export] protected bool signalOnMinValue = false;
	[Export] protected bool signalOnMaxValue = false;

	[Export] public Enums.StatTurnBehavior turnBehavior = Enums.StatTurnBehavior.None;
	 public float incrementValue = 0;
	 public float decrementValue = 0;
	[Signal] public delegate void CurrentValueChangedEventHandler(int value, GridObject gridObject);
	[Signal] public delegate void CurrentValueMinEventHandler(int value, GridObject gridObject);
	[Signal] public delegate void CurrentValueMaxEventHandler(int value, GridObject gridObject);
	[Signal] public delegate void FatalWoundsChangedEventHandler(
		int totalWounds,
		int bodyPart,
		int woundsOnBodyPart,
		GridObject gridObject
	);

	
	public GridObjectStat()
	{
		Stat = Enums.Stat.None;
		CurrentValue = 0;
	}
	public GridObjectStat(Enums.Stat statType, float currentValue, int minValue, int maxValue)
	{
		Stat = statType;
		CurrentValue = currentValue;
		this.minValue = minValue;
		this.maxValue = maxValue;
	}
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
		if (minValue == -1)
		{
			minValue = 0;
		}

		if (maxValue == -1)
		{
			maxValue = GD.RandRange(50, 100);
		}
		
		CurrentValue = MinMaxValue.max;
		EmitSignal(SignalName.CurrentValueChanged, CurrentValue,parentGridObject);
	}

	public void AddValue(float value)
	{
		float old = CurrentValue;
		CurrentValue = Mathf.Clamp(CurrentValue + value, minValue, maxValue);

		if (CurrentValue != old)
		{
			EmitSignal(SignalName.CurrentValueChanged, CurrentValue, parentGridObject);

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
				EmitSignal(SignalName.CurrentValueMin, CurrentValue, parentGridObject);
			}
		}
		
	}

	public void SetValue(float value)
	{
		float old = CurrentValue;
		CurrentValue = Mathf.Clamp(value, minValue, maxValue);

		if (CurrentValue != old)
		{
			EmitSignal(SignalName.CurrentValueChanged, CurrentValue, parentGridObject);
		}
		
		if (CurrentValue <= minValue && signalOnMinValue)
		{
			EmitSignal(SignalName.CurrentValueMin, CurrentValue,parentGridObject);
		}
		
		if (CurrentValue >= maxValue && signalOnMaxValue)
		{
			EmitSignal(SignalName.CurrentValueMax, CurrentValue, parentGridObject);
		}
	}

	public DamageResult ApplyDamage(
		float damage,
		bool canCauseFatalWounds = true,
		Enums.BodyPart bodyPart = Enums.BodyPart.None
	)
	{
		if (damage <= 0)
		{
			return new DamageResult(0, 0, Enums.BodyPart.None);
		}

		float previousValue = CurrentValue;
		RemoveValue(damage);
		float healthDamage = previousValue - CurrentValue;

		if (
			Stat != Enums.Stat.Health
			|| healthDamage <= 0
			|| CurrentValue <= minValue
			|| !CanReceiveFatalWounds
			|| !canCauseFatalWounds
			|| !RollsFatalWounds(healthDamage, GD.Randf())
		)
		{
			return new DamageResult(healthDamage, 0, Enums.BodyPart.None);
		}

		Enums.BodyPart woundedBodyPart = bodyPart == Enums.BodyPart.None
			? WoundableBodyParts[GD.RandRange(0, WoundableBodyParts.Length - 1)]
			: bodyPart;
		int woundsAdded = GD.RandRange(1, 3);
		AddFatalWounds(woundedBodyPart, woundsAdded);

		return new DamageResult(healthDamage, woundsAdded, woundedBodyPart);
	}

	public static float GetFatalWoundChance(float healthDamage)
	{
		if (healthDamage <= 0) return 0;
		return Mathf.Clamp(healthDamage / FatalWoundGuaranteedDamage, 0, 1);
	}

	public static bool RollsFatalWounds(float healthDamage, float roll)
	{
		float chance = GetFatalWoundChance(healthDamage);
		return chance >= 1f || roll < chance;
	}

	public int GetFatalWounds(Enums.BodyPart bodyPart)
	{
		return _fatalWounds.GetValueOrDefault(bodyPart);
	}

	public int GetTotalFatalWounds()
	{
		return _fatalWounds.Values.Sum();
	}

	public IReadOnlyDictionary<Enums.BodyPart, int> GetFatalWoundsByBodyPart()
	{
		return _fatalWounds;
	}

	public bool HealFatalWound(Enums.BodyPart bodyPart)
	{
		if (GetFatalWounds(bodyPart) <= 0) return false;

		_fatalWounds[bodyPart]--;
		if (_fatalWounds[bodyPart] == 0)
		{
			_fatalWounds.Remove(bodyPart);
		}

		AddValue(HealthRestoredPerFatalWound);
		EmitFatalWoundsChanged(bodyPart);
		return true;
	}

	public int HealFatalWounds(Enums.BodyPart bodyPart, int amount)
	{
		int healed = 0;
		for (int i = 0; i < amount && HealFatalWound(bodyPart); i++)
		{
			healed++;
		}

		return healed;
	}

	public int ApplyFatalWoundBleeding()
	{
		if (Stat != Enums.Stat.Health || CurrentValue <= minValue) return 0;

		int bleedingDamage = GetTotalFatalWounds();
		if (bleedingDamage > 0)
		{
			RemoveValue(bleedingDamage);
		}

		return bleedingDamage;
	}

	public float GetRangedAccuracyMultiplier()
	{
		int accuracyWounds =
			GetFatalWounds(Enums.BodyPart.Head)
			+ GetFatalWounds(WeaponHoldingArm);
		return GetFatalWoundStatMultiplier(
			accuracyWounds,
			MaximumFatalWoundPenalty
		);
	}

	public float GetTimeUnitMultiplier()
	{
		int legWounds =
			GetFatalWounds(Enums.BodyPart.LeftLeg)
			+ GetFatalWounds(Enums.BodyPart.RightLeg);
		return GetFatalWoundStatMultiplier(legWounds, 1f);
	}

	public float GetStaminaRecoveryPenalty(float currentStamina)
	{
		return Mathf.Max(
			0,
			currentStamina
			* PenaltyPerFatalWound
			* GetFatalWounds(Enums.BodyPart.Torso)
		);
	}

	private static float GetFatalWoundStatMultiplier(int wounds, float maximumPenalty)
	{
		float penalty = Mathf.Min(wounds * PenaltyPerFatalWound, maximumPenalty);
		return 1f - penalty;
	}

	private void AddFatalWounds(Enums.BodyPart bodyPart, int amount)
	{
		if (bodyPart == Enums.BodyPart.None || amount <= 0) return;

		_fatalWounds[bodyPart] = GetFatalWounds(bodyPart) + amount;
		EmitFatalWoundsChanged(bodyPart);
	}

	private void EmitFatalWoundsChanged(Enums.BodyPart bodyPart)
	{
		EmitSignal(
			SignalName.FatalWoundsChanged,
			GetTotalFatalWounds(),
			(int)bodyPart,
			GetFatalWounds(bodyPart),
			parentGridObject
		);
	}
	
	
	public void OnTurnEnded(
		float incrementMultiplier = 1f,
		float incrementPenalty = 0f
	)
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
				float normalIncrement;
				if (incrementValue > 0 && incrementValue < 1)
				{
					//Percentage Increment
					normalIncrement = maxValue * incrementValue;
				}
				else
				{
					normalIncrement = incrementValue;
				}
				AddValue(
					Mathf.Max(
						0,
						normalIncrement * incrementMultiplier - incrementPenalty
					)
				);

				break;
			case Enums.StatTurnBehavior.Decrement:
				if (decrementValue > 0 && decrementValue < 1)
				{
					//Percentage decrement
					RemoveValue(maxValue * decrementValue);
				}
				else
				{
					RemoveValue(decrementValue);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
	
	public override Godot.Collections.Dictionary<string, Variant> Save()
	{
		var retVal =  new Godot.Collections.Dictionary<string, Variant>();
		
		retVal["min"] = MinMaxValue.min;
		retVal["max"] = MinMaxValue.max;
		retVal["current"] = CurrentValue;
		retVal["signalOnMin"] = signalOnMinValue;
		retVal["signalOnMax"] = signalOnMaxValue;
		retVal["canReceiveFatalWounds"] = CanReceiveFatalWounds;
		retVal["weaponHoldingArm"] = (int)WeaponHoldingArm;

		var fatalWounds = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var wound in _fatalWounds)
		{
			fatalWounds[wound.Key.ToString()] = wound.Value;
		}
		retVal["fatalWounds"] = fatalWounds;
		
		return retVal;
	}

	public override void Load(Godot.Collections.Dictionary<string, Variant> data)
	{
		minValue = (int)data["min"];
		maxValue = (int)data["max"];
		CurrentValue =  (float)data["current"];
		signalOnMinValue = (bool)data["signalOnMin"];
		signalOnMaxValue = (bool)data["signalOnMax"];

		if (data.ContainsKey("canReceiveFatalWounds"))
		{
			CanReceiveFatalWounds = data["canReceiveFatalWounds"].AsBool();
		}
		if (data.ContainsKey("weaponHoldingArm"))
		{
			WeaponHoldingArm =
				(Enums.BodyPart)data["weaponHoldingArm"].AsInt32();
		}

		_fatalWounds.Clear();
		if (data.ContainsKey("fatalWounds"))
		{
			var fatalWounds =
				(Godot.Collections.Dictionary<string, Variant>)data["fatalWounds"];
			foreach (var wound in fatalWounds)
			{
				if (
					Enum.TryParse(wound.Key, out Enums.BodyPart bodyPart)
					&& bodyPart != Enums.BodyPart.None
				)
				{
					_fatalWounds[bodyPart] = wound.Value.AsInt32();
				}
			}
		}
	}

	public readonly record struct DamageResult(
		float HealthDamage,
		int FatalWoundsAdded,
		Enums.BodyPart WoundedBodyPart
	);
}
