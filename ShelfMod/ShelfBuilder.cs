#pragma warning disable CS8632
using UnityEngine;

namespace ShelfMod
{
    public static class ShelfBuilder
    {
        private const string SHELF_ROOT_NAME = "ModShelf";

        private static GameObject _gameShelfTemplate;
        private static bool _searchedForTemplate;
        private static Vector3 _templateSize;

        public static GameObject CreateShelf(float width, float depth, int tiers, bool isPreview)
        {
            if (!_searchedForTemplate)
                FindGameShelfTemplate();

            float totalHeight = tiers * 0.35f * 1.5f + 0.1f * 1.5f;
            float tierSpacing = totalHeight / tiers;

            GameObject root = new GameObject(SHELF_ROOT_NAME);

            if (_gameShelfTemplate != null && _templateSize.sqrMagnitude > 0f)
            {
                CreateFromTemplate(root, width, depth, totalHeight);
            }
            else
            {
                CreateFallbackShelf(root, width, depth, totalHeight, tierSpacing, tiers);
            }

            if (!isPreview)
            {
                for (int i = 0; i <= tiers; i++)
                {
                    float zoneY = (i < tiers) ? i * tierSpacing + 0.05f : tiers * tierSpacing + 0.05f;
                    CreateSnapZone(root, width, depth, zoneY, "SnapZone_" + i, i, tiers + 1);
                }
            }

            return root;
        }

        private static void FindGameShelfTemplate()
        {
            _searchedForTemplate = true;

            var allObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "Shelfs")
                {
                    var log = ShelfModMain.Instance.LoggerInstance;
                    log.Msg("[ShelfMod] Found Shelfs object: children=" + obj.transform.childCount);

                    for (int c = 0; c < obj.transform.childCount; c++)
                    {
                        Transform child = obj.transform.GetChild(c);
                        Renderer[] childRenderers = child.GetComponentsInChildren<Renderer>();
                        if (childRenderers.Length > 0)
                        {
                            _gameShelfTemplate = child.gameObject;

                            Bounds b = childRenderers[0].bounds;
                            for (int i = 1; i < childRenderers.Length; i++)
                                b.Encapsulate(childRenderers[i].bounds);
                            _templateSize = b.size;

                            log.Msg("[ShelfMod] Using child: " + child.name
                                + " size=" + _templateSize.ToString("F2")
                                + " renderers=" + childRenderers.Length);

                            foreach (var r in childRenderers)
                            {
                                log.Msg("[ShelfMod]   Renderer: " + r.gameObject.name
                                    + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null")
                                    + " bounds=" + r.bounds.ToString("F2"));
                            }
                            return;
                        }
                    }
                    return;
                }
            }
        }

        private static void CreateFromTemplate(GameObject root, float width, float depth, float totalHeight)
        {
            GameObject visual = Object.Instantiate(_gameShelfTemplate);
            visual.name = "ShelfVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            float sx = width / Mathf.Max(_templateSize.x, 0.01f);
            float sy = totalHeight / Mathf.Max(_templateSize.y, 0.01f);
            float sz = depth / Mathf.Max(_templateSize.z, 0.01f);
            visual.transform.localScale = new Vector3(sx, sy, sz);

            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                visual.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            }
        }

        private static void CreateFallbackShelf(GameObject root, float width, float depth, float totalHeight, float tierSpacing, int tiers)
        {
            var shelfMat = CreateMat(new Color(0.35f, 0.28f, 0.2f));
            var supportMat = CreateMat(new Color(0.25f, 0.25f, 0.27f));

            CreateBox(root, "LeftSupport",
                new Vector3(0.04f, totalHeight, depth),
                new Vector3(-width * 0.5f, totalHeight * 0.5f, 0f),
                supportMat, false);

            CreateBox(root, "RightSupport",
                new Vector3(0.04f, totalHeight, depth),
                new Vector3(width * 0.5f, totalHeight * 0.5f, 0f),
                supportMat, false);

            for (int i = 0; i < tiers; i++)
            {
                float y = i * tierSpacing;
                CreateBox(root, "Tier_" + i,
                    new Vector3(width, 0.03f, depth),
                    new Vector3(0f, y, 0f),
                    shelfMat, true);
            }

            CreateBox(root, "TopPlank",
                new Vector3(width, 0.03f, depth),
                new Vector3(0f, tiers * tierSpacing, 0f),
                shelfMat, true);
        }

        private static void CreateBox(GameObject parent, string name, Vector3 scale, Vector3 localPos, Material mat, bool addCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            if (!addCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null)
                    Object.Destroy(col);
            }

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = mat;
        }

        private static void CreateSnapZone(GameObject parent, float width, float depth, float y, string name, int tierIndex, int totalTiers)
        {
            int slotsPerRow = 5;
            float slotWidth = width / slotsPerRow;
            float slotHeight = 0.40f;
            int depthRows = Mathf.Max(1, Mathf.RoundToInt(depth / 0.25f));
            float rowSpacing = depthRows > 1 ? depth / depthRows : 0f;
            int stackLevels = 2;
            float stackSpacing = slotHeight / stackLevels;

            int slotIdx = 0;
            for (int st = 0; st < stackLevels; st++)
            {
                float stackY = y + (st - (stackLevels - 1) / 2f) * stackSpacing;

                for (int r = 0; r < depthRows; r++)
                {
                    float zOffset = depthRows > 1
                        ? (r - (depthRows - 1) / 2f) * rowSpacing
                        : 0f;

                    for (int s = 0; s < slotsPerRow; s++)
                    {
                        float xOffset = (s - (slotsPerRow - 1) / 2f) * slotWidth;

                        GameObject go = new GameObject("Slot_" + tierIndex + "_" + slotIdx);
                        go.transform.SetParent(parent.transform, false);
                        go.transform.localPosition = new Vector3(xOffset, stackY, zOffset);

                        ShelfSnapZone zone = go.AddComponent<ShelfSnapZone>();
                        zone.TierIndex = tierIndex;
                        zone.SlotIndex = slotIdx;
                        zone.TotalTiers = totalTiers;
                        zone.DepthRowIndex = r;
                        zone.TotalDepthRows = depthRows;
                        zone.StackIndex = st;
                        zone.TotalStackLevels = stackLevels;
                        zone.SlotCenter = go.transform;
                        zone.SlotSize = new Vector3(slotWidth * 0.9f, stackSpacing * 0.9f, (rowSpacing > 0 ? rowSpacing : depth) * 0.9f);
                        zone.SlotWorldOffset = go.transform.position;

                        slotIdx++;
                    }
                }
            }
        }

        public static Vector3 SnapToGrid(Vector3 point, float gridSize)
        {
            return new Vector3(
                Mathf.Round(point.x / gridSize) * gridSize,
                Mathf.Round(point.y / gridSize) * gridSize,
                Mathf.Round(point.z / gridSize) * gridSize
            );
        }

        private static Shader _cachedShader;

        private static Material CreateMat(Color color)
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_cachedShader == null) _cachedShader = Shader.Find("Standard");
            }

            var mat = new Material(_cachedShader);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
