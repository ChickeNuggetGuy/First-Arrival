using Godot;
using System;
using System.Linq; // Needed for sorting
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

		// 1. Collect all valid items into a temporary list
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
							foundItems.Add(new ItemInfo { Data = item, FileName = fileName, FullPath = fullPath });
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

		// 2. Sort them Alphabetically by Filename
		// This ensures that 'Apple.tres' always gets a lower ID than 'Sword.tres',
		// making the ID assignment deterministic/stable.
		foundItems.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));

		// 3. Re-assign IDs sequentially
		int newIdCounter = 0;
		int fixedCount = 0;

		foreach (var info in foundItems)
		{
			ItemData item = info.Data;

			// Check if ID needs update
			if (item.ItemID != newIdCounter)
			{
				// Use Set because the setter is protected
				item.Set("ItemID", newIdCounter);
				
				// SAVE the change to disk so it sticks
				ResourceSaver.Save(item, info.FullPath);
				fixedCount++;
			}

			// Add to dictionary
			if (!Items.ContainsKey(newIdCounter))
			{
				Items.Add(newIdCounter, item);
			}

			newIdCounter++;
		}
    
		EmitChanged();
		
		if (fixedCount > 0)
			GD.Print($"[ItemDatabase] Auto-fixed {fixedCount} IDs.");
			
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
		public string FullPath;
	}
}