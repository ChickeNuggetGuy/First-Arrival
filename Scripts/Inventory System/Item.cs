using System.Collections.Generic;
using System.Linq;
using FirstArrival.Scripts.Managers;
using Godot;

namespace FirstArrival.Scripts.Inventory_System;

public partial class Item : Node3D, IContextUser<Item>
{
	public Item parent
	{
		get => this; set{} }
	public ItemData ItemData { get; protected set; }
	[Export]public MeshInstance3D Visual { get;protected set; }
	public int CurrentAmmo { get; private set; }
	public int AmmoCapacity => Mathf.Max(0, ItemData?.MagazineCapacity ?? 0);
	public bool IsRangedWeapon => ItemData?.ActionDefinitions?.Any(
		action => action is RangedAttackActionDefinition
	) == true;


	private InventoryGrid _currentGrid;
	public InventoryGrid currentGrid
	{
		get => _currentGrid;
		set
		{
			if (this is UnitBodyItem body) body.OnInventoryChanging(_currentGrid, value);
			_currentGrid = value;
		}
	}
	public void Init(ItemData itemData)
	{
		ItemData = itemData;
		CurrentAmmo = AmmoCapacity;
		if (Visual == null)
		{
			Visual = new MeshInstance3D();
			AddChild(Visual);
		}
		if (ItemData.ItemMesh != null)
		{
			Visual.Mesh = ItemData.ItemMesh;
			Visual.Scale = ItemData.visualScale;
			
			Visual.Rotation = ItemData.LeftHandItemRotation;
		}
	}

	public ItemData GetRequiredAmmoItem()
	{
		if (ItemData?.AmmoItem != null)
			return ItemData.AmmoItem;

		return ItemData?.ActionDefinitions?
			.OfType<RangedAttackActionDefinition>()
			.Select(definition => definition.ammoItem)
			.FirstOrDefault(ammo => ammo != null);
	}

	public bool HasAmmoForAttack(int ammoCost)
	{
		return IsRangedWeapon && ammoCost > 0 && CurrentAmmo >= ammoCost;
	}

	public bool TryConsumeAmmo(int ammoCost)
	{
		if (!HasAmmoForAttack(ammoCost)) return false;

		CurrentAmmo -= ammoCost;
		NotifyRuntimeStateChanged();
		return true;
	}

	public bool CanReloadWith(Item ammoItem, out string reason)
	{
		if (!IsRangedWeapon || AmmoCapacity <= 0)
		{
			reason = "Item is not a configured ranged weapon";
			return false;
		}

		if (CurrentAmmo >= AmmoCapacity)
		{
			reason = "Weapon is already fully loaded";
			return false;
		}

		ItemData requiredAmmo = GetRequiredAmmoItem();
		if (requiredAmmo == null)
		{
			reason = "Weapon has no ammo item configured";
			return false;
		}

		if (ammoItem?.ItemData == null || ammoItem.currentGrid == null)
		{
			reason = "Ammo is not in an inventory";
			return false;
		}

		if (ammoItem.ItemData.ItemID != requiredAmmo.ItemID)
		{
			reason = $"{ammoItem.ItemData.ItemName} is not compatible with {ItemData.ItemName}";
			return false;
		}

		reason = "Success";
		return true;
	}

	public bool TryReloadWith(Item ammoItem, out string reason)
	{
		if (!CanReloadWith(ammoItem, out reason)) return false;

		InventoryGrid ammoGrid = ammoItem.currentGrid;
		if (!ammoGrid.TryRemoveItem(ammoItem, 1))
		{
			reason = "Could not consume the ammo clip";
			return false;
		}

		CurrentAmmo = AmmoCapacity;
		NotifyRuntimeStateChanged();

		if (ammoItem.currentGrid == null && GodotObject.IsInstanceValid(ammoItem))
			ammoItem.QueueFree();

		reason = "Success";
		return true;
	}

	public void RestoreAmmo(int loadedAmmo)
	{
		CurrentAmmo = Mathf.Clamp(loadedAmmo, 0, AmmoCapacity);
		NotifyRuntimeStateChanged();
	}

	private void NotifyRuntimeStateChanged()
	{
		currentGrid?.NotifyItemChanged();
	}

	public virtual void ShowVisual(BoneAttachment3D attachment)
	{
		if (attachment != null)
		{
			attachment.AddChild(this);
		}
		
		if (ItemData.ItemMesh != null)
		{
			Visual.Mesh = ItemData.ItemMesh;
			
			Visual.Rotation = ItemData.LeftHandItemRotation;
		}
	}
	
	public virtual void HideVisual(BoneAttachment3D attachment)
	{
		if (attachment != null)
		{
			attachment.RemoveChild(this);
		}
		
	}




	public Dictionary<string,Callable> GetContextActions()
	{
		Dictionary<string,Callable> actions = new Dictionary<string,Callable>();
		foreach (var action in ItemData.ActionDefinitions ?? new Godot.Collections.Array<ActionDefinition>())
		{
			if (action is RangedAttackActionDefinition rangedAttackActionDefinition)
			{
				actions.Add(rangedAttackActionDefinition.Type.ToString().ToPascalCase() + " Shot" ,
					Callable.From(() => ActionManager.Instance.SetSelectedAction(action,
					new Dictionary<string, Variant>()
					{
						{ "item", this }
					})));
			}
			else
			{
				actions.Add(action.GetActionName(), Callable.From(() => ActionManager.Instance.SetSelectedAction(action,
					new Dictionary<string, Variant>()
					{
						{ "item", this }
					})));
			}
		}

		if (IsRangedWeapon && CurrentAmmo < AmmoCapacity)
		{
			actions["Reload"] = Callable.From(
				() => ActionManager.Instance?.RequestReload(this)
			);
		}

		if (ItemData?.IsAmmunition == true && ActionManager.Instance != null)
		{
			foreach (Item weapon in ActionManager.Instance.GetReloadableWeapons(this))
			{
				Item reloadWeapon = weapon;
				actions[$"Reload {reloadWeapon.ItemData.ItemName}"] = Callable.From(
					() => ActionManager.Instance?.RequestReload(reloadWeapon, this)
				);
			}
		}
		return actions;
	}
	
}
