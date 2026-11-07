using System;
using System.IO;
using System.Linq;
using Framework.Assets.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace App.Build.Editor
{
    /// <summary>
    /// 桌面（Windows 64 位）一键打包：方便在 PC 上快速验证「真机同款」的资源加载路径
    /// （走 AssetBundle 而不是 Editor 直读 Assets/Res）。
    /// 命令行：Unity.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod App.Build.Editor.DesktopBuildMenu.BuildWindows64WithBundles
    /// </summary>
    public static class DesktopBuildMenu
    {
        private const string MainScene = "Assets/Scenes/Main.unity";
        private const string OutputFolder = "Builds/Windows";

        [MenuItem("Build/Desktop/Build Windows64", false, 100)]
        public static void BuildWindows64()
        {
            BuildWindows64(rebuildBundles: false);
        }

        [MenuItem("Build/Desktop/Build Windows64 (Rebuild Bundles)", false, 101)]
        public static void BuildWindows64WithBundles()
        {
            BuildWindows64(rebuildBundles: true);
        }

        [MenuItem("Build/Desktop/Open Output Folder", false, 200)]
        public static void OpenOutputFolder()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        /// <summary>构建 Windows 64 位播放器；rebuildBundles=true 时先重建 StreamingAssets/Bundles。</summary>
        public static void BuildWindows64(bool rebuildBundles)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Build Windows64", "准备场景…", 0.1f);

                var scenes = EnsureScenes();
                if (scenes.Length == 0)
                {
                    return;
                }

                if (rebuildBundles)
                {
                    EditorUtility.DisplayProgressBar("Build Windows64", "重建 AssetBundles…", 0.35f);
                    ResAssetBundleBuilder.RebuildCurrentVersion(BuildTarget.StandaloneWindows64);
                }
                else if (!ResAssetBundleBuilder.StreamingBundlesMatch(BuildTarget.StandaloneWindows64))
                {
                    Debug.LogWarning(
                        "[DesktopBuild] StreamingAssets/Bundles 不是 Windows 平台的产物（或缺 platform.txt），" +
                        "已自动按 StandaloneWindows64 重建。");
                    EditorUtility.DisplayProgressBar("Build Windows64", "重建 AssetBundles…", 0.35f);
                    ResAssetBundleBuilder.RebuildCurrentVersion(BuildTarget.StandaloneWindows64);
                }

                var outputPath = PrepareOutputPath();
                EditorUtility.DisplayProgressBar("Build Windows64", $"输出: {outputPath}", 0.6f);

                ApplyDesktopPlayerSettings();

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });

                LogReport(report, outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DesktopBuild] 打包异常: {ex}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 桌面播放器按竖屏窗口打开（540×960），方便直接看效果；
        /// 这些是 Standalone 的平台设置，不影响 Android/WebGL。
        /// </summary>
        private static void ApplyDesktopPlayerSettings()
        {
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            // 关掉“用显示器原生分辨率”，否则播放器会直接铺满 4K 屏（横屏），竖屏游戏会被看穿整张地图
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        private static string[] EnsureScenes()
        {
            var enabled = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
            if (enabled.Length > 0)
            {
                return enabled;
            }

            if (!File.Exists(MainScene))
            {
                Debug.LogError($"[DesktopBuild] Build Settings 无场景，且找不到 {MainScene}。");
                return Array.Empty<string>();
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            Debug.LogWarning($"[DesktopBuild] Build Settings 无场景，已自动加入: {MainScene}");
            return new[] { MainScene };
        }

        private static bool HasStreamingBundles()
        {
            var dir = ResAssetBundleBuilder.GetStreamingBundlesFullPath();
            return Directory.Exists(dir) && Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length > 0;
        }

        private static string PrepareOutputPath()
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
            Directory.CreateDirectory(root);

            var safeName = PlayerSettings.productName;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Client";
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(root, $"{safeName}_{PlayerSettings.bundleVersion}_{stamp}");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, safeName + ".exe");
        }

        private static void LogReport(BuildReport report, string outputPath)
        {
            var summary = report.summary;
            var sizeMb = summary.totalSize / (1024f * 1024f);
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[DesktopBuild] SUCCESS: {outputPath} | size={sizeMb:F1}MB " +
                    $"| warnings={summary.totalWarnings} errors={summary.totalErrors} " +
                    $"| {summary.totalTime.TotalSeconds:F1}s");
                return;
            }

            Debug.LogError(
                $"[DesktopBuild] FAILED: {summary.result} | errors={summary.totalErrors} " +
                $"| {summary.totalTime.TotalSeconds:F1}s");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
