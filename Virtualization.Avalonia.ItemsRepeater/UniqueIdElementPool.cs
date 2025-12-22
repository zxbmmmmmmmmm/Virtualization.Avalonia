using System.Diagnostics;
using Avalonia.Controls;

namespace Virtualization.Avalonia;

internal class UniqueIdElementPool(ItemsRepeater ir)
{
    // ItemsRepeater is not fully constructed yet. Don't interact with it.

    public void Add(Control element)
    {
        Debug.Assert(ir.ItemsSourceView!.HasKeyIndexMapping);

        var virtInfo = ItemsRepeater.GetVirtualizationInfo(element);
        var key = virtInfo.UniqueId;

        if (!_elementMap.TryAdd(key, element))
        {
            throw new InvalidOperationException("The ID is not unique");
        }
    }

    public Control? Remove(int index)
    {
        Debug.Assert(ir.ItemsSourceView!.HasKeyIndexMapping);

        var key = ir.ItemsSourceView.KeyFromIndex(index);
        _elementMap.Remove(key, out var element);

        return element;
    }

    public void Clear() => _elementMap.Clear();

    public IEnumerator<KeyValuePair<string, Control>> GetEnumerator() => _elementMap.GetEnumerator();

    private readonly Dictionary<string, Control> _elementMap = new();
}
