using System.Collections.Generic;
using Godot;

namespace BehaviorTree.Core;

/// <summary>
/// Shared key-value store that allows nodes to communicate
/// without direct coupling.
/// </summary>
[GlobalClass]
public partial class Blackboard : Resource
{
	private readonly Dictionary<string, Variant> _data = new();

	public void Set(string key, Variant value) => _data[key] = value;

	public Variant Get(string key, Variant defaultValue = default)
	{
		return _data.TryGetValue(key, out var value) ? value : defaultValue;
	}
	
	public T Get<[MustBeVariant] T>(string key, T defaultValue = default)
	{
		if (_data.TryGetValue(key, out var value))
		{
			try
			{
				// Now safe to call As<T>() because T is guaranteed to be a Variant
				return value.As<T>();
			}
			catch
			{
				// Fallback if conversion fails (e.g. asking for int but stored Vector3)
				return defaultValue;
			}
		}
		return defaultValue;
	}

	public bool Has(string key) => _data.ContainsKey(key);

	public void Remove(string key) => _data.Remove(key);

	public void Clear() => _data.Clear();
}