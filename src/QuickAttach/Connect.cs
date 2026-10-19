using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickAttach;

[ComVisible(true)]
[Guid("02AC128C-3671-4CD8-88FB-B95021C55B0F")]
[ProgId("Abigroup.QuickAttach")]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public sealed class Connect : RibbonController, IDTExtensibility2
{
    public void OnConnection(object application, int connectMode, object addInInst, ref Array custom)
    {
        // Runs on the Outlook UI thread; keep it short and move the rest to the background.
        Start(new WindowsFormsSynchronizationContext());
    }

    public void OnDisconnection(int removeMode, ref Array custom) => Stop();

    public void OnAddInsUpdate(ref Array custom)
    {
    }

    public void OnStartupComplete(ref Array custom)
    {
    }

    public void OnBeginShutdown(ref Array custom)
    {
    }
}
