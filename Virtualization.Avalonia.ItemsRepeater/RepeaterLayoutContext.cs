using Avalonia;
using Avalonia.Controls;

using Virtualization.Avalonia.Layouts;

namespace Virtualization.Avalonia;

internal class RepeaterLayoutContext(ItemsRepeater owner) : VirtualizingLayoutContext
{
    public override object? GetItemAt(int index) => GetOwner()?.ItemsSourceView?[index];
    
    public override Control? GetElementAt(int index)
    {
        var owner = GetOwner() ?? throw new NullReferenceException();
        return owner.TryGetElement(index);
    }

    public override Control? GetElementAt(Point point)
    {
        var owner = GetOwner() ?? throw new NullReferenceException();
        foreach (var child in owner.Children)
        {
            if (!child.IsVisible)
                continue;
            var p = child.TranslatePoint(default, owner);
            if (!p.HasValue)
                continue;
            var bounds = new Rect(p.Value, child.Bounds.Size);
            if (bounds.Contains(point))
                return child;
        }
        return null;
    }

    public override int GetElementIndexAt(Point point) => GetElementAt(point) is { } element ? IndexOf(element) : -1;

    public override int IndexOf(Control? element)
    {
        if (element is null)
            return -1;
        var owner = GetOwner();
        return owner?.GetElementIndex(element) ?? -1;
    }

    public override Control GetOrCreateElementAt(int index, ElementRealizationOptions options = ElementRealizationOptions.None)
    {
        var owner = GetOwner() ?? throw new NullReferenceException();
        return owner.GetElementImpl(index,
            (options & ElementRealizationOptions.ForceCreate) == ElementRealizationOptions.ForceCreate,
            (options & ElementRealizationOptions.SuppressAutoRecycle) == ElementRealizationOptions.SuppressAutoRecycle);
    }

    public override void RecycleElement(Control? element)
    {
        if (element is null)
            return;
        var owner = GetOwner();
#if DEBUG && REPEATER_TRACE
        Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this, $"RepeaterLayout - RecycleElement {owner?.GetElementIndex(element)}");
#endif
        owner?.ClearElement(element);
    }

    public override object? LayoutState
    {
        get => GetOwner()?.LayoutState;
        set
        {
            if (GetOwner() is { } ir)
                ir.LayoutState = value;
        }
    }
    
    public override int ItemsCount => GetOwner()?.ItemsSourceView?.Count ?? 0;

    public override Rect VisibleRect => GetOwner()?.VisibleWindow ?? default;

    public override Rect RealizationRect => GetOwner()?.RealizationWindow ?? default;

    public override int RecommendedAnchorIndex
    {
        get
        {
            if (GetOwner() is { SuggestedAnchor: { } anchor } repeater)
                return repeater.GetElementIndex(anchor);

            return -1;
        }
    }

    /// <inheritdoc cref="ItemsRepeater.LayoutOrigin" />
    public override Point LayoutOrigin
    {
        get => GetOwner()?.LayoutOrigin ?? default;
        set
        {
            if (GetOwner() is { } ir)
                ir.LayoutOrigin = value;
        }
    }

    protected internal override WeakReference<ItemsRepeater> Owner { get; } = new(owner);
}
