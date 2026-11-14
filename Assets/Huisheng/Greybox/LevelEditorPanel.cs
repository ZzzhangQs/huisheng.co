using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Huisheng.Greybox
{
    sealed class LevelEditorPanel
    {
        const int PageSize = 12;
        const float HeaderHeight = 30;
        const float RowHeight = 62;
        readonly GreyboxDemo demo;
        readonly TMP_FontAsset font;
        readonly GameObject root;
        readonly RectTransform grid;
        readonly TextMeshProUGUI info;
        readonly TextMeshProUGUI pageLabel;
        readonly TextMeshProUGUI toolLabel;
        readonly TextMeshProUGUI modeLabel;
        readonly Dictionary<string, ChartEvent> runner = new();
        readonly Dictionary<string, ChartEvent> dodge = new();
        LevelDocument draft;
        LevelChart preview;
        SeedPlacement moving;
        PlantType plant;
        bool delete;
        int page;

        public bool IsOpen => root.activeSelf;

        public LevelEditorPanel(GreyboxDemo demo, Transform canvas, TMP_FontAsset font)
        {
            this.demo = demo;
            this.font = font;
            root = new GameObject("Level editor", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            root.transform.SetParent(canvas, false);
            root.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            var panel = (RectTransform)root.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var title = Label(panel, "种子谱面", 27);
            Box(title.rectTransform, 0, 1, 16, 0, -16, -38);
            Button(panel, "切换种子", () => { plant = (PlantType)(((int)plant + 1) % 3); delete = false; moveMode = false; moving = null; UpdateTools(); }, 0, .15f, 16, -61);
            Button(panel, "放置", () => { delete = false; moveMode = false; moving = null; UpdateTools(); }, .15f, .27f, 0, -61);
            Button(panel, "移动", () => { delete = false; moving = null; moveMode = true; UpdateTools(); }, .27f, .39f, 0, -61);
            Button(panel, "删除", () => { delete = true; moveMode = false; moving = null; UpdateTools(); }, .39f, .51f, 0, -61);
            Button(panel, "预览", Preview, .51f, .67f, 0, -61);
            Button(panel, "试玩", Playtest, .67f, .83f, 0, -61);
            Button(panel, "关闭", Close, .83f, 1, 0, -61);

            toolLabel = Label(panel, "", 19);
            Box(toolLabel.rectTransform, 0, 1, 16, 0, -100, -32);
            modeLabel = Label(panel, "", 17);
            Box(modeLabel.rectTransform, 0, 1, 16, 0, -132, -28);

            grid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(panel, false);
            Box(grid, 0, 1, 16, -16, -168, -(HeaderHeight + 3 * RowHeight));

            Button(panel, "上一页", () => ChangePage(-1), 0, .14f, 16, -398);
            pageLabel = Label(panel, "", 18);
            Box(pageLabel.rectTransform, .15f, .42f, 0, 0, -398, -34);
            Button(panel, "下一页", () => ChangePage(1), .42f, .55f, 0, -398);
            Button(panel, "保存 JSON", Save, .56f, .75f, 0, -398);
            Button(panel, "读取 JSON", Load, .76f, 1, 0, -398);
            info = Label(panel, "", 17);
            Box(info.rectTransform, 0, 1, 16, -16, -443, -165);
            root.SetActive(false);
        }

        bool moveMode;

        static string PlantName(PlantType type) => type switch
        {
            PlantType.Reed => "芦苇",
            PlantType.Lily => "睡莲",
            _ => "普通种子"
        };

        public void Open(LevelDocument current)
        {
            if (draft == null)
            {
                bool valid = LevelChartGenerator.TryGenerate(current.Layout, current.Settings, out _, out string error);
                draft = valid ? JsonUtility.FromJson<LevelDocument>(JsonUtility.ToJson(current))
                    : new LevelDocument(new SeedLayout(), new DemoSettings());
                if (!valid) info.text = error + "\n已打开默认谱面。";
            }
            root.SetActive(true);
            UpdateTools();
            RefreshGrid();
        }

        public void Close() => root.SetActive(false);

        int CellCount => Mathf.CeilToInt(draft.Layout.TotalBeats * draft.Layout.Subdivisions);

        void ChangePage(int step)
        {
            page = Mathf.Clamp(page + step, 0, (CellCount - 1) / PageSize);
            RefreshGrid();
        }

        void UpdateTools(bool keepMove = false)
        {
            if (!keepMove && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            toolLabel.text = $"当前种子 {PlantName(plant)}";
            modeLabel.text = delete ? "删除：点击已有种子的格子。"
                : moveMode ? moving == null ? "移动：先选种子，再选空格。"
                    : $"正在移动{PlantName(moving.PlantType)}，请选择空格。"
                : "放置：点击空格。";
        }

        void RefreshGrid()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                GameObject child = grid.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }

            int first = page * PageSize;
            pageLabel.text = $"页码 {page + 1}/{(CellCount + PageSize - 1) / PageSize}";
            for (int col = 0; col < PageSize && first + col < CellCount; col++)
            {
                float beat = (first + col) / (float)draft.Layout.Subdivisions;
                float left = Mathf.Lerp(.08f, 1, col / (float)PageSize);
                float right = Mathf.Lerp(.08f, 1, (col + 1f) / PageSize);
                var header = Label(grid, beat.ToString("0.###", CultureInfo.InvariantCulture), 14);
                header.alignment = TextAlignmentOptions.Center;
                Box(header.rectTransform, left, right, 0, 0, 0, -HeaderHeight);
                for (int lane = 0; lane < 3; lane++)
                {
                    int targetLane = lane;
                    SeedPlacement seed = Find(beat, lane);
                    string caption = seed == null ? "+" : PlantName(seed.PlantType);
                    if (seed != null && preview != null)
                        caption += $"\n障碍 {(runner.ContainsKey(seed.SeedID) ? "有" : "-")} / 弹幕 {(dodge.TryGetValue(seed.SeedID, out var shot) ? (shot.Lane + 1).ToString() : "-")}";
                    if (seed != null && seed == moving) caption = "> " + caption;
                    Button(grid, caption, () => SelectCell(beat, targetLane), left, right, 1,
                        -HeaderHeight - lane * RowHeight, RowHeight - 3, 14);
                }
            }

            for (int lane = 0; lane < 3; lane++)
            {
                var label = Label(grid, lane == 0 ? "左轨" : lane == 1 ? "中轨" : "右轨", 17);
                Box(label.rectTransform, 0, .08f, 0, 0, -HeaderHeight - lane * RowHeight, -RowHeight);
            }
        }

        SeedPlacement Find(float beat, int lane) =>
            draft.Layout.Seeds.Find(seed => Mathf.Abs(seed.BeatPosition - beat) < .0001f && seed.LaneIndex == lane);

        void SelectCell(float beat, int lane)
        {
            var seed = Find(beat, lane);
            if (delete)
            {
                if (seed == null) return;
                draft.Layout.Seeds.Remove(seed);
            }
            else if (moveMode)
            {
                if (seed != null)
                {
                    moving = seed == moving ? null : seed;
                    UpdateTools(true);
                    RefreshGrid();
                    return;
                }
                if (moving == null) return;
                if (!IsPlayable(beat)) return;
                moving.BeatPosition = beat;
                moving.LaneIndex = lane;
                moving = null;
            }
            else
            {
                if (seed != null || !IsPlayable(beat)) return;
                draft.Layout.Seeds.Add(new SeedPlacement(Guid.NewGuid().ToString("N"), beat, lane, plant));
            }

            preview = null;
            info.text = "谱面已修改，点击预览重新生成。";
            UpdateTools(true);
            RefreshGrid();
        }

        bool IsPlayable(float beat)
        {
            if (beat * 60d / draft.Layout.BPM + draft.Layout.OffsetSeconds >= 0) return true;
            info.text = "这个格子早于关卡开始时间。";
            return false;
        }

        void Preview()
        {
            if (!LevelChartGenerator.TryGenerate(draft.Layout, draft.Settings, out preview, out string error))
            {
                info.text = error;
                RefreshGrid();
                return;
            }
            runner.Clear();
            dodge.Clear();
            foreach (ChartEvent e in preview.Runner) runner.Add(e.SeedID, e);
            foreach (ChartEvent e in preview.Dodge) dodge.Add(e.SeedID, e);
            info.text = $"音符 {preview.Music.Count} | 障碍 {preview.Runner.Count} | 弹幕 {preview.Dodge.Count}\n"
                + "障碍栏显示是否生成障碍；弹幕栏显示弹幕所在轨道（1 左，3 右）。";
            if (preview.Adjustments.Count > 0)
                info.text += $"\n已跳过 {preview.Adjustments.Count} 个无法安全安排的事件。 {preview.Adjustments[0]}";
            RefreshGrid();
        }

        void Playtest()
        {
            if (demo.TryStartLevel(draft, out string error)) Close();
            else info.text = error;
        }

        void Save()
        {
            info.text = LevelJson.TrySave(draft, out string error) ? $"已保存至 {LevelJson.SavePath}" : error;
        }

        void Load()
        {
            if (!LevelJson.TryLoad(out var loaded, out string error))
            {
                info.text = error;
                return;
            }
            draft = loaded;
            moving = null;
            page = 0;
            Preview();
            info.text = "已读取保存的谱面。\n" + info.text;
        }

        TextMeshProUGUI Label(Transform parent, string content, int size)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.text = content;
            label.font = font;
            label.fontSize = size;
            label.raycastTarget = false;
            return label;
        }

        void Button(Transform parent, string content, Action action, float left, float right, float inset,
            float y, float height = 34, int size = 17)
        {
            var button = new GameObject(content, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(parent, false);
            Box((RectTransform)button.transform, left, right, inset, -4, y, -height);
            var image = button.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white * .28f + new Color(0, 0, 0, .72f);
            var control = button.GetComponent<UnityEngine.UI.Button>();
            control.targetGraphic = image;
            control.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            control.onClick.AddListener(() => action());
            var label = Label(button.transform, content, size);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9;
            label.fontSizeMax = size;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(2, 0);
            label.rectTransform.offsetMax = new Vector2(-2, 0);
        }

        static void Box(RectTransform rect, float left, float right, float xMin, float xMax, float y, float height)
        {
            rect.anchorMin = new Vector2(left, 1);
            rect.anchorMax = new Vector2(right, 1);
            rect.offsetMin = new Vector2(xMin, y + height);
            rect.offsetMax = new Vector2(xMax, y);
        }
    }
}
