using Godot;
using System.Collections.Generic;

namespace BehaviorTree.Core;

/// <summary>
/// Like Selector but shuffles child order each time it starts
/// fresh. Great for adding variety to enemy AI.
/// </summary>
[GlobalClass]
public partial class BTRandomSelector : BTComposite
{
    private List<int> _shuffledIndices = new();
    private int _currentIndex;
    private bool _isRunning;

    public override BTStatus Tick(double delta)
    {
        if (!_isRunning)
        {
            ShuffleIndices();
            _currentIndex = 0;
            _isRunning = true;
        }

        for (int i = _currentIndex; i < _shuffledIndices.Count; i++)
        {
            var status = Children[_shuffledIndices[i]].Tick(delta);

            switch (status)
            {
                case BTStatus.Success:
                    _isRunning = false;
                    return BTStatus.Success;

                case BTStatus.Running:
                    _currentIndex = i;
                    return BTStatus.Running;

                case BTStatus.Failure:
                    continue;
            }
        }

        _isRunning = false;
        return BTStatus.Failure;
    }

    private void ShuffleIndices()
    {
        _shuffledIndices.Clear();
        for (int i = 0; i < Children.Count; i++)
            _shuffledIndices.Add(i);

        // Fisher-Yates shuffle
        var rng = new RandomNumberGenerator();
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (_shuffledIndices[i], _shuffledIndices[j]) =
                (_shuffledIndices[j], _shuffledIndices[i]);
        }
    }
}
