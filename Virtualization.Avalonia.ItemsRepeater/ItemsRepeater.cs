using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Virtualization.Avalonia.Layouts;

namespace Virtualization.Avalonia;

public partial class ItemsRepeater : Panel
{
    public ItemsRepeater()
    {
        _viewportManager = new ViewportManager(this);
        ViewManager = new ViewManager(this);
        TransitionManager = new TransitionManager(this);

        SetCurrentValue(LayoutProperty, new StackLayout());

        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        SetValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Once);
        XYFocus.SetNavigationModes(this, XYFocusNavigationModes.Enabled);

        Loaded += OnRepeaterLoaded;
        Unloaded += OnRepeaterUnloaded;
        LayoutUpdated += OnLayoutUpdated;

        // TODO: Why is this here...
        //var layout = Layout as VirtualizingLayout;
        //OnLayoutChanged(null, layout);
        AddHandler(RequestBringIntoViewEvent, OnBringIntoViewRequested);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ItemsRepeaterAutomationPeer(this);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        CheckIfLayoutInProgress();
        CheckIfProcessingCollectionChange();

        var layout = GetEffectiveLayout();

        if (layout is StackLayout)
        {
            ++_stackLayoutMeasureCounter;
            if (_stackLayoutMeasureCounter >= MaxStackLayoutIterations)
            {
#if DEBUG && REPEATER_TRACE
                //Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this,"MeasureOverride shortcut - {Counter}", _stackLayoutMeasureCounter);
#endif
                // Shortcut the apparent layout cycle by returning the previous desired size.
                // This can occur when children have variable sizes that prevent the ItemsPresenter's desired size from settling.
                Rect layoutExtent = _viewportManager.LayoutExtent;
                Size ds = new Size(layoutExtent.Width - layoutExtent.X,
                    layoutExtent.Height - layoutExtent.Y);
                return ds;
            }
        }

        _viewportManager.OnOwnerMeasuring();

        try
        {
            _isLayoutInProgress = true;
            ViewManager.PrunePinnedElements();

            var layoutContext = LayoutContext;

            var desiredSize = layout.Measure(layoutContext, availableSize);
            var extent = new Rect(LayoutOrigin, desiredSize);

            // Clear auto recycle candidate elements that have not been kept alive by layout - i.e layout did not
            // call GetElementAt(index).
            foreach (var element in Children)
            {
                var virtInfo = GetVirtualizationInfo(element);

                if (virtInfo is
                    { Owner: VirtualizationInfo.ElementOwner.Layout, AutoRecycleCandidate: true, KeepAlive: false })
                {
#if DEBUG && REPEATER_TRACE
                    Logger.TryGet(LogEventLevel.Verbose, "Repeater")?.Log(this,"AutoClear - {Index}", virtInfo.Index);
#endif
                    ClearElement(element);
                }
            }

            _viewportManager.SetLayoutExtent(extent);
            _lastAvailableSize = availableSize;

            return desiredSize;
        }
        finally
        {
            _isLayoutInProgress = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        CheckIfLayoutInProgress();
        CheckIfProcessingCollectionChange();

        try
        {
            _isLayoutInProgress = true;
            Size arrangeSize = default;

            if (GetEffectiveLayout() is { } layout)
            {
                arrangeSize = layout.Arrange(LayoutContext, finalSize);
            }

            // The view manager might clear elements during this call.
            // That's why we call it before arranging cleared elements
            // off screen.
            ViewManager.OnOwnerArranged();

            foreach (var element in Children)
            {
                var vi = GetVirtualizationInfo(element);
                vi.KeepAlive = false;

                if (vi.Owner is VirtualizationInfo.ElementOwner.ElementFactory or VirtualizationInfo.ElementOwner.PinnedPool)
                {
                    // Toss it away. And arrange it with size 0 so that XYFocus won't use it.
                    element.Arrange(new Rect(
                        ClearedElementsArrangePosition.X - element.DesiredSize.Width,
                        ClearedElementsArrangePosition.Y - element.DesiredSize.Height,
                        0, 0));
                }
                else
                {
                    var newBounds = element.Bounds;
                    if (vi.ArrangeBounds != InvalidRect && newBounds != vi.ArrangeBounds)
                    {
                        TransitionManager.OnElementBoundsChanged(element, vi.ArrangeBounds, newBounds);
                    }

                    vi.ArrangeBounds = newBounds;
                }
            }

            _viewportManager.OnOwnerArranged();
            TransitionManager.OnOwnerArranged();

            return arrangeSize;
        }
        finally
        {
            _isLayoutInProgress = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        var property = args.Property;

        if (property == ItemsSourceProperty)
        {
            if (args.NewValue == args.OldValue)
                return;
            var newValue = args.GetNewValue<IEnumerable?>();
            var newDataSource = newValue as FAItemsSourceView;
            if (newValue is not null && newDataSource is null)
                newDataSource = new FAItemsSourceView(newValue);

            OnDataSourcePropertyChanged(ItemsSourceView, newDataSource);
        }
        else if (property == ItemTemplateProperty)
            OnItemTemplateChanged(args.GetOldValue<IDataTemplate?>(), args.GetNewValue<IDataTemplate?>());
        else if (property == LayoutProperty)
            OnLayoutChanged(args.GetOldValue<Layout>(), args.GetNewValue<Layout>());
        else if (property == ItemTransitionProviderProperty)
            OnTransitionProviderChanged(args.GetOldValue<ItemCollectionTransitionProvider>(), args.GetNewValue<ItemCollectionTransitionProvider>());
        else if (property == HorizontalCacheLengthProperty)
            _viewportManager.HorizontalCacheLength = args.GetNewValue<double>();
        else if (property == VerticalCacheLengthProperty)
            _viewportManager.VerticalCacheLength = args.GetNewValue<double>();
    }

    public int GetElementIndex(Control element)
    {
        // Verify that element is actually a child of this ItemsRepeater
        var parent = element.GetVisualParent();
        if (parent != this)
            return -1;
        var virtInfo = GetVirtualizationInfo(element);
        return ViewManager.GetElementIndex(virtInfo);
    }

    public Control? TryGetElement(int index)
    {
        foreach (var element in Children)
            if (GetVirtualizationInfo(element) is { IsRealized: true } virtInfo && virtInfo.Index == index)
                return element;
        return null;
    }

    public void PinElement(Control element) =>
        ViewManager.UpdatePin(element, true);

    public void UnpinElement(Control element) =>
        ViewManager.UpdatePin(element, false);

    public Control? GetOrCreateElement(int index)
    {
        if (ItemsSourceView is null)
            throw new InvalidOperationException($"{nameof(ItemsSource)} doesn't have a value");

        if (index >= 0 && index >= ItemsSourceView.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        CheckIfLayoutInProgress();

        var element = TryGetElement(index);
        var isAnchorOutsideRealizedRange = element is null;

        if (isAnchorOutsideRealizedRange)
        {
            element = LayoutContext.GetOrCreateElementAt(index);
            element.Measure(Size.Infinity);
        }

        _viewportManager.OnMakeAnchor(element, isAnchorOutsideRealizedRange);
        InvalidateMeasure();

        return element;
    }

    // Change from WinUI, to avoid an extra property read in ViewportManager,
    // we pass the VirtualizationInfo in here too since its called from 
    // ViewManager.GetElementFromElementFactory where we already have
    // a ref to the VirtualizationInfo
    internal void OnElementPrepared(Control element, int index, VirtualizationInfo vInfo)
    {
        _viewportManager.OnElementPrepared(element, vInfo);
        if (ElementPrepared is null)
            return;
        if (_elementPreparedArgs is null)
            _elementPreparedArgs = new ItemsRepeaterElementPreparedEventArgs(element, index);
        else
            _elementPreparedArgs.Update(element, index);

        ElementPrepared.Invoke(this, _elementPreparedArgs);
    }

    internal void OnElementClearing(Control element)
    {
        if (ElementClearing is null)
            return;
        if (_elementClearingArgs is null)
            _elementClearingArgs = new(element);
        else
            _elementClearingArgs.Update(element);

        ElementClearing.Invoke(this, _elementClearingArgs);
    }

    internal void OnElementIndexChanged(Control element, int oldIndex, int newIndex)
    {
        if (ElementIndexChanged is null)
            return;
        if (_elementIndexChangedArgs is null)
        {
            _elementIndexChangedArgs = new(element, oldIndex, newIndex);
        }
        else
        {
            _elementIndexChangedArgs.Update(element, oldIndex, newIndex);
        }

        ElementIndexChanged.Invoke(this, _elementIndexChangedArgs);
    }

    internal Control GetElementImpl(int index, bool forceCreate, bool suppressAutoRecycle)
    {
        return ViewManager.GetElement(index, forceCreate, suppressAutoRecycle);
    }

    internal void ClearElement(Control element)
    {
        // Clearing an element due to a collection change
        // is stricter in that pinned elements will be forcibly
        // unpinned and sent back to the view generator.
        bool isClearedDueToCollectionChange =
            IsProcessingCollectionChange &&
            _processingItemsSourceChange.Action is
                NotifyCollectionChangedAction.Remove or
                NotifyCollectionChangedAction.Replace or
                NotifyCollectionChangedAction.Reset;

        ViewManager.ClearElement(element, isClearedDueToCollectionChange);
        _viewportManager.OnElementCleared(element);
    }

    private IEnumerable<Control>? GetChildrenInTabFocusOrder() =>
        CreateChildrenInTabFocusOrderIterable();

    private void OnBringIntoViewRequested(object? sender, RequestBringIntoViewEventArgs args)
    {
        _viewportManager.OnBringIntoViewRequested(args);
    }

    private void OnRepeaterLoaded(object? sender, RoutedEventArgs e)
    {
        // If we skipped an unload event, reset the scrollers now and invalidate measure so that we get a new
        // layout pass during which we will hookup new scrollers.
        if (_loadedCounter > _unloadedCounter)
        {
            InvalidateMeasure();
            _viewportManager.ResetScrollers();
        }
        ++_loadedCounter;
    }

    private void OnRepeaterUnloaded(object? sender, RoutedEventArgs e)
    {
        _stackLayoutMeasureCounter = 0;
        ++_unloadedCounter;

        // Only reset the scrollers if this unload event is in-sync.
        if (_unloadedCounter == _loadedCounter)
        {
            _viewportManager.ResetScrollers();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    { 
        // Now that the layout has settled, reset the measure counter to detect the next potential StackLayout layout cycle.
        _stackLayoutMeasureCounter = 0;

        EnsureDefaultLayoutState();
    }

    private void OnDataSourcePropertyChanged(FAItemsSourceView? oldValue, FAItemsSourceView? newValue)
    {
        CheckIfLayoutInProgress();

        EnsureDefaultLayoutState();

        if (ItemsSourceView != null)
            ItemsSourceView.CollectionChanged -= OnItemsSourceViewChanged;

        ItemsSourceView = newValue;

        if (ItemsSourceView != null)
            ItemsSourceView.CollectionChanged += OnItemsSourceViewChanged;

        if (GetEffectiveLayout() is not { } l)
            return;
        var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        try
        {
            _processingItemsSourceChange = args;
            if (l is VirtualizingLayout vl)
            {
                vl.OnItemsChangedCore(LayoutContext, newValue, args);
            }
            else if (Layout is NonVirtualizingLayout nvl)
            {
                // Walk through all the elements and make sure they are cleared for
                // non-virtualizing layouts.
                foreach (var item in Children)
                    if (GetVirtualizationInfo(item).IsRealized)
                        ClearElement(item);

                Children.Clear();
            }

            InvalidateMeasure();
        }
        finally
        {
            _processingItemsSourceChange = null;
        }
    }

    // WinUI has type of IElementFactory here, but we need to use IDataTemplate
    // In WinUI, DataTemplate inherits from IElementFactory so that covers everything
    // For us, we need to work with what Avalonia gives us, easiest is IDataTemplate
    private void OnItemTemplateChanged(IDataTemplate? oldValue, IDataTemplate? newValue)
    {
        if (oldValue is not null)
            CheckIfLayoutInProgress();

        EnsureDefaultLayoutState();

        // Since the ItemTemplate has changed, we need to re-evaluate all the items that
        // have already been created and are now in the tree. The easiest way to do that
        // would be to do a reset. Note that this has to be done before we change the template
        // so that the cleared elements go back into the old template.
        if (GetEffectiveLayout() is { } layout)
        {
            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            try
            {
                _processingItemsSourceChange = args;

                switch (layout)
                {
                    case VirtualizingLayout vl:
                        vl.OnItemsChangedCore(LayoutContext, newValue, args);
                        break;
                    case NonVirtualizingLayout nvl:
                    {
                        // Walk through all the elements and make sure they are cleared for
                        // non-virtualizing layouts.
                        foreach (var child in Children)
                            if (GetVirtualizationInfo(child).IsRealized)
                                ClearElement(child);

                        break;
                    }
                }
            }
            finally
            {
                _processingItemsSourceChange = null;
            }
        }

        ItemTemplateShim = newValue as IElementFactory;
        if (ItemTemplateShim is null)
        {
            // ItemTemplate set does not implement IElementFactoryShim. We also 
            // want to support DataTemplate and DataTemplateSelectors automagically.
            if (newValue is not null)
            {
                ItemTemplateShim = new ItemTemplateWrapper(newValue);
                //_isItemTemplateEmpty = template.Build(null) is null;
            }
        }

        InvalidateMeasure();
    }

    private void OnLayoutChanged(Layout? oldValue, Layout? newValue)
    {
        var isInitialSetup = !_wasLayoutChangedCalled;
        _wasLayoutChangedCalled = true;

        CheckIfLayoutInProgress();

        ViewManager.OnLayoutChanging();
        TransitionManager.OnLayoutChanging();

        if (oldValue is null && !isInitialSetup)
            oldValue = GetDefaultLayout();
        newValue ??= GetDefaultLayout();    

        if (oldValue is not null)
        {
            oldValue.UninitializeForContext(LayoutContext);
            oldValue.MeasureInvalidated -= InvalidateMeasureForLayout;
            oldValue.ArrangeInvalidated -= InvalidateArrangeForLayout;
            _stackLayoutMeasureCounter = 0;

            // Walk through all the elements and make sure they are cleared
            foreach (var element in Children)
                if (GetVirtualizationInfo(element).IsRealized)
                    ClearElement(element);

            LayoutState = null;
        }

        newValue.InitializeForContext(LayoutContext);
        newValue.MeasureInvalidated += InvalidateMeasureForLayout;
        newValue.ArrangeInvalidated += InvalidateArrangeForLayout;

        if (_ownsTransitionProvider)
            TransitionManager.OnTransitionProviderChanged(newValue.CreateDefaultItemTransitionProvider());

        var isVirtualizingLayout = newValue is VirtualizingLayout;
        _viewportManager.OnLayoutChanged(isVirtualizingLayout);
        InvalidateMeasure();
    }

    private void OnTransitionProviderChanged(ItemCollectionTransitionProvider _, ItemCollectionTransitionProvider newValue)
    {
        _ownsTransitionProvider = false;
        TransitionManager.OnTransitionProviderChanged(newValue);
    }

    private void OnItemsSourceViewChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        CheckIfLayoutInProgress();
        CheckIfProcessingCollectionChange();

        try
        {
            _processingItemsSourceChange = args;

            TransitionManager.OnItemsSourceChanged(sender, args);
            ViewManager.OnItemsSourceChanged(sender, args);

            if (GetEffectiveLayout() is not { } layout)
                return;
            if (layout is VirtualizingLayout vl)
                vl.OnItemsChangedCore(LayoutContext, sender, args);
            else
                InvalidateMeasure();
        }
        finally
        {
            _processingItemsSourceChange = null;
        }
    }

    private void InvalidateMeasureForLayout(Layout sender, EventArgs args) => InvalidateMeasure();

    private void InvalidateArrangeForLayout(Layout sender, EventArgs args) => InvalidateArrange();

    private void EnsureDefaultLayoutState()
    {
        if (_wasLayoutChangedCalled)
            return;
        // Initialize the cached layout to the default value
        // OnLayoutChanged has not been called yet for this ItemsRepeater.
        // This is the first call for the default VirtualizingLayout layout after the control's creation.
        var layout = GetEffectiveLayout() as VirtualizingLayout;
        OnLayoutChanged(null, layout);
    }

    private IEnumerable<Control>? CreateChildrenInTabFocusOrderIterable()
        => Children.Count is 0 ? new ChildrenInTabFocusOrderIterable(this) : null;

    private Layout GetEffectiveLayout() => Layout ?? GetDefaultLayout();

    private void CheckIfLayoutInProgress([CallerMemberName] string callerMemberName = "")
    {
        if (_isLayoutInProgress)
            throw new InvalidOperationException($"{callerMemberName} is not allowed during layout.");
    }

    private void CheckIfProcessingCollectionChange([CallerMemberName] string callerMemberName = "")
    {
        if (IsProcessingCollectionChange)
            throw new InvalidOperationException($"{callerMemberName} is not allowed during a change in the data source.");
    }

    private static Layout GetDefaultLayout() =>
        // Default to StackLayout if the Layout property was not set.
        // We use thread_local here to get a unique instance per thread, since Layout objects
        // are not sharable across different xaml threads.
        //static thread_local winrt::Layout defaultLayout = winrt::StackLayout();
        //return defaultLayout;
        new StackLayout();

    // Used to avoid layout cycles with StackLayout layouts where variable sized children prevent
    // the ItemsRepeater's layout to settle.
    private byte _stackLayoutMeasureCounter;
    // StackLayout measurements are shortcut when _stackLayoutMeasureCounter reaches this value
    // to prevent a layout cycle exception.
    // The XAML Framework's iteration limit is 250, but that limit has been reached in practice
    // with this value as small as 61. It was never reached with 60. 
    internal const short MaxStackLayoutIterations = 60;
    internal static Point ClearedElementsArrangePosition = new Point(-10000, -10000);
    internal static Rect InvalidRect = new Rect(-1,-1,-1,-1);

    private readonly ViewportManager _viewportManager;

    private VirtualizingLayoutContext LayoutContext => field ??= new RepeaterLayoutContext(this);
    private NotifyCollectionChangedEventArgs? _processingItemsSourceChange;
    
    private Size _lastAvailableSize;
    private bool _isLayoutInProgress;

    // Cached Event args to avoid creation cost every time
    private ItemsRepeaterElementPreparedEventArgs? _elementPreparedArgs;
    private ItemsRepeaterElementClearingEventArgs? _elementClearingArgs;
    private ItemsRepeaterElementIndexChangedEventArgs? _elementIndexChangedArgs;

    // Loaded events fire on the first tick after an element is put into the tree 
    // while unloaded is posted on the UI tree and may be processed out of sync with subsequent loaded
    // events. We keep these counters to detect out-of-sync unloaded events and take action to rectify.
    private int _loadedCounter;
    private int _unloadedCounter;

    // If no ItemCollectionTransitionProvider is explicitly provided, we'll retrieve a default one
    // from the Layout object. In that case, we'll want to know that we own that object and can
    // overwrite it if the Layout object changes.
    private bool _ownsTransitionProvider = true;

    // Tracks whether OnLayoutChanged has already been called or not so that
    // EnsureDefaultLayoutState does not trigger a second call after the control's creation.
    private bool _wasLayoutChangedCalled;
}
