#pragma warning disable CS8632
using System;
using UnityEngine;

namespace ShelfMod
{
    public class ShelfSnapZone : MonoBehaviour
    {
        public ShelfSnapZone(IntPtr ptr) : base(ptr) { }

        public int TierIndex;
        public int SlotIndex;
        public int TotalTiers;
        public int DepthRowIndex;
        public int TotalDepthRows;
        public int StackIndex;
        public int TotalStackLevels;
        public Transform SlotCenter;
        public Vector3 SlotSize;
        public Vector3 SlotWorldOffset;

        private bool _occupied;
        private GameObject _snappedItem;

        public bool IsOccupied
        {
            get { return _occupied; }
        }

        public GameObject SnappedItem
        {
            get { return _snappedItem; }
        }

        public bool TrySnap(GameObject item)
        {
            if (_occupied || item == null)
                return false;

            _occupied = true;
            _snappedItem = item;

            Il2Cpp.UsableObject uo = item.GetComponent<Il2Cpp.UsableObject>();

            Il2Cpp.PlayerManager.ObjectInHand objType = Il2Cpp.PlayerManager.ObjectInHand.None;
            string itemName = item.name;
            if (uo != null)
            {
                objType = uo.objectInHandType;
                if (uo.item != null)
                    itemName = uo.item.itemName;
                uo.DropObject();
                uo.objectInHands = false;
            }

            item.transform.SetParent(SlotCenter, false);

            if (TotalStackLevels > 1)
            {
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }
            else
            {
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;

                Renderer rend = item.GetComponentInChildren<Renderer>();
                if (rend != null && SlotCenter != null)
                {
                    float itemH = rend.bounds.size.y;
                    float yBot = 0.5f, yMid1 = 0.15f, yMid2 = -0.15f, yTop = -0.5f;

                    GetOffsetMultipliers(objType, itemName, out yBot, out yMid1, out yMid2, out yTop);

                    int lastTier = TotalTiers > 0 ? TotalTiers - 1 : 3;
                    float y;
                    if (TierIndex == 0)
                        y = itemH * yBot;
                    else if (TierIndex == 1)
                        y = itemH * yMid1;
                    else if (TierIndex == lastTier)
                        y = itemH * yTop;
                    else
                        y = itemH * yMid2;
                    item.transform.localPosition = new Vector3(0f, y, 0f);
                }
            }

            FreezeItem(item);

            return true;
        }

        public GameObject ReleaseItem()
        {
            if (!_occupied || _snappedItem == null)
                return null;

            GameObject released = _snappedItem;
            released.transform.SetParent(null);

            UnfreezeItem(released);

            _occupied = false;
            _snappedItem = null;

            return released;
        }

        internal static void GetOffsetMultipliers(Il2Cpp.PlayerManager.ObjectInHand objType, string itemName,
            out float yBot, out float yMid1, out float yMid2, out float yTop)
        {
            if (itemName == null) itemName = "";

            switch (objType)
            {
                case Il2Cpp.PlayerManager.ObjectInHand.CableSpinner:
                    yBot  =  0.75f;
                    yMid1 =  0.25f;
                    yMid2 = -0.25f;
                    yTop  = -0.75f;
                    return;
            }

            string lower = itemName.ToLowerInvariant();
            if (lower.Contains("cable") || lower.Contains("spool") || lower.Contains("spinner"))
            {
                yBot  =  0.75f;
                yMid1 =  0.25f;
                yMid2 = -0.25f;
                yTop  = -0.75f;
                return;
            }
            if (lower.Contains("switch16cu"))
            {
                yBot  =  4.5f;
                yMid1 = -1.0f;
                yMid2 = -5.5f;
                yTop  = -10.5f;
                return;
            }

            yBot  =  8.0f;
            yMid1 = -2.5f;
            yMid2 = -9.0f;
            yTop  = -16.5f;
        }

        internal static bool IsSmallItem(GameObject item)
        {
            if (item == null) return false;

            Il2Cpp.UsableObject uo = item.GetComponent<Il2Cpp.UsableObject>();
            if (uo != null && uo.item != null)
            {
                string name = uo.item.itemName.ToLowerInvariant();
                if (name.Contains("sftp") || name.Contains("box") || name.Contains("small")
                    || name.Contains("cable") || name.Contains("spool") || name.Contains("spinner"))
                    return true;
            }

            Renderer rend = item.GetComponentInChildren<Renderer>();
            if (rend == null) return false;
            Vector3 size = rend.bounds.size;
            return size.z < 0.25f && size.x < 0.40f;
        }

        private static void FreezeItem(GameObject item)
        {
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private static void UnfreezeItem(GameObject item)
        {
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }
}
