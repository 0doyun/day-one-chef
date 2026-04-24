// Korean IME bridge contract.
// Implementation swaps between Phase 1 (OSS library wrapper) and Phase 2
// (HTML overlay + .jslib) without touching downstream consumers.
// See: docs/architecture/ADR-0001-korean-ime-strategy.md

using UnityEngine;
using UnityEngine.Events;

namespace DayOneChef.Bridge
{
    /// <summary>
    /// Abstract contract for a Korean IME text input that works on Unity
    /// WebGL despite the platform's lack of native IME composition support.
    ///
    /// Consumers (GameManager, Gemini client) bind only to this interface
    /// and are ignorant of whether Phase 1 (kou-yeung/WebGLInput) or Phase 2
    /// (DIY overlay) is providing the actual IME plumbing.
    /// </summary>
    public interface IKoreanImeBridge
    {
        /// <summary>
        /// Fired on every IME composition finalization and on direct input
        /// (ASCII, backspace). Argument is the current full string value.
        /// Does NOT signal submission — only that the text changed.
        /// </summary>
        UnityEvent<string> TextChanged { get; }

        /// <summary>
        /// Fired exactly once per Enter keypress with IME not composing,
        /// or once per blur-with-content. Argument is the final string.
        /// </summary>
        UnityEvent<string> Submitted { get; }

        /// <summary>
        /// Show the input, focusing it and displaying the placeholder.
        /// <paramref name="canvasRect"/> is the screen-space region, in
        /// CSS pixels relative to the Unity canvas origin, where the input
        /// should render. Consumed only by Phase 2; Phase 1 uses the
        /// existing TMP_InputField layout.
        /// </summary>
        void ShowOverlay(Rect canvasRect, string placeholder);

        /// <summary>Hide and blur the input without firing Submitted.</summary>
        void HideOverlay();

        /// <summary>Programmatically overwrite the current value.</summary>
        void SetValue(string text);
    }
}
