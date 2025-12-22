using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Virtualization.Avalonia.Sample.Models;

namespace Virtualization.Avalonia.Sample.ViewModels;

public partial class SampleViewModel : ObservableObject
{
    [ObservableProperty]
    public partial IDataTemplate? ItemTemplate { get; set; } = Application.Current?.FindResource("TextItemsTemplate") as IDataTemplate;

    [ObservableProperty]
    public partial IEnumerable? Items { get; set; } = TextItems;

    [ObservableProperty]
    public partial DataType SelectedDataSource { get; set; } = DataType.TextItems;

    [ObservableProperty]
    public partial Stretch ImageStretch { get; set; }

    private static IReadOnlyList<Item>? TextItems => field ??= InitializeDataSources();

    private static Item[] InitializeDataSources()
    {
        const int all = 1000;
        const string str = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
        var textItems = new Item[all];
        for (var i = 0; i < all - 1; i++)
        {
            var randomLines = Random.Shared.Next(1, 6);
            var s = "";
            for (var j = 0; j < randomLines; j++)
            {
                s += str[(Random.Shared.Next(str.Length / 5) * 5)..];
                s += "\n";
            }
            s = s.TrimEnd('\n');

            textItems[i] = new()
            {
                Value = i,
                Name = $"Item {i}",
                Description = s
            };
        }
        //textItems[all - 1] = new()
        //{
        //    Value = all - 1,
        //    Name = $"Item {all - 1}",
        //    Description = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n", all))
        //};
        return textItems;
    }

    partial void OnSelectedDataSourceChanged(DataType value)
    {
        ItemTemplate = Application.Current?.FindResource($"{value}Template") as IDataTemplate;

        Items = value is DataType.TextItems ? TextItems : null;
    }

    public void LoadFolder(string folder)
    {
        if (SelectedDataSource is not DataType.AsyncImageItems and not DataType.ImageItems)
            return;
        var supported = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff" };
        var items = Directory.EnumerateFiles(folder)
            .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        Items = SelectedDataSource switch
        {
            DataType.AsyncImageItems => items.Select(t => new ImageUri(t)).ToArray(),
            DataType.ImageItems => items
                .Select((t, i) =>
                {
                    using var s = File.OpenRead(t);
                    var bmp = Bitmap.DecodeToWidth(s, 300);
                    return new ImageItem
                    {
                        Index = i,
                        Image = bmp
                    };
                })
                .ToArray(),
            _ => Items
        };
    }

    [RelayCommand]
    public async Task OpenFolder()
    {
        if (TopLevel.GetTopLevel(MainWindow.Current) is not { } topLevel)
            return;
        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new());
        if (folder is not [{ } first] || first.TryGetLocalPath() is not { } path)
            return;
        LoadFolder(path);
    }
}

public record ImageUri(string Uri)
{
    public Task<Bitmap> BitmapAsync => Task.Run(() => Bitmap.DecodeToWidth(File.OpenRead(Uri), 300));
}

public enum DataType
{
    TextItems,
    ImageItems,
    AsyncImageItems
}
