using System;
using System.Runtime.InteropServices;

namespace QuickAttach;

/// <summary>Add-in entry interface from the Extensibility type library, declared locally to avoid the PIA.</summary>
[ComImport]
[Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IDTExtensibility2
{
    [DispId(1)]
    void OnConnection([MarshalAs(UnmanagedType.IDispatch)] object application, int connectMode, [MarshalAs(UnmanagedType.IDispatch)] object addInInst, ref Array custom);

    [DispId(2)]
    void OnDisconnection(int removeMode, ref Array custom);

    [DispId(3)]
    void OnAddInsUpdate(ref Array custom);

    [DispId(4)]
    void OnStartupComplete(ref Array custom);

    [DispId(5)]
    void OnBeginShutdown(ref Array custom);
}
