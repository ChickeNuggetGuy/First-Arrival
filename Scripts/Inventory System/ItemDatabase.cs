using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using FirstArrival.Scripts.Inventory_System;

namespace FirstArrival.Scripts.Inventory_System;

[Tool]
[GlobalClass]
public partial class ItemDatabase : Resource
{
	[Export] public string DirectoryPath = "res://Data/Items/";

	[Export] public Godot.Collections.Dictionary<int, ItemData> Items = new();

	[Export]
	private bool UpdateDatabase
	{
		get => false;
		set
		{
			if (value)
			{
				PopulateDatabase();
			}
		}
	}

	private void PopulateDatabase()
	{
		Items.Clear();

		if (!DirAccess.DirExistsAbsolute(DirectoryPath))
		{
			GD.PrintErr($"[ItemDatabase] Path does not exist: {DirectoryPath}");
			return;
		}

		List<ItemInfo> foundItems = new List<ItemInfo>();

		using var dir = DirAccess.Open(DirectoryPath);
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();

			while (fileName != "")
			{
				if (!dir.CurrentIsDir() && (fileName.EndsWith(".tres") || fileName.EndsWith(".res")))
				{
					string fullPath = $"{DirectoryPath}/{fileName}";

					try 
					{
						Resource rawRes = ResourceLoader.Load(fullPath, "", ResourceLoader.CacheMode.Replace);
						
						if (rawRes is ItemData item)
						{
							foundItems.Add(new ItemInfo { Data = item, FileName = fileName });
						}
					}
					catch (System.Exception e)
					{
						GD.PrintErr($"[ItemDatabase] Error loading '{fileName}': {e.Message}");
					}
				}
				fileName = dir.GetNext();
			}
		}

		foundItems.Sort((a, b) =>
		{
			int idComparison = a.Data.ItemID.CompareTo(b.Data.ItemID);
			return idComparison != 0
				? idComparison
				: string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
		});

		foreach (var info in foundItems)
		{
			ItemData item = info.Data;

			if (item.ItemID < 0)
			{
				GD.PushError(
					$"[ItemDatabase] '{info.FileName}' has no stable ItemID. " +
					"Assign a non-negative ID before adding it to the database."
				);
				continue;
			}

			if (Items.ContainsKey(item.ItemID))
			{
				GD.PushError(
					$"[ItemDatabase] Duplicate ItemID {item.ItemID} in '{info.FileName}'. " +
					$"Keeping '{Items[item.ItemID].ItemName}' and skipping '{item.ItemName}'."
				);
				continue;
			}

			Items.Add(item.ItemID, item);
		}
    
		EmitChanged();
		GD.Print($"[ItemDatabase] Scan complete. Database contains {Items.Count} items.");
	}

	public ItemData GetItem(int id)
	{
		return Items.GetValueOrDefault(id);
	}

	public List<ItemData> GetAllItems()
	{
		return Items.Values.ToList();
	}

	// Helper struct for sorting
	private struct ItemInfo
	{
		public ItemData Data;
		public string FileName;
	}
}
