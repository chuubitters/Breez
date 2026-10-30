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
    bool autoPick;

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
        autoPick = true; // โหลดเสร็จ => ถ้าวันนี้ว่าง เลือกวันล่าสุดที่มีข้อมูลให้
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
        if (!BreezBackend.Instance.IsLoggedIn) { SetStatus("กรุณาเข้าสู่ระบบก่อน จึงจะดูประวัติได้"); ShowEmpty("กรุณาเข้าสู่ระบบก่อน"); return; }
        SetStatus("กำลังโหลด...");
        ShowEmpty("กำลังโหลด...");
        int id = ++requestId;
        string body = Json.Obj("p_from", Json.Str(BreezText.Iso(from)), "p_to", Json.Str(BreezText.Iso(windowEnd)));
        BreezBackend.Instance.Rpc("my_history", body, (ok, text) =>
        {
            if (this == null || id != requestId) return;
            if (!ok)
            {
                Debug.LogWarning("[Breez] my_history failed: " + text);
                SetStatus("โหลดไม่สำเร็จ");
                ShowEmpty("โหลดข้อมูลไม่สำเร็จ ลองใหม่อีกครั้ง");
                return;
            }
            try { data = JsonUtility.FromJson<HistoryData>(text); }
            catch (Exception e) { Debug.LogWarning("[Breez] History parse error: " + e.Message); SetStatus("อ่านข้อมูลไม่ได้"); ShowEmpty("อ่านข้อมูลไม่ได้"); return; }
            if (data == null) data = new HistoryData();
            cache[key] = data;
            if (autoPick) { autoPick = false; PickLatestDayWithData(); }
            SetStatus(WeekSummary());
            Render();
        });
    }

    /// <summary>สรุปของวัน: "อารมณ์ มีความสุข · เล่น Calm Flow ×2, Soundscape"</summary>
    string DaySummary(DateTime d)
    {
        if (data == null) return "";
        string iso = BreezText.Iso(d);
        var moodNames = new List<string>();
        if (CurrentView != View.Plays && data.moods != null)
            foreach (var m in data.moods)
                if (m.date == iso) { string n = BreezText.Mood(m.mood); if (!moodNames.Contains(n)) moodNames.Add(n); }
        var modes = new List<string>();
        var counts = new Dictionary<string, int>();
        if (CurrentView != View.Moods && data.sessions != null)
            foreach (var s in data.sessions)
                if (s.date == iso)
                {
                    if (!counts.ContainsKey(s.mode)) { counts[s.mode] = 0; modes.Add(s.mode); }
                    counts[s.mode]++;
                }
        var parts = new List<string>();
        if (moodNames.Count > 0) parts.Add("อารมณ์ " + string.Join(", ", moodNames));
        if (modes.Count > 0)
        {
            var list = new List<string>();
            foreach (var m in modes) list.Add(counts[m] > 1 ? $"{m} ×{counts[m]}" : m);
            parts.Add("เล่น " + string.Join(", ", list));
        }
        return string.Join("  ·  ", parts);
    }

    /// <summary>วันที่เลือกไม่มีข้อมูล => เลือกวันล่าสุดในสัปดาห์ที่มีข้อมูล (ตามมุมมองที่เปิด)</summary>
    void PickLatestDayWithData()
    {
        if (HasData(selected)) return;
        for (int i = 0; i < 7; i++)
        {
            var d = windowEnd.AddDays(-i);
            if (HasData(d)) { selected = d; return; }
        }
    }

    bool HasData(DateTime d)
    {
        if (data == null) return false;
        string iso = BreezText.Iso(d);
        if (CurrentView != View.Plays && data.moods != null) foreach (var m in data.moods) if (m.date == iso) return true;
        if (CurrentView != View.Moods && data.sessions != null) foreach (var s in data.sessions) if (s.date == iso) return true;
        return false;
    }

    string WeekSummary()
    {
        int moods = data?.moods?.Length ?? 0, plays = data?.sessions?.Length ?? 0;
        if (CurrentView == View.Moods) return moods > 0 ? $"สัปดาห์นี้บันทึกอารมณ์ {moods} ครั้ง" : "สัปดาห์นี้ยังไม่ได้บันทึกอารมณ์";
        if (CurrentView == View.Plays) return plays > 0 ? $"สัปดาห์นี้เล่น {plays} ครั้ง" : "สัปดาห์นี้ยังไม่มีการเล่น";
        return $"สัปดาห์นี้: อารมณ์ {moods} · เล่น {plays} ครั้ง";
    }

    /// <summary>ข้อความในช่องรายการ (ตอนโหลด / ว่าง / ผิดพลาด)</summary>
    void ShowEmpty(string message)
    {
        foreach (var t in new[] { moodEmpty, sessionEmpty })
        {
            if (t == null) continue;
            bool show = (t == moodEmpty && CurrentView != View.Plays) || (t == sessionEmpty && CurrentView != View.Moods);
            t.gameObject.SetActive(show);
            t.text = message;
            t.color = textMuted;
            t.transform.SetAsLastSibling();
        }
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
                    int plays = info != null ? info.sessions : 0;
                    if (CurrentView == View.Moods) c.count.text = hasMood ? BreezText.Mood(info.mood) : "";
                    else if (CurrentView == View.Plays) c.count.text = plays > 0 ? $"เล่น {plays} ครั้ง" : "";
                    else c.count.text = plays > 0 ? $"{plays} ครั้ง" : hasMood ? BreezText.Mood(info.mood) : "";
                    c.count.enableAutoSizing = true;
                    c.count.fontSizeMin = 14;
                    c.count.fontSizeMax = Mathf.Max(c.count.fontSizeMax, 20);
                }
                if (c.button != null) c.button.interactable = !future;
            }

        if (dayTitleText != null)
        {
            string sum = DaySummary(selected);
            dayTitleText.text = BreezText.LongDate(selected) + (string.IsNullOrEmpty(sum) ? "" : $"  <size=72%><color=#7A8A7E>{sum}</color></size>");
            dayTitleText.richText = true;
            dayTitleText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }

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
        if (!loaded) return;   // ระหว่างโหลด ข้อความมาจาก ShowEmpty
        string dayWord = selected == Today ? "วันนี้" : "วันนั้น";
        if (moodEmpty != null)
        {
            moodEmpty.gameObject.SetActive(moods == 0 && CurrentView != View.Plays);
            moodEmpty.text = $"ไม่ได้บันทึกอารมณ์ใน{dayWord}\nกดวันอื่นด้านบนเพื่อดูย้อนหลัง";
            moodEmpty.color = textMuted;
            moodEmpty.transform.SetAsLastSibling();
        }
        if (sessionEmpty != null)
        {
            sessionEmpty.gameObject.SetActive(sessions == 0 && CurrentView != View.Moods);
            sessionEmpty.text = $"ไม่มีการเล่นใน{dayWord}\nกดวันอื่นด้านบนเพื่อดูย้อนหลัง";
            sessionEmpty.color = textMuted;
            sessionEmpty.transform.SetAsLastSibling();
        }
        if (moodContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(moodContent);
        if (sessionContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(sessionContent);
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
        SwapStaticTexts();
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
        HideTemplateTexts(go);
        var icon = Find<Image>(go, "Icon");
        if (icon != null) { var sp = IconFor(m.mood); icon.sprite = sp; icon.enabled = sp != null; }
        var dot = Find<Image>(go, "Dot");
        if (dot != null) dot.color = MoodColor(m.mood);

        string th = BreezText.Mood(m.mood), en = Capitalize(m.mood);
        string name = string.Equals(th, en, StringComparison.OrdinalIgnoreCase) ? th : $"{th}  <color=#8A948B><size=75%>{en}</size></color>";
        Label(go.transform, "MoodName", "อารมณ์: " + name, 28, textNormal, 100, 15, 760, 40, left: true, bold: true);
        string sub = $"บันทึกเวลา {m.time} น." + (string.IsNullOrEmpty(m.note) ? "" : $"  ·  \"{m.note}\"");
        Label(go.transform, "MoodInfo", sub, 20, textMuted, 100, -19, 900, 30, left: true);
    }

    void AddSessionRow(SummarySession s)
    {
        if (sessionTemplate == null || sessionContent == null) return;
        var go = Instantiate(sessionTemplate, sessionContent);
        go.SetActive(true);
        rows.Add(go);
        HideTemplateTexts(go);

        Label(go.transform, "ModeName", $"{ModeIcon(s.mode)} {s.mode}", 28, textNormal, 24, 30, 760, 40, left: true, bold: true);
        Label(go.transform, "PlayInfo", $"เล่นเวลา {s.time} น.  ·  ใช้เวลา {BreezText.Clock(s.duration)} นาที", 20, textNormal, 24, -2, 900, 30, left: true);
        string result = BreezText.SessionResultFull(s);
        if (!string.IsNullOrEmpty(result)) Label(go.transform, "PlayResult", result, 20, textMuted, 24, -32, 900, 30, left: true);

        bool scored = s.score > 0;
        Label(go.transform, "ScoreValue", scored ? s.score.ToString() : "-", 40, scored ? textNormal : textMuted, -28, 10, 160, 50, left: false, bold: true, anchorRight: true);
        Label(go.transform, "ScoreCaption", scored ? "คะแนน" : "ไม่มีคะแนน", 18, textMuted, -28, -28, 160, 26, left: false, anchorRight: true);
    }

    // -----------------------------------------------------------------
    // ข้อความในแถว/ช่องวัน สร้างใหม่จากต้นแบบที่แสดงผลได้แน่นอน (ข้อความสถานะของหน้านี้)
    // ไม่ใช้ข้อความในเทมเพลตเดิม (บางตัวไม่แสดงผลในบางเครื่อง)
    // -----------------------------------------------------------------
    TMP_Text proto;
    TMP_Text Proto => proto != null ? proto : (proto = statusText != null ? statusText : sessionEmpty != null ? sessionEmpty : moodEmpty);

    static void HideTemplateTexts(GameObject row)
    {
        foreach (var t in row.GetComponentsInChildren<TMP_Text>(true))
            if (t.transform.parent == row.transform) t.gameObject.SetActive(false);
    }

    TMP_Text Label(Transform parent, string name, string text, float size, Color color, float x, float y, float w, float h,
                   bool left, bool bold = false, bool anchorRight = false)
    {
        var proto = Proto;
        if (proto == null) return null;
        var go = Instantiate(proto.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        foreach (Transform ch in go.transform) Destroy(ch.gameObject);   // ต้นแบบอาจมี sub-mesh ลูก
        var t = go.GetComponent<TMP_Text>();
        var rt = t.rectTransform;
        float ax = anchorRight ? 1f : left ? 0f : 0.5f;
        rt.anchorMin = rt.anchorMax = new Vector2(ax, 0.5f);
        rt.pivot = new Vector2(anchorRight ? 1f : left ? 0f : 0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        rt.localScale = Vector3.one;
        t.richText = true;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.color = color;
        t.alignment = anchorRight ? TextAlignmentOptions.Right : left ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>ช่องวัน + หัวข้อ: ใช้ข้อความที่สร้างใหม่แทนของเดิม</summary>
    readonly Dictionary<TMP_Text, TMP_Text> swapped = new Dictionary<TMP_Text, TMP_Text>();

    TMP_Text Swap(TMP_Text original, bool bold)
    {
        if (original == null) return null;
        if (swapped.TryGetValue(original, out var done) && done != null) return done;
        var rt = original.rectTransform;
        var t = Label(rt.parent, original.name + "Label", original.text, original.fontSize, original.color, 0, 0, rt.sizeDelta.x, rt.sizeDelta.y, false, bold);
        if (t == null) return original;
        var nrt = t.rectTransform;
        nrt.anchorMin = rt.anchorMin; nrt.anchorMax = rt.anchorMax; nrt.pivot = rt.pivot;
        nrt.anchoredPosition = rt.anchoredPosition; nrt.sizeDelta = rt.sizeDelta;
        t.alignment = original.alignment;
        t.overflowMode = TextOverflowModes.Ellipsis;
        nrt.SetSiblingIndex(rt.GetSiblingIndex() + 1);
        original.gameObject.SetActive(false);
        swapped[original] = t;
        return t;
    }

    void SwapStaticTexts()
    {
        if (chips != null)
            foreach (var c in chips)
            {
                c.weekday = Swap(c.weekday, false);
                c.day = Swap(c.day, true);
            }
        titleText = Swap(titleText, true);
        dayTitleText = Swap(dayTitleText, true);
        moodEmpty = Swap(moodEmpty, false);
        sessionEmpty = Swap(sessionEmpty, false);
        if (closeButton != null)
        {
            var lbl = closeButton.GetComponentInChildren<TMP_Text>(true);
            var t = Swap(lbl, true);
            if (t != null) { t.text = "X"; t.fontSize = 30; }
        }
    }

    static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

    /// <summary>จุดสีของโหมด (ใช้ตัว • ที่ฟอนต์ของเกมมี)</summary>
    static string ModeIcon(string mode)
    {
        switch (mode)
        {
            case GameDataRecorder.CalmFlow: return "<color=#A9C77E><size=140%>•</size></color>";
            case GameDataRecorder.PowerRelease: return "<color=#F2A7B8><size=140%>•</size></color>";
            case GameDataRecorder.BreatheFocus: return "<color=#EDC965><size=140%>•</size></color>";
            case GameDataRecorder.Soundscape: return "<color=#8DB8D9><size=140%>•</size></color>";
            case GameDataRecorder.QuickSession: return "<color=#E79AB0><size=140%>•</size></color>";
            default: return "<color=#BBBBBB><size=140%>•</size></color>";
        }
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
