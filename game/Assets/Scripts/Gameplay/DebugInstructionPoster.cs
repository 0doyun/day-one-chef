// Editor-time debug helper. Until the HUD / IME overlay lands (Day 7+)
// there is no in-game way to submit a player 지시, so this component
// lets you press a key in Play mode to fire a canned test instruction
// at GameRound. Remove or gate behind DEBUG once the real HUD arrives.

using UnityEngine;
using UnityEngine.InputSystem;

namespace DayOneChef.Gameplay
{
    public class DebugInstructionPoster : MonoBehaviour
    {
        [SerializeField] private GameRound _round;
        [SerializeField, TextArea(1, 3)] private string _testInstruction =
            "빵 화구에 올려서 구워줘";
        [SerializeField] private Key _triggerKey = Key.T;

        public void Bind(GameRound round) => _round = round;

        private async void Update()
        {
            if (_round == null) return;
            if (Keyboard.current == null) return;
            if (!Keyboard.current[_triggerKey].wasPressedThisFrame) return;

            Debug.Log($"[DebugInstructionPoster] Submitting test instruction: {_testInstruction}");
            await _round.SubmitInstructionAsync(_testInstruction);
        }
    }
}
