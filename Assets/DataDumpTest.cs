using UnityEngine;
using DS4Api;

public class DataDumpTest : MonoBehaviour
{
    private DS4 controller;

    private Rect windowRect = new Rect(300, 20, 470, 300);

    private DS4Data data;

    void Update()
    {
        if (!DS4Manager.HasWiimote()) { return; }

        controller = DS4Manager.Controllers[0];

        DS4Data tentative = data;
        do
        {
            data = tentative;
            tentative = controller.ReadDS4Data();

            if(tentative != null && data != null)
            {
                float diff = (float)(tentative.Timestamp - data.Timestamp) * 0.00125f / 188f; // s since last report
                if (diff == 0) diff = 0.00125f;
                Vector3 gyro = new Vector3(tentative.Gyro[0], tentative.Gyro[1], tentative.Gyro[2]) * diff / 5f;
                gyro_total += gyro;
            }
        } while (tentative != null);
    }

    private Vector3 gyro_total = Vector3.zero;

    void OnGUI()
    {
        GUI.Box(new Rect(0, 0, 300, Screen.height), "");

        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("DS4 Found: " + DS4Manager.HasWiimote());
        if (GUILayout.Button("Find DS4"))
            DS4Manager.FindWiimotes();

        if (GUILayout.Button("Cleanup"))
        {
            DS4Manager.Cleanup(controller);
            controller = null;
        }

        if (data == null)
            return;

        GUILayout.Label("X: " + data.Cross);
        GUILayout.Label("\u25cb: " + data.Circle);
        GUILayout.Label("\u25a1: " + data.Square);
        GUILayout.Label("\u25b3: " + data.Triangle);

        GUILayout.Label("PS: " + data.PS);
        GUILayout.Label("Share: " + data.Share);
        GUILayout.Label("Options: " + data.Options);

        GUILayout.Label("D-Pad Up: " + data.DpadUp);
        GUILayout.Label("D-Pad Down: " + data.DpadDown);
        GUILayout.Label("D-Pad Left: " + data.DpadLeft);
        GUILayout.Label("D-Pad Right: " + data.DpadRight);

        GUILayout.Label("Left Stick: (" + data.lstick[0] + "," + data.lstick[1] + ")");
        GUILayout.Label("Right Stick: (" + data.rstick[0] + "," + data.rstick[1] + ")");

        GUILayout.Label("L1: " + data.L1);
        GUILayout.Label("R1: " + data.R1);
        GUILayout.Label("L2: " + data.L2 + " (" + data.L2_analog + ")");
        GUILayout.Label("R2: " + data.R2 + " (" + data.R2_analog + ")");
        GUILayout.Label("L3: " + data.L3);
        GUILayout.Label("R3: " + data.R3);

        GUILayout.Label("Trackpad Button: " + data.TouchButton);
        GUILayout.Label("Trackpad Finger 1: (" + data.Touches[0, 0] + ", " + data.Touches[0, 1] + ")");
        GUILayout.Label("Trackpad Finger 2: (" + data.Touches[1, 0] + ", " + data.Touches[1, 1] + ")");

        Vector3 gyro = new Vector3(data.Gyro[0], data.Gyro[1], data.Gyro[2]) / 2000f;
        GUILayout.Label("Gyro: " + gyro);
        GUILayout.Label("Gyro Total: " + gyro_total);
        if (GUILayout.Button("Reset total"))
            gyro_total = Vector3.zero;
        GUILayout.Label("Accel: (" + data.Accel[0] + ", " + data.Accel[1] + ", " + data.Accel[2] + ")");

        GUILayout.EndVertical();

        if (controller != null)
            windowRect = GUI.Window(0, windowRect, DataWindow, "Data");
    }

    private Vector2 scrollPosition = Vector2.zero;

    void DataWindow(int id)
    {
        byte[] data = controller.dump;

        GUILayout.BeginVertical(GUILayout.Width(470), GUILayout.Height(300));
        GUILayout.Space(20);

        GUILayout.BeginHorizontal(GUILayout.Height(25));
        GUILayout.Space(10);
        GUILayout.Label("##", GUILayout.Width(40));
        GUILayout.Label("Val", GUILayout.Width(40));
        for (int x = 7; x >= 0; x--)
            GUILayout.Label(x.ToString(), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(240));

        for (int x = 0; x < data.Length; x++)
        {
            byte val = data[x];

            GUILayout.BeginHorizontal(GUILayout.Height(25));
            GUILayout.Space(10);
            GUILayout.Label(x.ToString(), GUILayout.Width(40));
            GUILayout.Label(val.ToString("X2"), GUILayout.Width(40));
            byte bit = (byte)0x80;
            for (int i = 0; i < 8; i++)
            {
                bool flipped = (val & bit) == bit;
                GUILayout.Label(flipped ? "1" : "0", GUILayout.Width(40));

                bit = (byte)(bit >> 1);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
}