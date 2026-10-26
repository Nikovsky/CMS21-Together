using System;
using System.IO;
using System.Linq;
using System.Text;
using CMS.ContainersSave;
using CMS21Together.Shared;
using CMS21Together.Shared.Data;
using MelonLoader;
using Newtonsoft.Json;
using UnhollowerBaseLib;
using UnityEngine;

namespace CMS21Together.ClientSide.Data;

public static class SaveSystem
{
	private static readonly string GAME_SAVE_FOLDER = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\LocalLow\Red Dot Games\Car Mechanic Simulator 2021\Save");
	public const string MAGIC_WORD = "CMS21-TOGETHER";
	public static ModProfileExtension[] Extensions = new ModProfileExtension[MainMod.MAX_SAVE_COUNT];
	public static ProfileData selectedSave;
	public static int selectedSaveIndex;
    public static bool IsInitialized { get; private set; }



    public static void Initialize()
    {
        IsInitialized = false;
        var gm = Singleton<GameManager>.Instance;

        gm.RDGPlayerPrefs.SetInt("selectedProfile", 0);
        gm.ProfileManager.Load();

        var gdm = gm.GameDataManager;

        var allSaves = new Il2CppReferenceArray<SaveData>(
            MainMod.MAX_SAVE_COUNT);

        var vanillaSaves = new Il2CppReferenceArray<SaveData>(4);

        Extensions = new ModProfileExtension[
            MainMod.MAX_SAVE_COUNT];

        // Read all files without modifying game profiles.
        for (int i = 0; i < MainMod.MAX_SAVE_COUNT; i++)
        {
            allSaves[i] = GetSave(i);
        }

        // Initialize the vanilla profiles using the main manager.
        for (int i = 0; i < 4; i++)
        {
            vanillaSaves[i] = allSaves[i];
        }

        gdm.ReloadProfiles(vanillaSaves);


        var vanillaProfiles = gdm.ProfileData;

        int vanillaCount = vanillaProfiles == null
            ? 0
            : vanillaProfiles.Length;

        MelonLogger.Msg(
            $"[SAVE-001][VANILLA] profiles={vanillaCount}");

        if (vanillaCount < 4)
        {
            MelonLogger.Error(
                "[SAVE-001] Vanilla initialization failed. " +
                $"Profiles available: {vanillaCount}");
            return;
        }


        var expandedProfiles =
            new Il2CppReferenceArray<ProfileData>(
                MainMod.MAX_SAVE_COUNT);

        // Preserve the four profiles loaded by the game.
        for (int i = 0; i < 4; i++)
        {
            expandedProfiles[i] = vanillaProfiles[i];
        }

        // Load multiplayer profiles using isolated managers.
        for (int i = 4; i < MainMod.MAX_SAVE_COUNT; i++)
        {
            SaveData currentSave = allSaves[i];
            bool hasModData = false;

            if (currentSave != null && currentSave.HasData)
            {
                try
                {
                    byte[] managedData = currentSave.Data.ToArray();

                    MelonLogger.Msg(
                        $"[SAVE-001][FILE] slot={i} " +
                        $"bytes={managedData.Length}");

                    using var ms = new MemoryStream(managedData);
                    using var reader = new BinaryReader(ms);

                    if (TryFindModData(reader, out var payload))
                    {
                        Extensions[i] =
                            ModProfileExtension.FromBytes(payload);

                        hasModData = true;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(
                        $"[SAVE-001] Extension read failed " +
                        $"for slot {i}: {ex}");
                }
            }

            if (Extensions[i] == null)
            {
                Extensions[i] = new ModProfileExtension();
            }

            if (!hasModData)
            {
                var empty = new ProfileData();
                empty.Init();

                expandedProfiles[i] = empty;

                if (currentSave != null && currentSave.HasData)
                {
                    MelonLogger.Warning(
                        $"[SAVE-001] Slot {i} has data " +
                        "but no readable mod extension.");
                }

                continue;
            }

            try
            {
                var singleSave =
                    new Il2CppReferenceArray<SaveData>(1);

                singleSave[0] = currentSave;

                // A separate manager prevents later reloads
                // from mutating previously loaded profiles.
                var isolatedManager = new GameDataManager();
                isolatedManager.InitContainer();
                isolatedManager.ReloadProfiles(singleSave);

                var profiles = isolatedManager.ProfileData;

                if (profiles == null ||
                    profiles.Length == 0 ||
                    profiles[0] == null)
                {
                    throw new InvalidDataException(
                        $"Slot {i}: no profile decoded.");
                }

                // Retain the decoded object directly.
                // Do not use DataHelper.Copy().
                expandedProfiles[i] = profiles[0];
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    $"[SAVE-001] Failed loading slot {i}: {ex}");

                // Abort initialization rather than installing
                // a partially populated profile array.
                return;
            }
        }

        // Install the complete profile array only once.
        gdm.ProfileData = expandedProfiles;

        MelonLogger.Msg(
            $"[SAVE-001][INIT] profiles={gdm.ProfileData.Length}");

        MelonLogger.Msg(
            "[SaveSystem] Profile initialization complete.");

        IsInitialized = true;
    }



    public static void LoadGame(ModProfileExtension data, int index)
	{
		if (SaveSlotGuard.Inspect(index) == SaveSlotState.Protected)
		{
			MelonLogger.Warning($"[SAVE-001B] Protected slot {index}: game load blocked.");
			return;
		}
		Singleton<GameManager>.Instance.RDGPlayerPrefs.SetInt("selectedProfile", index);
		Singleton<GameManager>.Instance.ProfileManager.Load();
		
		ProfileData profileData = Singleton<GameManager>.Instance.ProfileManager.GetSelectedProfileData();
		selectedSave = profileData;
		selectedSaveIndex = index;
		
		DifficultyLevel level = GetDifficultyFromGamemode(data.SelectedGamemode);
		Singleton<GameManager>.Instance.ProfileManager.SetDifficultyForCurrentProfile(level);
		Il2CppSystem.IO.BinaryWriter writer = new Il2CppSystem.IO.BinaryWriter();
	

		if (index == MainMod.MAX_SAVE_COUNT - 1)
		{
			profileData.WriteSaveHeader(writer);
			profileData.WriteSaveVersion(writer);
			Singleton<GameManager>.Instance.ProfileManager.SetNameForCurrentProfile("NO_SAVE");
			StartGame(index);
		}
		else if (Singleton<GameManager>.Instance.ProfileManager.GetProfilePlayTime(index) == 0U)
		{
			profileData.WriteSaveHeader(writer);
			profileData.WriteSaveVersion(writer);
			Singleton<GameManager>.Instance.ProfileManager.SetNameForCurrentProfile(data.Name);
		}
	}
	
	public static void StartGame(int index)
	{
		if (SaveSlotGuard.Inspect(index) == SaveSlotState.Protected)
		{
			MelonLogger.Warning($"[SAVE-001B] Protected slot {index}: game start blocked.");
			return;
		}
		Application.runInBackground = true;
		
		Singleton<GameManager>.Instance.ProfileManager.selectedProfile = index;
		Singleton<GameManager>.Instance.GameDataManager.LoadProfile();
		Singleton<GameManager>.Instance.StartCoroutine(Singleton<GameManager>.Instance.GameDataManager.Load(true));
		NotificationCenter.m_instance.StartCoroutine(NotificationCenter.m_instance.SelectSceneToLoad("garage", SceneType.Garage, true, true));
	}
	
	public static void DeleteSave(int index)
	{
		if (SaveSlotGuard.Inspect(index) == SaveSlotState.Protected)
		{
			MelonLogger.Warning($"[SAVE-001B] Protected slot {index}: deletion blocked.");
			return;
		}
		var saveFilePath = Path.Combine(GAME_SAVE_FOLDER, $"profile{index}.cms21b");
		if (File.Exists(saveFilePath))
		{
			if (Singleton<GameManager>.Instance.GameDataManager.ProfileData[index] != null)
			{
				var name = Singleton<GameManager>.Instance.GameDataManager.ProfileData[index].Name;
				Singleton<GameManager>.Instance.GameDataManager.ProfileData[index].Init();
				Singleton<GameManager>.Instance.GameDataManager.ProfileData[index].Name = name;
				Singleton<GameManager>.Instance.GameDataManager.ClearData();
			}
			File.Delete(saveFilePath);
			MelonLogger.Msg($"Save file {saveFilePath} deleted");
		}
		else
		{
			MelonLogger.Error("Error deleting save file ");
		}
	}
	
	private static SaveData GetSave(int saveIndex)
	{
		Il2CppStructArray<byte> bytes = LoadProfileSave(saveIndex, out var format, out var parameter);
		
		var saveData = new SaveData();
		saveData.Data = bytes;
		saveData.Format = format;
		saveData.HasData = parameter;

		return saveData;
	}

	private static byte[] LoadProfileSave(int profileIndex, out byte format, out bool hasData)
	{
		var path = string.Format("{0}/profile{1}{2}b", GlobalStrings.SaveDirectory, profileIndex, ".cms21");
		if (File.Exists(path))
		{
			format = 1;
			hasData = true;
			return File.ReadAllBytes(path);
		}

		path = string.Format("{0}/profile{1}{2}", GlobalStrings.SaveDirectory, profileIndex, ".cms21");
		if (File.Exists(path))
		{
			format = 0;
			hasData = true;
			return File.ReadAllBytes(path);
		}

		format = 0;
		hasData = false;
		return Array.Empty<byte>();
	}
	
	private static bool TryFindModData(BinaryReader reader, out byte[] payload)
	{
		payload = null;
		try
		{
			long fileLength = reader.BaseStream.Length;
			byte[] magicBytes = Encoding.UTF8.GetBytes(MAGIC_WORD);
			int magicLength = magicBytes.Length;

			// Sécurité minimale : le fichier doit au moins contenir Magic + Length (int)
			if (fileLength < magicLength + 4) return false;

			// 1. Lire la taille du payload (les 4 derniers octets du fichier)
			reader.BaseStream.Seek(-4, SeekOrigin.End);
			int payloadLength = reader.ReadInt32();

			// 2. Vérifier le Magic Word juste avant la taille
			// Position = Fin - 4 (length) - magicLength
			reader.BaseStream.Seek(-(4 + magicLength), SeekOrigin.End);
			byte[] foundMagic = reader.ReadBytes(magicLength);

			// Comparaison des Magic Bytes
			for (int i = 0; i < magicLength; i++)
			{
				if (foundMagic[i] != magicBytes[i]) return false;
			}

			// 3. Lire le payload
			// Il se trouve juste avant le Magic Word
			long payloadPos = fileLength - 4 - magicLength - payloadLength;
			if (payloadPos < 0) return false;

			reader.BaseStream.Seek(payloadPos, SeekOrigin.End); // On peut aussi calculer depuis le début
			reader.BaseStream.Position = payloadPos; 
			payload = reader.ReadBytes(payloadLength);

			return true;
		}
		catch (Exception ex)
		{
			MelonLogger.Error($"[SaveSystem] Load Error: {ex.Message}");
		}
		return false;
	}
	
	private static DifficultyLevel GetDifficultyFromGamemode(Gamemode saveDataSelectedGamemode)
	{
		if (saveDataSelectedGamemode == Gamemode.Normal)
			return DifficultyLevel.Normal;
		if  (saveDataSelectedGamemode == Gamemode.Easy)
			return DifficultyLevel.Easy;
		if  (saveDataSelectedGamemode == Gamemode.Expert)
			return DifficultyLevel.Expert;
		return DifficultyLevel.Sandbox;
	}
	
	public static Gamemode GetGamemodeFromInt(int selectedGamemode)
	{
		if (selectedGamemode == 1)
			return Gamemode.Normal;
		if (selectedGamemode == 0)
			return Gamemode.Easy;
		if (selectedGamemode == 2)
			return Gamemode.Expert;
		return Gamemode.Sandbox;
	}

	public static Gamemode GetGamemodeFromDifficulty(DifficultyLevel difficultyLevel)
	{
		if (difficultyLevel == DifficultyLevel.Sandbox)
			return Gamemode.Sandbox;
		if (difficultyLevel == DifficultyLevel.Easy)
			return Gamemode.Easy;
		if (difficultyLevel == DifficultyLevel.Expert)
			return Gamemode.Expert;
		return Gamemode.Normal;
	}
}