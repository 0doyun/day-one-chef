// In-game HUD wired by MainKitchenSetup. Owns three panels:
//
//   - OrderPanel: top of screen — round counter, recipe title, and
//     the recipe component list ("빵 → 익힘, 치즈 → 자른 상태, ...")
//     so the player sees what they're being asked to make.
//   - MonologuePanel: left side, near the chef — types out the
//     Gemini #1 monologue character-by-character. The "허세 모먼트"
//     anchor per GDD §11.
//   - InputPanel: bottom — TMP_InputField (Korean IME via the
//     kou-yeung/WebGLInput plugin in WebGL builds) + send button.
//     Submit fires GameRound.SubmitInstructionAsync.
//
// All three panels live on a single Screen Space Overlay Canvas so
// nothing is bound to camera position; that keeps the HUD readable
// when the chef-animator drives the world camera around.

using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.UI
{
    public class KitchenHUD : MonoBehaviour
    {
        [Header("Order panel")]
        [SerializeField] private GameObject _orderCardRoot;
        [SerializeField] private TMP_Text _orderTitle;
        [SerializeField] private TMP_Text _orderCounter;
        // Day 13-B: recipe components are rendered as ingredient sprite
        // chips inside this row instead of the old "빵 → 익힘  •  패티 →
        // 익힘" text — first-time players couldn't parse the arrow syntax.
        [SerializeField] private RectTransform _orderComponentsRow;
        [SerializeField] private TMP_FontAsset _chipFont;
        [SerializeField] private IngredientIconEntry[] _ingredientIcons;

        [System.Serializable]
        public struct IngredientIconEntry
        {
            public IngredientType type;
            public Sprite sprite;
        }

        [Header("Monologue panel")]
        [SerializeField] private GameObject _monologueRoot;
        [SerializeField] private TMP_Text _monologueText;
        [SerializeField] private float _typingSecondsPerChar = 0.04f;

        [Header("Input panel")]
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _sendButton;
        [SerializeField] private TMP_Text _statusText;

        private GameRound _round;
        private Coroutine _typing;

        public void Bind(GameRound round)
        {
            _round = round;
            if (_input != null)
            {
                _input.onSubmit.AddListener(_ => Submit());
            }
            if (_sendButton != null)
            {
                _sendButton.onClick.AddListener(Submit);
            }
            HideMonologue();
            SetStatus(string.Empty);
        }

        public void PresentOrder(Order order, int processedCount, int total)
        {
            HideMonologue();
            SetStatus(string.Empty);
            // Day 13-B: Flutter's right-panel RecipePanel owns the recipe
            // view now. The Unity OrderCard remains in the scene (so the
            // existing wiring doesn't break) but stays hidden — keep it
            // off here. Toggle back on if Flutter ever loses the panel.
            if (_orderCardRoot != null) _orderCardRoot.SetActive(false);
            if (_input != null)
            {
                _input.text = string.Empty;
                _input.interactable = order != null;
                if (order != null) _input.ActivateInputField();
            }
            if (_orderCounter != null)
            {
                _orderCounter.text = total > 0 ? $"라운드 {processedCount + 1}/{total}" : "";
            }
            if (_orderTitle != null)
            {
                _orderTitle.text = order?.Recipe?.DisplayName ?? "주문 없음";
            }
            BuildComponentChips(order?.Recipe);
        }

        public void StartMonologue(string monologue)
        {
            // Day 13-B: clear the "thinking" status and hide the order
            // card so the chef's bubble owns the screen during action
            // execution. Card comes back on the next PresentOrder.
            SetStatus(string.Empty);
            if (_orderCardRoot != null) _orderCardRoot.SetActive(false);
            if (_monologueRoot == null || _monologueText == null) return;
            if (string.IsNullOrWhiteSpace(monologue))
            {
                HideMonologue();
                return;
            }
            _monologueRoot.SetActive(true);
            if (_typing != null) StopCoroutine(_typing);
            _typing = StartCoroutine(TypeMonologue(monologue));
        }

        public void HideMonologue()
        {
            if (_monologueRoot != null) _monologueRoot.SetActive(false);
            if (_typing != null) { StopCoroutine(_typing); _typing = null; }
        }

        public void SetStatus(string status)
        {
            if (_statusText != null) _statusText.text = status ?? string.Empty;
        }

        public void OnRoundCalling()
        {
            if (_input != null) _input.interactable = false;
            // Day 13-B: thinking is now visualised on the chef's head
            // via ChefAnimator.ShowThinking — clear the status row so
            // we don't double up the "thinking…" signal in two places.
            SetStatus(string.Empty);
        }

        public void OnRoundEnd(bool success, string reason)
        {
            SetStatus(success ? $"성공! {reason}" : $"실패: {reason}");
        }

        private async void Submit()
        {
            if (_round == null || _input == null) return;
            var text = _input.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("한 글자 이상 입력해주세요.");
                _input.ActivateInputField();
                return;
            }
            _input.interactable = false;
            SetStatus("지시 전송 중…");
            try
            {
                await _round.SubmitInstructionAsync(text);
            }
            catch (System.Exception ex)
            {
                SetStatus($"전송 실패: {ex.Message}");
            }
        }

        private IEnumerator TypeMonologue(string text)
        {
            if (_monologueText == null) yield break;
            _monologueText.text = string.Empty;
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (var ch in text)
            {
                sb.Append(ch);
                _monologueText.text = sb.ToString();
                yield return new WaitForSeconds(_typingSecondsPerChar);
            }
            _typing = null;
        }

        private void BuildComponentChips(Recipe recipe)
        {
            if (_orderComponentsRow == null) return;
            for (var i = _orderComponentsRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_orderComponentsRow.GetChild(i).gameObject);
            }
            var comps = recipe?.Components;
            if (comps == null || comps.Count == 0) return;
            for (var i = 0; i < comps.Count; i++)
            {
                BuildChip(comps[i]);
            }
        }

        private void BuildChip(RecipeComponent c)
        {
            var chip = new GameObject($"Chip_{c.Type}",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            chip.transform.SetParent(_orderComponentsRow, false);
            var v = chip.GetComponent<VerticalLayoutGroup>();
            v.spacing = 2f;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.childControlWidth = false;
            v.childControlHeight = false;
            var le = chip.GetComponent<LayoutElement>();
            le.preferredWidth = 78f;
            le.preferredHeight = 60f;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(chip.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.sizeDelta = new Vector2(36f, 36f);
            var img = iconGo.GetComponent<Image>();
            img.sprite = ResolveIcon(c.Type);
            img.preserveAspect = true;
            img.color = img.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            var labelGo = new GameObject("State", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(chip.transform, false);
            var lblRt = (RectTransform)labelGo.transform;
            lblRt.sizeDelta = new Vector2(78f, 18f);
            var lbl = labelGo.GetComponent<TextMeshProUGUI>();
            if (_chipFont != null) lbl.font = _chipFont;
            lbl.fontSize = 12f;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = new Color(0.96f, 0.91f, 0.78f, 1f);
            lbl.text = $"{KoreanIngredientName(c.Type)} {KoreanStateShort(c.RequiredState)}";
        }

        private Sprite ResolveIcon(IngredientType type)
        {
            if (_ingredientIcons == null) return null;
            for (var i = 0; i < _ingredientIcons.Length; i++)
            {
                if (_ingredientIcons[i].type == type) return _ingredientIcons[i].sprite;
            }
            return null;
        }

        private static string KoreanIngredientName(IngredientType t) => t switch
        {
            IngredientType.Bread   => "빵",
            IngredientType.Patty   => "패티",
            IngredientType.Cheese  => "치즈",
            IngredientType.Lettuce => "상추",
            IngredientType.Tomato  => "토마토",
            IngredientType.Egg     => "계란",
            IngredientType.Cabbage => "양배추",
            IngredientType.Potato  => "감자",
            _ => t.ToString(),
        };

        // Short state labels — they sit under a 36×36 icon at 12pt and
        // need to fit in ~78px chip width without truncation. The full
        // descriptive forms still live in the round-end punchline.
        private static string KoreanStateShort(IngredientState s) => s switch
        {
            IngredientState.Raw     => "그대로",
            IngredientState.Cooked  => "익힘",
            IngredientState.Chopped => "썰기",
            IngredientState.Cracked => "껍질깸",
            IngredientState.Mixed   => "섞기",
            IngredientState.Beaten  => "풀기",
            _ => s.ToString(),
        };
    }
}
