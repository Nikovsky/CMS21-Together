using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnhollowerBaseLib;
using CMS.MainMenu.Controls;
using CMS.MainMenu.Sections;
using CMS.UI.Controls;
using CMS.MainMenu;
using CMS.UI.Logic.Navigation;
using CMS21Together.Shared;
using CMS21Together.Shared.Data;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CMS21Together.ClientSide.Data.NewUI;

public static class UICore
{
	public static GameObject templateButton;
	public static GameObject templateText;
	public static GameObject templateInputField;
	public static GameObject templateSelector;
	public static GameObject templateImage;

	public static GameObject Active_Panel;
	
	public static GameObject UI_Main;
	public static GameObject V_Main;
	public static GameObject MP_Main;
	public static GameObject MP_Host;
	public static GameObject MP_Lobby;
	
	public static GameObject TMP_Window;
	public static GameObject TMP_Info_Window;

	private static bool update_notice = false;
	public static int last_index_pressed;

    private static GameObject _playButtons;
    private static GameObject _tutorials;
    private static GameObject _tutorialWindow;
    private static GameObject _settings;
    private static MainMenuManager _vanillaMenuManager;
    private static bool _menuManagerStateCaptured;
    private static bool _menuManagerWasEnabled;

    private static string _lastWindowState;

    private static bool IsActive(GameObject obj)
    {
        return obj != null && obj.activeInHierarchy;
    }

    public static void TraceWindowInput()
    {
        if (V_Main == null || MP_Main == null)
            return;

        string key = "";

        if (Input.GetKeyDown(KeyCode.UpArrow)) key = "UP";
        else if (Input.GetKeyDown(KeyCode.DownArrow)) key = "DOWN";
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) key = "LEFT";
        else if (Input.GetKeyDown(KeyCode.RightArrow)) key = "RIGHT";
        else if (Input.GetKeyDown(KeyCode.Return)) key = "ENTER";
        else if (Input.GetKeyDown(KeyCode.KeypadEnter)) key = "NUM_ENTER";
        else if (Input.GetKeyDown(KeyCode.Escape)) key = "ESC";
        else if (Input.GetKeyDown(KeyCode.Space)) key = "SPACE";

        string state =
            $"vanilla={IsActive(V_Main)} " +
            $"play={IsActive(_playButtons)} " +
            $"tutorials={IsActive(_tutorials)} " +
            $"tutorialWindow={IsActive(_tutorialWindow)} " +
            $"settings={IsActive(_settings)} " +
            $"mpMain={IsActive(MP_Main)} " +
            $"mpHost={IsActive(MP_Host)} " +
            $"mpLobby={IsActive(MP_Lobby)}";

        if (key == "" && state == _lastWindowState)
            return;

        _lastWindowState = state;

        MelonLogger.Msg(
            $"[UI-001][KEYWIN] frame={Time.frameCount} " +
            $"key={(key == "" ? "NONE" : key)} {state}");
    }

    private static void SetMainMenuManagerForPanel(
        GameObject targetPanel)
    {
        if (_vanillaMenuManager == null || targetPanel == null)
            return;

        bool showVanilla = targetPanel == V_Main;

        if (!showVanilla)
        {
            if (!_menuManagerStateCaptured)
            {
                _menuManagerWasEnabled =
                    _vanillaMenuManager.enabled;

                _menuManagerStateCaptured = true;
            }

            _vanillaMenuManager.enabled = false;
        }
        else if (_menuManagerStateCaptured)
        {
            _vanillaMenuManager.enabled =
                _menuManagerWasEnabled;

            _menuManagerStateCaptured = false;
        }

        MelonLogger.Msg(
            $"[UI-001][MAINMENU] frame={Time.frameCount} " +
            $"target={targetPanel.name} " +
            $"enabled={_vanillaMenuManager.enabled}");
    }

    public static void InitializeUI(string sceneName)
	{
		if (sceneName != "Menu") return;
		
		templateImage = GameObject.Find("Logo");
		templateButton = GameObject.Find("MainMenuButton");
		templateInputField = GameObject.Find("Main").transform.GetChild(8).gameObject;
		templateText = templateButton.GetComponentInChildren<Text>().gameObject;
		templateSelector = GameObject.Find("MainMenuWindows").transform.GetChild(3).GetChild(0).gameObject
									 .GetComponentInChildren<StringSelector>().gameObject;

		UI_Main = GameObject.Find("MainButtons").transform.parent.gameObject;
        V_Main = GameObject.Find("MainButtons")
			.GetComponent<MainSection>().gameObject;

        _vanillaMenuManager =
            Object.FindObjectOfType<MainMenuManager>();

        _menuManagerStateCaptured = false;

        if (_vanillaMenuManager != null)
        {
            MelonLogger.Msg(
                "[UI-001][MAINMENU] Found MainMenuManager: " +
                _vanillaMenuManager.gameObject.name);
        }
        else
        {
            MelonLogger.Warning(
                "[UI-001][MAINMENU] MainMenuManager not found");
        }

        var root = UI_Main.transform.parent;

        _playButtons = root.Find("Main/PlayButtons")?.gameObject;
        _tutorials = root.Find("Tutorials")?.gameObject;
        _tutorialWindow = root.Find("TutorialWindow")?.gameObject;
        _settings = root.Find("Settings")?.gameObject;

        _lastWindowState = null;

        MP_Main = CreateNewPanel("MP_Main");
		MP_Host = CreateNewPanel("MP_Host");
		MP_Lobby = CreateNewPanel("MP_Lobby");
		
		// UI-001: Lobby must remain hidden until a session is created.
		MP_Lobby.SetActive(false);
		
		LoadCustomlogo();
		GameObject.Find("Logo").GetComponent<RectTransform>().sizeDelta = new Vector2(250, 250);
		UIMenu.SetupMainMenu();
		UIMenu.SetupMultiplayerMenu();
		UIMenu.SetupHostMenu();

		Active_Panel = UI_Main;
		MelonCoroutines.Start(CheckModVersion());
	}
    private static IEnumerator CheckModVersion()
	{
		if (update_notice)
			yield break;
		yield return new WaitForEndOfFrame();
		yield return new WaitForSeconds(1);

		switch (ContentManager.Instance.IsNewVersionAvailable(MainMod.ASSEMBLY_MOD_VERSION))
		{
			case VersionStatus.Outdated:
				UICustomPanel.CreateInfoPanel("A new version of the mod is available !");
				update_notice = true;
				break;
			case VersionStatus.Latest:
				MelonLogger.Msg("Mod is up-to-date !");
				break;
			case VersionStatus.Dev:
				UICustomPanel.CreateInfoPanel("You are using a development build !");
				break;
		}
	}

	private static void DestroyChildren(Transform parent)
	{
		var toDestroy = new List<GameObject>();
		for (int i = 0; i < parent.childCount; i++)
			toDestroy.Add(parent.GetChild(i).gameObject);
		foreach (var go in toDestroy)
			Object.Destroy(go);
	}
	
	private static void LoadCustomlogo()
	{
		var stream = DataHelper.LoadContent("CMS21Together.Assets.cms21TogetherLogo.png");

		var buffer = new byte[stream.Length];
		stream.Read(buffer, 0, (int)stream.Length);

		Object[] textures = Object.FindObjectsOfTypeIncludingAssets(Il2CppType.Of<Texture2D>());
		if (textures.Length < 1) return;

		for (var index = 0; index < textures.Length; index++)
		{
			var texture = textures[index].TryCast<Texture2D>();
			if (texture != null)
				if (texture.name == "cms21Logo")
					ImageConversion.LoadImage(texture, buffer);
		}
	}
	
	public static void ShowPanel(GameObject panelToShow, bool destroyChildren=false)
	{
        TracePanelState("ShowPanel/BEFORE", true);

        if (destroyChildren)
			DestroyChildren(Active_Panel.transform);

		Active_Panel =  panelToShow;
		if (TMP_Window)
			Object.Destroy(TMP_Window);
		if (TMP_Info_Window)
			Object.Destroy(TMP_Info_Window);

        V_Main.gameObject.SetActive(false);
		MP_Main.gameObject.SetActive(false);
		MP_Host.gameObject.SetActive(false);
		MP_Lobby.gameObject.SetActive(false);

		panelToShow.SetActive(true);
        SetMainMenuManagerForPanel(panelToShow); 

        TracePanelState("ShowPanel/AFTER", true);
    }

	private static GameObject CreateNewPanel(string name)
	{
		GameObject panel = Object.Instantiate(UICore.V_Main, UICore.V_Main.transform.parent, false);
		panel.transform.position = new Vector3(panel.transform.position.x, 0, panel.transform.position.z);
		DestroyChildren(panel.transform);
		panel.name = name;
		return panel;
	}
	public static GameObject CreateElement(GameObject template, Transform parent)
	{
		var obj = Object.Instantiate(template, parent, false);
		obj.SetActive(true);
		var rect = obj.GetComponent<RectTransform>();
		
		rect.localScale = Vector3.one;
		rect.localPosition = Vector3.zero;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0, 100);
		rect.sizeDelta = new Vector2(336, 65);
		
		return obj;
	}

	public static void ShowCustomPanel(Transform currentPanel, UICustomPanelType panelType, MainMenuButton btn, int index)
	{
		switch (panelType)
		{
			case UICustomPanelType.SaveInfo:
				UICustomPanel.CreateSaveInfoPanel(btn, index);
				break;
			case UICustomPanelType.CreateSave:
				UICustomPanel.CreateNewSavePanel(btn, index);
				break;
			case UICustomPanelType.JoinAsHostMenu:
				UICustomPanel.JoinAsHostPanel(btn, index);
				break;
			case UICustomPanelType.JoinMenu:
				UICustomPanel.JoinPanel();
				break;
			case UICustomPanelType.LobbyMenu:
				break;
		}
	}

	public static string DescribeUI(GameObject obj)
	{
		if (obj == null)
			return "null";

		return $"{obj.name}#{obj.GetInstanceID()}" +
			$"[self={obj.activeSelf}," +
			$"hierarchy={obj.activeInHierarchy}]";
	}

	public static void TraceKeyboardInput()
	{
		if (UI_Main == null || !UI_Main.activeInHierarchy)
			return;

        if (Input.GetKeyDown(KeyCode.F11))
        {
            DumpMenuControllers();
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            DumpMenuHierarchy();
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            DumpNavigationManagers();
        }

        if (!Input.GetKeyDown(KeyCode.Return) &&
			!Input.GetKeyDown(KeyCode.KeypadEnter))
			return;

		var eventSystem = EventSystem.current;
		var selected = eventSystem != null
			? eventSystem.currentSelectedGameObject
			: null;

		MelonLogger.Msg(
			$"[UI-001] ENTER frame={Time.frameCount} " +
			$"selected={DescribeUI(selected)} " +
			$"activePanel={DescribeUI(Active_Panel)} " +
			$"vanilla={DescribeUI(V_Main)} " +
			$"multiplayer={DescribeUI(MP_Main)} " +
			$"host={DescribeUI(MP_Host)} " +
			$"lobby={DescribeUI(MP_Lobby)} " +
			$"window={DescribeUI(TMP_Window)}"
		);
    }
    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static void DumpNavigationManagers()
    {
        var objects = Object.FindObjectsOfTypeIncludingAssets(
            Il2CppType.Of<ListNavigationManager>());

        MelonLogger.Msg(
            $"[UI-001][NAV] Managers found: {objects.Length}");

        foreach (var obj in objects)
        {
            var manager = obj.TryCast<ListNavigationManager>();

            if (manager == null)
                continue;

            var go = manager.gameObject;

            if (go == null)
                continue;

            MelonLogger.Msg(
                $"[UI-001][NAV] " +
                $"id={manager.GetInstanceID()} " +
                $"active={go.activeInHierarchy} " +
                $"path={GetHierarchyPath(go.transform)}");
        }

        var windows = GameObject.Find("MainMenuWindows");

        MelonLogger.Msg(
            $"[UI-001][NAV] MainMenuWindows=" +
            $"{DescribeUI(windows)}");
    }

    private static string _lastPanelState;

    public static void TracePanelState(
        string source = "LateUpdate",
        bool force = false)
    {
        if (V_Main == null ||
            MP_Main == null ||
            MP_Host == null ||
            MP_Lobby == null)
            return;

        var state =
            $"vanilla={V_Main.activeInHierarchy} " +
            $"mpMain={MP_Main.activeInHierarchy} " +
            $"mpHost={MP_Host.activeInHierarchy} " +
            $"mpLobby={MP_Lobby.activeInHierarchy}";

        if (!force && state == _lastPanelState)
            return;

        _lastPanelState = state;

        bool multiplayerActive =
            MP_Main.activeInHierarchy ||
            MP_Host.activeInHierarchy ||
            MP_Lobby.activeInHierarchy;

        bool overlap =
            V_Main.activeInHierarchy &&
            multiplayerActive;

        MelonLogger.Msg(
            $"[UI-001][STATE] " +
            $"frame={Time.frameCount} " +
            $"source={source} " +
            $"OVERLAP={overlap} " +
            state);
    }
    private static void LogMenuNode(GameObject obj, string path)
    {
        var nav = obj.GetComponent<ListNavigationManager>();

        string navState = nav == null
            ? "none"
            : nav.enabled ? "enabled" : "disabled";

        MelonLogger.Msg(
            $"[UI-001][TREE] {path} " +
            $"self={obj.activeSelf} " +
            $"hierarchy={obj.activeInHierarchy} " +
            $"navigation={navState}");
    }

    public static void DumpMenuHierarchy()
    {
        if (UI_Main == null || UI_Main.transform.parent == null)
            return;

        // MainMenuWindows is the parent of Main.
        var root = UI_Main.transform.parent;

        MelonLogger.Msg(
            $"[UI-001][TREE] frame={Time.frameCount} " +
            $"root={root.name}");

        for (int i = 0; i < root.childCount; i++)
        {
            var section = root.GetChild(i);

            LogMenuNode(section.gameObject, section.name);

            for (int j = 0; j < section.childCount; j++)
            {
                var child = section.GetChild(j);

                LogMenuNode(
                    child.gameObject,
                    section.name + "/" + child.name);
            }
        }
    }

    private static void DumpComponents(string label, GameObject obj)
    {
        if (obj == null)
            return;

        MelonLogger.Msg($"[UI-001][COMP] Object: {label}");

        foreach (var component in obj.GetComponents<Component>())
        {
            if (component == null)
                continue;

            try
            {
                IntPtr classPtr =
                    IL2CPP.il2cpp_object_get_class(component.Pointer);

                if (classPtr == IntPtr.Zero)
                    continue;

                string className = Marshal.PtrToStringAnsi(
                    IL2CPP.il2cpp_class_get_name(classPtr));

                string classNamespace = Marshal.PtrToStringAnsi(
                    IL2CPP.il2cpp_class_get_namespace(classPtr));

                string fullName = string.IsNullOrEmpty(classNamespace)
                    ? className
                    : classNamespace + "." + className;

                MelonLogger.Msg(
                    $"[UI-001][COMP] {label} -> {fullName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[UI-001][COMP] {label}: {ex.Message}");
            }
        }
    }

    public static void DumpMenuControllers()
    {
        if (UI_Main == null)
            return;

        var root = UI_Main.transform.parent;
        if (root == null)
            return;

        DumpComponents("MainMenuWindows", root.gameObject);
        DumpComponents("Main", UI_Main);

        var main = UI_Main.transform;

        string[] panels =
        {
        "MainButtons",
        "PlayButtons",
        "MP_Main",
        "MP_Host",
        "MP_Lobby"
    };

        foreach (var name in panels)
        {
            var panel = main.Find(name);

            if (panel != null)
                DumpComponents(name, panel.gameObject);
        }

        var tutorials = root.Find("Tutorials");

        if (tutorials != null)
            DumpComponents("Tutorials", tutorials.gameObject);
    }
}