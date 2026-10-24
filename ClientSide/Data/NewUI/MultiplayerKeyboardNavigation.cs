
using System.Collections.Generic;
using CMS.MainMenu.Controls;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CMS21Together.ClientSide.Data.NewUI;

public static class MultiplayerKeyboardNavigation
{
    private static Transform _currentRoot;
    private static MainMenuButton _selectedButton;

    private static Transform GetRoot()
    {
        var panel = UICore.Active_Panel;

        if (panel == null ||
            (panel != UICore.MP_Main &&
             panel != UICore.MP_Host &&
             panel != UICore.MP_Lobby))
            return null;

        if (UICore.TMP_Info_Window != null &&
            UICore.TMP_Info_Window.activeInHierarchy)
            return UICore.TMP_Info_Window.transform;

        if (UICore.TMP_Window != null &&
            UICore.TMP_Window.activeInHierarchy &&
            UICore.TMP_Window.name != "LobbyWindow")
            return UICore.TMP_Window.transform;

        return panel.transform;
    }

    private static void SetRoot(Transform root)
    {
        if (_currentRoot == root)
            return;

        if (_selectedButton != null)
            _selectedButton.Deselect();

        _selectedButton = null;
        _currentRoot = root;
    }

    public static void OnMouseHover(MainMenuButton button)
    {
        var root = GetRoot();

        if (root == null ||
            button == null ||
            button.transform.parent != root)
            return;

        SetRoot(root);

        if (_selectedButton != null &&
            _selectedButton != button)
            _selectedButton.Deselect();

        _selectedButton = button;
    }

    private static List<MainMenuButton> GetButtons(Transform root)
    {
        var buttons = new List<MainMenuButton>();

        for (int i = 0; i < root.childCount; i++)
        {
            var button = root.GetChild(i)
                .GetComponent<MainMenuButton>();

            if (button != null &&
                button.gameObject.activeInHierarchy &&
                button.CanSelect())
                buttons.Add(button);
        }

        return buttons;
    }

    private static void Navigate(Transform root, int direction)
    {
        var buttons = GetButtons(root);
        if (buttons.Count == 0)
            return;

        int currentIndex = buttons.FindIndex(
            b => b == _selectedButton);

        int nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : buttons.Count - 1)
            : (currentIndex + direction + buttons.Count)
              % buttons.Count;

        var next = buttons[nextIndex];

        if (_selectedButton != null &&
            _selectedButton != next)
            _selectedButton.Deselect();

        _selectedButton = next;
        next.Select();
    }

    private static void Activate(Transform root)
    {
        if (_selectedButton == null ||
            _selectedButton.transform.parent != root ||
            !_selectedButton.gameObject.activeInHierarchy ||
            !_selectedButton.CanSelect())
            return;

        _selectedButton.OnClick.Invoke();
    }

    private static bool ClickButton(Transform root, string label)
    {
        foreach (var button in GetButtons(root))
        {
            if (button.text.text != label)
                continue;

            button.OnClick.Invoke();
            return true;
        }

        return false;
    }

    private static void HandleEscape(Transform root)
    {
        if (UICore.TMP_Info_Window != null &&
            root.gameObject == UICore.TMP_Info_Window)
        {
            ClickButton(root, "Confirm");
            return;
        }

        if (UICore.TMP_Window != null &&
            root.gameObject == UICore.TMP_Window)
        {
            if (!ClickButton(root, "Cancel") &&
                root.gameObject.name == "SaveInfoWindow")
                Object.Destroy(root.gameObject);

            return;
        }

        if (UICore.Active_Panel == UICore.MP_Lobby)
        {
            // Preserve lobby cleanup and server shutdown.
            ClickButton(root, "Back to menu");
        }
        else if (UICore.Active_Panel == UICore.MP_Host)
        {
            UICore.ShowPanel(UICore.MP_Main);
        }
        else if (UICore.Active_Panel == UICore.MP_Main)
        {
            UICore.ShowPanel(UICore.V_Main);
        }
    }

    private static bool HasFocusedInput(Transform root)
    {
        var fields = root.GetComponentsInChildren<InputField>();

        foreach (var field in fields)
        {
            if (field != null && field.isFocused)
                return true;
        }

        return false;
    }

    public static void Tick()
    {
        var root = GetRoot();
        SetRoot(root);

        if (root == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscape(root);
            return;
        }

        if (!Input.GetKeyDown(KeyCode.DownArrow) &&
            !Input.GetKeyDown(KeyCode.UpArrow) &&
            !Input.GetKeyDown(KeyCode.Return) &&
            !Input.GetKeyDown(KeyCode.KeypadEnter))
            return;

        if (HasFocusedInput(root))
            return;

        if (Input.GetKeyDown(KeyCode.DownArrow))
            Navigate(root, 1);
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            Navigate(root, -1);
        else if (Input.GetKeyDown(KeyCode.Return) ||
                 Input.GetKeyDown(KeyCode.KeypadEnter))
            Activate(root);
    }
}
