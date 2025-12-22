using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Virtualization.Avalonia;

public class ItemsRepeaterAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    public new ItemsRepeater Owner => (ItemsRepeater)base.Owner;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        var repeater = Owner;
        var childrenPeers = base.GetChildrenCore();
        var peerCount = childrenPeers.Count;

        var realizedPeers = new List<(int, AutomationPeer)>(peerCount);

        for (var i = 0; i < peerCount; i++)
        {
            var childPeer = childrenPeers[i];
            if (GetElement((ControlAutomationPeer)childPeer, repeater) is { } c && ItemsRepeater.GetVirtualizationInfo(c) is { IsRealized: true } vi)
                realizedPeers.Add((vi.Index, childPeer));
        }

        realizedPeers.Sort((lhs, rhs) => lhs.Item1 < rhs.Item1 ? 1 : -1);

        return realizedPeers.Select(x => x.Item2).ToArray();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    private static Control GetElement(ControlAutomationPeer childPeer, ItemsRepeater repeater)
    {
        var childElement = childPeer.Owner;
        var parent = childElement.GetVisualParent();
        while (parent != null && parent != repeater)
        {
            childElement = (Control)parent;
            parent = childElement.GetVisualParent();
        }

        return childElement;
    }
}
