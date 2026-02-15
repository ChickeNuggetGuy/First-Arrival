using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstArrival.Scripts.Utility;
using Godot;

namespace FirstArrival.Scripts.Utility;


public static class NodeExtensions
{
	// Using LINQ for a more concise version:
	public static T FindChildByType<T>(this Node parent) where T : Node
	{
		return parent.GetChildren()
					 .OfType<Node>() // Ensure we're working with Node objects
					 .FirstOrDefault(child => child.GetType() == typeof(T)) as T;
	}


	public static Vector3 GetAbsoluteDirectionVector(this Enums.Direction direction)
	{
		switch (direction)
		{
			case Enums.Direction.North:
				return new Vector3(0, 0, 1);
			case Enums.Direction.NorthEast:
				return new Vector3(1, 0, 1);
			case Enums.Direction.East:
				return new Vector3(1, 0, 0);
			case Enums.Direction.SouthEast:
				return new Vector3(1, 0, -1);
			case Enums.Direction.South:
				return new Vector3(0, 0, -1);
			case Enums.Direction.SouthWest:
				return new Vector3(-1, 0, -1);
			case Enums.Direction.West:
				return new Vector3(-1, 0, 0);
			case Enums.Direction.NorthWest:
				return new Vector3(-1, 0, 1);
			default:
				return new Vector3(-1, -1, -1);
		}
	}

	public static Vector2 GetAbsoluteDirectionVector2D(this Enums.Direction direction)
	{
		switch (direction)
		{
			case Enums.Direction.North:
				return new Vector2(0, 1);
			case Enums.Direction.NorthEast:
				return new Vector2(1, 1);
			case Enums.Direction.East:
				return new Vector2(1, 0);
			case Enums.Direction.SouthEast:
				return new Vector2(1, 0 - 1);
			case Enums.Direction.South:
				return new Vector2(0, -1);
			case Enums.Direction.SouthWest:
				return new Vector2(-1, -1);
			case Enums.Direction.West:
				return new Vector2(-1, 0);
			case Enums.Direction.NorthWest:
				return new Vector2(-1, 1);
			default:
				return new Vector2(-1, - -1);
		}
	}

	public static Vector3 GetCellCenter(this GridCell gridCell)
	{
		return gridCell.WorldCenter;
	}
	
	public static void ChangeParent(this Node3D node, Node3D newParent)
	{
		if (node.GetParent() != null)
		{
			node.GetParent().RemoveChild(node); // Remove from old parent
		}
		newParent.AddChild(node); // Add to new parent
	}

	public static bool Contains<T>(this T[,] array, T value)
	{
		if (array == null)
			throw new System.ArgumentNullException(nameof(array));

		for (int i = 0; i < array.GetLength(0); i++)
		{
			for (int j = 0; j < array.GetLength(1); j++)
			{
				if (EqualityComparer<T>.Default.Equals(array[i, j], value))
					return true;
			}
		}
		return false;
	}


	public static bool TryGetAllComponentsInChildrenRecursive<T>(
		this Node node,
		out List<T> retList,
		string attachedScriptPropertyName = null
	) where T : class
	{
		retList = new List<T>();

		foreach (Node child in node.GetChildren())
		{
			// Check if the child itself is of type T.
			if (child is T component)
			{
				retList.Add(component);
			}
			// If not, and if a property name was provided, try to get that property.
			else if (!string.IsNullOrEmpty(attachedScriptPropertyName))
			{
				var propertyInfo = child.GetType().GetProperty(attachedScriptPropertyName);
				if (propertyInfo != null)
				{
					var propertyValue = propertyInfo.GetValue(child);
					if (propertyValue is T propertyComponent)
					{
						retList.Add(propertyComponent);
					}
				}
			}

			// Recursively check the child's children.
			if (child.TryGetAllComponentsInChildrenRecursive<T>(out List<T> childComponents, attachedScriptPropertyName))
			{
				retList.AddRange(childComponents);
			}
		}

		return retList.Count > 0;
	}
	
	public static bool TryGetAllComponentsInChildren<T>(
		this Node node,
		out List<T> retList,
		string attachedScriptPropertyName = null
	) where T : class
	{
		retList = new List<T>();

		foreach (Node child in node.GetChildren())
		{
			// Check if the child itself is of type T.
			if (child is T component)
			{
				retList.Add(component);
			}
		}

		return retList.Count > 0;
	}

	public static T GetOrCreateNodeAndAddAsChild<T>(this Node parent, string path) where T : Node, new()
	{
		T node = parent.GetNodeOrNull<T>(path);
		if (node == null)
		{
			node = new T();
			parent.AddChild(node);
		}
		return node;
	}
	
	
	/// <summary>
	/// Gets the first child node of the specified type, or null if none is found.
	/// </summary>
	public static T GetChildOfType<T>(this Node node) where T : Node
	{
		foreach (var child in node.GetChildren())
		{
			if (child is T typedChild)
			{
				return typedChild;
			}
		}
		return null;
	}

	/// <summary>
	/// Gets or creates a child node of the specified type.
	/// </summary>
	public static T GetOrCreateChildOfType<T>(this Node node) where T : Node, new()
	{
		T child = GetChildOfType<T>(node);
		if (child == null)
		{
			child = new T();
			node.AddChild(child);
		}
		return child;
	}


	/// <summary>
	/// Recursively searches for a parent node of the specified type, up to a maximum number of iterations.
	/// </summary>
	/// <typeparam name="T">The type of the parent node to search for.</typeparam>
	/// <param name="node">The starting node.</param>
	/// <param name="maxDepth">The maximum number of parent levels to check.</param>
	/// <returns>The first parent node of the specified type, or null if not found within the limit.</returns>
	public static T FindParentByTypeRecursive<T>(this Node node, int maxDepth = 4) where T : Node
	{
		int depth = 0;
		Node current = node.GetParent();

		while (current != null && depth < maxDepth)
		{
			if (current is T typedParent)
			{
				return typedParent;
			}

			current = current.GetParent();
			depth++;
		}

		return null;
	}

    public static Godot.Collections.Array<Resource> LoadFilesFromDirectory(string path)
    {
        var files = new Godot.Collections.Array<Resource>();
        var dir = DirAccess.Open(path);

        if (dir != null)
        {
            // List files in the directory
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (fileName != "")
            {
                if (!dir.CurrentIsDir()) // Skip directories
                {
                    string filePath = Path.Combine(path, fileName).Replace("\\", "/");
                    var resource = GD.Load<Resource>(filePath);
                    if (resource != null)
                    {
                        files.Add(resource);
                    }
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }
        else
        {
            GD.Print("Failed to open directory: ", path);
        }

        return files;
    }

    public static Godot.Collections.Array<Resource> LoadFilesOfTypeFromDirectory(string path, string resourceType)
    {
        var files = new Godot.Collections.Array<Resource>();
        var dir = DirAccess.Open(path);

        if (dir != null)
        {
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (fileName != "")
            {
                if (!dir.CurrentIsDir() && (fileName.EndsWith(".tres") || fileName.EndsWith(".res")))
                {
                    string filePath = Path.Combine(path, fileName).Replace("\\", "/");
                    var resource = GD.Load<Resource>(filePath);
                    if (resource != null)
                    {
                        // Check if it has the correct script assigned
                        Script script = (Script)resource.GetScript();
                        if (script != null && script.ResourcePath.GetFile().GetBaseName() == resourceType)
                        {
                            files.Add(resource);
                        }
                        else if (resource.IsClass(resourceType)) // Fallback to original check
                        {
                            files.Add(resource);
                        }
                        else
                        {
                            GD.Print("File: ", fileName, " Script: ", script, " Resource name: ", 
                                script != null ? script.ResourcePath.GetFile().GetBaseName() : "No script");
                        }
                    }
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }
        else
        {
            GD.Print("Failed to open directory: ", path);
        }

        return files;
    }
    
    
    /// <summary>
    /// Collects all CollisionShape3D nodes from a node's children.
    /// </summary>
    /// <param name="node">The root node to search from.</param>
    /// <param name="collisionShapes">Output list of found collision shapes.</param>
    /// <param name="recursive">If true, searches all descendants. If false, only immediate children.</param>
    /// <param name="includeDisabled">If true, includes disabled collision shapes.</param>
    /// <returns>True if any collision shapes were found.</returns>
    public static bool TryGetCollisionShapes(
	    this Node node,
	    out List<CollisionShape3D> collisionShapes,
	    bool recursive = true,
	    bool includeDisabled = false)
    {
	    collisionShapes = new List<CollisionShape3D>();
    
	    if (node is CollisionShape3D rootCs && rootCs.Shape != null && (includeDisabled || !rootCs.Disabled))
		    collisionShapes.Add(rootCs);
    
	    CollectCollisionShapesInternal(node, collisionShapes, recursive, includeDisabled);
    
	    return collisionShapes.Count > 0;
    }

    private static void CollectCollisionShapesInternal(
	    Node node,
	    List<CollisionShape3D> output,
	    bool recursive,
	    bool includeDisabled)
    {
	    foreach (Node child in node.GetChildren())
	    {
		    if (child is CollisionShape3D cs && cs.Shape != null && (includeDisabled || !cs.Disabled))
			    output.Add(cs);

		    if (recursive && child.GetChildCount() > 0)
			    CollectCollisionShapesInternal(child, output, recursive, includeDisabled);
	    }
    }
    
}