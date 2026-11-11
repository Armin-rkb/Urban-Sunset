using System.Runtime.InteropServices;
using UnityEngine;

namespace UrbanSunset
{
    /// <summary>Thin wrapper around the browser features in UrbanSunsetBridge.jslib.</summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void US_Vibrate(int ms);
        [DllImport("__Internal")] static extern int US_IsTouchDevice();
        [DllImport("__Internal")] static extern void US_Share(string text);
        [DllImport("__Internal")] static extern void US_GameReady();
#endif

        public static bool HapticsEnabled = true;

        public static void Vibrate(int ms)
        {
            if (!HapticsEnabled) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            US_Vibrate(ms);
#elif UNITY_ANDROID || UNITY_IOS
            if (ms >= 40) Handheld.Vibrate();
#endif
        }

        public static bool IsTouchDevice()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return US_IsTouchDevice() == 1;
#else
            return Input.touchSupported;
#endif
        }

        public static void Share(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            US_Share(text);
#else
            GUIUtility.systemCopyBuffer = text + " " + Application.absoluteURL;
#endif
        }

        /// <summary>Tells the HTML page the first frame is ready so it can hide the loading screen.</summary>
        public static void GameReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            US_GameReady();
#endif
        }
    }
}
