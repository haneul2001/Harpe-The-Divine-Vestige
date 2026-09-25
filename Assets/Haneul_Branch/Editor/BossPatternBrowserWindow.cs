using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 보스 패턴 정리 — 메뉴 [Harpe > 보스 패턴 정리]
//
// 보스 하나가 쓰는 공격을 "어떤 애니메이션으로 나가는가" 기준으로 모아 본다.
// 패턴은 프리팹의 컴포넌트로 붙어 있고 클립은 컨트롤러 안에 있어서,
// 지금 어떤 패턴이 어떤 클립을 쓰는지 보려면 인스펙터와 애니메이터를 번갈아 열어야 했다.
//
// 두 가지를 보여 준다.
//   [애니메이션] 클립 한 장씩 — 프레임을 돌려 보고, 타격 이벤트가 몇 초에 박혀 있는지 본다
//   [패턴]       패턴 한 줄씩 — 어느 페이즈에 몇 퍼센트로 나오고, 예고가 언제 다 차는지 본다
//
// 칸/줄 클릭: 선택 · 더블클릭: 프리팹 선택 · 우클릭: 메뉴
public class BossPatternBrowserWindow : EditorWindow
{
    private static readonly string[] SearchRoots = { "Assets/Haneul_Branch" };

    // 예고 표시가 다 차는 시점. DangerZone.SetTiming과 같은 식이어야 화면과 말이 맞는다
    private const float FillLeadRatio = 0.25f;
    private const float FillLeadMin = 0.2f;
    private const float FillLeadMax = 0.5f;

    private class Frame
    {
        public float time;
        public Sprite sprite;
    }

    private class ClipInfo
    {
        public AnimationClip clip;
        public string stateName = "";
        public int atkIndex = -1;            // 공격 클립이 아니면 -1
        public float hitTime = -1f;          // 타격 이벤트 시각. 없으면 -1
        public float length, fps;
        public bool looping;
        public List<Frame> frames = new List<Frame>();
        public Rect pivotBounds;
        public List<PatternInfo> users = new List<PatternInfo>();
    }

    private class PatternInfo
    {
        public BossPattern comp;
        public SerializedObject so;          // 인스펙터에서 고친 값을 매 프레임 다시 읽는다
        public string name = "", className = "";
        public int atkIndex, minPhase, maxPhase;
        public float weight, cooldown, minDistance, maxDistance, hitDelay;
        public ClipInfo clip;
        public List<string> settings = new List<string>();
        public float[] pickChance;           // 페이즈별 뽑힐 확률
        public DangerShape[] shapes = new DangerShape[0];
    }

    private class PhaseInfo
    {
        public string label = "";
        public float enterAt = 1f;
        public float warningMultiplier = 1f;
        public float attackIntervalMultiplier = 1f;
    }

    private readonly List<ClipInfo> clips = new List<ClipInfo>();
    private readonly List<PatternInfo> patterns = new List<PatternInfo>();
    private readonly List<PhaseInfo> phases = new List<PhaseInfo>();

    private GameObject[] bosses = new GameObject[0];
    private string[] bossNames = new string[0];
    private int bossIndex;

    private BossEnemy boss;
    private float warningDuration = 0.4f;
    private float defaultHitDelay = 0.5f;
    private int maxHp;

    // 방이 보스를 스폰할 때 프리팹 배율을 덮어쓴다. 그 배율로 봐야 게임에서 보이는 크기가 된다
    private float spawnScale = 0f;
    private float scaleFix = 1f;
    private string spawnRoom = "";

    private int tab;                          // 0 = 애니메이션, 1 = 패턴
    private bool playing = true;
    private float speed = 1f;
    private float cellSize = 190f;
    private bool darkBackground = true;
    private string search = "";
    private Vector2 scroll;

    private object selected;                  // ClipInfo 또는 PatternInfo
    private double clock, lastClockTime, lastRepaint;

    [MenuItem("Harpe/보스 패턴 정리")]
    private static void Open()
    {
        var w = GetWindow<BossPatternBrowserWindow>("보스 패턴");
        w.minSize = new Vector2(620f, 420f);
        w.Rescan();
    }

    private void OnEnable()
    {
        lastClockTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        if (bosses.Length == 0) Rescan();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (patternEditor != null) { DestroyImmediate(patternEditor); patternEditor = null; }
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        clock += playing ? (now - lastClockTime) * speed : 0.0;
        lastClockTime = now;

        // 30fps면 픽셀 애니메이션엔 충분하다. 60으로 올리면 에디터만 무거워진다
        if (playing && now - lastRepaint > 1.0 / 30.0)
        {
            lastRepaint = now;
            Repaint();
        }
    }

    // ────────────────────────────── 수집 ──────────────────────────────

    private void Rescan()
    {
        var found = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", SearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.GetComponentInChildren<BossEnemy>(true) != null) found.Add(go);
        }
        found.Sort(delegate(GameObject a, GameObject b) { return string.Compare(a.name, b.name, System.StringComparison.Ordinal); });

        bosses = found.ToArray();
        bossNames = found.Select(x => x.name).ToArray();
        bossIndex = Mathf.Clamp(bossIndex, 0, Mathf.Max(0, bosses.Length - 1));

        Load();
    }

    private void Load()
    {
        clips.Clear();
        patterns.Clear();
        phases.Clear();
        boss = null;
        selected = null;

        if (bosses.Length == 0) return;

        GameObject prefab = bosses[bossIndex];
        boss = prefab.GetComponentInChildren<BossEnemy>(true);
        if (boss == null) return;

        ReadBoss();
        ReadSpawnScale(prefab);
        ReadClips(prefab);
        ReadPatterns();
        LinkAndScore();
    }

    // 이 보스를 스폰하는 방을 찾아 배치 배율을 읽는다.
    // 프리팹 배율만 보고 그리면 실제보다 작은 장판을 보여 주게 된다.
    private void ReadSpawnScale(GameObject prefab)
    {
        spawnScale = 0f;
        scaleFix = 1f;
        spawnRoom = "";

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", SearchRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            foreach (var room in go.GetComponentsInChildren<Room>(true))
            {
                var so = new SerializedObject(room);
                var list = so.FindProperty("spawns");
                if (list == null || !list.isArray) continue;

                for (int i = 0; i < list.arraySize; i++)
                {
                    var el = list.GetArrayElementAtIndex(i);
                    var pr = el.FindPropertyRelative("prefab");
                    if (pr == null || pr.objectReferenceValue != prefab) continue;

                    var sc = el.FindPropertyRelative("scale");
                    if (sc == null) continue;

                    spawnScale = sc.floatValue;
                    spawnRoom = go.name;
                    break;
                }
                if (spawnScale > 0f) break;
            }
            if (spawnScale > 0f) break;
        }

        float prefabScale = Mathf.Abs(prefab.transform.localScale.x);
        if (spawnScale > 0f && prefabScale > 0.0001f) scaleFix = spawnScale / prefabScale;
    }

    private void ReadBoss()
    {
        var so = new SerializedObject(boss);
        warningDuration = Prop(so, "attackWarningDuration", 0.4f);
        defaultHitDelay = Prop(so, "defaultHitDelay", 0.5f);
        maxHp = boss.maxHp;

        var list = so.FindProperty("phases");
        if (list == null || !list.isArray) return;

        for (int i = 0; i < list.arraySize; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            phases.Add(new PhaseInfo
            {
                label = Str(el, "label"),
                enterAt = Float(el, "enterAtHpRatio", 1f),
                warningMultiplier = Float(el, "warningMultiplier", 1f),
                attackIntervalMultiplier = Float(el, "attackIntervalMultiplier", 1f),
            });
        }
        // 체력이 큰 쪽이 앞선 페이즈다 — 보스가 실행 중에 정렬하는 것과 같은 순서로 맞춘다
        phases.Sort(delegate(PhaseInfo a, PhaseInfo b) { return b.enterAt.CompareTo(a.enterAt); });
    }

    private void ReadClips(GameObject prefab)
    {
        var animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // atkIndex 몇 번이 어느 상태로 가는지 — AnyState 전이의 조건에 그대로 적혀 있다
        var indexOfState = new Dictionary<string, int>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller != null)
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;
                foreach (var tr in layer.stateMachine.anyStateTransitions)
                {
                    if (tr.destinationState == null) continue;
                    foreach (var c in tr.conditions)
                        if (c.parameter == "atkIndex" && c.mode == AnimatorConditionMode.Equals)
                            indexOfState[tr.destinationState.name] = Mathf.RoundToInt(c.threshold);
                }
            }

        var stateOfClip = new Dictionary<AnimationClip, string>();
        if (controller != null)
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;
                foreach (var st in layer.stateMachine.states)
                {
                    var clip = st.state.motion as AnimationClip;
                    if (clip != null && !stateOfClip.ContainsKey(clip)) stateOfClip[clip] = st.state.name;
                }
            }

        foreach (var clip in animator.runtimeAnimatorController.animationClips.Distinct())
        {
            if (clip == null) continue;

            ClipInfo info = BuildClip(clip);
            if (info == null) continue;

            string state;
            if (stateOfClip.TryGetValue(clip, out state)) info.stateName = state;

            int idx;
            if (info.stateName.Length > 0 && indexOfState.TryGetValue(info.stateName, out idx)) info.atkIndex = idx;

            clips.Add(info);
        }

        // 공격 클립이 먼저, 그 안에서는 atkIndex 순
        clips.Sort(delegate(ClipInfo a, ClipInfo b)
        {
            bool aa = a.atkIndex >= 0, ba = b.atkIndex >= 0;
            if (aa != ba) return aa ? -1 : 1;
            if (aa && a.atkIndex != b.atkIndex) return a.atkIndex.CompareTo(b.atkIndex);
            return string.Compare(a.clip.name, b.clip.name, System.StringComparison.Ordinal);
        });
    }

    private ClipInfo BuildClip(AnimationClip clip)
    {
        // 스프라이트 키가 여러 줄이면 가장 긴 줄을 쓴다 (본체 SpriteRenderer)
        ObjectReferenceKeyframe[] best = null;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys != null && (best == null || keys.Length > best.Length)) best = keys;
        }
        if (best == null || best.Length == 0) return null;

        var info = new ClipInfo
        {
            clip = clip,
            length = clip.length,
            fps = clip.frameRate,
            looping = clip.isLooping,
        };

        foreach (var e in clip.events)
            if (e.functionName == "AnimAttackHit") info.hitTime = e.time;

        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
        foreach (var k in best)
        {
            var sprite = k.value as Sprite;
            if (sprite == null) continue;
            info.frames.Add(new Frame { time = k.time, sprite = sprite });

            Vector2 pivot = sprite.pivot;
            Rect r = sprite.rect;
            minX = Mathf.Min(minX, -pivot.x);
            maxX = Mathf.Max(maxX, r.width - pivot.x);
            minY = Mathf.Min(minY, -pivot.y);
            maxY = Mathf.Max(maxY, r.height - pivot.y);
        }
        if (info.frames.Count == 0) return null;

        info.pivotBounds = new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
        return info;
    }

    // 모든 패턴이 공통으로 쓰는 항목. 나머지는 그 패턴만의 설정이라 따로 보여 준다
    private static readonly HashSet<string> CommonFields = new HashSet<string>
    {
        "m_Script", "patternName", "minPhase", "maxPhase", "weight",
        "cooldown", "maxDistance", "minDistance", "animationIndex", "hitDelay"
    };

    private void ReadPatterns()
    {
        foreach (var p in boss.GetComponents<BossPattern>())
        {
            var info = new PatternInfo
            {
                comp = p,
                so = new SerializedObject(p),
                className = p.GetType().Name,
            };
            ReadPatternValues(info);
            patterns.Add(info);
        }

        patterns.Sort(delegate(PatternInfo a, PatternInfo b)
        {
            if (a.atkIndex != b.atkIndex) return a.atkIndex.CompareTo(b.atkIndex);
            return b.weight.CompareTo(a.weight);
        });
    }

    // 값은 매 프레임 다시 읽는다. 인스펙터에서 숫자를 고치면 그 자리에서 그림이 바뀌어야
    // "고쳐 보고 판단한다"가 된다 — 다시 훑기를 눌러야 하면 안 쓰게 된다.
    private void ReadPatternValues(PatternInfo info)
    {
        BossPattern p = info.comp;
        if (p == null || info.so == null) return;

        info.so.UpdateIfRequiredOrScript();

        info.name = p.Name;
        info.atkIndex = p.AnimationIndex;
        info.hitDelay = p.HitDelay;
        info.weight = p.Weight;
        info.minPhase = (int)Prop(info.so, "minPhase", 0f);
        info.maxPhase = (int)Prop(info.so, "maxPhase", -1f);
        info.cooldown = Prop(info.so, "cooldown", 0f);
        info.minDistance = Prop(info.so, "minDistance", 0f);
        info.maxDistance = Prop(info.so, "maxDistance", 0f);

        info.settings.Clear();
        var it = info.so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (CommonFields.Contains(it.name)) continue;
            string v = Describe(it);
            if (v != null) info.settings.Add(it.displayName + " " + v);
        }

        // 장판 모양은 패턴이 직접 알려 준다 — 게임에 뜨는 표시와 같은 값이다.
        // 몸 크기에서 나온 값만 스폰 배율로 고쳐 잰다
        DangerShape[] raw = p.DangerShapes(boss);
        info.shapes = new DangerShape[raw != null ? raw.Length : 0];
        for (int i = 0; i < info.shapes.Length; i++) info.shapes[i] = raw[i].Rescaled(scaleFix);
    }

    private void RefreshLive()
    {
        for (int i = patterns.Count - 1; i >= 0; i--)
        {
            if (patterns[i].comp == null) { patterns.RemoveAt(i); continue; }
            ReadPatternValues(patterns[i]);
        }
        Rescore();
    }

    private void LinkAndScore()
    {
        foreach (var p in patterns)
        {
            p.clip = clips.FirstOrDefault(c => c.atkIndex == p.atkIndex);
            if (p.clip != null) p.clip.users.Add(p);
        }
        Rescore();
    }

    private void Rescore()
    {
        // 페이즈별로 "지금 뽑힐 만한 것"들 사이의 비율. 거리·쿨다운은 상황이라 여기선 못 센다
        int n = Mathf.Max(1, phases.Count);
        foreach (var p in patterns) p.pickChance = new float[n];

        for (int phase = 0; phase < n; phase++)
        {
            float sum = 0f;
            foreach (var p in patterns) if (Usable(p, phase)) sum += p.weight;
            if (sum <= 0f) continue;
            foreach (var p in patterns) p.pickChance[phase] = Usable(p, phase) ? p.weight / sum : 0f;
        }
    }

    private static bool Usable(PatternInfo p, int phase)
    {
        if (p.weight <= 0f) return false;
        if (phase < p.minPhase) return false;
        if (p.maxPhase >= 0 && phase > p.maxPhase) return false;
        return true;
    }

    // ────────────────────────────── 값 읽기 ──────────────────────────────

    private static float Prop(SerializedObject so, string name, float fallback)
    {
        var p = so.FindProperty(name);
        if (p == null) return fallback;
        if (p.propertyType == SerializedPropertyType.Float) return p.floatValue;
        if (p.propertyType == SerializedPropertyType.Integer) return p.intValue;
        return fallback;
    }

    private static float Float(SerializedProperty parent, string name, float fallback)
    {
        var p = parent.FindPropertyRelative(name);
        return p != null && p.propertyType == SerializedPropertyType.Float ? p.floatValue : fallback;
    }

    private static string Str(SerializedProperty parent, string name)
    {
        var p = parent.FindPropertyRelative(name);
        return p != null && p.propertyType == SerializedPropertyType.String ? p.stringValue : "";
    }

    // 보여 줄 만한 값이면 짧은 글로, 아니면 null (그리지 않는다)
    private static string Describe(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float: return p.floatValue.ToString("0.##");
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue ? "예" : "아니오";
            case SerializedPropertyType.String: return p.stringValue.Length > 0 ? p.stringValue : null;
            case SerializedPropertyType.Vector2: return p.vector2Value.x.ToString("0.##") + "x" + p.vector2Value.y.ToString("0.##");
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue != null ? p.objectReferenceValue.name : "없음";
            default: return null;
        }
    }

    // ────────────────────────────── 그리기 ──────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();

        if (bosses.Length == 0)
        {
            EditorGUILayout.HelpBox("BossEnemy가 붙은 프리팹을 못 찾았다. [다시 훑기]를 눌러 봐.", MessageType.Info);
            return;
        }
        if (boss == null)
        {
            EditorGUILayout.HelpBox("이 프리팹에서 BossEnemy를 읽지 못했다.", MessageType.Warning);
            return;
        }

        RefreshLive();
        DrawSummary();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        if (tab == 0) DrawClipGrid();
        else DrawPatternList();
        EditorGUILayout.EndScrollView();

        if (tab == 1) DrawDetailPanel();
    }

    // 인스펙터나 다른 창에서 값을 고쳐도 이쪽이 따라오게 한다 (재생을 멈춰 둬도)
    private void OnInspectorUpdate()
    {
        if (!playing) Repaint();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int newBoss = EditorGUILayout.Popup(bossIndex, bossNames, EditorStyles.toolbarPopup, GUILayout.Width(150f));
        if (newBoss != bossIndex) { bossIndex = newBoss; scroll = Vector2.zero; Load(); }

        int newTab = GUILayout.Toolbar(tab, new[] { "애니메이션", "패턴" }, EditorStyles.toolbarButton, GUILayout.Width(140f));
        if (newTab != tab) { tab = newTab; scroll = Vector2.zero; }

        search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(140f));

        if (GUILayout.Button(playing ? "❚❚ 정지" : "▶ 재생", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            playing = !playing;

        GUILayout.Label("속도", EditorStyles.miniLabel, GUILayout.Width(28f));
        speed = GUILayout.HorizontalSlider(speed, 0.1f, 3f, GUILayout.Width(60f));

        if (tab == 0)
        {
            GUILayout.Label("크기", EditorStyles.miniLabel, GUILayout.Width(28f));
            cellSize = GUILayout.HorizontalSlider(cellSize, 120f, 320f, GUILayout.Width(70f));
        }

        darkBackground = GUILayout.Toggle(darkBackground, "어두운 배경", EditorStyles.toolbarButton, GUILayout.Width(78f));

        GUILayout.FlexibleSpace();
        GUILayout.Label(clips.Count + "클립 · " + patterns.Count + "패턴", EditorStyles.miniLabel);
        if (GUILayout.Button("다시 훑기", EditorStyles.toolbarButton, GUILayout.Width(68f))) Rescan();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(boss.name, EditorStyles.boldLabel, GUILayout.Width(150f));
        string scaleText = spawnScale > 0f
            ? " · 스폰 배율 " + spawnScale.ToString("0.##") + (spawnRoom.Length > 0 ? " (" + spawnRoom + ")" : "")
            : " · 스폰 배율 못 찾음 — 프리팹 크기로 그린다";
        GUILayout.Label("체력 " + maxHp.ToString("N0")
            + " · 예고 " + warningDuration.ToString("0.##") + "초"
            + " · 기본 타격지연 " + defaultHitDelay.ToString("0.##") + "초"
            + " · 페이즈 " + Mathf.Max(1, phases.Count) + "단계"
            + scaleText, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("프리팹 선택", EditorStyles.miniButton, GUILayout.Width(80f)))
        {
            Selection.activeObject = bosses[bossIndex];
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        EditorGUILayout.EndHorizontal();

        if (phases.Count > 1) DrawPhaseStrip();

        EditorGUILayout.EndVertical();
    }

    // 체력 막대를 페이즈 경계로 나눠 보여 준다 — 어느 구간이 어떤 페이즈인지가 한눈에 들어온다
    private void DrawPhaseStrip()
    {
        Rect bar = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true));
        bar = new Rect(bar.x + 2f, bar.y + 4f, bar.width - 4f, 18f);
        EditorGUI.DrawRect(bar, new Color(0.16f, 0.15f, 0.18f));

        for (int i = 0; i < phases.Count; i++)
        {
            float from = phases[i].enterAt;                                   // 이 페이즈가 시작되는 체력 비율
            float to = i + 1 < phases.Count ? phases[i + 1].enterAt : 0f;     // 끝나는 체력 비율

            // 체력은 오른쪽에서 왼쪽으로 줄어든다. 막대도 같은 방향으로 읽히게 그린다
            float x0 = bar.x + bar.width * (1f - from);
            float x1 = bar.x + bar.width * (1f - to);
            var seg = new Rect(x0 + (i == 0 ? 0f : 1f), bar.y, Mathf.Max(1f, x1 - x0 - 1f), bar.height);

            float k = phases.Count > 1 ? i / (float)(phases.Count - 1) : 0f;
            EditorGUI.DrawRect(seg, Color.Lerp(new Color(0.45f, 0.20f, 0.24f), new Color(0.85f, 0.25f, 0.20f), k));

            string label = (i + 1) + "페이즈";
            if (phases[i].warningMultiplier < 0.999f) label += " 예고 x" + phases[i].warningMultiplier.ToString("0.##");
            if (seg.width > 70f)
                GUI.Label(seg, label, MiniCentered());
        }
    }

    private GUIStyle centered;

    private GUIStyle MiniCentered()
    {
        if (centered == null)
        {
            centered = new GUIStyle(EditorStyles.miniLabel);
            centered.alignment = TextAnchor.MiddleCenter;
            centered.normal.textColor = Color.white;
        }
        return centered;
    }

    // ───────────────────── 애니메이션 탭 ─────────────────────

    private const float ClipLabelHeight = 74f;

    private void DrawClipGrid()
    {
        List<ClipInfo> shown = clips.Where(Match).ToList();
        if (shown.Count == 0)
        {
            EditorGUILayout.HelpBox("보여 줄 스프라이트 클립이 없다.", MessageType.Info);
            return;
        }

        float viewWidth = position.width - 24f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (cellSize + 6f)));
        int rows = Mathf.CeilToInt(shown.Count / (float)columns);

        GUILayout.Space(rows * (cellSize + ClipLabelHeight + 8f));
        Rect area = GUILayoutUtility.GetLastRect();

        for (int i = 0; i < shown.Count; i++)
        {
            int cx = i % columns, cy = i / columns;
            var cell = new Rect(
                area.x + cx * (cellSize + 6f),
                area.y + cy * (cellSize + ClipLabelHeight + 8f),
                cellSize, cellSize + ClipLabelHeight);
            DrawClipCell(shown[i], cell);
        }
    }

    private void DrawClipCell(ClipInfo c, Rect cell)
    {
        var art = new Rect(cell.x, cell.y, cell.width, cellSize);
        EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));

        if (ReferenceEquals(selected, c))
        {
            var outline = new Rect(art.x - 1f, art.y - 1f, art.width + 2f, art.height + 2f);
            EditorGUI.DrawRect(outline, new Color(1f, 0.75f, 0.3f, 0.55f));
            EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));
        }

        DrawSprite(c, art);

        // 공격 클립임을 배지로 — 목록을 훑을 때 이것부터 눈에 들어와야 한다
        if (c.atkIndex >= 0)
        {
            var badge = new Rect(art.x + 4f, art.y + 4f, 62f, 16f);
            EditorGUI.DrawRect(badge, new Color(0.85f, 0.25f, 0.2f, 0.9f));
            GUI.Label(badge, " 공격 " + c.atkIndex, MiniCentered());
        }

        float y = art.yMax + 2f;
        GUI.Label(new Rect(cell.x, y, cell.width, 14f), c.clip.name, EditorStyles.miniBoldLabel);
        y += 14f;
        GUI.Label(new Rect(cell.x, y, cell.width, 14f),
            c.frames.Count + "프레임 · " + c.fps.ToString("0") + "fps · " + c.length.ToString("0.00") + "초"
            + (c.looping ? " · 반복" : ""), EditorStyles.miniLabel);
        y += 15f;

        DrawClipTimeline(c, new Rect(cell.x, y, cell.width, 14f));
        y += 16f;

        string users = c.users.Count == 0 ? "쓰는 패턴 없음" : string.Join(", ", c.users.Select(u => u.name).ToArray());
        GUI.Label(new Rect(cell.x, y, cell.width, 26f), users, EditorStyles.miniLabel);

        HandleInput(c, cell, AssetDatabase.GetAssetPath(c.clip), c.clip);
    }

    // 클립 길이 위에 타격 이벤트가 어디 박혀 있는지. 예고 시간을 맞추는 근거가 이 값이다
    private void DrawClipTimeline(ClipInfo c, Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.20f, 0.19f, 0.22f));

        if (c.hitTime < 0f)
        {
            GUI.Label(r, " 타격 이벤트 없음", EditorStyles.miniLabel);
            return;
        }

        float k = Mathf.Clamp01(c.hitTime / Mathf.Max(0.0001f, c.length));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * k, r.height), new Color(0.35f, 0.30f, 0.38f));
        EditorGUI.DrawRect(new Rect(r.x + r.width * k - 1f, r.y, 2f, r.height), new Color(1f, 0.85f, 0.3f));
        GUI.Label(r, " 타격 " + c.hitTime.ToString("0.00") + "초", EditorStyles.miniLabel);
    }

    private bool Match(ClipInfo c)
    {
        string q = search != null ? search.Trim() : "";
        if (q.Length == 0) return true;
        return c.clip.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
            || c.users.Any(u => u.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void DrawSprite(ClipInfo c, Rect art)
    {
        if (c.frames.Count == 0) return;

        float len = Mathf.Max(0.0001f, c.length);
        float t = (float)(clock % len);
        int index = 0;
        for (int i = 0; i < c.frames.Count; i++)
            if (c.frames[i].time <= t + 0.0001f) index = i;

        Sprite sprite = c.frames[Mathf.Clamp(index, 0, c.frames.Count - 1)].sprite;
        if (sprite == null || sprite.texture == null) return;

        // 모든 프레임을 합친 크기에 맞춰 배율을 잡는다 — 프레임마다 그림이 달라도 안 흔들린다.
        // 픽셀아트라 1배 이상은 정수 배로만 키운다
        float pad = 10f;
        float fit = Mathf.Min((art.width - pad * 2f) / c.pivotBounds.width, (art.height - pad * 2f) / c.pivotBounds.height);
        float scale = fit >= 1f ? Mathf.Floor(fit) : fit;

        float boxW = c.pivotBounds.width * scale, boxH = c.pivotBounds.height * scale;
        float boxX = art.x + (art.width - boxW) * 0.5f;
        float boxY = art.y + (art.height - boxH) * 0.5f;

        Vector2 pivot = sprite.pivot;
        Rect rect = sprite.rect;
        float left = -pivot.x, top = rect.height - pivot.y;
        float x = boxX + (left - c.pivotBounds.xMin) * scale;
        float y = boxY + (c.pivotBounds.yMax - top) * scale;    // GUI는 y가 아래로 커진다

        var dst = new Rect(x, y, rect.width * scale, rect.height * scale);
        var tex = sprite.texture;
        var uv = new Rect(rect.x / tex.width, rect.y / tex.height, rect.width / tex.width, rect.height / tex.height);
        GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
    }

    // ───────────────────── 패턴 탭 ─────────────────────

    private const float RowHeight = 96f;
    private const float PreviewWidth = 96f;

    private void DrawPatternList()
    {
        List<PatternInfo> shown = patterns.Where(MatchPattern).ToList();
        if (shown.Count == 0)
        {
            EditorGUILayout.HelpBox("보여 줄 패턴이 없다.", MessageType.Info);
            return;
        }

        int lastAtk = int.MinValue;
        foreach (var p in shown)
        {
            // 같은 애니메이션을 쓰는 패턴끼리 묶어 준다 — 이게 이 창의 정리 기준이다
            if (p.atkIndex != lastAtk)
            {
                lastAtk = p.atkIndex;
                ClipInfo c = clips.FirstOrDefault(x => x.atkIndex == p.atkIndex);
                string title = "공격 " + p.atkIndex + (c != null ? " — " + c.clip.name : " — 클립 없음");
                if (c != null && c.hitTime >= 0f) title += "  (타격 " + c.hitTime.ToString("0.00") + "초)";
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            }

            Rect row = GUILayoutUtility.GetRect(10f, RowHeight, GUILayout.ExpandWidth(true));
            DrawPatternRow(p, row);
        }
    }

    private void DrawPatternRow(PatternInfo p, Rect row)
    {
        row = new Rect(row.x + 2f, row.y + 1f, row.width - 4f, row.height - 4f);
        EditorGUI.DrawRect(row, ReferenceEquals(selected, p)
            ? new Color(0.28f, 0.25f, 0.20f) : new Color(0.20f, 0.19f, 0.22f));

        // ① 미리보기
        var art = new Rect(row.x + 2f, row.y + 2f, PreviewWidth, row.height - 4f);
        EditorGUI.DrawRect(art, darkBackground ? new Color(0.13f, 0.12f, 0.15f) : new Color(0.62f, 0.62f, 0.66f));
        if (p.clip != null) DrawSprite(p.clip, art);

        // ② 장판 범위 — 모든 줄이 같은 배율이라 크기를 서로 견줄 수 있다
        var map = new Rect(art.xMax + 4f, row.y + 2f, DiagramWidth, row.height - 4f);
        DrawShapeDiagram(p, map, SharedExtent(), false);

        // ③ 이름과 설정
        float x = map.xMax + 8f;
        float textWidth = Mathf.Max(120f, row.width * 0.30f);
        GUI.Label(new Rect(x, row.y + 4f, textWidth, 16f), p.name, EditorStyles.boldLabel);
        GUI.Label(new Rect(x, row.y + 20f, textWidth, 14f), p.className, EditorStyles.miniLabel);

        string limits = "무게 " + p.weight.ToString("0.##");
        if (p.cooldown > 0f) limits += " · 쿨 " + p.cooldown.ToString("0.##") + "초";
        if (p.maxDistance > 0f) limits += " · " + p.maxDistance.ToString("0.#") + " 안";
        if (p.minDistance > 0f) limits += " · " + p.minDistance.ToString("0.#") + " 밖";
        GUI.Label(new Rect(x, row.y + 34f, textWidth, 14f), limits, EditorStyles.miniLabel);

        string set = p.settings.Count == 0 ? "" : string.Join(" · ", p.settings.Take(6).ToArray());
        GUI.Label(new Rect(x, row.y + 50f, textWidth, 40f), set, EditorStyles.miniLabel);

        // ④ 예고 타임라인과 페이즈별 확률
        float rightX = x + textWidth + 10f;
        float rightWidth = row.xMax - rightX - 6f;
        if (rightWidth > 120f)
        {
            DrawTelegraphBar(p, new Rect(rightX, row.y + 6f, rightWidth, 34f));
            DrawChanceBar(p, new Rect(rightX, row.y + 46f, rightWidth, 40f));
        }

        HandleInput(p, row, AssetDatabase.GetAssetPath(bosses[bossIndex]), p.comp);
    }

    // 예고가 언제 다 차고, 언제 맞는지. DangerZone이 실제로 쓰는 계산과 같은 식으로 그린다
    private void DrawTelegraphBar(PatternInfo p, Rect r)
    {
        float hit = p.hitDelay >= 0f ? p.hitDelay : defaultHitDelay;
        float total = warningDuration + hit;
        float lead = Mathf.Clamp(total * FillLeadRatio, FillLeadMin, FillLeadMax);
        float fill = Mathf.Max(0.05f, total - lead);

        var bar = new Rect(r.x, r.y + 14f, r.width, 16f);
        EditorGUI.DrawRect(bar, new Color(0.14f, 0.13f, 0.16f));

        float fk = Mathf.Clamp01(fill / total);
        EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * fk, bar.height), new Color(0.72f, 0.20f, 0.17f));
        EditorGUI.DrawRect(new Rect(bar.x + bar.width * fk, bar.y, bar.width * (1f - fk), bar.height),
            new Color(1f, 0.30f, 0.24f));
        EditorGUI.DrawRect(new Rect(bar.xMax - 2f, bar.y - 3f, 2f, bar.height + 6f), new Color(1f, 0.85f, 0.3f));

        GUI.Label(new Rect(r.x, r.y, r.width, 14f),
            "예고 " + total.ToString("0.00") + "초 = 차오름 " + fill.ToString("0.00")
            + " + 꽉 찬 채 " + lead.ToString("0.00") + " → 타격", EditorStyles.miniLabel);
    }

    // 페이즈마다 이 패턴이 뽑힐 비율. 거리·쿨다운은 상황이라 빠져 있다
    private void DrawChanceBar(PatternInfo p, Rect r)
    {
        int n = Mathf.Max(1, phases.Count);
        GUI.Label(new Rect(r.x, r.y, r.width, 14f), "페이즈별 등장 비율 (거리·쿨 제외)", EditorStyles.miniLabel);

        float cellW = r.width / n;
        for (int i = 0; i < n; i++)
        {
            var cell = new Rect(r.x + cellW * i + 1f, r.y + 15f, cellW - 2f, 20f);
            float chance = p.pickChance != null && i < p.pickChance.Length ? p.pickChance[i] : 0f;

            EditorGUI.DrawRect(cell, new Color(0.14f, 0.13f, 0.16f));
            if (chance > 0f)
            {
                float h = cell.height * Mathf.Clamp01(chance / 0.25f);   // 25%를 꽉 찬 것으로 본다
                EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - h, cell.width, h), new Color(0.85f, 0.45f, 0.22f));
            }
            GUI.Label(cell, chance > 0f ? (chance * 100f).ToString("0") + "%" : "—", MiniCentered());
        }
    }

    // ───────────────────── 장판 범위 그림 ─────────────────────

    private const float DiagramWidth = 118f;

    private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color ZoneFill = new Color(0.85f, 0.18f, 0.15f, 0.30f);
    private static readonly Color ZoneEdge = new Color(1f, 0.30f, 0.24f, 0.95f);

    // 모든 줄을 같은 배율로 그리기 위한 기준 — 가장 큰 장판이 들어갈 만큼
    private float SharedExtent()
    {
        float max = 4f;
        foreach (var p in patterns)
            foreach (var s in p.shapes)
            {
                if (!s.Valid) continue;
                float reach = Mathf.Max(s.size.x, s.size.y) * 0.5f + Mathf.Abs(s.forward) + s.spread;
                max = Mathf.Max(max, reach);
            }
        return max;
    }

    // 위에서 내려다본 그림. 보스(또는 플레이어)를 가운데 두고 장판을 실제 비율로 깐다.
    // 공격 방향은 항상 오른쪽이다 — 방향까지 그리면 읽을 게 늘기만 한다.
    private void DrawShapeDiagram(PatternInfo p, Rect r, float extent, bool detailed)
    {
        EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.13f));

        float half = Mathf.Max(0.5f, extent) * 1.08f;
        float ppu = Mathf.Min(r.width, r.height) * 0.5f / half;      // 1 월드 단위가 몇 픽셀인가
        var center = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);

        // 1칸 = 1 월드 단위. 칸이 너무 촘촘해지면 건너뛴다
        int step = ppu < 6f ? 5 : (ppu < 14f ? 2 : 1);
        for (int i = -Mathf.CeilToInt(half); i <= Mathf.CeilToInt(half); i += step)
        {
            float gx = center.x + i * ppu, gy = center.y + i * ppu;
            if (gx > r.x && gx < r.xMax) EditorGUI.DrawRect(new Rect(gx, r.y, 1f, r.height), GridLine);
            if (gy > r.y && gy < r.yMax) EditorGUI.DrawRect(new Rect(r.x, gy, r.width, 1f), GridLine);
        }

        if (p.shapes.Length == 0 || !p.shapes[0].Valid)
        {
            GUI.Label(r, "범위 없음", MiniCentered());
            return;
        }

        foreach (var s in p.shapes)
        {
            if (!s.Valid) continue;

            if (s.count > 1)
            {
                // 흩어지는 자리는 매번 달라진다 — 고정된 표본을 깔아 "대충 이 정도 범위"를 보여 준다
                DrawCircleOutline(center, s.spread * ppu, new Color(1f, 0.5f, 0.3f, 0.25f));
                for (int i = 0; i < s.count; i++)
                {
                    float ang = 360f / s.count * i + 18f;
                    float dist = s.spread * (i == 0 ? 0f : 0.62f);
                    var at = center + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), -Mathf.Sin(ang * Mathf.Deg2Rad)) * dist * ppu;
                    DrawDisc(at, s.Radius * ppu);
                }
            }
            else if (s.IsSector)
            {
                DrawWedge(center, s.Radius * ppu, s.halfAngle);
            }
            else if (s.circle)
            {
                DrawDisc(center, s.Radius * ppu);
            }
            else
            {
                var box = new Rect(
                    center.x + (s.forward - s.size.x * 0.5f) * ppu,
                    center.y - s.size.y * 0.5f * ppu,
                    s.size.x * ppu, s.size.y * ppu);
                EditorGUI.DrawRect(box, ZoneFill);
                DrawOutline(box, ZoneEdge);
            }
        }

        // 기준점 — 이 장판이 누구를 따라다니는지
        bool atPlayer = p.shapes[0].origin == DangerOrigin.Player;
        var marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
        EditorGUI.DrawRect(marker, atPlayer ? new Color(0.45f, 0.85f, 1f) : new Color(1f, 0.9f, 0.4f));

        var caption = new Rect(r.x + 2f, r.yMax - 15f, r.width - 4f, 13f);
        string text = (atPlayer ? "플레이어 기준 · " : "보스 기준 · ") + p.shapes[0].ToString();
        GUI.Label(caption, detailed ? text : p.shapes[0].ToString(), EditorStyles.miniLabel);

        if (detailed)
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 13f),
                "한 칸 " + step + "m · 공격 방향 →", EditorStyles.miniLabel);
    }

    private void DrawDisc(Vector2 center, float radius)
    {
        // 가로줄을 쌓아 원을 채운다. Handles는 GUI 위에서 어긋나 보여 쓰지 않는다
        int lines = Mathf.Clamp(Mathf.CeilToInt(radius * 2f), 4, 220);
        for (int i = 0; i < lines; i++)
        {
            float t = (i + 0.5f) / lines * 2f - 1f;              // -1 ~ 1
            float w = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * radius;
            float y = center.y + t * radius;
            EditorGUI.DrawRect(new Rect(center.x - w, y, w * 2f, radius * 2f / lines + 0.5f), ZoneFill);
        }
        DrawCircleOutline(center, radius, ZoneEdge);
    }

    // 부채꼴. 꼭짓점이 center, 오른쪽(+x)으로 벌어진다 — 그림에서 공격 방향은 늘 오른쪽이다
    private void DrawWedge(Vector2 center, float radius, float halfAngle)
    {
        float limit = halfAngle * Mathf.Deg2Rad;
        int steps = Mathf.Clamp(Mathf.CeilToInt(radius), 6, 200);

        // 반지름을 잘게 끊어 점을 찍어 채운다. GUI 위에서는 Handles가 어긋나 보인다
        for (int i = 0; i < steps; i++)
        {
            float r = radius * (i + 0.5f) / steps;
            int arcSteps = Mathf.Clamp(Mathf.CeilToInt(r * limit * 2f), 2, 220);
            for (int j = 0; j <= arcSteps; j++)
            {
                float a = -limit + (limit * 2f) * j / arcSteps;
                var p = new Vector2(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r);
                EditorGUI.DrawRect(new Rect(p.x - 0.9f, p.y - 0.9f, 1.8f, 1.8f), ZoneFill);
            }
        }

        // 바깥 호와 양 옆 변
        int edge = Mathf.Clamp(Mathf.CeilToInt(radius * limit * 2f), 8, 260);
        for (int j = 0; j <= edge; j++)
        {
            float a = -limit + (limit * 2f) * j / edge;
            var p = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
            EditorGUI.DrawRect(new Rect(p.x - 0.9f, p.y - 0.9f, 1.8f, 1.8f), ZoneEdge);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            float a = limit * side;
            for (int i = 0; i <= steps; i++)
            {
                float r = radius * i / steps;
                var p = new Vector2(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r);
                EditorGUI.DrawRect(new Rect(p.x - 0.9f, p.y - 0.9f, 1.8f, 1.8f), ZoneEdge);
            }
        }
    }

    private void DrawCircleOutline(Vector2 center, float radius, Color color)
    {
        int steps = Mathf.Clamp(Mathf.CeilToInt(radius * 1.4f), 16, 160);
        for (int i = 0; i < steps; i++)
        {
            float a = 360f / steps * i * Mathf.Deg2Rad;
            EditorGUI.DrawRect(new Rect(
                center.x + Mathf.Cos(a) * radius - 0.8f,
                center.y + Mathf.Sin(a) * radius - 0.8f, 1.6f, 1.6f), color);
        }
    }

    private void DrawOutline(Rect r, Color color)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
    }

    // ───────────────────── 고르고 바로 고치는 칸 ─────────────────────

    private Editor patternEditor;
    private BossPattern editorTarget;
    private Vector2 detailScroll;

    private void DrawDetailPanel()
    {
        var p = selected as PatternInfo;
        if (p == null || p.comp == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(272f));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(p.name + "  —  여기서 고치면 위 그림이 바로 바뀐다", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", EditorStyles.miniButton, GUILayout.Width(44f))) selected = null;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        Rect map = GUILayoutUtility.GetRect(240f, 240f, GUILayout.Width(240f), GUILayout.Height(240f));
        float extent = 4f;
        foreach (var s in p.shapes)
            if (s.Valid)
                extent = Mathf.Max(extent, Mathf.Max(s.size.x, s.size.y) * 0.5f + Mathf.Abs(s.forward) + s.spread);
        DrawShapeDiagram(p, map, extent, true);

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        if (patternEditor == null || editorTarget != p.comp)
        {
            if (patternEditor != null) DestroyImmediate(patternEditor);
            patternEditor = Editor.CreateEditor(p.comp);
            editorTarget = p.comp;
        }
        if (patternEditor != null) patternEditor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private bool MatchPattern(PatternInfo p)
    {
        string q = search != null ? search.Trim() : "";
        if (q.Length == 0) return true;
        return p.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
            || p.className.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ────────────────────────────── 입력 ──────────────────────────────

    private void HandleInput(object item, Rect rect, string assetPath, Object ping)
    {
        Event ev = Event.current;
        if (!rect.Contains(ev.mousePosition)) return;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            selected = item;
            Selection.activeObject = ping;
            EditorGUIUtility.PingObject(ping);
            ev.Use();
            Repaint();
        }
        else if (ev.type == EventType.ContextClick)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("경로 복사"), false, delegate { EditorGUIUtility.systemCopyBuffer = assetPath; });
            menu.AddItem(new GUIContent("보스 프리팹 선택"), false, delegate
            {
                Selection.activeObject = bosses[bossIndex];
                EditorGUIUtility.PingObject(Selection.activeObject);
            });
            menu.ShowAsContext();
            ev.Use();
        }
    }
}
