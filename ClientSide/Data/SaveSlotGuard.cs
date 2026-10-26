using System;
using System.IO;
using System.Text;
using CMS21Together.Shared;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace CMS21Together.ClientSide.Data
{
    // SAVE-001B: fail closed for legacy, ambiguous, unreadable or unknown save slots.
    // This class only READS files and never attempts migration.
    internal enum SaveSlotState
    {
        Empty,
        CurrentV1,
        Protected
    }

    internal static class SaveSlotGuard
    {
        private const int MaxExtensionBytes = 8 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes(SaveSystem.MAGIC_WORD);

        internal static SaveSlotState Inspect(int index)
        {
            if (index < 4 || index >= MainMod.MAX_SAVE_COUNT)
                return SaveSlotState.Protected;

            try
            {
                // Legacy metadata must never be silently replaced by a new extension.
                string legacyPath = Path.Combine("Mods", "togetherMod", "saves", $"save_{index}.cms21mp");
                if (PathPresent(legacyPath))
                    return SaveSlotState.Protected;

                string root = GlobalStrings.SaveDirectory;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    return SaveSlotState.Protected;

                string binary = Path.Combine(root, $"profile{index}.cms21b");
                string plain = Path.Combine(root, $"profile{index}.cms21");
                bool hasBinary = PathPresent(binary);
                bool hasPlain = PathPresent(plain);

                if (hasBinary && hasPlain)
                    return SaveSlotState.Protected;
                if (!hasBinary && !hasPlain)
                    return SaveSlotState.Empty;

                return HasCurrentV1Extension(hasBinary ? binary : plain)
                    ? SaveSlotState.CurrentV1
                    : SaveSlotState.Protected;
            }
            catch (Exception ex)
            {
                // Do not log paths, player names, contents or identifiers.
                MelonLogger.Warning($"[SAVE-001B] Slot {index} inspection failed ({ex.GetType().Name}); protected.");
                return SaveSlotState.Protected;
            }
        }

        private static bool PathPresent(string path)
        {
            try
            {
                // Unlike File.Exists, GetAttributes does not silently treat permission
                // errors as non-existence. An access error is handled as Protected.
                File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static bool HasCurrentV1Extension(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (stream.Length <= Magic.Length + sizeof(int))
                    return false;

                stream.Seek(-sizeof(int), SeekOrigin.End);
                int payloadLength = reader.ReadInt32();
                long offset = stream.Length - Magic.Length - sizeof(int) - payloadLength;
                if (payloadLength <= 0 || payloadLength > MaxExtensionBytes || offset <= 0)
                    return false;

                stream.Seek(-(Magic.Length + sizeof(int)), SeekOrigin.End);
                byte[] marker = reader.ReadBytes(Magic.Length);
                if (marker.Length != Magic.Length)
                    return false;
                for (int i = 0; i < Magic.Length; i++)
                    if (marker[i] != Magic[i])
                        return false;

                stream.Position = offset;
                byte[] payload = reader.ReadBytes(payloadLength);
                if (payload.Length != payloadLength)
                    return false;

                JObject data = JObject.Parse(new UTF8Encoding(false, true).GetString(payload));
                JToken version = data["ModVersion"];
                JToken name = data["Name"];
                JToken gamemode = data["SelectedGamemode"];
                JToken playerInfos = data["PlayerInfos"];

                return data.Value<string>("MagicWord") == SaveSystem.MAGIC_WORD
                       && version != null && version.Type == JTokenType.Integer
                       && version.Value<int>() == 1
                       && name != null && name.Type == JTokenType.String
                       && gamemode != null
                       && (gamemode.Type == JTokenType.Integer || gamemode.Type == JTokenType.String)
                       && playerInfos != null && playerInfos.Type == JTokenType.Array;
            }
        }
    }
}
