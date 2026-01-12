using System;
using System.Runtime.InteropServices;

public static class LPT
{
    // InpOutx64 exports these names:
    [DllImport("inpoutx64.dll", EntryPoint = "Out32", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Out32(short portAddress, short data);

    [DllImport("inpoutx64.dll", EntryPoint = "Inp32", CallingConvention = CallingConvention.Cdecl)]
    public static extern short Inp32(short portAddress);

    // Some builds export these aliases; keep them as fallbacks if you ever swap DLLs.
    [DllImport("inpoutx64.dll", EntryPoint = "DlPortWritePortUchar", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DlPortWritePortUchar(ushort portAddress, byte data);

    [DllImport("inpoutx64.dll", EntryPoint = "DlPortReadPortUchar", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte DlPortReadPortUchar(ushort portAddress);
}
