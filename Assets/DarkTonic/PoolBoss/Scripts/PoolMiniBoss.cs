using System.Collections.Generic;
using UnityEngine;

namespace DarkTonic.PoolBoss
{
    /// <summary>
    /// Pool Mini Boss allows you to have groups of prefabs or Addressables that are only in Pool Boss during the time the Pool Mini Boss objects is enabled.
    /// </summary>
    public class PoolMiniBoss : MonoBehaviour
    {
        /*! \cond PRIVATE */

        public bool createOnEnable = true;
        public bool removeOnDisable = true;
        public List<PoolBossItem> poolItems = new List<PoolBossItem>();
        public List<PoolBossCategory> _categories = new List<PoolBossCategory> {
            new PoolBossCategory()
        };

        public int framesForInit = 1;

        public string newCategoryName = "New Category";
        public string addToCategoryName = "New Category";
        public PoolBoss.PrefabSource newItemPrefabSource = PoolBoss.PrefabSource.Prefab;

        public enum CreationPhase
        {
            NotStarted,
            Started,
            AllQueued,
            Created
        }
        /*! \endcond */

        private CreationPhase _creationPhase = CreationPhase.NotStarted;
        private int _itemsRemainingToCreate;
        private int _itemsStartedInit;
        private float _itemsToInitPerFrame;
        private int _initFrameStart;
        private int _lastFramInitContinued;

        void OnEnable()
        {
            CreateItemsIfReady(); // create in Enable event if it's all ready
        }

        // ReSharper disable once UnusedMember.Local
        void Start()
        {
            CreateItemsIfReady(); // if it wasn't ready in Enable, create everything in Start
        }

        // ReSharper disable once UnusedMember.Local
        void OnDisable()
        {
            // scene changing
            if (!removeOnDisable)
            {
                // nothing to do.
                return;
            }

            if (PoolBoss.Instance != null)
            {
                RemoveItems();
            }
        }

        private void CreateItemsIfReady()
        {
            if (PoolBoss.Instance == null)
            {
                return;
            }

            if (createOnEnable && _creationPhase == CreationPhase.NotStarted)
            {
                CreateItems();
            }
        }

        /// <summary>
        /// This method will begin creating the items configured in Pool Mini-Boss in the Pool Boss Game Object
        /// </summary>
        public void CreateItems()
        {
            if (_creationPhase != CreationPhase.NotStarted)
            {
                Debug.LogWarning("PoolMiniBoss '" + transform.name +
                                 "' has already created its items. Cannot create again.");
                return;
            }

            _creationPhase = CreationPhase.Started;

            // create categories
            for (var i = 0; i < _categories.Count; i++)
            {
                var aCat = _categories[i];
                var matchingCat = PoolBoss.Instance._categories.Find(delegate (PoolBossCategory cat)
                {
                    return cat.CatName == aCat.CatName;
                });
                if (matchingCat != null)
                {
                    continue;
                }

                PoolBoss.Instance._categories.Add(new PoolBossCategory
                {
                    CatName = aCat.CatName,
                    IsEditing = false,
                    IsExpanded = true,
                    IsTemporary = true,
                    ProspectiveName = aCat.CatName
                });
            }

            _itemsRemainingToCreate = poolItems.Count;

            _lastFramInitContinued = -1;
            _itemsToInitPerFrame = ((float)poolItems.Count) / framesForInit;
            _initFrameStart = Time.frameCount;
        }

        void Update()
        {
            if (_creationPhase != CreationPhase.Started || Time.frameCount <= _lastFramInitContinued)
            {
                return;
            }

            _lastFramInitContinued = Time.frameCount;

            var itemCountToStopAt = poolItems.Count;
            if (framesForInit != 1)
            {
                var framesInitSoFar = Time.frameCount - _initFrameStart + 1;
                itemCountToStopAt = (int)System.Math.Max(framesInitSoFar * _itemsToInitPerFrame, 0);
            }

            for (var p = _itemsStartedInit; p < itemCountToStopAt; p++)
            {
                CreateSingleItem();
            }
        }

        private void CreateSingleItem()
        {
            var anItem = poolItems[_itemsStartedInit];
            PoolBoss.CreateNewPoolItem(anItem.prefabTransform,
                anItem.instancesToPreload,
                anItem.allowInstantiateMore,
                anItem.itemHardLimit,
                anItem.logMessages,
                anItem.categoryName,
                anItem.prefabSource,
#if ADDRESSABLES_ENABLED
                anItem.prefabAddressable,
#endif
                true,
                ItemCreated);

            _itemsStartedInit++;
            
            if (_itemsStartedInit >= poolItems.Count)
            {
                _creationPhase = CreationPhase.AllQueued;
            }
        }

        private void ItemCreated()
        {
            _itemsRemainingToCreate--;

            if (_itemsRemainingToCreate > 0 || _creationPhase == CreationPhase.Created)
            {
                return;
            }

            _creationPhase = CreationPhase.Created;
        }

        /// <summary>
        /// This method will remove the Sound Groups, Variations, buses, ducking triggers and Playlist objects specified in the Dynamic Sound Group Creator's Inspector. It is called automatically if you check the "Auto-remove Items" checkbox, otherwise you will need to call this method manually.
        /// </summary>
        public void RemoveItems()
        {
            var maxIterations = poolItems.Count;
            for (var i = 0; i < poolItems.Count; i++)
            {
                var anItem = poolItems[i];

                Transform itemTransform = null;
                switch (anItem.prefabSource)
                {
                    case PoolBoss.PrefabSource.Prefab:
                        itemTransform = anItem.prefabTransform;
                        break;
#if ADDRESSABLES_ENABLED
                    case PoolBoss.PrefabSource.Addressable:
                        var itemGO = PoolBoss.GetAddressablePoolItem(anItem.prefabAddressable);
                        if (itemGO != null)
                        {
                            itemTransform = itemGO.transform;
                        }
                        break;
#endif
                }

                if (itemTransform != null)
                {
                    // despawn items
                    PoolBoss.DespawnAllOfPrefab(itemTransform);

                    // REMOVE items
                    PoolBoss.DestroyPoolItem(itemTransform, anItem.prefabSource
#if ADDRESSABLES_ENABLED
                    , anItem.prefabAddressable
#endif
                    );
                }

                maxIterations--;
                if (maxIterations < 0)
                {
                    break;
                }
            }

            // remove categories
            for (var i = 0; i < _categories.Count; i++)
            {
                var aCat = _categories[i];
                var matchingCat = PoolBoss.Instance._categories.Find(delegate (PoolBossCategory cat)
                {
                    return cat.CatName == aCat.CatName && cat.IsTemporary == true;
                });
                if (matchingCat == null)
                {
                    continue;
                }

                // check if any items still use this category
                var itemsWithCategory = PoolBoss.Instance.poolItems.FindAll(delegate (PoolBossItem item)
                {
                    return item.categoryName == matchingCat.CatName;
                });

                if (itemsWithCategory.Count > 0)
                {
                    Debug.LogWarning("PoolMiniBoss can't delete category '" + matchingCat.CatName + "' because there are " +
                        itemsWithCategory.Count + " item(s) in the pool assigned to that category.");

                    return;
                }

                PoolBoss.Instance._categories.Remove(matchingCat);
            }

            _creationPhase = CreationPhase.NotStarted; // so you can create again on next enable
            _itemsStartedInit = 0;
        }
    }
}