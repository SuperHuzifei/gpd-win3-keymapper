// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// GPD protocol v1 implementation. Protocol references are listed in the deliverable.
public sealed class GpdHid : IDisposable {
    public SafeFileHandle Handle;
    public string Path;
    public ushort UsagePage, InputLength, OutputLength, FeatureLength;
    public string Firmware;
    public static List<GpdHid> Find() {
        Guid guid; HidD_GetHidGuid(out guid);
        IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
        var found = new List<GpdHid>();
        try {
            for (uint i=0; ; i++) {
                var info = new SP_DEVICE_INTERFACE_DATA(); info.cbSize=Marshal.SizeOf(info);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref info)) break;
                uint size=0;
                SetupDiGetDeviceInterfaceDetail(set, ref info, IntPtr.Zero, 0, out size, IntPtr.Zero);
                IntPtr detail=Marshal.AllocHGlobal((int)size);
                try {
                    Marshal.WriteInt32(detail, IntPtr.Size==8?8:6);
                    if(!SetupDiGetDeviceInterfaceDetail(set,ref info,detail,size,out size,IntPtr.Zero)) continue;
                    string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                    if(path.IndexOf("vid_2f24&pid_0135",StringComparison.OrdinalIgnoreCase)<0) continue;
                    var h=CreateFile(path,0xc0000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
                    if(h.IsInvalid) { h.Dispose(); continue; }
                    IntPtr prep; HIDP_CAPS caps;
                    if(!HidD_GetPreparsedData(h,out prep)) {h.Dispose();continue;}
                    try { if(HidP_GetCaps(prep,out caps)!=0x110000) {h.Dispose();continue;} }
                    finally {HidD_FreePreparsedData(prep);}
                    if(caps.UsagePage!=0xff00) {h.Dispose();continue;}
                    found.Add(new GpdHid {Handle=h,Path=path,UsagePage=caps.UsagePage,
                        InputLength=caps.InputReportByteLength,OutputLength=caps.OutputReportByteLength,
                        FeatureLength=caps.FeatureReportByteLength});
                } finally {Marshal.FreeHGlobal(detail);}
            }
        } finally {SetupDiDestroyDeviceInfoList(set);}
        return found;
    }
    public byte[] Request(byte command, byte page, bool response, byte[] payload) {
        byte[] send=new byte[OutputLength];
        send[0]=1;send[1]=0xa5;send[2]=command;send[3]=0x5a;send[4]=(byte)(command^0xff);send[6]=page;
        if(payload!=null) {if(payload.Length!=16)throw new ArgumentException("16-byte page required"); Buffer.BlockCopy(payload,0,send,8,16);}
        if(!HidD_SetOutputReport(Handle,send,send.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(),"HID command failed");
        Thread.Sleep(60);
        if(!response)return null;
        byte[] recv=new byte[64];recv[0]=1;
        if(!HidD_GetInputReport(Handle,recv,recv.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(),"HID read failed");
        return recv;
    }
    public byte[] Ready(byte mode) {
        for(int attempt=0;attempt<8;attempt++) {
            byte[] b=Request((byte)(mode<<4),0,true,null);
            if(b[8]==0xaa) {
                Firmware=String.Format("X{0:x}{1:x2} K{2:x}{3:x2}",b[9],b[10],b[11],b[12]);
                return b;
            }
        }
        throw new InvalidDataException("Controller did not acknowledge protocol v1");
    }
    public byte[] ReadConfig() {
        Ready(1);
        byte[] config=new byte[128];
        for(byte i=0;i<2;i++) Buffer.BlockCopy(Request(0x11,i,true,null),0,config,i*64,64);
        byte[] c=Request(0x12,0,true,null);
        int checksum=0;foreach(byte v in config)checksum+=v;
        int reported=BitConverter.ToUInt16(c,24);
        if(checksum!=reported)throw new InvalidDataException("Checksum mismatch: "+checksum+" != "+reported);
        return config;
    }
    public void WriteConfig(byte[] config) {
        if(config==null || config.Length!=128)throw new ArgumentException("128-byte configuration required");
        Ready(1);
        if(Firmware!="X221 K118")throw new InvalidOperationException("This firmware has not been validated: "+Firmware);
        Ready(2);
        for(byte page=0;page<8;page++) {
            var block=new byte[16];Buffer.BlockCopy(config,page*16,block,0,16);
            Request(0x21,page,false,block);
        }
        var status=Request(0x22,0,true,null);
        int sum=0;foreach(byte v in config)sum+=v;
        if(BitConverter.ToUInt16(status,24)!=sum)throw new InvalidDataException("Staged configuration checksum failed; configuration was not committed");
        Request(0x23,0,false,null);
        Thread.Sleep(250);
        var readback=ReadConfig();
        for(int i=0;i<128;i++)if(readback[i]!=config[i])throw new InvalidDataException("Configuration readback differs at byte "+i+". Restore from backup if needed.");
    }
    public void Dispose() {if(Handle!=null)Handle.Dispose();}
    [StructLayout(LayoutKind.Sequential)]struct SP_DEVICE_INTERFACE_DATA {public int cbSize; public Guid InterfaceClassGuid;public int Flags;public IntPtr Reserved;}
    [StructLayout(LayoutKind.Sequential)]struct HIDP_CAPS {
        public ushort Usage,UsagePage,InputReportByteLength,OutputReportByteLength,FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes,NumberInputButtonCaps,NumberInputValueCaps,NumberInputDataIndices,NumberOutputButtonCaps,NumberOutputValueCaps,NumberOutputDataIndices,NumberFeatureButtonCaps,NumberFeatureValueCaps,NumberFeatureDataIndices;
    }
    [DllImport("hid.dll")]static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr data);
    [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")]static extern int HidP_GetCaps(IntPtr data,out HIDP_CAPS caps);
    [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_SetOutputReport(SafeFileHandle h,byte[] data,int length);
    [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]static extern bool HidD_GetInputReport(SafeFileHandle h,[In,Out]byte[] data,int length);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string file,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr hwnd,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref SP_DEVICE_INTERFACE_DATA info);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref SP_DEVICE_INTERFACE_DATA info,IntPtr detail,uint size,out uint needed,IntPtr device);
    [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
