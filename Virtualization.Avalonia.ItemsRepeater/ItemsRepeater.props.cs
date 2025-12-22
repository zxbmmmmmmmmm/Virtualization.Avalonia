using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Virtualization.Avalonia.Layouts;

namespace Virtualization.Avalonia;

/// <summary>
/// Represents a data-driven collection control that incorporates a flexible layout system,
/// custom views, and virtualization, with no default UI or interaction policies.
/// </summary>
public partial class ItemsRepeater : Panel
{
    /// <summary>
    /// Defines the <see cref="VerticalCacheLength"/> property
    /// </summary>
    public static readonly StyledProperty<double> VerticalCacheLengthProperty =
        AvaloniaProperty.Register<ItemsRepeater, double>(nameof(VerticalCacheLength), defaultValue: 2.0);

    /// <summary>
    /// Defines the <see cref="HorizontalCacheLength"/> property
    /// </summary>
    public static readonly StyledProperty<double> HorizontalCacheLengthProperty =
        AvaloniaProperty.Register<ItemsRepeater, double>(nameof(HorizontalCacheLength), defaultValue: 2.0);

    /// <summary>
    /// Defines the <see cref="Layout"/> property
    /// </summary>
    public static readonly StyledProperty<Layout?> LayoutProperty =
        AvaloniaProperty.Register<ItemsRepeater, Layout?>(nameof(Layout));

    /// <summary>
    /// Defines the <see cref="ItemsSource"/> property
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ItemsRepeater, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Defines the <see cref="VerticalCacheLength"/> property
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        ItemsControl.ItemTemplateProperty.AddOwner<ItemsRepeater>();

    /// <summary>
    /// Defines the <see cref="ItemTransitionProvider"/> property
    /// </summary>
    public static readonly StyledProperty<ItemCollectionTransitionProvider> ItemTransitionProviderProperty =
        AvaloniaProperty.Register<ItemsRepeater, ItemCollectionTransitionProvider>(nameof(ItemTransitionProvider));

    /// <summary>
    /// Gets or sets a value that indicates the size of the buffer used to realize items when panning or scrolling vertically.
    /// </summary>
    public double VerticalCacheLength
    {
        get => GetValue(VerticalCacheLengthProperty);
        set => SetValue(VerticalCacheLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets a value that indicates the size of the buffer used to realize items when panning or scrolling vertically.
    /// </summary>
    public double HorizontalCacheLength
    {
        get => GetValue(HorizontalCacheLengthProperty);
        set => SetValue(HorizontalCacheLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the layout used to size and position elements in the ItemsRepeater.
    /// </summary>
    public Layout? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// Gets or sets an object source used to generate the content of the ItemsRepeater.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to display each item.
    /// </summary>
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="ItemCollectionTransitionProvider"/> for the ItemsRepeater
    /// </summary>
    public ItemCollectionTransitionProvider ItemTransitionProvider
    {
        get => GetValue(ItemTransitionProviderProperty);
        set => SetValue(ItemTransitionProviderProperty, value);
    }

    /// <summary>
    /// Gets a standardized view of the supported interactions between a given ItemsSource object and the ItemsRepeater control and its associated components.
    /// </summary>
    /// <remarks>
    /// Note the return type is <see cref="FAItemsSourceView"/> and not the ItemsSourceView in Avalonia
    /// </remarks>
    public FAItemsSourceView? ItemsSourceView { get; private set; }

    internal Control? MadeAnchor => _viewportManager.MadeAnchor;

    internal object? LayoutState { get; set; }

    internal TransitionManager TransitionManager { get; }

    internal Rect VisibleWindow => _viewportManager.GetLayoutVisibleWindow();

    internal Rect RealizationWindow => _viewportManager.GetLayoutRealizationWindow();

    internal Control? SuggestedAnchor => _viewportManager.SuggestedAnchor;

    /// <remarks>
    /// The value of _layoutOrigin is expected to be set by the layout
    /// when it gets measured. It should not be used outside of measure.
    /// </remarks>
    internal Point LayoutOrigin { get; set; }

    internal IElementFactory? ItemTemplateShim { get; private set; }

    internal ViewManager ViewManager { get; }

    [MemberNotNullWhen(true, nameof(_processingItemsSourceChange))]
    private bool IsProcessingCollectionChange => _processingItemsSourceChange is not null;

    internal bool ShouldPhase => ContainerContentChanging is not null;

    /// <summary>
    /// Occurs each time an element is prepared for use.
    /// </summary>
    public event EventHandler<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>? ElementPrepared;

    /// <summary>
    /// Occurs each time an element is cleared and made available to be re-used.
    /// </summary>
    public event EventHandler<ItemsRepeater, ItemsRepeaterElementClearingEventArgs>? ElementClearing;

    /// <summary>
    /// Occurs for each realized UIElement when the index for the item it represents has changed.
    /// </summary>
    public event EventHandler<ItemsRepeater, ItemsRepeaterElementIndexChangedEventArgs>? ElementIndexChanged;

    /// <summary>
    /// Occurs when container content is changing, used for Phased rendering
    /// </summary>
    public event EventHandler<ItemsRepeater, ContainerContentChangingEventArgs>? ContainerContentChanging;

    internal static readonly AttachedProperty<VirtualizationInfo?> VirtualizationInfoProperty =
        AvaloniaProperty.RegisterAttached<ItemsRepeater, Control, VirtualizationInfo?>(nameof(VirtualizationInfo));

    internal static VirtualizationInfo GetVirtualizationInfo(Control c)
        => c.GetValue(VirtualizationInfoProperty) ?? CreateAndInitializeVirtualizationInfo(c);

    internal static VirtualizationInfo CreateAndInitializeVirtualizationInfo(Control element)
    {
        var result = new VirtualizationInfo();
        element.SetValue(VirtualizationInfoProperty, result);
        return result;
    }

    internal void RaiseContainerContentChanging(ContainerContentChangingEventArgs args)
    {
        ContainerContentChanging?.Invoke(this, args);
    }
}
