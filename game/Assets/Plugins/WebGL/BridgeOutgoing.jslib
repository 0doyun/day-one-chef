// emscripten bridge: Unity C# → JavaScript. Invoked by UnityBridge.cs
// via a [DllImport("__Internal")] extern. The bundled JS hands the
// serialized payload to webview_flutter's injected channel named
// `FlutterBridge`. If the channel isn't there (plain browser, dev
// testing, pre-bootstrap race), the call degrades to a console log
// so we can still see what the game *wanted* to send.

mergeInto(LibraryManager.library, {
  BridgeSendToFlutter: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    try {
      if (typeof FlutterBridge !== 'undefined' && FlutterBridge.postMessage) {
        FlutterBridge.postMessage(json);
        return;
      }
      // Fallback so plain-browser smoke tests don't lose the message.
      console.warn('[UnityBridge] FlutterBridge channel unavailable, payload:', json);
    } catch (e) {
      console.error('[UnityBridge] postMessage failed:', e, 'payload:', json);
    }
  }
});
