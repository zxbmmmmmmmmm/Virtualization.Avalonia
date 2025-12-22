using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Virtualization.Avalonia;

public class RecyclePool
{
    public static readonly AttachedProperty<IDataTemplate> OriginTemplateProperty =
        AvaloniaProperty.RegisterAttached<RecyclePool, Control, IDataTemplate>("OriginTemplate");

    public static readonly AttachedProperty<string> ReuseKeyProperty =
        AvaloniaProperty.RegisterAttached<RecyclePool, Control, string>("ReuseKey");

    public static string GetReuseKey(Control element) => element.GetValue(ReuseKeyProperty);

    public static void SetReuseKey(Control element, string key) => element.SetValue(ReuseKeyProperty, key);

    public void PutElement(Control element, string key) => PutElementCore(element, key, owner: null);

    public void PutElement(Control element, string key, Panel? owner) => PutElementCore(element, key, owner);

    public Control? TryGetElement(string key) => TryGetElementCore(key, owner: null);

    public Control? TryGetElement(string key, Panel? owner) => TryGetElementCore(key, owner);

    protected virtual void PutElementCore(Control element, string key, Panel? owner)
    {
        var elementInfo = new ElementInfo(element, owner);

        if (_elements.TryGetValue(key, out var value))
            value.Add(elementInfo);
        else
            _elements[key] = [elementInfo];
    }

    protected virtual Control? TryGetElementCore(string key, Control? owner)
    {
        if (!_elements.TryGetValue(key, out var elements) || elements.Count <= 0)
            return null;

        var index = elements.FindIndex(x => x.Owner == owner || x.Owner is null);

        if (index is not -1)
        {
            var e = elements[index];
            _ = elements.Remove(e);
            return e.Element;
        }

        var elementInfo = elements[^1];
        _ = elements.Remove(elementInfo);

        // Element is still under its parent. remove it from its parent.
        if (elementInfo.Owner is { Children: var children })
        {
            if (!children.Remove(elementInfo.Element))
                throw new Exception($"{nameof(ItemsRepeater)}'s child not found in its Children collection.");
        }

        return elementInfo.Element;
    }

    // RecyclePoolFactory.cpp

    public static RecyclePool? TryGetPoolInstance(IDataTemplate template) => _PoolInstance.GetValueOrDefault(template);

    public static void SetPoolInstance(IDataTemplate template, RecyclePool pool) => _PoolInstance.Add(template, pool);

    private record struct ElementInfo(Control Element, Panel? Owner);

    private readonly Dictionary<string, List<ElementInfo>> _elements = [];

    // WinUI stores this as a DependencyProperty on DataTemplate (attached), but since
    // we use IDataTemplate, we need a cache not tied to the property system
    private static readonly Dictionary<IDataTemplate, RecyclePool> _PoolInstance = [];
}
