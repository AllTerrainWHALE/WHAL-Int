using System;
using System.Threading;
using System.Windows.Forms;

namespace WHAL_Int.Formatter;

public static class ClipboardHelper
{
    public static void CopyToClipboard(string text)
    {
        var thread = new Thread(() => Clipboard.SetText(text));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
