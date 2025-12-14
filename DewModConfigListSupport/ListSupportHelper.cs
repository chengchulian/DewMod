using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DewModConfigListSupport.attribute;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewModConfigListSupport;

public static class ListSupportHelper
{
    // 宽度配置
    private static float addBtnWidth = 60f;
    private static float delBtnWidth = 60f;
    private static float labelWidth = 400f; 
    private static float inputWidth = 600f; 
    private static float searchWidth = 400f; 
    private static float dropdownWidth = 350f;

    public static void InitListSupport()
    {
        // 动态值列表
        DewGUI.fieldBuilders.Add(
            (
                (Type type, FieldInfo info) => info != null && info.GetCustomAttribute<ValuesAttribute>() != null,
                (Type type, FieldInfo info, Transform parent) => BuildDynamicValuesList(type, info, parent)
            )
        );

        // 普通 List<T>
        DewGUI.fieldBuilders.Add(
            (
                (Type type, FieldInfo info) =>
                    info != null
                    && type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(List<>)
                    && info.GetCustomAttribute<ValuesAttribute>() == null,
                (Type type, FieldInfo info, Transform parent) => BuildSimpleList(type, parent)
            )
        );

        Debug.Log("[DewModConfigListSupport] List support enabled");
    }

    private static FieldBuildResult BuildDynamicValuesList(Type type, FieldInfo info, Transform parent)
    {
        var attr = info.GetCustomAttribute<ValuesAttribute>();
        MethodInfo method = attr.providerType.GetMethod(
            attr.methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
        );
        if (method == null)
            throw new Exception($"ValuesAttribute: Cannot find method {attr.methodName} in {attr.providerType}");

        IList list = (IList)Activator.CreateInstance(type);
        Type elementType = type.GetGenericArguments()[0];

        VerticalLayoutGroup root = DewGUI.CreateVerticalLayoutGroup(parent);
        FieldBuildResult res = new FieldBuildResult { root = root.gameObject };

        // 构建搜索行（Dropdown + Search + Add）
        TMP_Dropdown dropdown = null;
        TMP_InputField searchBox = null;
        Button addBtn = null;
        CreateRow(root.transform, out dropdown, out searchBox, out addBtn, isDropdown: true);

        List<object> filteredValues = new();

        List<object> AllValues()
        {
            object ret = method.Invoke(null, null);
            if (ret is IEnumerable en) return en.Cast<object>().ToList();
            throw new Exception($"Values method must return IEnumerable: {method.Name}");
        }

        void RefreshDropdown(string filter)
        {
            var all = AllValues();
            filteredValues = string.IsNullOrWhiteSpace(filter)
                ? all
                : all.Where(v => (v?.ToString() ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            dropdown.ClearOptions();
            dropdown.AddOptions(filteredValues.Select(v => v.ToString()).ToList());
        }

        void RefreshListUI()
        {
            for (int i = 1; i < root.transform.childCount; i++)
                GameObject.Destroy(root.transform.GetChild(i).gameObject);

            foreach (var (item, index) in list.Cast<object>().Select((v, i) => (v, i)))
                CreateListItem(root.transform, item.ToString(), () =>
                {
                    list.RemoveAt(index);
                    res.onChanged?.Invoke(list);
                    RefreshListUI();
                });
        }

        searchBox.onValueChanged.AddListener(filter => RefreshDropdown(filter));
        addBtn.onClick.AddListener(() =>
        {
            if (filteredValues.Count == 0) return;
            object selected = filteredValues[dropdown.value];
            list.Add(selected);
            res.onChanged?.Invoke(list);
            RefreshListUI();
        });

        res.getValue = () => list;
        res.setValue = obj =>
        {
            list.Clear();
            if (obj != null)
                foreach (var o in (IEnumerable)obj)
                    list.Add(o);

            RefreshDropdown("");
            RefreshListUI();
        };

        RefreshDropdown("");
        RefreshListUI();
        return res;
    }

    private static FieldBuildResult BuildSimpleList(Type type, Transform parent)
    {
        FieldBuildResult res = new FieldBuildResult();
        IList list = (IList)Activator.CreateInstance(type);
        Type elementType = type.GetGenericArguments()[0];

        VerticalLayoutGroup root = DewGUI.CreateVerticalLayoutGroup(parent);
        res.root = root.gameObject;

        TMP_Dropdown dropdown = null;
        TMP_InputField inputBox = null;
        Button addBtn = null;
        CreateRow(root.transform, out dropdown, out inputBox, out addBtn, isDropdown: false);

        void RefreshListUI()
        {
            for (int i = 1; i < root.transform.childCount; i++)
                GameObject.Destroy(root.transform.GetChild(i).gameObject);

            foreach (var (item, index) in list.Cast<object>().Select((v, i) => (v, i)))
                CreateListItem(root.transform, item.ToString(), () =>
                {
                    list.RemoveAt(index);
                    res.onChanged?.Invoke(list);
                    RefreshListUI();
                });
        }

        addBtn.onClick.AddListener(() =>
        {
            string text = inputBox.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            object value;
            try { value = Convert.ChangeType(text, elementType); }
            catch { Debug.LogWarning($"无法转换 '{text}' 为 {elementType}"); return; }

            list.Add(value);
            res.onChanged?.Invoke(list);
            RefreshListUI();
            inputBox.text = "";
        });

        res.getValue = () => list;
        res.setValue = obj =>
        {
            list.Clear();
            if (obj != null)
                foreach (var o in (IEnumerable)obj)
                    list.Add(o);
            RefreshListUI();
        };

        RefreshListUI();
        return res;
    }

    #region 公共UI方法

    // 创建输入/搜索行
    private static void CreateRow(Transform parent, out TMP_Dropdown dropdown, out TMP_InputField input, out Button addBtn, bool isDropdown)
    {
        HorizontalLayoutGroup row = DewGUI.CreateHorizontalLayoutGroup(parent);
        dropdown = null;
        input = null;

        if (isDropdown)
        {
            dropdown = UnityEngine.Object.Instantiate(DewGUI.widgetDropdown, row.transform);
            dropdown.SetWidth(dropdownWidth);
        }

        input = UnityEngine.Object.Instantiate(DewGUI.widgetInputField, row.transform);
        input.placeholder.GetComponent<TextMeshProUGUI>().text = isDropdown ? "Search..." : "Input...";
        input.SetWidth(isDropdown? searchWidth : inputWidth ); 

        addBtn = UnityEngine.Object.Instantiate(DewGUI.widgetButton, row.transform);
        addBtn.GetComponentInChildren<TextMeshProUGUI>().text = "＋";
        addBtn.SetWidth(addBtnWidth);
    }

    // 创建列表项
    private static void CreateListItem(Transform parent, string text, Action onDelete)
    {
        HorizontalLayoutGroup row = DewGUI.CreateHorizontalLayoutGroup(parent);

        var label = UnityEngine.Object.Instantiate(DewGUI.widgetTextLabel, row.transform);
        label.SetText(text);
        label.SetWidth(labelWidth);

        Button delBtn = UnityEngine.Object.Instantiate(DewGUI.widgetButton, row.transform);
        delBtn.GetComponentInChildren<TextMeshProUGUI>().text = "×";
        delBtn.SetWidth(delBtnWidth);
        delBtn.onClick.AddListener(() => onDelete());
    }

    #endregion
}