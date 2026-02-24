using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

[Tool]
[GlobalClass]
public partial class ItemData : Resource
{
	[Export]
	public int ItemID { get; protected set; }

	[Export]
	public string ItemName { get; protected set; }

	[Export(PropertyHint.MultilineText)]
	public string ItemDescription { get; protected set; }

	[Export]
	public Texture2D ItemIcon { get; protected set; }

	[Export] public bool globeOnly { get; protected set; } = false;

	[Export(PropertyHint.ResourceType, "GridShape")]
	public GridShape ItemShape { get; set; }

	[Export] public int weight;
	
	[Export] public Mesh ItemMesh { get; protected set; }
	[Export] public Vector3 visualScale = new Vector3(.01f, .01f, .01f);
	[Export] public Vector3 LeftHandItemPosition { get; protected set; }
	[Export] public Vector3 LeftHandItemRotation { get; protected set; }
	
	[Export] public Vector3 RightHandItemPosition { get; protected set; }
	[Export] public Vector3 RightHandItemRotation { get; protected set; }

	[Export]
	public Array<ActionDefinition> ActionDefinitions;
	
	[Export] public int MaxStackSize { get; protected set; } = 1;
	
	public static Item CreateItem(ItemData itemData)
	{
		Item retVal = new Item();
		
		
		retVal.Init((ItemData)itemData.Duplicate());
		return retVal;
	}
	
	public bool TryGetItemActionDefinition<T>(out T def) where T : ItemActionDefinition
	{
		def = null;
		if (ActionDefinitions == null || ActionDefinitions.Count == 0)
		{
			return false;
		}

		for (int i = 0; i < ActionDefinitions.Count; i++)
		{
			if (ActionDefinitions[i].GetType() == typeof(T))
			{
				def = ActionDefinitions[i] as T;
				return true;
			}
		}

		return false;
	}
	
	
	/// <summary>
	/// Returns the pixel-space rectangle for a specific cell of the item icon.
	/// Useful for AtlasTexture.Region.
	/// </summary>
	public Rect2 GetTextureRegionForCell(int localX, int localZ)
	{
		if (ItemIcon == null || ItemShape == null) return new Rect2();

		Vector2 texSize = ItemIcon.GetSize();
    
		float cellW = texSize.X / Mathf.Max(1, ItemShape.SizeX);
		float cellH = texSize.Y / Mathf.Max(1, ItemShape.SizeZ);

		return new Rect2(
			localX * cellW,
			localZ * cellH,
			cellW,
			cellH
		);
	}

	/// <summary>
	/// Returns normalized UV bounds (0.0 to 1.0) for a specific cell.
	/// Useful for custom shaders.
	/// </summary>
	public Rect2 GetUVBoundsForCell(int localX, int localY)
	{
		if (ItemShape == null) 
			return new Rect2(0, 0, 1, 1);

		float width = 1.0f / ItemShape.SizeX;
		float height = 1.0f / ItemShape.SizeZ;

		return new Rect2(
			localX * width, 
			localY * height, 
			width, 
			height
		);
	}
}