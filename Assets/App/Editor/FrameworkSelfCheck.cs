using System.Collections.Generic;
using System.Linq;
using System.Text;
using App.Resources;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// 工程自检：确认框架必需资源存在、Res 下的预制体没有丢脚本。
    /// 菜单 Tools/Client/Check Project，命令行：
    /// Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod App.EditorTools.FrameworkSelfCheck.Run -logFile &lt;log&gt;
    /// 退出码 0 = 通过，1 = 有问题（详见日志）。
    /// </summary>
    public static class FrameworkSelfCheck
    {
        private static readonly string[] RequiredResPaths =
        {
            "Assets/Res/" + ResResourcePaths.UIRoot + ".prefab",
            "Assets/Res/" + ResResourcePaths.Home + ".prefab",
            "Assets/Res/" + ResResourcePaths.ConfirmDialog + ".prefab",
            "Assets/Res/" + ResResourcePaths.ToastPanel + ".prefab",
            "Assets/Res/" + ResResourcePaths.DemoConfig + ".json",
        };

        [MenuItem("Tools/Client/Check Project")]
        public static void Run()
        {
            var problems = new List<string>();

            CheckRequiredAssets(problems);
            CheckPrefabScripts(problems);

            if (problems.Count == 0)
            {
                Debug.Log("[SelfCheck] PASS: framework assets and prefabs look fine.");
                EditorApplication.Exit(0);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("[SelfCheck] FAIL: " + problems.Count + " problem(s).");
            foreach (var problem in problems)
            {
                builder.AppendLine("  - " + problem);
            }

            Debug.LogError(builder.ToString());
            EditorApplication.Exit(1);
        }

        private static void CheckRequiredAssets(List<string> problems)
        {
            foreach (var path in RequiredResPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                {
                    problems.Add("missing asset: " + path);
                }
            }
        }

        private static void CheckPrefabScripts(List<string> problems)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Res" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    problems.Add("prefab failed to load: " + path);
                    continue;
                }

                var missing = prefab.GetComponentsInChildren<Component>(true).Count(component => component == null);
                if (missing > 0)
                {
                    problems.Add(path + " has " + missing + " missing script reference(s).");
                }
            }
        }
    }
}
