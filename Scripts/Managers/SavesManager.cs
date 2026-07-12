using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class SavesManager : Manager<SavesManager>
{
    private const string SaveExt = ".sav";
    public const string AutosaveName = "autosave";
    private const string SceneDataKey = "scene_data";
	
    
    [Export] public string saveDir = "user://saves/";
    public string currentSavename = "new SaveGame";

    public Godot.Collections.Dictionary<String, Variant> SessionData { protected get; set; } = new();
    public static Godot.Collections.Dictionary<string, Variant> PendingSaveData { get; set; }
    public static bool LoadFromAutosave { get; set; }
    public static string PendingSaveName { get; set; } = "";
	
    [Signal]
    public delegate void GameSavesChangedEventHandler();

    public override string GetManagerName() => "SavesManager";

    protected override Task _Setup(bool loadingData) => Task.CompletedTask;
    protected override Task _Execute(bool loadingData) => Task.CompletedTask;
	

    /// <summary>
    /// Gathers the full game state from all active managers and the GameManager Autoload.
    /// </summary>
    public Godot.Collections.Dictionary<string, Variant> PackageFullState()
    {
        var gm = GameManager.Instance;
        if (gm == null) return new Godot.Collections.Dictionary<string, Variant>();

        var managersDict = new Godot.Collections.Dictionary<string, Variant>();

        // 1. Pack all managers currently tracked by the GameManager (Globals + Scene Locals)
        foreach (var m in gm.activeSceneManagers)
        {
            if (GodotObject.IsInstanceValid(m))
            {
                managersDict[m.GetManagerName()] = m.Save();
            }
        }

        // 2. Pack the GameManager itself
        managersDict[gm.GetManagerName()] = gm.Save();

        return new Godot.Collections.Dictionary<string, Variant>
        {
            ["version"] = 1,
            ["scene"] = gm.currentScene.ToString(),
            ["managers"] = managersDict,
            ["save_name"] = currentSavename
        };
    }
	

    public bool SaveGame(string saveName, GameManager.GameScene scene, bool isNewGame = false)
    {
        EnsureSaveDir();
        if (!saveName.EndsWith(SaveExt))
            saveName += SaveExt;
        string fullPath = saveDir.PathJoin(saveName);

        Godot.Collections.Dictionary<string, Variant> existingRoot = null;
        if (!isNewGame && FileAccess.FileExists(fullPath))
        {
            using var readFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            if (readFile != null)
            {
                try { existingRoot = readFile.GetVar().AsGodotDictionary<string, Variant>(); }
                catch { /* ignore corrupted */ }
            }
        }

        var newRoot = PackageFullState();
        newRoot["isNewGame"] = isNewGame;

        if (existingRoot != null && existingRoot.ContainsKey(SceneDataKey))
            newRoot[SceneDataKey] = existingRoot[SceneDataKey];

        using var writeFile = FileAccess.Open(fullPath, FileAccess.ModeFlags.Write);
        if (writeFile == null) return false;

        writeFile.StoreVar(newRoot);
        EmitSignal(SignalName.GameSavesChanged);
        return true;
    }

    /// <summary>
    /// Reads from disk and hands transition control to GameManager.
    /// </summary>
    public async Task<bool> LoadSaveAsync(string saveName)
    {
        string fullName = saveName.EndsWith(SaveExt) ? saveName : saveName + SaveExt;
        string path = saveDir.PathJoin(fullName);

        if (!FileAccess.FileExists(path)) return false;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return false;

        try
        {
            var root = file.GetVar().AsGodotDictionary<string, Variant>();

            PendingSaveData = root;
            LoadFromAutosave = false;
            PendingSaveName = saveName.Replace(SaveExt, "");
            currentSavename = PendingSaveName;

            if (root.TryGetValue("scene", out var sceneVar) &&
                Enum.TryParse<GameManager.GameScene>(sceneVar.AsString(), out var scene))
            {
                // Route through GameManager to handle scene change and manager discovery
                await GameManager.Instance.ChangeSceneAsync(scene, true);
                return true;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"LoadSaveAsync failed: {e.Message}");
        }
        return false;
    }

    public async Task  LoadSceneData(GameManager.GameScene scene)
    {
	    
    }

    public Godot.Collections.Dictionary<string, Variant> GetSceneTransitionState()
    {
		return PackageFullState();
    }

    public void UpdateGlobeTransitionBase(TeamBaseCellDefinition updatedBase)
    {
        if (string.IsNullOrEmpty(currentSavename) || currentSavename == "new SaveGame") return;

        string fullPath = saveDir.PathJoin(currentSavename + SaveExt);
        if (!FileAccess.FileExists(fullPath)) return;

        Godot.Collections.Dictionary<string, Variant> root;
        using (var read = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read))
            root = read.GetVar().AsGodotDictionary<string, Variant>();

        if (!root.TryGetValue(SceneDataKey, out var sceneDataVar)) return;
        var sceneData = sceneDataVar.AsGodotDictionary<string, Variant>();

        if (!sceneData.TryGetValue(GameManager.GameScene.GlobeScene.ToString(), out var globeVar)) return;
        var globe = globeVar.AsGodotDictionary<string, Variant>();

        if (!globe.TryGetValue("managers", out var mgrVar)) return;
        var managers = mgrVar.AsGodotDictionary<string, Variant>();

        if (!managers.TryGetValue("GameManager", out var gmVar)) return;
        var gmData = gmVar.AsGodotDictionary<string, Variant>();

        gmData["currentBase"] = updatedBase.Save();

        using var write = FileAccess.Open(fullPath, FileAccess.ModeFlags.Write);
        write.StoreVar(root);
    }
	

    // ==================== Utilities ====================

    public List<string> GetSaveFileDisplayNames()
    {
        List<string> names = new();
        using var dir = DirAccess.Open(saveDir);
        if (dir == null) return names;
        dir.ListDirBegin();
        string f = dir.GetNext();
        while (f != "")
        {
            if (!dir.CurrentIsDir() && f.EndsWith(SaveExt))
                names.Add(f.Replace(SaveExt, ""));
            f = dir.GetNext();
        }
        return names;
    }

    public bool DeleteSave(string saveName)
    {
        string file = saveName.EndsWith(SaveExt) ? saveName : saveName + SaveExt;
        using var dir = DirAccess.Open(saveDir);
        if (dir == null || dir.Remove(file) != Error.Ok) return false;
        EmitSignal(SignalName.GameSavesChanged);
        return true;
    }

    public void EnsureSaveDir()
    {
        if (!DirAccess.DirExistsAbsolute(saveDir))
            DirAccess.MakeDirRecursiveAbsolute(saveDir);
    }

    public override Godot.Collections.Dictionary<string, Variant> Save()
    {
        return new Godot.Collections.Dictionary<string, Variant> { ["current_savename"] = currentSavename };
    }

    public override Task Load(Godot.Collections.Dictionary<string, Variant> data)
    {
        if (data != null && data.ContainsKey("current_savename"))
            currentSavename = data["current_savename"].AsString();
        return Task.CompletedTask;
    }

    public override void Deinitialize() { }



    #region Session Data

    public bool TryGetSessionData(String dataName, out Variant data)
    {
	    data = default;
	    if (SessionData == null || SessionData.Count == 0)
	    {
		    return false;
	    }
	    
	    return SessionData.TryGetValue(dataName, out data);
    }

    public void SetSessionData(String dataName, Variant data)
    {
	    SessionData[dataName] = data;
    }

    
    public void StashSceneState(string stateKey)
    {
	    SetSessionData(stateKey, PackageFullState());
	    GD.Print($"[SavesManager] Stashed '{stateKey}' into Session Data.");
    }

    public Godot.Collections.Dictionary<string, Variant> ConsumeSceneState(string stateKey)
    {
	    if (TryGetSessionData(stateKey, out Variant data))
	    {
		    SessionData.Remove(stateKey); // Clean up memory once we grab it
		    return data.AsGodotDictionary<string, Variant>();
	    }
    
	    GD.PrintErr($"[SavesManager] Failed to find '{stateKey}' in Session Data!");
	    return null;
    }
    #endregion
}