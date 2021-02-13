﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpWifiPasswordDump
{
    internal static class Win32
    {
        // From https://pinvoke.net/
        #region functions
        [SuppressUnmanagedCodeSecurity]
        [DllImport("Wlanapi.dll")]
        public static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved,
            out uint pdwNegotiatedVersion,out IntPtr phClientHandle);

        [DllImport("Wlanapi.dll")]
        public static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport("Wlanapi.dll")]
        public static extern uint WlanGetProfileList(
        IntPtr hClientHandle,
        [MarshalAs(UnmanagedType.LPStruct), In] Guid pInterfaceGuid,
        IntPtr pReserved,
        out IntPtr ppProfileList); // Pointer to WLAN_PROFILE_INFO_LIST

        [DllImport("Wlanapi.dll")]
        public static extern uint WlanGetProfile(
        IntPtr hClientHandle,
        [MarshalAs(UnmanagedType.LPStruct), In] Guid pInterfaceGuid,
        [MarshalAs(UnmanagedType.LPWStr)] string strProfileName,
        IntPtr pReserved,
        [MarshalAs(UnmanagedType.LPWStr)] out string pstrProfileXml,
        ref uint pdwFlags,
        out uint pdwGrantedAccess);



    #endregion

        #region enums
    public enum Flags
    {
        WLAN_PROFILE_GET_PLAINTEXT_KEY = 4,
        WLAN_READ_ACCESS = 20001
    }

    public enum WLAN_INTERFACE_STATE
    {
        wlan_interface_state_not_ready = 0,
        wlan_interface_state_connected = 1,
        wlan_interface_state_ad_hoc_network_formed = 2,
        wlan_interface_state_disconnecting = 3,
        wlan_interface_state_disconnected = 4,
        wlan_interface_state_associating = 5,
        wlan_interface_state_discovering = 6,
        wlan_interface_state_authenticating = 7
    }

    #endregion

        #region structs
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;

        public WLAN_INTERFACE_STATE isState;
    }
    public struct WLAN_INTERFACE_INFO_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_INTERFACE_INFO[] InterfaceInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WLAN_PROFILE_INFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;

        public uint dwFlags;
    }

    public struct WLAN_PROFILE_INFO_LIST
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        public WLAN_PROFILE_INFO[] ProfileInfo;
    }
        #endregion

    }
    class Program
    {
     static void EnumerateProfiles()
        {
            IntPtr hClient = IntPtr.Zero;
            var result = Win32.WlanOpenHandle(2, IntPtr.Zero, out _, out hClient);
            
            // Enumerating network interfaces
            var interfaceList = IntPtr.Zero;
            result = Win32.WlanEnumInterfaces(hClient, IntPtr.Zero, out interfaceList);
            var uintSize = Marshal.SizeOf<uint>(); // in case microsoft changes the size in the future 
            var numberOfItems = (uint)Marshal.ReadInt32(interfaceList, 0);

            var dwIndex = (uint)Marshal.ReadInt32(interfaceList, uintSize /* Offset for dwNumberOfItems */);
            var InterfaceInfo = new Win32.WLAN_INTERFACE_INFO[numberOfItems];

            for (int i = 0; i < numberOfItems; i++)
            {
                var interfaceInfo = new IntPtr(interfaceList.ToInt64()
                    + (uintSize * 2) /* Offset for dwNumberOfItems and dwIndex */
                    + (Marshal.SizeOf<Win32.WLAN_INTERFACE_INFO>() * i) /* Offset for preceding items */);

                InterfaceInfo[i] = Marshal.PtrToStructure<Win32.WLAN_INTERFACE_INFO>(interfaceInfo);
                var wlanInterface =  InterfaceInfo[i];
                Console.WriteLine($"Interface: {wlanInterface.strInterfaceDescription}\n");
                Console.WriteLine("SSID: PASSWORD");
                Console.WriteLine("-------------");

                var profileList = IntPtr.Zero;
                result = Win32.WlanGetProfileList(hClient, wlanInterface.InterfaceGuid,
                    IntPtr.Zero, out profileList);

                var profileListNumberOfItems = (uint)Marshal.ReadInt32(profileList, 0);
                dwIndex = (uint)Marshal.ReadInt32(profileList, uintSize);
                Win32.WLAN_PROFILE_INFO[] ProfileInfo = new Win32.WLAN_PROFILE_INFO[profileListNumberOfItems];

                for(int j = 0; j < profileListNumberOfItems; j++)
                {
                    var profileInfo = new IntPtr(profileList.ToInt64()
                        + uintSize * 2 + (Marshal.SizeOf<Win32.WLAN_PROFILE_INFO>() * j));
                    ProfileInfo[j] = Marshal.PtrToStructure<Win32.WLAN_PROFILE_INFO>(profileInfo);

                    var profileName = ProfileInfo[j].strProfileName;

                    String xml;
                    uint access = (uint)Win32.Flags.WLAN_READ_ACCESS;
                    uint flags = (uint)Win32.Flags.WLAN_PROFILE_GET_PLAINTEXT_KEY;
                    result = Win32.WlanGetProfile(hClient, wlanInterface.InterfaceGuid, profileName, IntPtr.Zero,
                        out xml, ref flags, out access);
                   
                   
                    // Extracting clear text password from XML
                    var match = Regex.Match(xml, "<keyMaterial>(.*)</keyMaterial>");
                    string password=  match.Groups[1].Value;
                    
                    // Only print profiles with passwords saved
                    if(password != String.Empty) Console.WriteLine($"{profileName}:{password}");

                }

            }

        }
    static void Main(string[] args)
        {
            EnumerateProfiles();
        }
    }
}
