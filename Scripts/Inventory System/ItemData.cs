using FirstArrival.Scripts.ActionSystem.ItemActions;
using FirstArrival.Scripts.Utility;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Inventory_System;

public enum ItemCategory
{
	General,
	Weapon,
	Explosive,
	Craft
}

public enum WeaponClass
{
	None,
	Melee,
	Sidearm,
	SubmachineGun,
	PersonalDefenseWeapon,
	Shotgun,
	AssaultRifle,
	MarksmanRifle,
	SniperRifle,
	LightMachineGun,
	GeneralPurposeMachineGun
}

public enum EquipmentTier
{
	Starting,
	Standard,
	Advanced,
	Experimental
}

[Tool]
[GlobalClass]
public partial class ItemData : Resource
{
	[Export]
	public int ItemID { get; protected set; } = -1;

	[Export]
	public string ItemName { get; protected set; }

	[Export(PropertyHint.MultilineText)]
	public string ItemDescription { get; protected set; }

	[Export]
	public Texture2D ItemIcon { get; protected set; }

	[Export] public bool globeOnly { get; protected set; } = false;

	[ExportGroup("Classification")]
	[Export] public ItemCategory Category { get; protected set; } = ItemCategory.General;
	[Export] public WeaponClass WeaponClass { get; protected set; } = WeaponClass.None;
	[Export(PropertyHint.Range, "1900,2100,1")]
	public int HistoricalIntroductionYear { get; protected set; } = 2001;

	[ExportGroup("Progression")]
	[Export] public EquipmentTier ResearchTier { get; protected set; } = EquipmentTier.Starting;
	[Export] public string RequiredResearch { get; protected set; } = string.Empty;
	[Export] public bool AvailableAtCampaignStart { get; protected set; } = true;

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

	[ExportGroup("Ammunition")]
	[Export] public bool IsAmmunition { get; protected set; }
	[Export(PropertyHint.Range, "0,500,1")]
	public int AmmoDamage { get; protected set; }
	[Export] public ItemData AmmoItem { get; protected set; }
	[Export(PropertyHint.Range, "0,500,1")]
	public int MagazineCapacity { get; protected set; }
	[Export(PropertyHint.Range, "0,100,1")]
	public int ReloadTimeUnitCost { get; protected set; } = 25;
	
	[Export] public int MaxStackSize { get; protected set; } = 1;
	
	
	[ExportGroup("Trading")]
	[Export] public bool ShowInBuySellWindow { get; protected set; } = true;
	[Export] public int buyPrice;
	[Export] public int sellPrice;
	
	public static Item CreateItem(ItemData itemData)
	{
		Item retVal = new Item();
		
		
		retVal.Init((ItemData)itemData.Duplicate());
		return retVal;
	}

	public static ItemData CreateBodyData(string unitName, bool dead)
	{
		var shape = new GridShape { SizeX = 2, SizeY = 1, SizeZ = 3 };
		shape.FillAll(true);
		return new ItemData
		{
			ItemID = -2,
			ItemName = $"{unitName} ({(dead ? "dead" : "unconscious")})",
			ItemDescription = "A body linked to the original unit.",
			ItemShape = shape,
			ItemIcon = GD.Load<Texture2D>("res://Data/InventorySystem/unit_body.svg"),
			MaxStackSize = 1,
			weight = 4,
			ShowInBuySellWindow = false,
			ActionDefinitions = new() { new ThrowActionDefinition() },
			ItemMesh = new CapsuleMesh { Radius = 0.22f, Height = 1.5f },
			visualScale = Vector3.One,
			LeftHandItemRotation = new Vector3(Mathf.Pi / 2, 0, 0)
		};
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
		int columns = Mathf.Max(1, ItemShape.SizeX);
		int rows = Mathf.Max(1, ItemShape.SizeZ);

		// Snap both edges independently so textures whose dimensions are not evenly
		// divisible by the item shape still produce adjoining, non-overlapping regions.
		float left = Mathf.Round(texSize.X * localX / columns);
		float top = Mathf.Round(texSize.Y * localZ / rows);
		float right = Mathf.Round(texSize.X * (localX + 1) / columns);
		float bottom = Mathf.Round(texSize.Y * (localZ + 1) / rows);

		return new Rect2(
			left,
			top,
			right - left,
			bottom - top
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
