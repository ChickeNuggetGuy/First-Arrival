using Godot;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

namespace FirstArrival.Scripts.TurnSystem;

/// <summary>
/// Runs the configured AI turn segments in order, then ends the turn.
/// Behavior trees are processed by BTActionSegment; keeping that work in one
/// place prevents the same tree from being ticked concurrently.
/// </summary>
[GlobalClass]
public partial class AITurn : Turn
{
    protected override async Task _Execute()
    {
        await base._Execute();
        TurnManager.Instance.RequestEndOfTurn();
    }
}
