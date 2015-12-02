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

        private const int TOUCHPAD_DATA_OFFSET = 35;
        private DS4Orientation _Orientation;

        public byte[] dump;

        public DS4(IntPtr hidapi_handle, string hidapi_path)
        {
            _hidapi_handle = hidapi_handle;
            _hidapi_path = hidapi_path;
            _Orientation = new DS4Orientation();
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

            cState.Timestamp = (ushort)((data[11] << 8) | data[10]);

            short[] Gyro = new short[3];
            short[] Accel = new short[3];
            Gyro[0] = (short)((data[14] << 8) | data[13]);
            Gyro[1] = (short)((data[16] << 8) | data[15]);
            Gyro[2] = (short)((data[18] << 8) | data[17]);

            Accel[0] = (short)((data[20] << 8) | data[19]);
            Accel[1] = (short)((data[22] << 8) | data[21]);
            Accel[2] = (short)((data[24] << 8) | data[23]);

            _Orientation.ApplyGyroAccel(Accel, Gyro, cState.Timestamp);
            cState.Orientation = _Orientation;

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
            Orientation = new DS4Orientation();
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

        public DS4Orientation Orientation;

        // 188 in this value = 1.25ms
        public ushort Timestamp;
         
        public int TouchCount = 0; // 0, 1, 2
         
        public int[,] Touches; // Range: 0 - 4095
    }

    public class DS4Orientation
    {
        public Quaternion Orientation
        {
            get { return _Orientation; }
        }
        private Quaternion _Orientation;

        public Vector3 Accel_Raw
        {
            get { return _Accel_Raw; }
        }
        private Vector3 _Accel_Raw = Vector3.zero;

        public Vector3 Gyro_Raw
        {
            get { return _Gyro_Raw; }
        }
        private Vector3 _Gyro_Raw = Vector3.zero;

        public float Accel_Deviation
        {
            get { return _Accel_Deviation; }
        }
        private float _Accel_Deviation;

        public DS4Orientation() : this(Quaternion.identity) { }

        public DS4Orientation(Quaternion initial)
        {
            _Orientation = initial;
            accel_array = new List<Vector3>(max_accel_array_size);
        }

        private List<Vector3> accel_array;
        private int max_accel_array_size = 20;
        private float max_still_sd = 100; // Maximum standard deviation of accel to still be considered still
        private int previous_timestamp = -1;

        public void ApplyGyroAccel(short[] Accel, short[] Gyro, ushort timestamp)
        {
            if (previous_timestamp != -1 && timestamp > previous_timestamp)
            {
                float diff = (float)(timestamp - previous_timestamp) * 0.00125f / 188f; // s since last report
                if (diff == 0) diff = 0.00125f;
                _Gyro_Raw = new Vector3(-Gyro[0], -Gyro[1], Gyro[2]) * diff / 20f;
                previous_timestamp = timestamp;
            }
            else
            {
                previous_timestamp = timestamp;
                return;
            }
            _Accel_Raw = new Vector3(-Accel[0], Accel[1], Accel[2]) * 9.8f / 8100f;

            

            if (accel_array.Count == max_accel_array_size)
                accel_array.RemoveAt(0);
                
            accel_array.Add(new Vector3(Accel[0], Accel[1], Accel[2]));

            float sd = StandardDeviation().magnitude;
            _Accel_Deviation = sd;

            if (sd <= max_still_sd)
                _Orientation = Quaternion.FromToRotation(Vector3.up, Accel_Raw);
            else
                _Orientation = Quaternion.Euler(Gyro_Raw) * _Orientation;
        }

        private Vector3 StandardDeviation()
        {
            Vector3 mean = Vector3.zero;
            for (int x = 0; x < accel_array.Count; x++)
                mean += accel_array[x];
            mean /= accel_array.Count;

            Vector3 ret = Vector3.zero;
            for (int x = 0; x < accel_array.Count; x++)
            {
                Vector3 d = accel_array[x] - mean;
                ret += new Vector3(d.x * d.x, d.y * d.y, d.z * d.z) / accel_array.Count;
            }

            ret.x = Mathf.Sqrt(ret.x);
            ret.y = Mathf.Sqrt(ret.y);
            ret.z = Mathf.Sqrt(ret.z);

            return ret;
        }
    }

    public class DS4Out
    {
        byte SmallRumble;
        byte BigRumble;
        Color Backlight;

    }
}