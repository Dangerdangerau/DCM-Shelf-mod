#pragma warning disable CS8632
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ShelfMod
{
    public static class ShelfSaveHandler
    {
        private const string SAVE_FOLDER = "Mods/ShelfMod";
        private const string SAVE_FILE = "shelves.json";

        private static string GetGameSaveName()
        {
            try
            {
                string savesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Low", "Waseku", "Data Center", "saves");

                if (!Directory.Exists(savesDir))
                    return null;

                var saveFiles = Directory.GetFiles(savesDir, "*.save");
                if (saveFiles.Length == 0)
                    return null;

                string newest = saveFiles[0];
                for (int i = 1; i < saveFiles.Length; i++)
                {
                    if (File.GetLastWriteTime(saveFiles[i]) > File.GetLastWriteTime(newest))
                        newest = saveFiles[i];
                }

                return Path.GetFileNameWithoutExtension(newest);
            }
            catch
            {
                return null;
            }
        }

        private static string GetSavePath()
        {
            string gameSave = GetGameSaveName();
            string fileName = string.IsNullOrEmpty(gameSave) ? SAVE_FILE : "shelves_" + gameSave + ".json";

            return Path.Combine(
                Directory.GetCurrentDirectory(),
                SAVE_FOLDER,
                fileName);
        }

        [System.Serializable]
        public class ShelfData
        {
            public float[] Position = new float[3];
            public float[] Rotation = new float[4];
            public float Width;
            public float Depth;
            public int Tiers;
            public string SceneName = "";
            public List<SnappedItemData> SnappedItems = new List<SnappedItemData>();
        }

        [System.Serializable]
        public class SnappedItemData
        {
            public int TierIndex;
            public int SlotIndex;
            public float[] LocalPosition = new float[3];
            public string ItemName = "";
        }

        [System.Serializable]
        public class SavePayload
        {
            public List<ShelfData> Shelves = new List<ShelfData>();
        }

        public static void SaveAll(string sceneName)
        {
            SavePayload payload = new SavePayload();

            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (!go.name.StartsWith("ModShelf"))
                    continue;

                ShelfData data = new ShelfData();
                data.Position[0] = go.transform.position.x;
                data.Position[1] = go.transform.position.y;
                data.Position[2] = go.transform.position.z;
                data.Rotation[0] = go.transform.rotation.x;
                data.Rotation[1] = go.transform.rotation.y;
                data.Rotation[2] = go.transform.rotation.z;
                data.Rotation[3] = go.transform.rotation.w;
                data.SceneName = sceneName;

                var info = go.GetComponent<ShelfMod.ShelfInfo>();
                if (info != null)
                {
                    data.Width = info.Width;
                    data.Depth = info.Depth;
                    data.Tiers = info.Tiers;
                }
                else
                {
                    data.Width = 2.4f;
                    data.Depth = 0.6f;
                    data.Tiers = 3;
                }

                ShelfSnapZone[] zones = go.GetComponentsInChildren<ShelfSnapZone>();
                for (int j = 0; j < zones.Length; j++)
                {
                    if (!zones[j].IsOccupied || zones[j].SnappedItem == null)
                        continue;

                    SnappedItemData itemData = new SnappedItemData();
                    itemData.TierIndex = zones[j].TierIndex;
                    itemData.SlotIndex = zones[j].SlotIndex;
                    itemData.LocalPosition[0] = zones[j].SnappedItem.transform.localPosition.x;
                    itemData.LocalPosition[1] = zones[j].SnappedItem.transform.localPosition.y;
                    itemData.LocalPosition[2] = zones[j].SnappedItem.transform.localPosition.z;
                    itemData.ItemName = zones[j].SnappedItem.name;
                    data.SnappedItems.Add(itemData);
                }

                payload.Shelves.Add(data);
            }

            try
            {
                string dir = Path.GetDirectoryName(GetSavePath());
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                File.WriteAllText(GetSavePath(), json);
            }
            catch (System.Exception ex)
            {
                if (ShelfModMain.Instance != null)
                    ShelfModMain.Instance.LoggerInstance.Error("[ShelfMod] Failed to save: " + ex.Message);
            }
        }

        public static SavePayload Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SavePayload>(json);
            }
            catch (System.Exception ex)
            {
                if (ShelfModMain.Instance != null)
                    ShelfModMain.Instance.LoggerInstance.Error("[ShelfMod] Failed to load: " + ex.Message);
                return null;
            }
        }
    }
}
