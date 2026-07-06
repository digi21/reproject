using Windows.ApplicationModel.DataTransfer;

namespace Reproject;

public interface IClipboardService
{
    void SetText(string text);
}

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
    }
}
