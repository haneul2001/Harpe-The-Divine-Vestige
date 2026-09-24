using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 소울이 한 단계 찰 때마다 뜨는 특성 선택 화면.
//
// 화면이 멈추고(timeScale 0) 검은 막이 덮인 뒤, 카드 세 장이 차례로 뒤집혀 드러난다.
// 한 장을 고르면 나머지는 사라지고 게임이 이어진다.
//
// 카드 그림·뒤집기 프레임은 상점과 같은 것을 쓴다 (AbilityUISkin) —
// 같은 카드가 화면마다 달라 보이면 "내가 가진 그 카드"라는 게 안 읽힌다.
public class TraitPickPanel : MonoBehaviour
{
    // 카드 원본 100x155의 정수배. 한 판의 성장을 정하는 화면이라 상점(2배)보다 크게 잡는다
    private static readonly Vector2 CardSize = new Vector2(300f, 465f);
    private const float CardGap = 60f;
    private const float PixelScale = 3f;
    private const float KeyCapPixels = StatusIconSlot.KeyCapPixels;
    // 카드를 크게 그리는 화면이라 이름 글씨도 같이 키운다
    private const float NameScale = 1.4f;

    private AbilityUISkin skin;
    private Font font;

    private GameObject rootGo;
    private RectTransform row;
    private Image backdrop;
    private Text title;
    private TooltipView tooltip;

    private readonly List<Slot> slots = new List<Slot>();
    private System.Action<AbilityCard> onPicked;
    private float previousTimeScale = 1f;
    private float previousFixedDelta;
    private bool picking;

    public bool IsOpen { get; private set; }

    private class Slot
    {
        public AbilityCard card;
        public RectTransform root;
        public AbilityCardView view;
        public List<Image> halo = new List<Image>();
        public CanvasGroup group;
        public float phase;      // 등급별 맥동이 한 박자로 겹치지 않게 카드마다 다른 시작점
        public bool revealed;
    }

    // parent: 씬이 바뀌어도 살아남는 오브젝트(TraitLevelUp). 여기에 붙이지 않으면
    // 층을 넘어가는 순간 화면이 통째로 사라지면서 시간이 멈춘 채로 남는다.
    public static TraitPickPanel Create(AbilityUISkin skin, Font font, Transform parent = null)
    {
        var go = new GameObject("TraitPickPanel");
        if (parent != null) go.transform.SetParent(parent, false);
        var panel = go.AddComponent<TraitPickPanel>();
        panel.skin = skin;
        panel.font = font;
        panel.Build();
        return panel;
    }

    // ─────────────────────────────────────────────
    // 조립
    // ─────────────────────────────────────────────

    private void Build()
    {
        var canvasGo = new GameObject("TraitPickCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // 상점(250)보다 위 — 성장 선택은 무엇에도 가리면 안 된다

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        rootGo = UIFactory.Stretch("Root", canvasGo.transform).gameObject;

        // 검은 막 — 뒤 화면을 눌러도 아무 일이 없게 클릭을 막는다
        backdrop = UIFactory.Panel("Backdrop", rootGo.transform, new Color(0f, 0f, 0f, 0.8f));
        UIFactory.SetAnchoredBox(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        title = UIFactory.Label("Title", rootGo.transform, font, 34,
            new Color(0.93f, 0.83f, 0.56f), TextAnchor.MiddleCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, CardSize.y * 0.5f + 40f), new Vector2(0f, CardSize.y * 0.5f + 104f));
        title.text = "영혼이 모였다 — 특성을 하나 골라라";
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(3f, -3f);

        row = UIFactory.Empty("Row", rootGo.transform);
        UIFactory.SetAnchoredBox(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var tipGo = new GameObject("TooltipView");
        tipGo.transform.SetParent(rootGo.transform, false);
        tooltip = tipGo.AddComponent<TooltipView>();
        tooltip.Build(canvasRect, font, 420f, new Color(0.05f, 0.05f, 0.06f, 0.97f),
            new Color(0.66f, 0.58f, 0.39f, 0.9f), new Vector2(20f, -14f), skin);
        tipGo.transform.SetAsLastSibling();

        rootGo.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // 열기 / 고르기
    // ─────────────────────────────────────────────

    public void Show(List<AbilityCard> cards, System.Action<AbilityCard> picked)
    {
        if (cards == null || cards.Count == 0) return;

        onPicked = picked;
        picking = false;
        IsOpen = true;
        rootGo.SetActive(true);

        // 보스 피니시 연출처럼 timeScale을 낮춰 둔 상태에서 열리면, 그 값을 "원래 값"으로
        // 기억했다가 닫을 때 되돌려 놓아 게임이 계속 느려진다. 1보다 작으면 1로 본다.
        previousTimeScale = Time.timeScale > 0f ? Mathf.Max(1f, Time.timeScale) : 1f;
        // Room이 슬로우를 걸면 fixedDeltaTime도 같은 비율로 줄여 둔다.
        // 지금 timeScale로 나눠서 "슬로우가 없을 때의 값"을 복원해 둔다.
        previousFixedDelta = Time.timeScale > 0f ? Time.fixedDeltaTime / Time.timeScale : Time.fixedDeltaTime;
        Time.timeScale = 0f;
        SetPlayerInputLocked(true);

        BuildCards(cards);
        StartCoroutine(Intro());
    }

    private void BuildCards(List<AbilityCard> cards)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
        slots.Clear();

        float step = CardSize.x + CardGap;
        float left = -(cards.Count - 1) * step * 0.5f;

        for (int i = 0; i < cards.Count; i++)
        {
            AbilityCard card = cards[i];
            Color rarityColor = card.Rarity.Color();

            RectTransform holder = UIFactory.Empty("Card" + i, row);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = CardSize;
            holder.anchoredPosition = new Vector2(left + step * i, 0f);

            var slot = new Slot { card = card, root = holder, phase = i * 0.7f };

            // 등급 빛 — 카드 윤곽을 따라 번진다. 카드 그림을 등급 색으로 물들여 조금씩 키운
            // 복사본을 뒤에 겹치면, 카드가 위를 덮으므로 테두리 바깥으로 삐져나온 부분만 빛으로 남는다.
            // 큰 겹부터 만들어야 뒤로 간다 (자식은 나중에 추가될수록 위에 그려진다)
            Sprite silhouette = skin.Frame(card.Rarity);
            if (silhouette != null)
            {
                float[] pads = CardFx.HaloPads(card.Rarity);
                for (int p = pads.Length - 1; p >= 0; p--)
                {
                    Image layer = UIFactory.Panel("Halo" + p, holder, new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f), false);
                    layer.sprite = silhouette;
                    if (CardFx.HaloMaterial() != null) layer.material = CardFx.HaloMaterial();
                    float pad = pads[p] * PixelScale;
                    UIFactory.SetAnchoredBox(layer.rectTransform, Vector2.zero, Vector2.one,
                        new Vector2(-pad, -pad), new Vector2(pad, pad));
                    slot.halo.Insert(0, layer);   // 0번이 가장 안쪽(진한) 겹
                }
            }

            RectTransform cardSlot = UIFactory.Empty("View", holder);
            UIFactory.SetAnchoredBox(cardSlot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            slot.view = cardSlot.gameObject.AddComponent<AbilityCardView>();
            slot.view.Build(card, tooltip, font, new Color(0.11f, 0.11f, 0.14f, 1f), 10f, skin, NameScale);

            // 몇 번 키로 고르는 카드인지 — 상태바 스킬 키와 같은 키캡 그림
            BuildKeyCap(holder, (i + 1).ToString());

            // 등급은 카드 색으로도 보이지만, 글자로 한 번 더 못박아 준다
            Text rarityLabel = UIFactory.Label("Rarity", holder, font, 20, rarityColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(rarityLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, -KeyCapPixels * PixelScale - 44f), new Vector2(0f, -KeyCapPixels * PixelScale - 16f));
            rarityLabel.text = card.Rarity.Label();
            var rsh = rarityLabel.gameObject.AddComponent<Shadow>();
            rsh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            rsh.effectDistance = new Vector2(2f, -2f);

            slot.group = holder.gameObject.AddComponent<CanvasGroup>();

            AbilityCard captured = card;
            var relay = cardSlot.gameObject.AddComponent<ClickRelay>();
            relay.onClick = delegate { Pick(captured); };

            slots.Add(slot);
        }
    }

    // 상태바 스킬 칸과 같은 방식으로 그린다 (StatusIconSlot.SetKeyLabel과 같은 규칙:
    // 글자는 키캡 아랫단 음영 2픽셀만큼 올리고, 폭은 원본 픽셀 격자에 맞춰 반올림)
    private void BuildKeyCap(RectTransform holder, string keyName)
    {
        const float cs = PixelScale;

        Image cap = UIFactory.PixelImage("KeyCap", holder, skin.KeyCap(), cs);
        if (skin.KeyCap() == null) cap.color = new Color(0.26f, 0.24f, 0.31f, 1f);

        RectTransform rt = cap.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);

        Text label = UIFactory.Label("Key", rt, font, 27, new Color(0.93f, 0.90f, 0.96f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        label.fontSize = 27;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.text = keyName;
        UIFactory.SetAnchoredBox(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 2f * cs), Vector2.zero);

        float h = KeyCapPixels * cs;
        float w = Mathf.Max(h, label.preferredWidth + 6f * cs);
        rt.sizeDelta = new Vector2(Mathf.Ceil(w / cs) * cs, h);
    }

    // 카드가 한 장씩 시간차를 두고 뒤집힌다 — 세 장이 동시에 펴지면 뭘 봐야 할지 모른다
    private IEnumerator Intro()
    {
        Sprite[] back = skin.CardBackFlip();

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            Sprite[] face = skin.FaceFlip(slot.card.Rarity);
            if (back == null || face == null)
            {
                slot.revealed = true;
                continue;
            }

            // 고르는 화면이라 떨지 않고 바로 넘긴다 (charge 0). 대신 장마다 0.18초씩 늦게 시작한다
            Slot captured = slot;
            CardDrawAnimation.Play(slot.root, slot.view, slot.card.Rarity, back, face,
                skin.Frame(slot.card.Rarity), PixelScale,
                delegate { captured.revealed = true; }, 0f, 0.18f * i);
        }
        yield break;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 고를 카드가 하나도 없이 떠 있으면 아무것도 못 하고 시간만 멈춘다 —
        // 그대로 두면 게임이 얼어붙으므로 스스로 닫는다.
        // (에디터에서 플레이 중 재컴파일로 카드 참조가 날아갔을 때 실제로 이 상태가 된다)
        if (slots.Count == 0)
        {
            Close();
            return;
        }

        // 이 화면이 떠 있는 동안은 무조건 멈춰 있어야 한다.
        // 위에 겹쳐 열린 다른 창(능력 목록·상점)이 닫히면서 자기가 기억한 값으로 시간을 되돌리면
        // 고르는 도중에 게임이 다시 흐른다. 매 프레임 다시 0으로 눌러 둔다.
        if (Time.timeScale != 0f) Time.timeScale = 0f;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (!slot.revealed) continue;

            if (picking) continue;   // 고르는 중에는 PickRoutine이 밝기를 잡는다

            // 등급 빛 맥동 — 전설로 갈수록 두껍고 빠르게 뛴다
            AbilityRarity r = slot.card.Rarity;
            float pulse = 1f + CardFx.PulseDepth(r) * Mathf.Sin(Time.unscaledTime * CardFx.PulseSpeed(r) + slot.phase);
            SetHalo(slot, CardFx.BaseAlpha(r) * pulse);
        }

        if (picking) return;

        // 숫자 키로도 고를 수 있게 — 급할 때 마우스를 찾는 게 더 답답하다
        for (int i = 0; i < slots.Count && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Pick(slots[i].card); return; }
    }

    // 겹마다 정해진 비율로 밝기를 나눠 준다 — 안쪽이 진하고 바깥이 옅어야 윤곽처럼 보인다
    private static void SetHalo(Slot slot, float strength)
    {
        for (int i = 0; i < slot.halo.Count; i++)
        {
            Image layer = slot.halo[i];
            Color c = layer.color;
            layer.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(strength * CardFx.HaloLayerAlpha(i, slot.halo.Count)));
        }
    }

    private void Pick(AbilityCard card)
    {
        if (picking || !IsOpen) return;
        picking = true;

        if (tooltip != null) tooltip.Hide();
        StartCoroutine(PickRoutine(card));
    }

    // 고른 카드는 커지며 빛나고 나머지는 스르르 꺼진다
    private IEnumerator PickRoutine(AbilityCard card)
    {
        const float dur = 0.45f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                bool chosen = slot.card == card;

                if (chosen)
                {
                    slot.root.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(k * Mathf.PI));
                    SetHalo(slot, Mathf.Lerp(CardFx.BaseAlpha(slot.card.Rarity), 1.6f, Mathf.Sin(k * Mathf.PI)));
                }
                else
                {
                    slot.group.alpha = 1f - k;
                    slot.root.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k);
                }
            }
            yield return null;
        }

        Close();
        if (onPicked != null) onPicked(card);
    }

    private void Close()
    {
        IsOpen = false;
        if (tooltip != null) tooltip.Hide();
        rootGo.SetActive(false);

        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
        slots.Clear();

        RestoreTime();
        SetPlayerInputLocked(false);
    }

    // fixedDeltaTime도 같이 되돌린다 — Room이 슬로우를 걸 때 물리 간격까지 줄여 놓기 때문에
    // timeScale만 1로 돌리면 물리가 느린 채로 남는다
    private void RestoreTime()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (previousFixedDelta > 0f) Time.fixedDeltaTime = previousFixedDelta * Time.timeScale;
    }

    // 층 이동 등으로 화면이 통째로 사라져도 시간은 반드시 돌려놓는다
    private void OnDestroy()
    {
        if (IsOpen) RestoreTime();
    }

    private static void SetPlayerInputLocked(bool locked)
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        PlayerMove move = p.GetComponent<PlayerMove>();
        if (move != null) move.inputLocked = locked;
    }
}
