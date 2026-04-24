// Unity → JS → Flutter outbound bridge. Serializes a BridgeMessage to
// JSON and hands it to BridgeOutgoing.jslib, which in turn invokes the
// WebView's injected `FlutterBridge.postMessage` channel. In the
// editor / non-WebGL builds the call is a structured Debug.Log so
// gameplay code is not littered with platform ifdefs.

using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DayOneChef.Bridge
{
    public static class UnityBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void BridgeSendToFlutter(string json);
#endif

        public static void Send(BridgeMessage message)
        {
            if (message == null) return;
            var json = JsonUtility.ToJson(message);
#if UNITY_WEBGL && !UNITY_EDITOR
            BridgeSendToFlutter(json);
#else
            Debug.Log($"[UnityBridge] (non-WebGL stub) {json}");
#endif
        }
    }
}
