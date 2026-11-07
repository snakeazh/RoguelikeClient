using System.Collections.Generic;
using System.IO;
using App.Bootstrap;
using App.Net;
using App.Resources;
using App.UI.Home;
using Framework.UI.Binding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace App.EditorTools
{
    /// <summary>
    /// 一键生成 Demo 资源：Res/UI/Home.prefab + Scenes/Main.unity。
    /// 菜单 Tools/Client/Rebuild Demo Assets，命令行：
    /// Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod App.EditorTools.DemoPrefabBuilder.RebuildAll
    /// 手工改过 prefab 后就不要再跑，避免覆盖。
    /// </summary>
    public static class DemoPrefabBuilder
    {
        private const string HomePrefabPath = "Assets/Res/UI/Home.prefab";
        private const string BattleHudPrefabPath = "Assets/Res/UI/Battle/BattleHud.prefab";
        private const string LevelUpPrefabPath = "Assets/Res/UI/Battle/LevelUp.prefab";
        private const string BattleResultPrefabPath = "Assets/Res/UI/Battle/BattleResult.prefab";
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        // Demo 用动态字体：源字体同 GameFont，但图集运行时按需扩字，避免静态子集缺字。
        private const string DemoFontPath = "Assets/Res/Fonts/DemoFont SDF.asset";
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/GameFont.otf";
        private const int FontSamplingSize = 80;
        private const int FontAtlasPadding = 6;
        private const int FontAtlasWidth = 2048;
        private const int FontAtlasHeight = 2048;

        [MenuItem("Tools/Client/Rebuild Demo Assets")]
        public static void RebuildAll()
        {
            EnsureFolder("Assets/Res/Config");
            EnsureFolder("Assets/Res/UI/Battle");
            var font = EnsureDemoFont();
            BuildHomePrefab();
            BuildBattlePrefabs(font);
            SetDefaultTmpFont(font);
            RepointTmpFonts(font);
            BuildMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DemoPrefabBuilder] Home + battle prefabs + Main.unity rebuilt.");
        }

        /// <summary>
        /// 把框架自带预制体（UIRoot / ConfirmDialog / ToastPanel）里指向静态 GameFont SDF 的 TMP 引用
        /// 换成动态 DemoFont，避免改文案时缺字。其它字体（美术特殊字体）不动。
        /// </summary>
        private static void RepointTmpFonts(TMP_FontAsset demoFont)
        {
            var prefabPaths = new[]
            {
                "Assets/Res/UI/UIRoot.prefab",
                "Assets/Res/UI/ConfirmDialog.prefab",
                "Assets/Res/UI/Popup/ToastPanel.prefab",
            };

            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                var changed = 0;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (IsStaticGameFont(text.font))
                    {
                        text.font = demoFont;
                        changed++;
                    }
                }

                foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
                {
                    if (IsStaticGameFont(input.fontAsset))
                    {
                        input.fontAsset = demoFont;
                        changed++;
                    }
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[DemoPrefabBuilder] {path}: repointed {changed} TMP reference(s) to dynamic font.");
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsStaticGameFont(TMP_FontAsset font)
        {
            return font != null && font.name.StartsWith("GameFont SDF");
        }

        /// <summary>
        /// TMP Settings 的默认字体原本指向静态 GameFont SDF（字符集烘焙死在 Lang-zh.txt 里），
        /// 这里改为动态字体，新建的 TMP 文本就不会因为缺字变空格。
        /// </summary>
        private static void SetDefaultTmpFont(TMP_FontAsset demoFont)
        {
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[DemoPrefabBuilder] TMP Settings not found: " + settingsPath);
                return;
            }

            var so = new SerializedObject(settings);
            var property = so.FindProperty("m_defaultFontAsset");
            if (property == null)
            {
                Debug.LogWarning("[DemoPrefabBuilder] TMP Settings has no m_defaultFontAsset field.");
                return;
            }

            if (property.objectReferenceValue == demoFont)
            {
                return;
            }

            property.objectReferenceValue = demoFont;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[DemoPrefabBuilder] TMP default font set to " + demoFont.name + ".");
        }

        private static void BuildHomePrefab()
        {
            var font = EnsureDemoFont();

            var root = new GameObject("Home", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            root.AddComponent<HomeView>();
            var reference = root.AddComponent<UIReference>();

            CreateText(rootRect, font, "Title", "Roguelike Client", 56, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 80f), Color.white);

            CreateText(rootRect, font, "Subtitle", "Framework 演示工程 · 不含业务逻辑", 26, FontStyles.Normal,
                new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1100f, 50f),
                new Color(0.75f, 0.78f, 0.85f));

            CreateText(rootRect, font, "Content", "配置表加载中…", 24, FontStyles.Normal,
                new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(900f, 300f), Color.white,
                TextAlignmentOptions.TopLeft);

            CreateText(rootRect, font, "Status", "点击按钮开始体验", 24, FontStyles.Normal,
                new Vector2(0.5f, 0f), new Vector2(0f, 420f), new Vector2(1100f, 50f),
                new Color(1f, 0.85f, 0.4f));

            CreateButton(rootRect, font, "BtnBattle", "开始战斗", new Vector2(0f, 260f), new Vector2(420f, 92f));
            CreateButton(rootRect, font, "BtnDialog", "确认对话框", new Vector2(-260f, 110f));
            CreateButton(rootRect, font, "BtnToast", "Toast 提示", new Vector2(0f, 110f));
            CreateButton(rootRect, font, "BtnClick", "点击音效", new Vector2(260f, 110f));

            reference.Collect();

            ApplyUiLayer(root);
            PrefabUtility.SaveAsPrefabAsset(root, HomePrefabPath);
            Object.DestroyImmediate(root);

            // 保存后重新打开一次，确保 UIReference 的收集结果写进了 prefab。
            var saved = PrefabUtility.LoadPrefabContents(HomePrefabPath);
            saved.GetComponent<UIReference>().Collect();
            PrefabUtility.SaveAsPrefabAsset(saved, HomePrefabPath);
            PrefabUtility.UnloadPrefabContents(saved);
        }

        /// <summary>
        /// 战斗三件套：HUD（Hud 层常驻）、升级三选一（Popup）、结算（Popup）。
        /// 全部是纯色 UGUI 占位，正式美术替换 prefab 即可，View 里的键名保持不变。
        /// </summary>
        private static void BuildBattlePrefabs(TMP_FontAsset font)
        {
            BuildBattleHud(font);
            BuildLevelUp(font);
            BuildBattleResult(font);
        }

        private static void BuildBattleHud(TMP_FontAsset font)
        {
            var root = new GameObject("BattleHud", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            root.AddComponent<App.UI.Battle.BattleHudView>();
            var reference = root.AddComponent<UIReference>();

            CreateSlider(rootRect, "ExpBar", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1200f, 22f),
                new Color(0.35f, 0.85f, 0.45f));

            CreateText(rootRect, font, "Timer", "05:00", 44, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(400f, 60f), Color.white);

            CreateText(rootRect, font, "Level", "Lv.1", 30, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(46f, -130f), new Vector2(400f, 40f), Color.white,
                TextAlignmentOptions.Left);

            CreateText(rootRect, font, "Kills", "击杀 0", 30, FontStyles.Normal,
                new Vector2(1f, 1f), new Vector2(-46f, -130f), new Vector2(400f, 40f), Color.white,
                TextAlignmentOptions.Right);

            CreateText(rootRect, font, "BossHint", string.Empty, 32, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1000f, 50f),
                new Color(1f, 0.4f, 0.35f));

            CreateText(rootRect, font, "HpText", "100 / 100", 26, FontStyles.Normal,
                new Vector2(0.5f, 0f), new Vector2(0f, 476f), new Vector2(700f, 40f), Color.white);

            CreateSlider(rootRect, "HpBar", new Vector2(0.5f, 0f), new Vector2(0f, 430f), new Vector2(640f, 32f),
                new Color(0.9f, 0.32f, 0.32f));

            CreateText(rootRect, font, "Weapons", "武器", 22, FontStyles.Normal,
                new Vector2(0f, 0f), new Vector2(46f, 250f), new Vector2(700f, 34f),
                new Color(0.8f, 0.85f, 1f), TextAlignmentOptions.Left);

            CreateText(rootRect, font, "Passives", "被动", 22, FontStyles.Normal,
                new Vector2(0f, 0f), new Vector2(46f, 208f), new Vector2(700f, 34f),
                new Color(0.8f, 0.85f, 1f), TextAlignmentOptions.Left);

            // 冲刺按钮：右下角固定大圆钮（占位用圆形贴图不方便，先用方块）。
            CreateButtonAt(rootRect, font, "BtnDash", "冲刺", new Vector2(1f, 0f), new Vector2(-70f, 120f),
                new Vector2(260f, 260f), 40);

            reference.Collect();

            ApplyUiLayer(root);

            PrefabUtility.SaveAsPrefabAsset(root, BattleHudPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildLevelUp(TMP_FontAsset font)
        {
            var root = new GameObject("LevelUp", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            root.AddComponent<App.UI.Battle.LevelUpView>();
            var reference = root.AddComponent<UIReference>();

            // 半透明遮罩，同时挡住底下 HUD 的点击。
            var dim = CreateImage(rootRect, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.72f), true);
            Stretch((RectTransform)dim.transform);

            CreateText(rootRect, font, "Header", "升级！选择一项强化", 40, FontStyles.Bold,
                new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1000f, 60f), Color.white);

            var xs = new[] { -360f, 0f, 360f };
            for (var i = 0; i < 3; i++)
            {
                var index = i + 1;
                var card = CreateButtonAt(rootRect, font, "BtnOption" + index, string.Empty,
                    new Vector2(0.5f, 0.5f), new Vector2(xs[i], 60f), new Vector2(320f, 620f));
                var rect = (RectTransform)card.transform;

                CreateText(rect, font, "Opt" + index + "Title", "选项 " + index, 30, FontStyles.Bold,
                    new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(280f, 90f), Color.white);

                CreateText(rect, font, "Opt" + index + "Desc", "描述", 22, FontStyles.Normal,
                    new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(276f, 420f),
                    new Color(0.82f, 0.86f, 0.95f), TextAlignmentOptions.TopLeft);
            }

            reference.Collect();

            ApplyUiLayer(root);

            PrefabUtility.SaveAsPrefabAsset(root, LevelUpPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildBattleResult(TMP_FontAsset font)
        {
            var root = new GameObject("BattleResult", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            root.AddComponent<App.UI.Battle.BattleResultView>();
            var reference = root.AddComponent<UIReference>();

            CreateImage(rootRect, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f),
                new Color(0.12f, 0.14f, 0.2f, 0.96f), true);

            CreateText(rootRect, font, "Title", "战斗结束", 64, FontStyles.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(800f, 90f), Color.white);

            CreateText(rootRect, font, "Subtitle", string.Empty, 30, FontStyles.Normal,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(800f, 50f),
                new Color(0.8f, 0.85f, 0.95f));

            CreateText(rootRect, font, "Stats", string.Empty, 26, FontStyles.Normal,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(720f, 420f), Color.white,
                TextAlignmentOptions.TopLeft);

            CreateButtonAt(rootRect, font, "BtnReturn", "返回首页", new Vector2(0.5f, 0.5f), new Vector2(0f, -290f),
                new Vector2(420f, 96f));

            reference.Collect();

            ApplyUiLayer(root);

            PrefabUtility.SaveAsPrefabAsset(root, BattleResultPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            camera.orthographic = true;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var bootstrapGo = new GameObject("Bootstrap");
            var bootstrap = bootstrapGo.AddComponent<AppBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("enableServerConnection").boolValue = false;
            so.FindProperty("serverEndpoint").enumValueIndex = (int)GameApiServerEndpoint.LocalHost;
            so.FindProperty("customServerHost").stringValue = "127.0.0.1";
            so.FindProperty("customServerPort").intValue = GameApiSettings.DefaultPort;
            so.FindProperty("targetFrameRate").intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == MainScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// 生成（或复用）动态 TMP 字体。动态图集在运行时按需加字，Demo 文案改字不会缺字；
        /// 正式项目建议按发布字符集烘焙静态字体，控制内存与包体。
        /// </summary>
        private static TMP_FontAsset EnsureDemoFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DemoFontPath);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                throw new FileNotFoundException("Source font not found: " + SourceFontPath);
            }

            EnsureFolder("Assets/Res/Fonts");

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                FontSamplingSize,
                FontAtlasPadding,
                GlyphRenderMode.SDFAA,
                FontAtlasWidth,
                FontAtlasHeight,
                AtlasPopulationMode.Dynamic,
                true);
            fontAsset.name = "DemoFont SDF";

            AssetDatabase.CreateAsset(fontAsset, DemoFontPath);

            // 图集与材质作为子资产保存，否则重新导入后引用会丢。
            if (fontAsset.atlasTextures != null)
            {
                for (var i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var texture = fontAsset.atlasTextures[i];
                    if (texture == null)
                    {
                        continue;
                    }

                    texture.name = fontAsset.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(DemoFontPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[DemoPrefabBuilder] Dynamic TMP font created: " + DemoFontPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DemoFontPath);
        }

        private static void CreateText(
            RectTransform parent,
            TMP_FontAsset font,
            string name,
            string content,
            float size,
            FontStyles style,
            Vector2 anchor,
            Vector2 position,
            Vector2 dimensions,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;

            AddBind(go, text);
        }

        private static void CreateButton(RectTransform parent, TMP_FontAsset font, string name, string label, Vector2 position)
        {
            CreateButton(parent, font, name, label, position, new Vector2(230f, 76f));
        }

        private static void CreateButton(
            RectTransform parent,
            TMP_FontAsset font,
            string name,
            string label,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.36f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            Stretch(labelRect);

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = label;
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            AddBind(go, button);
        }

        /// <summary>
        /// UIRoot 下的每层都有自己的 Canvas，UI 相机只渲染 UI 层（culling mask = UI）。
        /// 新建节点默认在 Default 层，不改层的话画面里什么都看不到（逻辑却一切正常）。
        /// </summary>
        private static void ApplyUiLayer(GameObject root)
        {
            var layer = LayerMask.NameToLayer("UI");
            if (layer < 0)
            {
                Debug.LogWarning("[DemoPrefabBuilder] Layer 'UI' not found, UI may not render.");
                return;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        private static Button CreateButtonAt(
            RectTransform parent,
            TMP_FontAsset font,
            string name,
            string label,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            float fontSize = 26f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.45f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            Stretch(labelRect);

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = label;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            AddBind(go, button);
            return button;
        }

        private static Image CreateImage(
            RectTransform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            Color color,
            bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>占位进度条：背景 Image + Fill 子节点，Slider 只做数值展示（不可交互）。</summary>
        private static Slider CreateSlider(
            RectTransform parent,
            string name,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var background = go.GetComponent<Image>();
            background.color = new Color(0.1f, 0.12f, 0.18f);
            background.raycastTarget = false;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            var fillArea = (RectTransform)fillAreaGo.transform;
            fillArea.SetParent(rect, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(fillArea, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;

            var slider = go.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fillRect;
            slider.handleRect = null;
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;

            AddBind(go, slider);
            return slider;
        }

        private static void AddBind(GameObject go, Object target)
        {
            var bind = go.AddComponent<UIBind>();
            var so = new SerializedObject(bind);
            so.FindProperty("_key").stringValue = go.name;
            so.FindProperty("_target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
