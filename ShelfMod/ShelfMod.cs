#pragma warning disable CS8632
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppInterop.Runtime.Injection;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(ShelfMod.ShelfModMain), "Shelf Mod", "1.2.0", "DataCenterModder")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace ShelfMod
{
    public class ShelfInfo : MonoBehaviour
    {
        public float Width;
        public float Depth;
        public int Tiers;
        public ShelfInfo(IntPtr ptr) : base(ptr) { }
    }

    public class ShelfModMain : MelonMod
    {
        public static ShelfModMain Instance;

        private const string CAT = "ShelfMod";
        private MelonPreferences_Entry<string> _keyPref;
        private MelonPreferences_Entry<float> _widthPref;
        private MelonPreferences_Entry<float> _depthPref;
        private MelonPreferences_Entry<int> _tiersPref;
        private MelonPreferences_Entry<float> _gridPref;

        private KeyCode _placeKey = KeyCode.F7;
        private float _width = 2.4f;
        private float _depth = 0.6f;
        private int _tiers = 3;
        private float _grid = 0.5f;

        private struct ShelfPreset { public float Width; public float Depth; public int Tiers; }
        private readonly System.Collections.Generic.Dictionary<string, ShelfPreset> _presets = new()
        {
            ["default"] = new ShelfPreset { Width = 2.4f, Depth = 0.6f, Tiers = 3 },
            ["small_shelf"] = new ShelfPreset { Width = 1.2f, Depth = 0.4f, Tiers = 3 },
        };

        private GameObject _preview;
        private bool _placing;
        private Camera _cam;
        private int _presetIndex = -1;
        private System.Collections.Generic.List<string> _presetKeys;

        private GameObject _ghostRoot;
        private Material _ghostMaterial;
        private ShelfSnapZone _lastGhostZone;
        private GameObject _lastGhostHeldItem;

        private GameObject _heldItem;
        private int _heldItemTrackFrame;
        private int _visualFeedbackFrame;

        private Il2Cpp.UsableObject _heldUO;
        private Renderer _heldRenderer;
        private bool _heldIsSmall;
        private float _previewRotation;

        private GameObject _packagedShelfToDestroy;
        private readonly System.Collections.Generic.Dictionary<string, ShelfPreset> _shopItemPresets = new()
        {
            ["Small Shelf"]  = new ShelfPreset { Width = 0.8f,  Depth = 0.35f, Tiers = 2 },
            ["Medium Shelf"] = new ShelfPreset { Width = 1.2f,  Depth = 0.40f, Tiers = 3 },
            ["Large Shelf"]  = new ShelfPreset { Width = 1.8f,  Depth = 0.45f, Tiers = 4 },
            ["Wide Shelf"]   = new ShelfPreset { Width = 2.4f,  Depth = 0.40f, Tiers = 3 },
        };

        public override void OnInitializeMelon()
        {
            Instance = this;

            ClassInjector.RegisterTypeInIl2Cpp<SnapManager>();
            ClassInjector.RegisterTypeInIl2Cpp<ShelfSnapZone>();
            ClassInjector.RegisterTypeInIl2Cpp<ShelfInfo>();

            var cat = MelonPreferences.CreateCategory(CAT, "Shelf Mod");
            _keyPref  = cat.CreateEntry("PlacementKey", "F7",  "Placement Toggle Key");
            _widthPref = cat.CreateEntry("ShelfWidth",  2.4f, "Shelf Width (m)");
            _depthPref = cat.CreateEntry("ShelfDepth",  0.6f, "Shelf Depth (m)");
            _tiersPref = cat.CreateEntry("ShelfTiers",  3,    "Number of Tiers");
            _gridPref  = cat.CreateEntry("GridSize",    0.5f, "Grid Snap (m)");

            KeyCode parsedKey;
            if (System.Enum.TryParse<KeyCode>(_keyPref.Value, out parsedKey))
                _placeKey = parsedKey;
            _width  = _widthPref.Value;
            _depth  = _depthPref.Value;
            _tiers  = _tiersPref.Value;
            _grid   = _gridPref.Value;

            Msg("Initialized - press " + _placeKey + " to place shelves.");

            _presetKeys = new System.Collections.Generic.List<string>(_presets.Keys);
            string presetList = "";
            for (int i = 0; i < _presetKeys.Count; i++)
                presetList += "\n  F" + (9 + i) + " = " + _presetKeys[i] + " (" + _presets[_presetKeys[i]].Width + "x" + _presets[_presetKeys[i]].Depth + ")";
            Msg("Presets:" + presetList);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _cam = null;
            HideGhostPreview();
            if (_ghostRoot != null) Object.Destroy(_ghostRoot);
            _ghostRoot = null;
            _lastGhostHeldItem = null;
            _currentSceneName = sceneName;

            if (SnapManager.Instance == null)
            {
                var go = new GameObject("ShelfSnapManager");
                go.AddComponent<SnapManager>();
                Object.DontDestroyOnLoad(go);
            }

            if (!_shopRegistered)
                RegisterShelfShopItem();

            LoadShelves();
        }

        private int _shopRetryFrame;

        public override void OnUpdate()
        {
            if (!_shopRegistered && Time.frameCount - _shopRetryFrame > 300)
            {
                _shopRetryFrame = Time.frameCount;
                RegisterShelfShopItem();
            }

            if (IsKeyPressed(_placeKey))
                TogglePlacement();

            if (IsKeyPressed(KeyCode.F9))
                CyclePreset();

            if (IsKeyPressed(KeyCode.E) && !_placing)
            {
                if (!TryStartPlacementFromHeldShelf())
                    TryEKeyShelfSnap();
            }

            if (IsKeyPressed(KeyCode.Q))
                _heldItem = null;

            if (_placing && _preview != null)
                UpdatePreview();

            if (!_placing && Time.frameCount - _visualFeedbackFrame >= 3)
            {
                _visualFeedbackFrame = Time.frameCount;
                UpdateVisualFeedback();
            }

            UpdateHeldItemTracking();
        }

        private void UpdateVisualFeedback()
        {
            if (_cam == null)
                _cam = Camera.main;
            if (_cam == null) return;

            GameObject heldItem = _heldItem;

            Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            RaycastHit hit = default;
            bool hitSomething = false;

            if (heldItem != null)
            {
                Collider heldCol = heldItem.GetComponentInChildren<Collider>();
                if (heldCol != null) heldCol.enabled = false;
                RaycastHit[] hits = Physics.RaycastAll(ray, 8f);
                float bestDist = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].distance < bestDist)
                    {
                        bestDist = hits[i].distance;
                        hit = hits[i];
                        hitSomething = true;
                    }
                }
                if (heldCol != null) heldCol.enabled = true;
            }
            else
            {
                hitSomething = Physics.Raycast(ray, out hit, 8f);
            }

            if (heldItem != _lastGhostHeldItem)
                RebuildGhost(heldItem);
            _lastGhostHeldItem = heldItem;

            if (heldItem != null && hitSomething && SnapManager.Instance != null)
            {
                ShelfSnapZone zone = SnapManager.Instance.FindNearestZoneForItem(hit.point, heldItem);
                if (zone != null)
                    ShowGhostPreview(zone, heldItem);
                else
                    HideGhostPreview();
            }
            else if (hitSomething && SnapManager.Instance != null)
            {
                HideGhostPreview();
                ShelfSnapZone zone = SnapManager.Instance.FindNearestOccupiedZone(hit.point);
                if (zone != null && zone.SnappedItem != null)
                    HighlightItemOnShelf(zone.SnappedItem);
                else
                    ClearItemHighlight();
            }
            else
            {
                HideGhostPreview();
                ClearItemHighlight();
            }
        }

        private void ShowGhostPreview(ShelfSnapZone zone, GameObject heldItem)
        {
            if (_ghostRoot == null)
                _ghostRoot = new GameObject("ShelfModGhostRoot");

            if (_ghostMaterial == null)
                _ghostMaterial = CreateGhostMaterial();

            if (_ghostRoot.transform.childCount == 0 && heldItem != null)
                BuildGhostFromItem(heldItem);

            if (_ghostRoot.transform.childCount == 0)
                BuildFallbackGhost(zone);

            Transform zoneTransform = zone.SlotCenter != null ? zone.SlotCenter : zone.transform;

            float yOff = 0f;
            if (zone.TotalStackLevels <= 1 && heldItem != null)
            {
                Renderer rend = _heldRenderer;
                if (rend == null) rend = heldItem.GetComponentInChildren<Renderer>();
                float itemH = rend != null ? rend.bounds.size.y : 0.1f;

                float yBot = 8.0f, yMid1 = -2.5f, yMid2 = -9.0f, yTop = -16.5f;
                if (_heldUO != null)
                {
                    string itemName = _heldUO.item != null ? _heldUO.item.itemName : heldItem.name;
                    ShelfSnapZone.GetOffsetMultipliers(_heldUO.objectInHandType, itemName, out yBot, out yMid1, out yMid2, out yTop);
                }
                int lastTier = zone.TotalTiers > 0 ? zone.TotalTiers - 1 : 3;
                if (zone.TierIndex == 0) yOff = itemH * yBot;
                else if (zone.TierIndex == 1) yOff = itemH * yMid1;
                else if (zone.TierIndex == lastTier) yOff = itemH * yTop;
                else yOff = itemH * yMid2;
            }

            _ghostRoot.transform.position = zoneTransform.position + new Vector3(0f, yOff, 0f);
            _ghostRoot.transform.rotation = zoneTransform.rotation;
            _ghostRoot.SetActive(true);
            _lastGhostZone = zone;
        }

        private void BuildGhostFromItem(GameObject heldItem)
        {
            Renderer[] renderers = heldItem.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer src = renderers[i];
                MeshFilter srcMF = src.GetComponent<MeshFilter>();
                if (srcMF == null || srcMF.sharedMesh == null) continue;

                GameObject go = new GameObject("Ghost_" + src.gameObject.name);
                go.transform.SetParent(_ghostRoot.transform, false);
                go.transform.localPosition = src.transform.localPosition;
                go.transform.localRotation = src.transform.localRotation;
                go.transform.localScale = src.transform.localScale;

                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = srcMF.sharedMesh;

                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                if (_ghostMaterial != null)
                    mr.material = _ghostMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private void BuildFallbackGhost(ShelfSnapZone zone)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GhostFallback";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) Object.Destroy(rb);
            go.transform.SetParent(_ghostRoot.transform, false);
            go.transform.localScale = zone.SlotSize;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_ghostMaterial != null) rend.material = _ghostMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        private void HideGhostPreview()
        {
            if (_ghostRoot != null)
                _ghostRoot.SetActive(false);
            _lastGhostZone = null;
        }

        private void RebuildGhost(GameObject heldItem)
        {
            if (_ghostRoot == null) return;
            for (int i = _ghostRoot.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(_ghostRoot.transform.GetChild(i).gameObject);
            if (heldItem != null)
                BuildGhostFromItem(heldItem);
        }

        private void HighlightItemOnShelf(GameObject item)
        {
            Renderer rend = item.GetComponentInChildren<Renderer>();
            if (rend == null) return;
            if (_heldRenderer == rend && _highlightedItemRenderer == rend) return;

            ClearItemHighlight();

            _highlightedItemRenderer = rend;
            _highlightedItemOriginalMat = rend.material;

            Material glowMat = new Material(_highlightedItemOriginalMat);
            glowMat.EnableKeyword("_EMISSION");
            glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glowMat.SetColor("_EmissionColor", new Color(0.5f, 1.0f, 0.5f) * 3f);
            Color c = glowMat.color;
            c.r = Mathf.Min(c.r * 1.3f, 1f);
            c.g = Mathf.Min(c.g * 1.3f, 1f);
            c.b = Mathf.Min(c.b * 1.3f, 1f);
            glowMat.color = c;
            rend.material = glowMat;
        }

        private Renderer _highlightedItemRenderer;
        private Material _highlightedItemOriginalMat;

        private void ClearItemHighlight()
        {
            if (_highlightedItemRenderer != null && _highlightedItemOriginalMat != null)
            {
                Material glowMat = _highlightedItemRenderer.material;
                _highlightedItemRenderer.material = _highlightedItemOriginalMat;
                Object.Destroy(glowMat);
            }
            _highlightedItemRenderer = null;
            _highlightedItemOriginalMat = null;
        }

        private Material CreateGhostMaterial()
        {
            if (_cachedGameMaterial == null)
            {
                foreach (var r in Object.FindObjectsOfType<Renderer>())
                {
                    if (r.sharedMaterial != null && r.sharedMaterial.shader != null)
                    {
                        _cachedGameMaterial = new Material(r.sharedMaterial);
                        break;
                    }
                }
            }
            if (_cachedGameMaterial == null) return null;
            var mat = new Material(_cachedGameMaterial);
            mat.color = new Color(0.1f, 1.0f, 0.3f, 0.5f);
            mat.SetColor("_BaseColor", new Color(0.1f, 1.0f, 0.3f, 0.5f));
            mat.renderQueue = 3000;
            return mat;
        }

        private string _currentSceneName;

        private Material _cachedGameMaterial;

        private bool TryStartPlacementFromHeldShelf()
        {
            GameObject heldObj = _heldItem;

            if (heldObj == null)
            {
                foreach (var uo in Object.FindObjectsOfType<Il2Cpp.UsableObject>())
                {
                    if (uo.objectInHands)
                    {
                        heldObj = uo.gameObject;
                        break;
                    }
                }
            }

            if (heldObj == null)
                return false;

            Il2Cpp.UsableObject uo2 = heldObj.GetComponent<Il2Cpp.UsableObject>();
            if (uo2 == null)
                uo2 = heldObj.GetComponentInChildren<Il2Cpp.UsableObject>();

            if (uo2 == null)
                return false;

            if (uo2.objectInHandType != Il2Cpp.PlayerManager.ObjectInHand.ModItem)
                return false;

            string itemName = uo2.item != null ? uo2.item.itemName : heldObj.name;
            if (itemName.EndsWith("(Clone)"))
                itemName = itemName.Substring(0, itemName.Length - 7);

            if (!_shopItemPresets.TryGetValue(itemName, out ShelfPreset preset))
                return false;

            _packagedShelfToDestroy = heldObj;
            _width = preset.Width;
            _depth = preset.Depth;
            _tiers = preset.Tiers;

            _placing = true;
            _cam = Camera.main;
            _previewRotation = 0f;
            _preview = ShelfBuilder.CreateShelf(_width, _depth, _tiers, true);
            Msg("Placing " + itemName + " - LMB to place, RMB to cancel");
            return true;
        }

        private void TryEKeyShelfSnap()
        {
            if (_cam == null)
                _cam = Camera.main;
            if (_cam == null) return;

            GameObject heldItem = _heldItem;

            Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            RaycastHit hit = default;
            bool hitSomething = false;

            if (heldItem != null)
            {
                Collider heldCol = heldItem.GetComponentInChildren<Collider>();
                if (heldCol != null) heldCol.enabled = false;
                RaycastHit[] hits = Physics.RaycastAll(ray, 8f);
                float bestDist = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].distance < bestDist)
                    {
                        bestDist = hits[i].distance;
                        hit = hits[i];
                        hitSomething = true;
                    }
                }
                if (heldCol != null) heldCol.enabled = true;
            }
            else
            {
                hitSomething = Physics.Raycast(ray, out hit, 8f);
            }

            if (!hitSomething)
                return;

            if (SnapManager.Instance == null) return;

            if (hit.transform != null && hit.transform.gameObject.name == "ShelfRemoveButton")
            {
                GameObject shelf = hit.transform.gameObject;
                while (shelf != null && !shelf.name.StartsWith("ModShelf"))
                    shelf = shelf.transform.parent != null ? shelf.transform.parent.gameObject : null;

                if (shelf != null)
                {
                    ShelfSnapZone[] zones = shelf.GetComponentsInChildren<ShelfSnapZone>();
                    for (int i = 0; i < zones.Length; i++)
                    {
                        if (SnapManager.Instance != null)
                            SnapManager.Instance.UnregisterZone(zones[i]);
                    }
                    Object.Destroy(shelf);
                    Msg("Shelf removed");
                    ShelfSaveHandler.SaveAll(_currentSceneName);
                }
                return;
            }

            if (heldItem != null)
            {
                ShelfSnapZone zone = SnapManager.Instance.FindNearestZoneForItem(hit.point, heldItem);
                if (zone == null) return;
                zone.TrySnap(heldItem);
                _heldItem = null;
                _heldUO = null;
                _heldRenderer = null;
                Msg("Item snapped to shelf slot " + zone.TierIndex + "-" + zone.SlotIndex);
            }
            else
            {
                GameObject released = SnapManager.Instance.ReleaseNearestItem(hit.point);
                if (released != null)
                {
                    Il2Cpp.UsableObject uo = released.GetComponent<Il2Cpp.UsableObject>();
                    if (uo != null)
                    {
                        uo.InteractOnClick();
                        Msg("Item picked up from shelf");
                    }
                    else
                    {
                        Transform camT = _cam.transform;
                        released.transform.position = camT.position + camT.forward * 1.0f;
                        Msg("Item removed from shelf");
                    }
                }
            }
        }

        private void UpdateHeldItemTracking()
        {
            int frame = Time.frameCount;
            if (frame - _heldItemTrackFrame < 5) return;
            _heldItemTrackFrame = frame;

            if (_heldItem != null)
            {
                Il2Cpp.UsableObject uo = _heldItem.GetComponent<Il2Cpp.UsableObject>();
                if (uo == null || !uo.objectInHands)
                {
                    _heldItem = null;
                    _heldUO = null;
                    _heldRenderer = null;
                    _heldIsSmall = false;
                }
            }

            if (_heldItem == null)
            {
                foreach (var uo in Object.FindObjectsOfType<Il2Cpp.UsableObject>())
                {
                    if (uo.objectInHands)
                    {
                        _heldItem = uo.gameObject;
                        _heldUO = uo;
                        _heldRenderer = uo.GetComponentInChildren<Renderer>();
                        _heldIsSmall = ShelfSnapZone.IsSmallItem(uo.gameObject);
                        break;
                    }
                }
            }
        }

        private void CyclePreset()
        {
            _presetIndex = (_presetIndex + 1) % _presetKeys.Count;
            string key = _presetKeys[_presetIndex];
            ShelfPreset p = _presets[key];
            _width = p.Width;
            _depth = p.Depth;
            _tiers = p.Tiers;
            Msg("Preset: " + key + " (" + _width + "x" + _depth + "x" + _tiers + ")");
        }

        private void TogglePlacement()
        {
            _placing = !_placing;
            ClearItemHighlight();
            HideGhostPreview();

            if (_placing)
            {
                _cam = Camera.main;
                _previewRotation = 0f;
                if (_preview == null)
                    _preview = ShelfBuilder.CreateShelf(_width, _depth, _tiers, true);
                Msg("Placement ON - LMB to place, RMB to cancel, Scroll to rotate");
            }
            else
            {
                KillPreview();
                _packagedShelfToDestroy = null;
                Msg("Placement OFF");
            }
        }


        private void KillPreview()
        {
            if (_preview != null)
                Object.Destroy(_preview);
            _preview = null;
        }

        private void UpdatePreview()
        {
            if (_cam == null)
                _cam = Camera.main;
            if (_cam == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = _cam.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 25f))
                return;

            Vector3 pos = ShelfBuilder.SnapToGrid(hit.point, _grid);

            if (Vector3.Angle(hit.normal, Vector3.up) > 30f)
            {
                _preview.SetActive(false);
                return;
            }

            _preview.SetActive(true);
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                Keyboard kb = Keyboard.current;
                float step = (kb != null && kb.leftShiftKey.isPressed) ? 15f : 45f;
                _previewRotation += (scroll > 0f ? step : -step);
                _preview.transform.rotation = Quaternion.Euler(0f, _previewRotation, 0f);
            }

            _preview.transform.position = pos;

            if (mouse.leftButton.wasPressedThisFrame)
                Place();
            if (mouse.rightButton.wasPressedThisFrame)
                TogglePlacement();
        }

        private void Place()
        {
            if (_preview == null)
                return;

            Vector3 pos = _preview.transform.position;
            Quaternion rot = _preview.transform.rotation;
            KillPreview();

            GameObject shelf = ShelfBuilder.CreateShelf(_width, _depth, _tiers, false);
            shelf.transform.SetPositionAndRotation(pos, rot);

            var info = shelf.AddComponent<ShelfInfo>();
            info.Width = _width;
            info.Depth = _depth;
            info.Tiers = _tiers;

            ShelfSnapZone[] zones = shelf.GetComponentsInChildren<ShelfSnapZone>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (SnapManager.Instance != null)
                    SnapManager.Instance.RegisterZone(zones[i]);
            }

            Msg("Shelf placed at " + pos.ToString("F1"));
            ShelfSaveHandler.SaveAll(_currentSceneName);

            if (_packagedShelfToDestroy != null)
            {
                Object.Destroy(_packagedShelfToDestroy);
                _packagedShelfToDestroy = null;
                Msg("Packaged shelf consumed");
            }

            _placing = false;
        }

        private void LoadShelves()
        {
            var payload = ShelfSaveHandler.Load();
            if (payload == null || payload.Shelves == null) return;

            int count = 0;
            foreach (var data in payload.Shelves)
            {
                if (data.SceneName != _currentSceneName) continue;
                if (data.Width <= 0f) data.Width = 2.4f;
                if (data.Depth <= 0f) data.Depth = 0.6f;
                if (data.Tiers <= 0) data.Tiers = 3;

                GameObject shelf = ShelfBuilder.CreateShelf(data.Width, data.Depth, data.Tiers, false);
                shelf.transform.position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
                shelf.transform.rotation = new Quaternion(data.Rotation[0], data.Rotation[1], data.Rotation[2], data.Rotation[3]);

                var info = shelf.AddComponent<ShelfInfo>();
                info.Width = data.Width;
                info.Depth = data.Depth;
                info.Tiers = data.Tiers;

                ShelfSnapZone[] zones = shelf.GetComponentsInChildren<ShelfSnapZone>();
                for (int i = 0; i < zones.Length; i++)
                {
                    if (SnapManager.Instance != null)
                        SnapManager.Instance.RegisterZone(zones[i]);
                }
                count++;
            }

            if (count > 0)
                Msg("Loaded " + count + " saved shelf(es)");
        }

        private static bool IsKeyPressed(KeyCode kc)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;

            switch (kc)
            {
                case KeyCode.F7:  return kb.f7Key.wasPressedThisFrame;
                case KeyCode.F8:  return kb.f8Key.wasPressedThisFrame;
                case KeyCode.F9:  return kb.f9Key.wasPressedThisFrame;
                case KeyCode.E:   return kb.eKey.wasPressedThisFrame;
                case KeyCode.Q:   return kb.qKey.wasPressedThisFrame;
                default: return false;
            }
        }

        private void Msg(string text)
        {
            LoggerInstance.Msg("[ShelfMod] " + text);
        }

        private bool _shopRegistered;

        private void RegisterShelfShopItem()
        {
            try
            {
                var modLoader = Il2Cpp.ModLoader.instance;
                if (modLoader == null)
                {
                    Msg("ModLoader.instance is null - retrying later");
                    return;
                }

                MethodInfo method = null;
                var methods = typeof(Il2Cpp.ModLoader).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var m in methods)
                {
                    if (m.Name == "LoadShopItem") { method = m; break; }
                }

                if (method == null)
                {
                    Msg("LoadShopItem not found");
                    return;
                }

                Msg("LoadShopItem found: " + method.Name + " params=" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));

                string baseDir = Path.Combine(Environment.CurrentDirectory, "Mods", "ShelfMod");
                Directory.CreateDirectory(baseDir);

                string texPath = Path.Combine(baseDir, "shelf.png");
                if (!File.Exists(texPath))
                    File.WriteAllBytes(texPath, MakePNG(4, 4, 0.35f, 0.28f, 0.2f));

                string iconPath = Path.Combine(baseDir, "icon.png");
                if (!File.Exists(iconPath))
                    File.WriteAllBytes(iconPath, MakeShelfIcon(64, 64));

                var variants = new (string name, float w, float d, int t, int price)[]
                {
                    ("Small Shelf",  0.8f,  0.35f, 2,  500),
                    ("Medium Shelf", 1.2f,  0.40f, 3,  1000),
                    ("Large Shelf",  1.8f,  0.45f, 4,  1500),
                    ("Wide Shelf",   2.4f,  0.40f, 3,  1200),
                };

                int count = 0;
                foreach (var (name, w, d, t, price) in variants)
                {
                    string folderName = "ShelfMod_" + w.ToString("0.0") + "x" + d.ToString("0.0") + "x" + t;
                    string folder = Path.Combine(baseDir, folderName);
                    Directory.CreateDirectory(folder);

                    string objPath = Path.Combine(folder, "shelf.obj");
                    File.WriteAllText(objPath, BuildShelfOBJ(w, d, t));

                    if (!File.Exists(Path.Combine(folder, "shelf.png")))
                        File.Copy(texPath, Path.Combine(folder, "shelf.png"), true);
                    if (!File.Exists(Path.Combine(folder, "icon.png")))
                        File.Copy(iconPath, Path.Combine(folder, "icon.png"), true);

                    var config = new Il2Cpp.ShopItemConfig();
                    config.itemName = name;
                    config.price = price;
                    config.xpToUnlock = 0;
                    config.sizeInU = 0;
                    config.mass = 5f + t * 2f;
                    config.modelScale = 1f;
                    config.colliderSize = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(new float[] { 0.2f, 0.1f, 0.2f });
                    config.colliderCenter = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(new float[] { 0f, 0f, 0f });
                    config.modelFile = "shelf.obj";
                    config.textureFile = "shelf.png";
                    config.iconFile = "icon.png";
                    config.objectType = Il2Cpp.PlayerManager.ObjectInHand.ModItem;

                    try
                    {
                        method.Invoke(modLoader, new object[] { folder, folderName, config });
                        count++;
                        Msg("  Registered: " + name + " -> " + folderName);
                    }
                    catch (Exception ex)
                    {
                        Msg("  FAILED to register " + name + ": " + ex.InnerException?.Message ?? ex.Message);
                    }
                }

                if (count > 0)
                {
                    _shopRegistered = true;
                    Msg(count + " shelf variants added to shop");
                }
            }
            catch (Exception e)
            {
                Msg("Shop registration failed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private static string BuildShelfOBJ(float width, float depth, int tiers)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "# ShelfMod packaged box\n" +
                "v -0.1 0 -0.1\nv 0.1 0 -0.1\nv 0.1 0.1 -0.1\nv -0.1 0.1 -0.1\n" +
                "v -0.1 0 0.1\nv 0.1 0 0.1\nv 0.1 0.1 0.1\nv -0.1 0.1 0.1\n" +
                "f 1 2 3 4\nf 6 5 8 7\nf 5 1 4 8\nf 2 6 7 3\nf 4 3 7 8\nf 5 6 2 1\n");
        }

        private static byte[] MakePNG(int w, int h, float r, float g, float b)
        {
            byte cr = (byte)(r * 255), cg = (byte)(g * 255), cb = (byte)(b * 255);

            using var ms = new MemoryStream();
            void Write(byte[] data) { ms.Write(data, 0, data.Length); }
            void WriteU32BE(uint val)
            {
                ms.WriteByte((byte)(val >> 24));
                ms.WriteByte((byte)(val >> 16));
                ms.WriteByte((byte)(val >> 8));
                ms.WriteByte((byte)(val));
            }
            void WriteChunk(string tag, byte[] data)
            {
                WriteU32BE((uint)data.Length);
                Write(System.Text.Encoding.ASCII.GetBytes(tag));
                Write(data);
                uint crc = CRC32(System.Text.Encoding.ASCII.GetBytes(tag), data);
                WriteU32BE(crc);
            }

            Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            byte[] ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 8); ihdr[1] = (byte)w;
            ihdr[2] = (byte)(h >> 8); ihdr[3] = (byte)h;
            ihdr[4] = 8; ihdr[5] = 2; ihdr[6] = 0; ihdr[7] = 0; ihdr[8] = 0;
            WriteChunk("IHDR", ihdr);

            byte[] raw = new byte[h * (1 + w * 3)];
            for (int y = 0; y < h; y++)
            {
                int row = y * (1 + w * 3);
                raw[row] = 0;
                for (int x = 0; x < w; x++)
                {
                    int px = row + 1 + x * 3;
                    raw[px] = cr; raw[px + 1] = cg; raw[px + 2] = cb;
                }
            }

            byte[] compressed;
            using (var cms = new MemoryStream())
            {
                using (var ds = new System.IO.Compression.DeflateStream(cms, System.IO.Compression.CompressionLevel.Fastest, true))
                    ds.Write(raw, 0, raw.Length);
                compressed = cms.ToArray();
            }
            WriteChunk("IDAT", compressed);
            WriteChunk("IEND", Array.Empty<byte>());

            return ms.ToArray();
        }

        private static readonly uint[] _crc32Table;
        static ShelfModMain()
        {
            _crc32Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                _crc32Table[i] = c;
            }
        }

        private static uint CRC32(byte[] tag, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in tag) crc = _crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            foreach (byte b in data) crc = _crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        private static byte[] MakeShelfIcon(int w, int h)
        {
            byte[] pixels = new byte[w * h * 3];

            void SetPixel(int x, int y, byte r, byte g, byte b)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) return;
                int idx = (y * w + x) * 3;
                pixels[idx] = r; pixels[idx + 1] = g; pixels[idx + 2] = b;
            }

            void FillRect(int x0, int y0, int x1, int y1, byte r, byte g, byte b)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        SetPixel(x, y, r, g, b);
            }

            byte bgR = 50, bgG = 50, bgB = 55;
            byte woodR = 180, woodG = 140, woodB = 90;
            byte darkWoodR = 140, darkWoodG = 110, darkWoodB = 70;
            byte metalR = 170, metalG = 175, metalB = 180;
            byte darkMetalR = 130, darkMetalG = 135, darkMetalB = 140;

            FillRect(0, 0, w - 1, h - 1, bgR, bgG, bgB);

            int legW = Math.Max(2, w / 16);
            int plankH = Math.Max(2, h / 14);
            int margin = w / 8;

            int leftX = margin;
            int rightX = w - margin - 1;
            int topY = h / 6;
            int bottomY = h - h / 6;

            FillRect(leftX, topY, leftX + legW - 1, bottomY, metalR, metalG, metalB);
            FillRect(rightX - legW + 1, topY, rightX, bottomY, metalR, metalG, metalB);

            int numShelves = 3;
            int shelfSpacing = (bottomY - topY) / (numShelves + 1);

            for (int i = 0; i < numShelves; i++)
            {
                int sy = topY + shelfSpacing * (i + 1) - plankH / 2;
                FillRect(leftX - 1, sy, rightX + 1, sy + plankH, woodR, woodG, woodB);
                FillRect(leftX - 1, sy + plankH, rightX + 1, sy + plankH, darkWoodR, darkWoodG, darkWoodB);
            }

            FillRect(leftX - 2, topY - plankH, rightX + 2, topY - 1, woodR, woodG, woodB);
            FillRect(leftX - 2, topY, rightX + 2, topY, darkWoodR, darkWoodG, darkWoodB);

            int midShelf = topY + shelfSpacing * 2 - plankH / 2;
            byte itemR = 100, itemG = 180, itemB = 220;
            int itemW = (rightX - leftX) / 3;
            int itemH = shelfSpacing - plankH * 2;
            FillRect(leftX + itemW / 2, midShelf - itemH, leftX + itemW / 2 + itemW - 2, midShelf - 1, itemR, itemG, itemB);

            itemR = 220; itemG = 120; itemB = 80;
            FillRect(rightX - itemW - itemW / 2, midShelf - itemH + 2, rightX - itemW / 2 - 2, midShelf - 1, itemR, itemG, itemB);

            using var ms = new MemoryStream();
            void Write(byte[] data) { ms.Write(data, 0, data.Length); }
            void WriteU32BE(uint val)
            {
                ms.WriteByte((byte)(val >> 24));
                ms.WriteByte((byte)(val >> 16));
                ms.WriteByte((byte)(val >> 8));
                ms.WriteByte((byte)(val));
            }
            void WriteChunk(string tag, byte[] data)
            {
                WriteU32BE((uint)data.Length);
                Write(System.Text.Encoding.ASCII.GetBytes(tag));
                Write(data);
                uint crc = CRC32(System.Text.Encoding.ASCII.GetBytes(tag), data);
                WriteU32BE(crc);
            }

            Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            byte[] ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 8); ihdr[1] = (byte)w;
            ihdr[2] = (byte)(h >> 8); ihdr[3] = (byte)h;
            ihdr[4] = 8; ihdr[5] = 2; ihdr[6] = 0; ihdr[7] = 0; ihdr[8] = 0;
            WriteChunk("IHDR", ihdr);

            byte[] raw = new byte[h * (1 + w * 3)];
            for (int y = 0; y < h; y++)
            {
                int row = y * (1 + w * 3);
                raw[row] = 0;
                for (int x = 0; x < w; x++)
                {
                    int px = row + 1 + x * 3;
                    raw[px] = pixels[(y * w + x) * 3];
                    raw[px + 1] = pixels[(y * w + x) * 3 + 1];
                    raw[px + 2] = pixels[(y * w + x) * 3 + 2];
                }
            }

            byte[] compressed;
            using (var cms = new MemoryStream())
            {
                using (var ds = new System.IO.Compression.DeflateStream(cms, System.IO.Compression.CompressionLevel.Fastest, true))
                    ds.Write(raw, 0, raw.Length);
                compressed = cms.ToArray();
            }
            WriteChunk("IDAT", compressed);
            WriteChunk("IEND", Array.Empty<byte>());

            return ms.ToArray();
        }
    }
}
