using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

public class TextureFormatTool : EditorWindow {
    // 拖拽相关变量
    private List<string> draggedPaths = new List<string>();
    private bool includeSubfolders = true;

    // Format 设置
    private readonly TextureImporterFormat[] androidFormats = new TextureImporterFormat[] {
        TextureImporterFormat.ASTC_12x12,
        TextureImporterFormat.ASTC_10x10,
        TextureImporterFormat.ASTC_8x8,
        TextureImporterFormat.ASTC_6x6,
        TextureImporterFormat.ASTC_5x5,
        TextureImporterFormat.ASTC_4x4,
    };

    private string[] formatNames;
    private int crunchOnFormatIndex = 0;  // 默认 ASTC_12x12
    private int crunchOffFormatIndex = 5; // 默认 ASTC_4x4

    // 添加滚动和折叠相关变量
    private Vector2 selectedFilesScrollPos;
    private bool showSelectedFiles = true;
    private const float MAX_LIST_HEIGHT = 150f; // 最大显示高度

    // 添加 Compression Quality 设置
    private readonly string[] compressionQualities = { "Fast", "Normal", "Best" };
    private int compressionQualityIndex = 2; // 默认 Best

    private bool isCancelled = false;  // 取消处理的标志

    [MenuItem("Tools/HW/工具/图片格式批量转换", false, 1)]
    static void Init() {
        TextureFormatTool window = (TextureFormatTool)EditorWindow.GetWindow(typeof(TextureFormatTool));
        window.titleContent = new GUIContent("图片格式批量转换");
        window.Show();
    }

    void OnEnable() {
        InitializeFormatNames();
        LoadSettings();
        LoadDraggedPaths(); // 加载上次的文件列表
    }

    private void InitializeFormatNames() {
        formatNames = androidFormats.Select(f => {
            return $"RGB(A) Compressed ASTC {f.ToString().Substring(5)} block";
        }).ToArray();
    }

    void OnGUI() {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope()) {
            // 拖拽区域
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽文件夹或图片到这里\n支持多选");
            HandleDragAndDrop(dropArea);

            // 显示当前选择
            if (draggedPaths.Count > 0) {
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope()) {
                    showSelectedFiles = EditorGUILayout.Foldout(showSelectedFiles,
                        $"当前选择: {draggedPaths.Count} 个文件", true);

                    if (GUILayout.Button("清除选择", GUILayout.Width(100))) {
                        draggedPaths.Clear();
                    }
                }

                if (showSelectedFiles) {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                        selectedFilesScrollPos = EditorGUILayout.BeginScrollView(
                            selectedFilesScrollPos,
                            GUILayout.MaxHeight(MAX_LIST_HEIGHT)
                        );

                        // 使用临时列表来避免在遍历时修改集合
                        List<string> pathsToRemove = new List<string>();

                        foreach (string path in draggedPaths) {
                            using (new EditorGUILayout.HorizontalScope()) {
                                // 添加一个小图标作为提示
                                GUIContent content = EditorGUIUtility.IconContent("d_ViewToolZoom");  // 使用放大镜图标
                                content.text = Path.GetFileName(path);
                                content.tooltip = "点击定位到资源";

                                // 检测鼠标是否悬停在按钮上
                                Rect buttonRect = GUILayoutUtility.GetRect(content, EditorStyles.label, GUILayout.ExpandWidth(true));
                                bool isHover = buttonRect.Contains(Event.current.mousePosition);

                                // 使用自定义样式
                                GUIStyle style = new GUIStyle(EditorStyles.label);
                                if (isHover) {
                                    style.normal.textColor = Color.blue; // 鼠标悬停时变蓝
                                    EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link); // 改变鼠标样式
                                }

                                if (GUI.Button(buttonRect, content, style)) {
                                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                                    if (obj != null) {
                                        Selection.activeObject = obj;
                                        EditorGUIUtility.PingObject(obj);
                                    }
                                }

                                // 添加删除按钮
                                if (GUILayout.Button("×", GUILayout.Width(20))) {
                                    pathsToRemove.Add(path);
                                }
                            }
                        }

                        // 移除被标记的路径
                        foreach (string path in pathsToRemove) {
                            draggedPaths.Remove(path);
                        }

                        EditorGUILayout.EndScrollView();
                    }
                }
            }

            includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);

            EditorGUILayout.Space(10);

            // Format 设置
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("Android Format Settings", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "将根据Default中的Use Crunch Compression设置选择对应格式:\n" +
                    "- 开启时使用: Use Crunch On Format\n" +
                    "- 关闭时使用: Use Crunch Off Format\n" +
                    "MaxSize和Resize Algorithm将继承Default设置",
                    MessageType.Info
                );

                crunchOnFormatIndex = EditorGUILayout.Popup(
                    "Use Crunch On Format",
                    crunchOnFormatIndex,
                    formatNames
                );

                crunchOffFormatIndex = EditorGUILayout.Popup(
                    "Use Crunch Off Format",
                    crunchOffFormatIndex,
                    formatNames
                );

                // 添加 Compression Quality 设置
                compressionQualityIndex = EditorGUILayout.Popup(
                    "Compression Quality",
                    compressionQualityIndex,
                    compressionQualities
                );
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledGroupScope(draggedPaths.Count == 0)) {
                if (GUILayout.Button("应用设置")) {
                    ProcessTextures();
                }
            }
        }
    }

    private void HandleDragAndDrop(Rect dropArea) {
        Event evt = Event.current;
        switch (evt.type) {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform) {
                    DragAndDrop.AcceptDrag();
                    HashSet<string> uniquePaths = new HashSet<string>(draggedPaths); // 使用HashSet避免重复

                    foreach (string path in DragAndDrop.paths) {
                        string assetPath = path;
                        if (path.StartsWith(Application.dataPath)) {
                            assetPath = "Assets" + path.Substring(Application.dataPath.Length);
                        }

                        if (Directory.Exists(path)) {
                            var texturePaths = GetTextureFilesInFolder(path).Select(p =>
                                p.StartsWith(Application.dataPath) ?
                                    "Assets" + p.Substring(Application.dataPath.Length) : p
                            );
                            foreach (var texturePath in texturePaths) {
                                uniquePaths.Add(texturePath);
                            }
                        }
                        else if (IsImageFile(path)) {
                            uniquePaths.Add(assetPath);
                        }
                    }

                    draggedPaths = uniquePaths.ToList(); // 更新draggedPaths
                    SaveDraggedPaths(); // 保存更新后的列表
                }
                Event.current.Use();
                break;
        }
    }

    private List<string> GetTextureFilesInFolder(string folderPath) {
        string searchPath = folderPath;
        if (folderPath.StartsWith("Assets")) {
            searchPath = Path.Combine(Application.dataPath, folderPath.Substring(7));
        }

        return Directory.GetFiles(
            searchPath,
            "*.*",
            includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
        ).Where(file => IsImageFile(file)).ToList();
    }

    private bool IsImageFile(string path) {
        string ext = Path.GetExtension(path).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
    }

    private void SaveSettings() {
        EditorPrefs.SetInt("TextureFormatTool_CrunchOnFormat", crunchOnFormatIndex);
        EditorPrefs.SetInt("TextureFormatTool_CrunchOffFormat", crunchOffFormatIndex);
        EditorPrefs.SetInt("TextureFormatTool_CompressionQuality", compressionQualityIndex); // 保存压缩质量设置
    }

    private void LoadSettings() {
        crunchOnFormatIndex = EditorPrefs.GetInt("TextureFormatTool_CrunchOnFormat", 0);
        crunchOffFormatIndex = EditorPrefs.GetInt("TextureFormatTool_CrunchOffFormat", 5);
        compressionQualityIndex = EditorPrefs.GetInt("TextureFormatTool_CompressionQuality", 2); // 加载压缩质量设置

        // 确保索引在有效范围内
        crunchOnFormatIndex = Mathf.Clamp(crunchOnFormatIndex, 0, androidFormats.Length - 1);
        crunchOffFormatIndex = Mathf.Clamp(crunchOffFormatIndex, 0, androidFormats.Length - 1);
        compressionQualityIndex = Mathf.Clamp(compressionQualityIndex, 0, compressionQualities.Length - 1);
    }

    void OnDisable() {
        SaveSettings();
        SaveDraggedPaths(); // 保存当前的文件列表
    }

    private void ProcessTextures() {
        if (draggedPaths.Count == 0) {
            EditorUtility.DisplayDialog("错误", "没有找到需要处理的图片!", "确定");
            return;
        }

        int total = draggedPaths.Count;
        int current = 0;
        int successCount = 0;
        isCancelled = false;

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        AssetDatabase.StartAssetEditing();
        try {
            foreach (string texturePath in draggedPaths) {
                float elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000f;
                string timeInfo = $"已用时: {elapsedSeconds:F1}s";

                // 计算预估剩余时间
                float averageTimePerItem = current > 0 ? elapsedSeconds / current : 0;
                float estimatedTimeRemaining = (total - current) * averageTimePerItem;
                string remainingInfo = current > 0 ? $" | 预计剩余: {estimatedTimeRemaining:F1}s" : "";

                // 显示进度并检查取消
                bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                    "处理中",
                    $"正在处理: {Path.GetFileName(texturePath)} ({current + 1}/{total})\n{timeInfo}{remainingInfo}",
                    (float)current / total
                );

                if (cancelled) {
                    isCancelled = true;
                    break;
                }

                if (ProcessSingleTexture(texturePath)) {
                    successCount++;
                }
                current++;
            }
        }
        finally {
            stopwatch.Stop();
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        float totalTime = stopwatch.ElapsedMilliseconds / 1000f;
        string message = isCancelled ?
            $"处理已取消!\n" +
            $"已处理: {current} / {total}\n" +
            $"已用时: {totalTime:F1}秒" :
            $"处理完成!\n" +
            $"总用时: {totalTime:F1}秒\n" +
            $"成功处理: {successCount} 个文件\n" +
            $"失败: {(current - successCount)} 个文件\n" +
            $"平均每个文件: {(totalTime / current):F3}秒";

        EditorUtility.DisplayDialog(
            isCancelled ? "已取消" : "完成",
            message,
            "确定"
        );
    }

    private bool ProcessSingleTexture(string texturePath) {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null) return false;

        try {
            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings();

            // 基本设置
            settings.name = "Android";
            settings.overridden = true;

            // 从 Default 继承 MaxSize 和 Resize Algorithm
            settings.maxTextureSize = defaultSettings.maxTextureSize;
            settings.resizeAlgorithm = defaultSettings.resizeAlgorithm;

            // 根据 Default 的 Crunch Compression 设置选择格式
            settings.format = defaultSettings.crunchedCompression ?
                androidFormats[crunchOnFormatIndex] :
                androidFormats[crunchOffFormatIndex];

            // 设置压缩质量
            settings.compressionQuality = compressionQualityIndex switch {
                0 => 0,    // Fast
                1 => 50,   // Normal
                _ => 100   // Best
            };

            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            return true;
        }
        catch (System.Exception e) {
            Debug.LogError($"处理文件 {texturePath} 时出错: {e.Message}");
            return false;
        }
    }

    private void SaveDraggedPaths() {
        // 保存文件数量
        EditorPrefs.SetInt("TextureFormatTool_PathCount", draggedPaths.Count);

        // 保存每个文件路径
        for (int i = 0; i < draggedPaths.Count; i++) {
            EditorPrefs.SetString($"TextureFormatTool_Path_{i}", draggedPaths[i]);
        }
    }

    private void LoadDraggedPaths() {
        draggedPaths.Clear();

        // 获取保存的文件数量
        int count = EditorPrefs.GetInt("TextureFormatTool_PathCount", 0);

        // 加载每个文件路径
        for (int i = 0; i < count; i++) {
            string path = EditorPrefs.GetString($"TextureFormatTool_Path_{i}", "");
            // 验证路径是否有效
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                draggedPaths.Add(path);
            }
        }
    }
}