using Avalonia;

namespace Virtualization.Avalonia.Layouts;

/// <summary>
/// Represents the base class for an object that sizes and arranges child elements for a host.
/// </summary>
public abstract class Layout : AvaloniaObject
{
    /// <summary>
    /// Occurs when the measurement state (layout) has been invalidated.
    /// </summary>
    public event EventHandler<Layout, EventArgs>? MeasureInvalidated;

    /// <summary>
    /// Occurs when the arrange state(layout) has been invalidated.
    /// </summary>
    public event EventHandler<Layout, EventArgs>? ArrangeInvalidated;

    /// <summary>
    /// Initializes any per-container state the layout requires when it is attached to a UIElement container.
    /// </summary>
    public void InitializeForContext(VirtualizingLayoutContext context)
    {
        switch (this)
        {
            case VirtualizingLayout vl:
            {
                vl.InitializeForContextCore(context);
                break;
            }
            case NonVirtualizingLayout nvl:
            {
                nvl.InitializeForContextCore(context.NonVirtualizingContextAdapter);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Removes any state the layout previously stored on the UIElement container.
    /// </summary>
    public void UninitializeForContext(VirtualizingLayoutContext context)
    {
        switch (this)
        {
            case VirtualizingLayout vl:
            {
                vl.UninitializeForContextCore(context);
                break;
            }
            case NonVirtualizingLayout nvl:
            {
                nvl.UninitializeForContextCore(context.NonVirtualizingContextAdapter);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Suggests a DesiredSize for a container element. A container element that supports attached layouts 
    /// should call this method from their own MeasureOverride implementations to form a recursive layout update. 
    /// The attached layout is expected to call the Measure for each of the container’s UIElement children.
    /// </summary>
    public Size Measure(VirtualizingLayoutContext context, Size availableSize)
    {
        return this switch
        {
            VirtualizingLayout vl => vl.MeasureOverride(context, availableSize),
            NonVirtualizingLayout nvl => nvl.MeasureOverride(context.NonVirtualizingContextAdapter, availableSize),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Positions child elements and determines a size for a container UIElement. Container elements that 
    /// support attached layouts should call this method from their layout override implementations to 
    /// form a recursive layout update.
    /// </summary>
    public Size Arrange(VirtualizingLayoutContext context, Size finalSize)
    {
        return this switch
        {
            VirtualizingLayout vl => vl.ArrangeOverride(context, finalSize),
            NonVirtualizingLayout nvl => nvl.ArrangeOverride(context.NonVirtualizingContextAdapter, finalSize),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Invalidates the measurement state (layout) for all UIElement containers that reference this layout.
    /// </summary>
    protected void InvalidateMeasure() =>
        MeasureInvalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Invalidates the arrange state (layout) for all UIElement containers that reference this layout. 
    /// After the invalidation, the UIElement will have its layout updated, which occurs asynchronously.
    /// </summary>
    protected void InvalidateArrange() =>
        ArrangeInvalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 
    /// </summary>
    protected internal virtual ItemCollectionTransitionProvider? CreateDefaultItemTransitionProvider() => null;
}
