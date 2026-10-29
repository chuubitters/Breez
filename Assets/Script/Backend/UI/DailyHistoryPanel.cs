using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable] public class HistoryDay { public string date; public int sessions; public float minutes; public string mood; }
[Serializable] public class HistoryMood { public string date; public string time; public string mood; public string note; }
[Serializable] public class HistoryData { public HistoryDay[] days; public SummarySession[] sessions; public HistoryMood[] moods; }

// =====================================================================
// Weekly Summary > ดูประวัติ: เลือกดูประวัติการเล่น + ประวัติอารมณ์ของวันอื่นๆ
//   ‹ 21 – 27 ต.ค. 2026 ›   เลื่อนดูทีละสัปดาห์
//   [จ. 21] [อ. 22] ...      กดเลือกวัน (จุดสี = อารมณ์ของวันนั้น, x ครั้ง = จำนวนที่เล่น)
//   อารมณ์ของวันนั้น | การเล่นของวันนั้น
// ข้อมูลจาก RPC my_history (supabase/migrations/20261029000002_my_history.sql)
// สร้างด้วยเมนู Breez > Add Daily History To Mood Tracking
// =====================================================================
public class DailyHistoryPanel : MonoBehaviour
{
    [Serializable]
    public class DayChip
    {
        public Button button;
        public Image background;
        public TMP_Text weekday;
        public TMP_Text day;
        public Image moodDot;
        public TMP_Text count;
    }

    [Serializable] public class MoodIcon { public string mood; public Sprite sprite; }

    [Header("Panel")]
    public GameObject panel;
    public Button closeButton;
    public Button prevButton;
    public Button nextButton;
    public TMP_Text rangeText;
    public TMP_Text dayTitleText;
    public TMP_Text statusText;
    public DayChip[] chips;

    [Header("Lists")]
    public RectTransform moodContent;
    public GameObject moodTemplate;
    public TMP_Text moodEmpty;
    public RectTransform sessionContent;
    public GameObject sessionTemplate;
    public TMP_Text sessionEmpty;
    public MoodIcon[] moodIcons;

    [Header("Colors")]
    public Color chipNormal = Color.white;
    public Color chipSelected = new Color32(0xEE, 0xF5, 0xD6, 255);
    public Color chipFuture = new Color32(0xF4, 0xF4, 0xF1, 255);
    public Color textNormal = new Color32(51, 51, 51, 255);
    public Color textMuted = new Color32(150, 150, 150, 255);

    DateTime windowEnd, selected;
    HistoryData data;
    readonly Dictionary<string, HistoryData> cache = new Dictionary<string, HistoryData>();
    readonly List<GameObject> rows = new List<GameObject>();
    int requestId;

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (prevButton != null) prevButton.onClick.AddListener(() => Shift(-7));
        if (nextButton != null) nextButton.onClick.AddListener(() => Shift(7));
        if (chips != null)
            for (int i = 0; i < chips.Length; i++)
            {
                int k = i;
                if (chips[i].button != null) chips[i].button.onClick.AddListener(() => SelectIndex(k));
            }
        if (moodTemplate != null) moodTemplate.SetActive(false);
        if (sessionTemplate != null) sessionTemplate.SetActive(false);
    }

    static DateTime Today => DateTime.Now.Date;

    /// <summary>ดูอะไร: ทั้งคู่ / เฉพาะอารมณ์ / เฉพาะการเล่น</summary>
    public enum View { Both, Moods, Plays }
    public View CurrentView { get; private set; } = View.Both;

    [Header("แยกดู (ไม่ใส่ก็ได้ จะหาจากชื่อ Title / MoodHeader / PlayHeader)")]
    public TMP_Text titleText;
    public TMP_Text moodHeader;
    public TMP_Text playHeader;
    public string titleBoth = "ประวัติย้อนหลัง";
    public string titleMoods = "ประวัติอารมณ์";
    public string titlePlays = "ประวัติการเล่น";

    RectTransform moodList, playList, moodHead, playHead;
    Vector2 moodListPos, moodListSize, playListPos, playListSize;
    bool layoutCached;

    public void OpenMoods(DateTime day) => Open(day, View.Moods);
    public void OpenPlays(DateTime day) => Open(day, View.Plays);

    public void Open(DateTime day) => Open(day, View.Both);

    public void Open(DateTime day, View view)
    {
        if (panel != null) panel.SetActive(true);
        CurrentView = view;
        ApplyView();
        selected = day.Date > Today ? Today : day.Date;
        windowEnd = selected.AddDays(6) > Today ? Today : selected.AddDays(6);
        if (windowEnd < selected) windowEnd = selected;
        cache.Clear();   // เปิดใหม่ = ดึงข้อมูลล่าสุด
        Load();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Shift(int days)
    {
        var end = windowEnd.AddDays(days);
        if (end > Today) end = Today;
        if (end == windowEnd) return;
        windowEnd = end;
        selected = selected.AddDays(days);
        if (selected > windowEnd) selected = windowEnd;
        if (selected < windowEnd.AddDays(-6)) selected = windowEnd.AddDays(-6);
        Load();
    }

    void Load()
    {
        var from = windowEnd.AddDays(-6);
        if (rangeText != null) rangeText.text = BreezText.Range(from, windowEnd);
        if (nextButton != null) nextButton.interactable = windowEnd < Today;

        string key = BreezText.Iso(from);
        if (cache.TryGetValue(key, out var cached)) { data = cached; Render(); return; }

        data = null;
        Render();
        if (!BreezBackend.Instance.IsLoggedIn) { SetStatus("กรุณาเข้าสู่ระบบ"); return; }
        SetStatus("กำลังโหลด...");
        int id = ++requestId;
        string body = Json.Obj("p_from", Json.Str(BreezText.Iso(from)), "p_to", Json.Str(BreezText.Iso(windowEnd)));
        BreezBackend.Instance.Rpc("my_history", body, (ok, text) =>
        {
            if (this == null || id != requestId) return;
            if (!ok) { SetStatus(text); return; }
            try { data = JsonUtility.FromJson<HistoryData>(text); }
            catch (Exception e) { Debug.LogWarning("[Breez] History parse error: " + e.Message); SetStatus("อ่านข้อมูลไม่ได้"); return; }
            cache[key] = data;
            SetStatus("");
            Render();
        });
    }

    void SelectIndex(int i)
    {
        var day = windowEnd.AddDays(-6 + i);
        if (day > Today) return;
        selected = day;
        Render();
    }

    // ---------------------------------------------------------------
    void Render()
    {
        var from = windowEnd.AddDays(-6);
        if (chips != null)
            for (int i = 0; i < chips.Length; i++)
            {
                var c = chips[i];
                var d = from.AddDays(i);
                var info = FindDay(d);
                bool sel = d == selected, future = d > Today;
                if (c.background != null) c.background.color = future ? chipFuture : sel ? chipSelected : chipNormal;
                if (c.weekday != null) { c.weekday.text = d == Today ? "วันนี้" : BreezText.WeekdayShortOf(d); c.weekday.color = textMuted; }
                if (c.day != null) { c.day.text = d.Day.ToString(); c.day.color = future ? textMuted : textNormal; }
                bool hasMood = info != null && !string.IsNullOrEmpty(info.mood);
                if (c.moodDot != null) { c.moodDot.enabled = hasMood && CurrentView != View.Plays; if (hasMood) c.moodDot.color = MoodColor(info.mood); }
                if (c.count != null)
                {
                    if (CurrentView == View.Moods) c.count.text = hasMood ? BreezText.Mood(info.mood) : "";
                    else c.count.text = info != null && info.sessions > 0 ? info.sessions + " ครั้ง" : "";
                }
                if (c.button != null) c.button.interactable = !future;
            }

        if (dayTitleText != null) dayTitleText.text = BreezText.LongDate(selected);

        foreach (var r in rows) if (r != null) Destroy(r);
        rows.Clear();

        string iso = BreezText.Iso(selected);
        int moods = 0, sessions = 0;
        if (data != null)
        {
            if (data.moods != null)
                foreach (var m in data.moods)
                    if (m.date == iso) { AddMoodRow(m); moods++; }
            if (data.sessions != null)
                foreach (var s in data.sessions)
                    if (s.date == iso) { AddSessionRow(s); sessions++; }
        }
        bool loaded = data != null;
        if (moodEmpty != null) { moodEmpty.gameObject.SetActive(loaded && moods == 0 && CurrentView != View.Plays); moodEmpty.text = "ไม่ได้บันทึกอารมณ์ในวันนี้"; }
        if (sessionEmpty != null) { sessionEmpty.gameObject.SetActive(loaded && sessions == 0 && CurrentView != View.Moods); sessionEmpty.text = "ไม่มีการเล่นในวันนี้"; }
    }

    // ---------------------------------------------------------------
    // แยกดู: ซ่อนอีกคอลัมน์ แล้วขยายคอลัมน์ที่ดูให้เต็มความกว้าง
    // ---------------------------------------------------------------
    void CacheLayout()
    {
        if (layoutCached) return;
        layoutCached = true;
        Transform card = panel != null ? panel.transform.Find("Card") : null;
        if (titleText == null && card != null) titleText = FindText(card, "Title");
        if (moodHeader == null && card != null) moodHeader = FindText(card, "MoodHeader");
        if (playHeader == null && card != null) playHeader = FindText(card, "PlayHeader");
        moodList = ScrollRoot(moodContent);
        playList = ScrollRoot(sessionContent);
        moodHead = moodHeader != null ? moodHeader.rectTransform : null;
        playHead = playHeader != null ? playHeader.rectTransform : null;
        if (moodList != null) { moodListPos = moodList.anchoredPosition; moodListSize = moodList.sizeDelta; }
        if (playList != null) { playListPos = playList.anchoredPosition; playListSize = playList.sizeDelta; }
    }

    void ApplyView()
    {
        CacheLayout();
        bool showMood = CurrentView != View.Plays, showPlay = CurrentView != View.Moods;
        if (titleText != null) titleText.text = CurrentView == View.Moods ? titleMoods : CurrentView == View.Plays ? titlePlays : titleBoth;
        if (moodList != null) moodList.gameObject.SetActive(showMood);
        if (playList != null) playList.gameObject.SetActive(showPlay);
        if (moodHead != null) moodHead.gameObject.SetActive(CurrentView == View.Both);
        if (playHead != null) playHead.gameObject.SetActive(CurrentView == View.Both);

        // คืนตำแหน่งเดิม
        if (moodList != null) { moodList.anchoredPosition = moodListPos; moodList.sizeDelta = moodListSize; }
        if (playList != null) { playList.anchoredPosition = playListPos; playList.sizeDelta = playListSize; }
        if (CurrentView == View.Both || moodList == null || playList == null) return;

        // ขยายคอลัมน์เดียวให้กว้างเต็มการ์ด (ขอบซ้ายคอลัมน์อารมณ์ ถึงขอบขวาคอลัมน์การเล่น) และใช้ที่ของหัวคอลัมน์
        float left = moodListPos.x - moodListSize.x / 2f;
        float right = playListPos.x + playListSize.x / 2f;
        float top = Mathf.Max(moodListPos.y + moodListSize.y / 2f, playListPos.y + playListSize.y / 2f) + 50f;
        float bottom = Mathf.Min(moodListPos.y - moodListSize.y / 2f, playListPos.y - playListSize.y / 2f);
        var target = CurrentView == View.Moods ? moodList : playList;
        target.sizeDelta = new Vector2(right - left, top - bottom);
        target.anchoredPosition = new Vector2((left + right) / 2f, (top + bottom) / 2f);
    }

    static RectTransform ScrollRoot(RectTransform content)
    {
        if (content == null) return null;
        var sr = content.GetComponentInParent<ScrollRect>(true);
        return sr != null ? (RectTransform)sr.transform : null;
    }

    static TMP_Text FindText(Transform root, string name)
    {
        var t = root.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }


    HistoryDay FindDay(DateTime d)
    {
        if (data == null || data.days == null) return null;
        string iso = BreezText.Iso(d);
        foreach (var x in data.days) if (x.date == iso) return x;
        return null;
    }

    void AddMoodRow(HistoryMood m)
    {
        if (moodTemplate == null || moodContent == null) return;
        var go = Instantiate(moodTemplate, moodContent);
        go.SetActive(true);
        rows.Add(go);
        var icon = Find<Image>(go, "Icon");
        if (icon != null) { var sp = IconFor(m.mood); icon.sprite = sp; icon.enabled = sp != null; }
        var dot = Find<Image>(go, "Dot");
        if (dot != null) dot.color = MoodColor(m.mood);
        SetText(go, "Name", BreezText.Mood(m.mood));
        SetText(go, "Time", m.time + " น.");
        SetText(go, "Note", string.IsNullOrEmpty(m.note) ? "" : m.note);
    }

    void AddSessionRow(SummarySession s)
    {
        if (sessionTemplate == null || sessionContent == null) return;
        var go = Instantiate(sessionTemplate, sessionContent);
        go.SetActive(true);
        rows.Add(go);
        SetText(go, "Mode", s.mode);
        SetText(go, "Time", s.time + " น.");
        string result = BreezText.SessionResult(s);
        string line = $"ระยะเวลา {BreezText.Clock(s.duration)} นาที";
        SetText(go, "Detail", string.IsNullOrEmpty(result) ? line : line + "\n" + result);
        bool scored = s.score > 0;
        SetText(go, "Score", scored ? s.score.ToString() : "—");
        SetText(go, "ScoreLabel", scored ? "คะแนน" : "");
    }

    Sprite IconFor(string mood)
    {
        if (moodIcons != null)
            foreach (var m in moodIcons)
                if (string.Equals(m.mood, mood, StringComparison.OrdinalIgnoreCase)) return m.sprite;
        return null;
    }

    public static Color MoodColor(string mood)
    {
        switch ((mood ?? "").ToLowerInvariant())
        {
            case "lovely": return new Color32(0xF6, 0xA3, 0xB8, 255);
            case "happy": return new Color32(0xF8, 0xC8, 0x3A, 255);
            case "bored": return new Color32(0xA7, 0xCB, 0x46, 255);
            case "angry": return new Color32(0xE5, 0x56, 0x6B, 255);
            default: return new Color32(0x7E, 0x63, 0xB4, 255);
        }
    }

    static T Find<T>(GameObject go, string child) where T : Component
    {
        var t = go.transform.Find(child);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void SetText(GameObject go, string child, string value)
    {
        var t = Find<TMP_Text>(go, child);
        if (t != null) t.text = value;
    }

    void SetStatus(string s) { if (statusText != null) statusText.text = s; }
}
