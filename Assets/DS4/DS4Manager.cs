using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace DS4Api
{

    public class DS4Manager
    {
        public const ushort pid = 0x05C4;
        public const ushort pid2 = 0x09CC;
        public const ushort vid = 0x054C;

        /// A list of all currently connected Wii Remotes.
        public static List<DS4> Controllers { get { return _Controllers; } }
        private static List<DS4> _Controllers = new List<DS4>();

        public static bool FindWiimotes()
        {
            IntPtr ptr = HIDapi.hid_enumerate(vid, pid);

            if (ptr == IntPtr.Zero)
            {
                ptr = HIDapi.hid_enumerate(vid, pid2);
                if (ptr == IntPtr.Zero)
                {
                    return false;
                }
            }
            IntPtr cur_ptr = ptr;

            hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

            bool found = false;

            while (cur_ptr != IntPtr.Zero)
            {
                DS4 remote = null;
                bool fin = false;
                foreach (DS4 r in Controllers)
                {
                    if (fin)
                        continue;

                    if (r.hidapi_path.Equals(enumerate.path))
                    {
                        remote = r;
                        fin = true;
                    }
                }
                if (remote == null)
                {
                    IntPtr handle = HIDapi.hid_open_path(enumerate.path);

                    remote = new DS4(handle, enumerate.path);

                    Debug.Log("Found New Remote: " + remote.hidapi_path);

                    Controllers.Add(remote);

                    // TODO: Initialization (?)
                }

                cur_ptr = enumerate.next;
                if (cur_ptr != IntPtr.Zero)
                    enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
            }

            HIDapi.hid_free_enumeration(ptr);

            return found;
        }

        public static void Cleanup(DS4 remote)
        {
            if (remote != null)
            {
                if (remote.hidapi_handle != IntPtr.Zero)
                    HIDapi.hid_close(remote.hidapi_handle);

                Controllers.Remove(remote);
            }
        }

        public static bool HasWiimote()
        {
            return !(Controllers.Count <= 0 || Controllers[0] == null || Controllers[0].hidapi_handle == IntPtr.Zero);
        }
    }
}