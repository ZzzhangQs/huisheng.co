using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Huisheng.Greybox
{
    [RequireComponent(typeof(Camera))]
    public sealed class GreyboxDemo : MonoBehaviour
    {
        [SerializeField] DemoSettings settings = new();
        [SerializeField] SeedLayout authorChart = new();
        LevelChart chart;
        LevelEditorPanel editor;
        DemoRun run;
        Camera viewCamera;
        GreyboxView view;
        TextMeshProUGUI hud;
        TextMeshProUGUI feedback;
        TMP_FontAsset chineseFont;
        float accumulator;
        bool jump;
        int laneStep;

        public DemoRun CurrentRun => run;
        public LevelChart GeneratedChart => chart;


        void Awake()
        {
            viewCamera = GetComponent<Camera>();
            viewCamera.orthographic = true;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.black;
            viewCamera.transform.position = new Vector3(0, 0, -10);
            var canvas = new GameObject("Status", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            hud = new GameObject("Controls and run status", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            hud.transform.SetParent(canvas.transform, false);
            var source = Resources.Load<Font>("PlantMusic/ChineseFont");
            if (source != null) chineseFont = TMP_FontAsset.CreateFontAsset(source);
            hud.font = chineseFont != null ? chineseFont : hud.font;
            hud.fontSize = 22;
            hud.raycastTarget = false;
            var rect = hud.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(16, -152);
            rect.offsetMax = new Vector2(-16, -12);
            feedback = new GameObject("Run feedback", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            feedback.transform.SetParent(canvas.transform, false);
            feedback.alignment = TextAlignmentOptions.Center;
            feedback.font = hud.font;
            feedback.fontSize = 28;
            feedback.raycastTarget = false;
            feedback.rectTransform.anchorMin = new Vector2(.05f, .3f);
            feedback.rectTransform.anchorMax = new Vector2(.95f, .7f);
            feedback.rectTransform.offsetMin = feedback.rectTransform.offsetMax = Vector2.zero;
            var events = new GameObject("Level editor input", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            editor = new LevelEditorPanel(this, canvas.transform, hud.font);
            if (!TryStartLevel(new LevelDocument(authorChart, settings), out string error))
            {
                hud.text = error + " | Tab 编辑关卡";
            }
        }

        public bool TryStartLevel(LevelDocument document, out string error)
        {
            if (document == null)
            {
                error = "缺少关卡数据。";
                return false;
            }
            var snapshot = JsonUtility.FromJson<LevelDocument>(JsonUtility.ToJson(document));
            if (!LevelChartGenerator.TryGenerate(snapshot.Layout, snapshot.Settings, out var generated, out error))
                return false;

            view?.Dispose();
            authorChart = snapshot.Layout;
            settings = snapshot.Settings;
            chart = generated;
            view = new GreyboxView(transform, chart);
            Restart();
            return true;
        }

        void Restart()
        {
            run = new DemoRun(settings, authorChart, chart);
            accumulator = 0;
            jump = false;
            laneStep = 0;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (editor == null) return;
            if (keyboard != null && (keyboard.tabKey.wasPressedThisFrame || (editor.IsOpen && keyboard.escapeKey.wasPressedThisFrame)))
            {
                if (editor.IsOpen) editor.Close();
                else editor.Open(new LevelDocument(authorChart, settings));
                return;
            }
            if (editor.IsOpen || run == null) return;
            float horizontal = 0;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (!run.Transitioning && !run.Failed && !run.Finished)
                {
                    if (run.Stage == Stage.River)
                        horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                            - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                    if (run.Stage == Stage.Runner) jump |= keyboard.spaceKey.wasPressedThisFrame;
                    if (run.Stage == Stage.Dodge)
                    {
                        int pressed = (keyboard.dKey.wasPressedThisFrame ? 1 : 0) - (keyboard.aKey.wasPressedThisFrame ? 1 : 0);
                        if (pressed != 0) laneStep = pressed;
                    }
                }
            }

            accumulator += Time.deltaTime;
            while (accumulator >= DemoRun.Tick)
            {
                run.Step(DemoRun.Tick, horizontal, jump, laneStep);
                accumulator -= DemoRun.Tick;
                jump = false;
                laneStep = 0;
            }
        }

        void LateUpdate()
        {
            if (editor == null || editor.IsOpen || run == null) return;
            viewCamera.orthographicSize = Mathf.Max(5.5f, 6.8f / viewCamera.aspect);
            bool ended = run.Failed || run.Finished;
            view.SetVisible(!ended);
            if (!ended) view.Draw(run, viewCamera.orthographicSize * viewCamera.aspect);
            hud.enabled = !ended;
            feedback.text = ended
                ? $"<size=48>{(run.Finished ? "通关成功" : "挑战失败")}</size>\n\n"
                    + $"得分 {run.Score}   |   剩余生命 {run.Life}   |   激活种子 {run.Performance.Count}/{run.Seeds.Length}\n\n"
                    + "按 R 从第一关重玩     按 Tab 编辑关卡"
                : run.Stage == Stage.Runner && !run.Transitioning && run.RecentHit
                    ? "撞到障碍，生命 −1\n继续前进，遇到下一个障碍时按空格跳跃。"
                    : "";
            if (ended) return;

            string stage = run.Stage switch
            {
                Stage.River => "第一关 · 三河道擦边",
                Stage.Runner => "第二关 · 横版跑酷",
                _ => "第三关 · 三轨闪避"
            };
            string controls = run.Transitioning ? "即将进入下一关…" : run.Stage switch
            {
                Stage.River => "按 A / D 或左右方向键移动，擦过种子外缘。",
                Stage.Runner => "在障碍到达前按空格跳跃。",
                _ => "按 A / D 换轨，躲开迎面而来的方块。"
            };
            string result = run.LastResult switch
            {
                Outcome.Perfect => "完美",
                Outcome.Good => "普通",
                Outcome.Hit => "碰撞",
                Outcome.Miss => "错过",
                Outcome.Avoided => "成功躲开",
                _ => "—"
            };
            hud.text = $"<size=30>剩余生命 {run.Life} / {settings.life}</size>\n"
                + $"{stage}  |  得分 {run.Score}  |  {run.Clock:0.0} 秒  |  第 {run.Rhythm.Beat:0.00} 拍\n"
                + $"{controls}  按 R 重玩，按 Tab 编辑关卡。\n"
                + $"判定 {result}  |  激活种子 {run.Performance.Count}/{run.Seeds.Length}";
        }

        void OnDestroy()
        {
            view?.Dispose();
            if (chineseFont != null)
            {
                Destroy(chineseFont.material);
                foreach (var atlas in chineseFont.atlasTextures) Destroy(atlas);
                Destroy(chineseFont);
            }
        }
    }
}
