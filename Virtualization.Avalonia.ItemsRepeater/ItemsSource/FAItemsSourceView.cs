using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace Virtualization.Avalonia;

// Source is combo of ItemsSourceView & InspectingDataSource

/// <summary>
/// Represents a standardized view of the supported interactions between a given ItemsSource object and an ItemsRepeater control.
/// </summary>
public sealed class FAItemsSourceView : IReadOnlyList<object?>
{
    public FAItemsSourceView(IEnumerable source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _vector = source;
        ListenToCollectionChanges();

        _uniqueIdMapping = source as IKeyIndexMapping;
    }

    /// <summary>
    /// Gets the number of items in the collection.
    /// </summary>
    public int Count
    {
        get
        {
            if (_cachedSize == -1)
                // Call the override the very first time. After this,
                // we can just update the size when there is a data source change.
                _cachedSize = _vector.Count();

            return _cachedSize;
        }
    }

    /// <summary>
    /// Gets a value that indicates whether the items source can provide a unique key for each item.
    /// </summary>
    public bool HasKeyIndexMapping => _uniqueIdMapping is not null;

    /// <summary>
    /// Occurs when the collection has changed to indicate the reason for the change and which items changed.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Retrieves the item at the specified index.
    /// </summary>

    public object? this[int index] => _vector.ElementAt(index);

    /// <summary>
    /// Retrieves the index of the item that has the specified unique identifier (key).
    /// </summary>
    public string KeyFromIndex(int index) => UniqueIdMapping.KeyFromIndex(index);

    /// <summary>
    /// Retrieves the index of the item that has the specified unique identifier (key).
    /// </summary>
    public int IndexFromKey(string id) => UniqueIdMapping.IndexFromKey(id);

    /// <summary>
    /// Retrieves the index of the specified item.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public int IndexOf(object value) => _vector is IList list ? list.IndexOf(value) : _vector.IndexOf(value);

    /// <summary>
    /// Called when the ItemsSource has raised a CollectionChanged event
    /// </summary>
    /// <param name="args"></param>
    private void OnItemsSourceChanged(NotifyCollectionChangedEventArgs args)
    {
        _cachedSize = _vector.Count();
        CollectionChanged?.Invoke(this, args);
    }

    private void UnListenToCollectionChanges()
    {
        _eventToken?.Dispose();
        _eventToken = null;
    }

    private void ListenToCollectionChanges()
    {
        if (_vector is not INotifyCollectionChanged incc)
            return;
        _eventToken = incc.GetWeakCollectionChangedObservable()
            .Subscribe(new SimpleObserver<NotifyCollectionChangedEventArgs>(OnCollectionChanged));
    }

    private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        OnItemsSourceChanged(args);
    }

    private IKeyIndexMapping UniqueIdMapping => _uniqueIdMapping ?? throw new NotSupportedException(nameof(UniqueIdMapping));
    private readonly IKeyIndexMapping? _uniqueIdMapping;
    private int _cachedSize = -1;
    private readonly IEnumerable _vector;
    private IDisposable? _eventToken;

    public IEnumerator<object?> GetEnumerator() => _vector.Cast<object?>().GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
