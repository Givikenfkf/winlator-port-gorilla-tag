using BepInEx;
using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;

namespace WinlatorXR_XRapiBridge
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string UDP_HOST = "127.0.0.1";
        private const int UDP_PORT = 7278;
        private const string XR_TMP_ROOT = @"Z:\tmp\xr";
        private const string VERSION_FILE = "version";
        private const string SYSTEM_FILE = "system";

        private UdpClient _udp;
        private IPEndPoint _endpoint;
        private float _targetHz = 90f;
        private float _interval;
        private float _hmdSyncValue = 0f;
        private int _frameCounter = 0;

        private void Awake()
        {
            Logger.LogInfo("[XRapiBridge] Awake");
            _interval = 1.0f / _targetHz;
            try
            {
                _udp = new UdpClient();
                _endpoint = new IPEndPoint(IPAddress.Parse(UDP_HOST), UDP_PORT);
                _udp.Client.Blocking = false;
            }
            catch (Exception e)
            {
                Logger.LogError("[XRapiBridge] UDP init failed: " + e);
            }

            try
            {
                Directory.CreateDirectory(XR_TMP_ROOT);
                File.WriteAllText(Path.Combine(XR_TMP_ROOT, VERSION_FILE), "0.2");
                Logger.LogInfo("[XRapiBridge] Wrote version file (0.2) to " + Path.Combine(XR_TMP_ROOT, VERSION_FILE));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[XRapiBridge] Could not write version file: " + e);
            }

            TryReadSystemFile();
            StartCoroutine(PoseLoop());
        }

        private void OnDestroy()
        {
            Logger.LogInfo("[XRapiBridge] Destroy");
            if (_udp != null) _udp.Close();
        }

        private IEnumerator PoseLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(_interval);
            while (true)
            {
                try
                {
                    SendPoseAndInputs();
                    if ((_frameCounter & 127) == 0) TryReadSystemFile();
                }
                catch (Exception e)
                {
                    Logger.LogWarning("[XRapiBridge] PoseLoop exception: " + e);
                }

                _frameCounter++;
                yield return wait;
            }
        }

        private void TryReadSystemFile()
        {
            try
            {
                string systemPath = Path.Combine(XR_TMP_ROOT, SYSTEM_FILE);
                if (File.Exists(systemPath))
                {
                    var sys = File.ReadAllText(systemPath);
                    Logger.LogInfo("[XRapiBridge] XR system info:\n" + sys);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("[XRapiBridge] Read system file error: " + e);
            }
        }

        private void SendPoseAndInputs()
        {
            Transform camT = Camera.main != null ? Camera.main.transform : null;
            GameObject leftObj = GameObject.Find("LeftHandModel") ?? GameObject.Find("LeftHand") ?? GameObject.Find("HandLeft");
            GameObject rightObj = GameObject.Find("RightHandModel") ?? GameObject.Find("RightHand") ?? GameObject.Find("HandRight");

            Vector3 lPos = new Vector3(-0.2f, 1.45f, 0.3f);
            Quaternion lRot = Quaternion.identity;
            Vector3 rPos = new Vector3(0.2f, 1.45f, 0.3f);
            Quaternion rRot = Quaternion.identity;
            Vector3 hPos = new Vector3(0f, 1.6f, 0f);
            Quaternion hRot = Quaternion.identity;
            float leftStickX = 0f, leftStickY = 0f, rightStickX = 0f, rightStickY = 0f;

            if (leftObj != null) { lPos = leftObj.transform.position; lRot = leftObj.transform.rotation; }
            if (rightObj != null) { rPos = rightObj.transform.position; rRot = rightObj.transform.rotation; }
            if (camT != null) { hPos = camT.position; hRot = camT.rotation; }

            bool L_GRIP = Input.GetButton("Fire1");
            bool L_MENU = false;
            bool L_THUMBSTICK_PRESS = false;
            bool L_THUMBSTICK_LEFT = false;
            bool L_THUMBSTICK_RIGHT = false;
            bool L_THUMBSTICK_UP = false;
            bool L_THUMBSTICK_DOWN = false;
            bool L_TRIGGER = Input.GetButton("Fire1");
            bool L_X = false;
            bool L_Y = false;

            bool R_A = false;
            bool R_B = false;
            bool R_GRIP = Input.GetButton("Fire2");
            bool R_THUMBSTICK_PRESS = false;
            bool R_THUMBSTICK_LEFT = false;
            bool R_THUMBSTICK_RIGHT = false;
            bool R_THUMBSTICK_UP = false;
            bool R_THUMBSTICK_DOWN = false;
            bool R_TRIGGER = Input.GetButton("Fire2");

            float[] floats = new float[29];

            floats[0] = lRot.x; floats[1] = lRot.y; floats[2] = lRot.z; floats[3] = lRot.w;
            floats[4] = leftStickX; floats[5] = leftStickY; floats[6] = lPos.x; floats[7] = lPos.y; floats[8] = lPos.z;

            floats[9] = rRot.x; floats[10] = rRot.y; floats[11] = rRot.z; floats[12] = rRot.w;
            floats[13] = rightStickX; floats[14] = rightStickY; floats[15] = rPos.x; floats[16] = rPos.y; floats[17] = rPos.z;

            floats[18] = hRot.x; floats[19] = hRot.y; floats[20] = hRot.z; floats[21] = hRot.w;
            floats[22] = hPos.x; floats[23] = hPos.y; floats[24] = hPos.z;

            floats[25] = 0.064f; floats[26] = 90f; floats[27] = 90f; floats[28] = (float)(_frameCounter & 0x7fffffff);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 29; i++) { sb.Append(floats[i].ToString("R")); if (i < 28) sb.Append(' '); }

            sb.Append(' ');
            sb.Append(BoolToTF(L_GRIP)); sb.Append(BoolToTF(L_MENU)); sb.Append(BoolToTF(L_THUMBSTICK_PRESS));
            sb.Append(BoolToTF(L_THUMBSTICK_LEFT)); sb.Append(BoolToTF(L_THUMBSTICK_RIGHT)); sb.Append(BoolToTF(L_THUMBSTICK_UP));
            sb.Append(BoolToTF(L_THUMBSTICK_DOWN)); sb.Append(BoolToTF(L_TRIGGER)); sb.Append(BoolToTF(L_X)); sb.Append(BoolToTF(L_Y));

            sb.Append(BoolToTF(R_A)); sb.Append(BoolToTF(R_B)); sb.Append(BoolToTF(R_GRIP)); sb.Append(BoolToTF(R_THUMBSTICK_PRESS));
            sb.Append(BoolToTF(R_THUMBSTICK_LEFT)); sb.Append(BoolToTF(R_THUMBSTICK_RIGHT)); sb.Append(BoolToTF(R_THUMBSTICK_UP));
            sb.Append(BoolToTF(R_THUMBSTICK_DOWN)); sb.Append(BoolToTF(R_TRIGGER));

            string payload = sb.ToString();

            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(payload);
                _udp.Send(bytes, bytes.Length, _endpoint);
                if ((_frameCounter & 255) == 0) Logger.LogDebug("[XRapiBridge] Sent UDP length " + bytes.Length);
            }
            catch (Exception e)
            {
                Logger.LogWarning("[XRapiBridge] UDP send failed: " + e);
            }

            _hmdSyncValue = floats[28];
        }

        private string BoolToTF(bool v) => v ? "T" : "F";

        private void OnGUI()
        {
            float intensity = (_hmdSyncValue % 256) / 255f;
            intensity = Mathf.Clamp01(intensity);
            Color col = new Color(intensity, 0f, 0f, 1f);

            int px = 6;
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            GUI.DrawTexture(new Rect(0, 0, px, px), tex);
            UnityEngine.Object.Destroy(tex);
        }
    }
}
