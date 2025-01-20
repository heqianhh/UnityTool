using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ObjectFinderWindow : EditorWindow {
    private enum SearchMode {
        Layer,
        Tag
    }

    private SearchMode currentMode = SearchMode.Layer;
    private int selectedLayerIndex = 0;
    private int selectedTagIndex = 0;
    private Vector2 scrollPosition;
    private List<GameObject> foundObjects = new List<GameObject>();
    private string[] layerNames;
    private string[] tagNames;

    [MenuItem("Tools/HW/工具/Hierarchy物体查找")]
    public static void ShowWindow() {
        GetWindow<ObjectFinderWindow>("Object Finder");
    }

    private void OnEnable() {
        // 获取所有可用的层名称
        List<string> layers = new List<string>();
        for (int i = 0; i < 32; i++) {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName)) {
                layers.Add(layerName);
            }
        }
        layerNames = layers.ToArray();

        // 获取所有可用的标签名称
        tagNames = UnityEditorInternal.InternalEditorUtility.tags;
    }

    private void OnGUI() {
        GUILayout.Space(10);

        // 模式选择标签页
        currentMode = (SearchMode)GUILayout.Toolbar((int)currentMode, new string[] { "Layer", "Tag" });

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        if (currentMode == SearchMode.Layer) {
            DrawLayerSearch();
        }
        else {
            DrawTagSearch();
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // 显示当前时间，方便查看最后刷新时间
        EditorGUILayout.LabelField($"Last Refresh: {System.DateTime.Now.ToString("HH:mm:ss")}", EditorStyles.miniLabel);
        GUILayout.Space(5);

        DrawObjectList();
    }

    private void DrawLayerSearch() {
        GUILayout.Label("Select Layer:", GUILayout.Width(80));
        int newSelectedLayer = EditorGUILayout.Popup(selectedLayerIndex, layerNames);

        if (GUILayout.Button("Refresh", GUILayout.Width(60))) {
            FindObjectsInLayer();
        }

        if (newSelectedLayer != selectedLayerIndex) {
            selectedLayerIndex = newSelectedLayer;
            FindObjectsInLayer();
        }
    }

    private void DrawTagSearch() {
        GUILayout.Label("Select Tag:", GUILayout.Width(80));
        int newSelectedTag = EditorGUILayout.Popup(selectedTagIndex, tagNames);

        if (GUILayout.Button("Refresh", GUILayout.Width(60))) {
            FindObjectsWithTag();
        }

        if (newSelectedTag != selectedTagIndex) {
            selectedTagIndex = newSelectedTag;
            FindObjectsWithTag();
        }
    }

    private void DrawObjectList() {
        if (foundObjects.Count == 0) {
            string searchType = currentMode == SearchMode.Layer ? "layer" : "tag";
            string searchName = currentMode == SearchMode.Layer ? layerNames[selectedLayerIndex] : tagNames[selectedTagIndex];
            EditorGUILayout.HelpBox($"No objects found with {searchType} '{searchName}'.", MessageType.Info);
            return;
        }

        string typeStr = currentMode == SearchMode.Layer ? "layer" : "tag";
        string nameStr = currentMode == SearchMode.Layer ? layerNames[selectedLayerIndex] : tagNames[selectedTagIndex];
        EditorGUILayout.LabelField($"Found {foundObjects.Count} objects with {typeStr} '{nameStr}'");
        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (var obj in foundObjects) {
            if (obj != null) {
                EditorGUILayout.BeginHorizontal("box");

                // 物体名称和路径
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(obj.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(GetGameObjectPath(obj), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // Ping按钮 - 在Hierarchy中高亮显示
                if (GUILayout.Button("Ping", GUILayout.Width(50))) {
                    EditorGUIUtility.PingObject(obj);
                }

                // Select按钮 - 选中物体
                if (GUILayout.Button("Select", GUILayout.Width(50))) {
                    Selection.activeGameObject = obj;
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void FindObjectsInLayer() {
        foundObjects.Clear();
        string layerName = layerNames[selectedLayerIndex];
        int layer = LayerMask.NameToLayer(layerName);

        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foundObjects = allObjects.Where(obj => obj.layer == layer).ToList();
    }

    private void FindObjectsWithTag() {
        foundObjects.Clear();
        string tagName = tagNames[selectedTagIndex];

        if (tagName == "Untagged") {
            // 对于 Untagged 标签，我们需要手动检查所有物体
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            foundObjects = allObjects.Where(obj => obj.CompareTag("Untagged")).ToList();
        }
        else {
            // 其他标签使用原有方法
            foundObjects = GameObject.FindGameObjectsWithTag(tagName).ToList();
        }
    }

    private string GetGameObjectPath(GameObject obj) {
        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null) {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}