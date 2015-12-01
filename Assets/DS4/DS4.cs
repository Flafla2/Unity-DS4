using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace DS4Api
{
    public class DS4
    {
        public IntPtr hidapi_handle { get { return _hidapi_handle; } }
        private IntPtr _hidapi_handle = IntPtr.Zero;

        public string hidapi_path { get { return _hidapi_path; } }
        private string _hidapi_path;

        private bool IsUSB;

        private const int TOUCHPAD_DATA_OFFSET = 35;

        public byte[] dump;

        public DS4(IntPtr hidapi_handle, string hidapi_path)
        {
            _hidapi_handle = hidapi_handle;
            _hidapi_path = hidapi_path;
        }

        public DS4Data ReadDS4Data()
        {
            byte[] data = new byte[78];
            int status = RecieveRaw(hidapi_handle, data);
            if (status <= 0) return null; // Either there is some sort of error or we haven't recieved anything

            dump = data;

            DS4Data cState = new DS4Data();

            // From DS4Tool Source                 //
            // https://code.google.com/p/ds4-tool/ //

            cState.lstick[0] = data[1];
            cState.lstick[1] = data[2];
            cState.rstick[0] = data[3];
            cState.rstick[1] = data[4];
            cState.L2_analog = data[8];
            cState.R2_analog = data[9];

            cState.Triangle = ((byte)data[5] & (1 << 7)) != 0;
            cState.Circle = ((byte)data[5] & (1 << 6)) != 0;
            cState.Cross = ((byte)data[5] & (1 << 5)) != 0;
            cState.Square = ((byte)data[5] & (1 << 4)) != 0;
            cState.DpadUp = ((byte)data[5] & (1 << 3)) != 0;
            cState.DpadDown = ((byte)data[5] & (1 << 2)) != 0;
            cState.DpadLeft = ((byte)data[5] & (1 << 1)) != 0;
            cState.DpadRight = ((byte)data[5] & (1 << 0)) != 0;

            //Convert dpad into individual On/Off bits instead of a clock representation
            int dpad_state = ((cState.DpadRight ? 1 : 0) << 0) |
                ((cState.DpadLeft ? 1 : 0) << 1) |
                ((cState.DpadDown ? 1 : 0) << 2) |
                ((cState.DpadUp ? 1 : 0) << 3);
            switch (dpad_state)
            {
                case 0: cState.DpadUp = true; cState.DpadDown = false; cState.DpadLeft = false; cState.DpadRight = false; break;
                case 1: cState.DpadUp = true; cState.DpadDown = false; cState.DpadLeft = false; cState.DpadRight = true; break;
                case 2: cState.DpadUp = false; cState.DpadDown = false; cState.DpadLeft = false; cState.DpadRight = true; break;
                case 3: cState.DpadUp = false; cState.DpadDown = true; cState.DpadLeft = false; cState.DpadRight = true; break;
                case 4: cState.DpadUp = false; cState.DpadDown = true; cState.DpadLeft = false; cState.DpadRight = false; break;
                case 5: cState.DpadUp = false; cState.DpadDown = true; cState.DpadLeft = true; cState.DpadRight = false; break;
                case 6: cState.DpadUp = false; cState.DpadDown = false; cState.DpadLeft = true; cState.DpadRight = false; break;
                case 7: cState.DpadUp = true; cState.DpadDown = false; cState.DpadLeft = true; cState.DpadRight = false; break;
                case 8: cState.DpadUp = false; cState.DpadDown = false; cState.DpadLeft = false; cState.DpadRight = false; break;
            }

            cState.R3 = (data[6] & (1 << 7)) != 0;
            cState.L3 = (data[6] & (1 << 6)) != 0;
            cState.Options = (data[6] & (1 << 5)) != 0;
            cState.Share = (data[6] & (1 << 4)) != 0;
            cState.R1 = (data[6] & (1 << 1)) != 0;
            cState.L1 = (data[6] & (1 << 0)) != 0;
            cState.R2 = (data[6] & (1 << 3)) != 0;
            cState.L2 = (data[6] & (1 << 2)) != 0;

            cState.PS = (data[7] & (1 << 0)) != 0;
            cState.TouchButton = (data[7] & (1 << 2 - 1)) != 0;

            // Note: The trackpad, for whatever reason, can send data in multiple "packets" - I assume this accounts for the case where
            // you miss a read request in between multiple trackpad polls.  We DO NOT use this here.

            bool tp_active1 = (data[0 + TOUCHPAD_DATA_OFFSET] >> 7) == 0; // >= 1 touch detected
            bool tp_active2 = (data[4 + TOUCHPAD_DATA_OFFSET] >> 7) == 0; // > 1 touch detected

            int touch1_x = data[1 + TOUCHPAD_DATA_OFFSET] | ((data[2 + TOUCHPAD_DATA_OFFSET] & 0xF) << 8);
            int touch1_y = ((data[2 + TOUCHPAD_DATA_OFFSET] & 0xF0) >> 4) | ((data[3 + TOUCHPAD_DATA_OFFSET] << 4));
            int touch2_x = data[5 + TOUCHPAD_DATA_OFFSET] | ((data[6 + TOUCHPAD_DATA_OFFSET] & 0xF) << 8);
            int touch2_y = ((data[6 + TOUCHPAD_DATA_OFFSET] & 0xF0) >> 4) | ((data[7 + TOUCHPAD_DATA_OFFSET] << 4));

            if(tp_active1 && tp_active2)
                cState.Touches = new int[,] { { touch1_x, touch1_y }, { touch2_x, touch2_y } };
            else if (tp_active1)
                cState.Touches = new int[,] { { touch1_x, touch1_y }, { -1, -1 } };
            else
                cState.Touches = new int[,] { { -1, -1 }, { -1, -1 } };

            // End section from ds4tool source //

            cState.Gyro[0] = (short)((data[14] << 8) | data[13]);
            cState.Gyro[1] = (short)((data[16] << 8) | data[15]);
            cState.Gyro[2] = (short)((data[18] << 8) | data[17]);

            cState.Accel[0] = (short)((data[20] << 8) | data[19]);
            cState.Accel[1] = (short)((data[22] << 8) | data[21]);
            cState.Accel[2] = (short)((data[24] << 8) | data[23]);

            return cState;
        }

        public static int MaxWriteFrequency = 20; // In ms
        private static Queue<WriteQueueData> WriteQueue;

        public static int SendRaw(IntPtr hidapi_handle, byte[] data)
        {
            if (hidapi_handle == IntPtr.Zero) return -2;

            if (WriteQueue == null)
            {
                WriteQueue = new Queue<WriteQueueData>();
                SendThreadObj = new Thread(new ThreadStart(SendThread));
                SendThreadObj.Start();
            }

            WriteQueueData wqd = new WriteQueueData();
            wqd.pointer = hidapi_handle;
            wqd.data = data;
            lock (WriteQueue)
                WriteQueue.Enqueue(wqd);

            return 0;
        }

        private static Thread SendThreadObj;
        private static void SendThread()
        {
            while (true)
            {
                lock (WriteQueue)
                {
                    if (WriteQueue.Count != 0)
                    {
                        WriteQueueData wqd = WriteQueue.Dequeue();
                        int res = HIDapi.hid_write(wqd.pointer, wqd.data, new UIntPtr(Convert.ToUInt32(wqd.data.Length)));
                        if (res == -1) Debug.LogError("HidAPI reports error " + res + " on write: " + Marshal.PtrToStringUni(HIDapi.hid_error(wqd.pointer)));
                        else Debug.Log("Sent " + res + "b: [" + wqd.data[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(wqd.data, 1));
                    }
                }
                Thread.Sleep(MaxWriteFrequency);
            }
        }

        public static int RecieveRaw(IntPtr hidapi_handle, byte[] buf)
        {
            if (hidapi_handle == IntPtr.Zero) return -2;

            HIDapi.hid_set_nonblocking(hidapi_handle, 1);
            int res = HIDapi.hid_read(hidapi_handle, buf, new UIntPtr(Convert.ToUInt32(buf.Length)));

            return res;
        }

        private class WriteQueueData
        {
            public IntPtr pointer;
            public byte[] data;
        }
    }

    public class DS4Data
    {
        public DS4Data()
        {
            Gyro = new short[3];
            Accel = new short[3];
            Touches = new int[2, 2];
            lstick = new byte[2];
            rstick = new byte[2];
        }

        public byte[] lstick;
        public byte[] rstick;
        public byte L2_analog;
        public byte R2_analog;
         
        public bool Triangle;
        public bool Circle;
        public bool Cross;
        public bool Square;
         
        public bool DpadUp;
        public bool DpadDown;
        public bool DpadLeft;
        public bool DpadRight;
         
        public bool L1;
        public bool R1;
        public bool L2;
        public bool R2;
        public bool L3;
        public bool R3;
         
        public bool Options;
        public bool Share;
         
        public bool PS;
        public bool TouchButton;
         
        public short[] Gyro; // size 3
        public short[] Accel; // size 3

        // 188 in this value = 1.25ms
        public short Timestamp;
         
        public int TouchCount = 0; // 0, 1, 2
         
        public int[,] Touches; // Range: 0 - 4095
    }

    public class DS4Out
    {
        byte SmallRumble;
        byte BigRumble;
        Color Backlight;

    }
}