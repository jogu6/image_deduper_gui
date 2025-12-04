using System;

namespace ImageDeduper.App.ViewModels;

public sealed class ImageFormatInfo
{
    public ImageFormatInfo(string name, string extensionsLabel, string statusText, bool showStoreLink, string? storeLinkLabel, Uri? storeLinkUri)
    {
        Name = name;
        ExtensionsLabel = extensionsLabel;
        StatusText = statusText;
        ShowStoreLink = showStoreLink;
        StoreLinkLabel = storeLinkLabel ?? string.Empty;
        StoreLinkUri = storeLinkUri;
    }

    public string Name { get; }
    public string ExtensionsLabel { get; }
    public string StatusText { get; }
    public bool ShowStoreLink { get; }
    public string StoreLinkLabel { get; }
    public Uri? StoreLinkUri { get; }
}
