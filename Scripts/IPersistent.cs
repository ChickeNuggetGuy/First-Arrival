using Godot;
using System;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

public interface IPersistent<T> where T : Node
{
	public T owner { get; set; }
	
	public Godot.Collections.Dictionary<string, Variant> SaveCall()
	{
		Godot.Collections.Dictionary<string, Variant> data =  new Godot.Collections.Dictionary<string, Variant>();
		data = Save();
		if (owner.TryGetAllComponentsInChildrenRecursive<IPersistent<T>>(out List<IPersistent<T>> saveList))
		{
			foreach (var saveNode in saveList)
			{
				data.Add(saveNode.owner.Name, saveNode.Save());
			}

		}
		return data;
	}

	public Godot.Collections.Dictionary<string, Variant> Save();
	
	public void Load(Godot.Collections.Dictionary<string,Variant> data);
}
