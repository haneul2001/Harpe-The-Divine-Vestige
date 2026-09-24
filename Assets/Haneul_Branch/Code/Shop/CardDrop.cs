using UnityEngine;
using UnityEngine.UI;

// 바닥에 떨어진 카드. 상점 주인을 죽이면 진열대에서 한 장이 떨어진다.
//
//  · 가까이 가면 카드 설명이 뜨고 머리 위에 V 키캡이 뜬다 (상인 앞에서 뜨던 것과 같은 모양)
//  · V를 누르면 먹는다
//
// 무엇을 주는지는 이 스크립트가 모른다 — 집었을 때 부를 함수만 받아 둔다.
// 덕분에 특성 카드든 능력치 카드든 같은 물건으로 떨어뜨릴 수 있다.
public class CardDrop : MonoBehaviour
{
    [SerializeField] private float pickupRange = 1.6f;
    [SerializeField] private KeyCode pickupKey = KeyCode.V;
    [Tooltip("그림이 위아래로 흔들리는 폭 (월드 단위)")]
    [SerializeField] private float bob = 0.12f;

    private string title;
    private string description;
    private Color accent = Color.white;
    private System.Action onCollect;

    private Transform player;
    private GameObject prompt;
    private GameObject infoPanel;
    private bool near;
    private bool taken;
    private Vector3 restPos;

    // 그림 1픽셀 = 월드 1/32 (게임 전체 픽셀 밀도)
    private const float PixelsPerUnit = 32f;

    public static CardDrop Spawn(Vector3 position, Sprite icon, string title, string description,
        Color accent, Sprite keyCap, Font font, Sprite panel, System.Action onCollect)
    {
        var go = new GameObject("CardDrop_" + title);
        go.transform.position = position;

        var drop = go.AddComponent<CardDrop>();
        drop.title = title;
        drop.description = description;
        drop.accent = accent;
        drop.onCollect = onCollect;
        drop.Build(icon, keyCap, font, panel);
        return drop;
    }

    private void Build(Sprite icon, Sprite keyCap, Font font, Sprite panelSprite)
    {
        restPos = transform.position;

        if (icon != null)
        {
            var art = new GameObject("Art");
            art.transform.SetParent(transform, false);

            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = icon;
            sr.sortingLayerName = "Object";
            sr.sortingOrder = 5;

            // 아이콘 원본 해상도와 무관하게 게임의 픽셀 격자에 맞춘다
            art.transform.localScale = Vector3.one * (icon.pixelsPerUnit / PixelsPerUnit);
        }

        BuildPrompt(keyCap, font);
        BuildInfo(font, panelSprite);
        SetNear(false);
    }

    // 상인 머리 위에 뜨던 것과 같은 모양 (키캡 그림 + 글자)
    private void BuildPrompt(Sprite keyCap, Font font)
    {
        prompt = new GameObject("Prompt");
        prompt.transform.SetParent(transform, false);
        prompt.transform.localPosition = new Vector3(0f, 1.1f, 0f);

        const float size = 0.5f;
        if (keyCap != null)
        {
            var cap = new GameObject("KeyCap");
            cap.transform.SetParent(prompt.transform, false);
            var sr = cap.AddComponent<SpriteRenderer>();
            sr.sprite = keyCap;
            sr.sortingLayerName = "Skill";
            sr.sortingOrder = 50;
            cap.transform.localScale = Vector3.one * (size / Mathf.Max(keyCap.bounds.size.x, 0.0001f));
        }

        var label = new GameObject("Key");
        label.transform.SetParent(prompt.transform, false);
        var tm = label.AddComponent<TextMesh>();
        tm.text = pickupKey.ToString();
        tm.fontSize = 48;
        tm.characterSize = size * 0.55f / (tm.fontSize * 0.1f);
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.93f, 0.83f, 0.56f);

        var mr = label.GetComponent<MeshRenderer>();
        Font keyFont = UIFactory.ResolveBoldFont();
        if (keyFont != null)
        {
            tm.font = keyFont;
            mr.sharedMaterial = keyFont.material;
        }
        else tm.fontStyle = FontStyle.Bold;

        mr.sortingLayerName = "Skill";
        mr.sortingOrder = 51;
    }

    // 설명은 월드 공간 캔버스로 — 화면 UI로 띄우면 카드가 어디 있는지와 따로 놀게 된다
    private void BuildInfo(Font font, Sprite panelSprite)
    {
        infoPanel = new GameObject("Info", typeof(Canvas));
        infoPanel.transform.SetParent(transform, false);
        infoPanel.transform.localPosition = new Vector3(0f, -1.15f, 0f);

        var canvas = infoPanel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Skill";
        canvas.sortingOrder = 60;

        var rect = (RectTransform)infoPanel.transform;
        rect.sizeDelta = new Vector2(300f, 110f);
        // UI 1칸 = 월드 1/80 — 방(20칸) 안에서 3.75칸 정도로, 플레이어를 가리지 않는 크기
        rect.localScale = Vector3.one * (1f / 80f);

        Image bg = UIFactory.Panel("Bg", infoPanel.transform, new Color(0.05f, 0.05f, 0.06f, 0.95f), false);
        UIFactory.ApplySprite(bg, panelSprite);
        if (panelSprite != null)
        {
            bg.color = Color.white;
            bg.pixelsPerUnitMultiplier = 0.5f;   // 테두리 두께를 카드 UI(2배)와 맞춘다
        }
        UIFactory.SetAnchoredBox(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Text name = UIFactory.Label("Name", infoPanel.transform, font, 24, accent, TextAnchor.UpperCenter, FontStyle.Bold);
        UIFactory.SetAnchoredBox(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(18f, -46f), new Vector2(-18f, -14f));
        name.text = title;

        Text body = UIFactory.Label("Body", infoPanel.transform, font, 18,
            new Color(0.86f, 0.87f, 0.9f), TextAnchor.UpperCenter);
        UIFactory.SetAnchoredBox(body.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(18f, 14f), new Vector2(-18f, -50f));
        body.text = description;
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (taken) return;

        // 눈에 띄게 둥실거린다 — 바닥 타일에 묻히면 떨어진 줄 모른다
        transform.position = restPos + Vector3.up * (Mathf.Sin(Time.time * 2.2f) * bob);

        if (player == null) return;

        bool nowNear = Vector2.Distance(player.position, restPos) <= pickupRange;
        if (nowNear != near) SetNear(nowNear);

        if (near && Input.GetKeyDown(pickupKey)) Collect();
    }

    private void SetNear(bool value)
    {
        near = value;
        if (prompt != null) prompt.SetActive(value);
        if (infoPanel != null) infoPanel.SetActive(value);
    }

    private void Collect()
    {
        if (taken) return;
        taken = true;

        if (onCollect != null) onCollect();
        ToastManager.Show(title + " 획득");
        Destroy(gameObject);
    }
}
