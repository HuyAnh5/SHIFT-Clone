/*! \cond PRIVATE */
#if ADDRESSABLES_ENABLED
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkTonic.PoolBoss
{
    /// <summary>
    /// This class will handle unloading and load audio data for Addressable Transforms. I use T for Transform only but this could be used for anything.
    /// </summary>
    // ReSharper disable once CheckNamespace 
    public class AddressableTracker<T>
    {
        public AsyncOperationHandle<T> AssetHandle { get; private set; }

        public AddressableTracker(AsyncOperationHandle<T> assetHandle)
        {
            AssetHandle = assetHandle;
        }
    }

    public static class PoolAddressableOptimizer
    {
        // maybe use a different Dictionary for each T you need, or a different similar class
        private static readonly Dictionary<string, AddressableTracker<GameObject>> AddressableTasksByAddressableId = new Dictionary<string, AddressableTracker<GameObject>>();
        private static readonly object _syncRoot = new object(); // to lock below

        public static GameObject RetrieveStoredHandle(AssetReference addressable)
        {
            if (!IsAddressableValid(addressable))
            {
                return null;
            }

            var addressableId = GetAddressableId(addressable);

            if (!AddressableTasksByAddressableId.ContainsKey(addressableId))
            {
                return null;
            }

            var gameObjectReference = AddressableTasksByAddressableId[addressableId].AssetHandle.Result;

            return gameObjectReference;
        }

        /// <summary>
        /// Start Coroutine when calling this, passing in success and failure action delegates.
        /// </summary>
        /// <param name="addressable"></param>
        /// <param name="successAction"></param>
        /// <param name="failureAction"></param>
        /// <returns></returns>
        public static IEnumerator LoadOrReturnTransformAsset(PoolBossItem poolItem, System.Action itemCreatedCallback,
            System.Action<PoolBossItem, Transform, System.Action> successAction, System.Action failureAction)
        {

            var addressable = poolItem.prefabAddressable;

            if (!IsAddressableValid(addressable))
            {
                if (failureAction != null)
                {
                    failureAction();
                }
                yield break;
            }

            var addressableId = GetAddressableId(addressable);

            AsyncOperationHandle<GameObject> loadHandle;
            GameObject gameObjectReference;
            var shouldReleaseLoadedAssetNow = false;

            if (AddressableTasksByAddressableId.ContainsKey(addressableId))
            {
                loadHandle = AddressableTasksByAddressableId[addressableId].AssetHandle;
                gameObjectReference = loadHandle.Result;
            }
            else
            {
                loadHandle = Addressables.LoadAssetAsync<GameObject>(addressable);

                while (!loadHandle.IsDone)
                {
                    yield return PoolBoss.EndOfFrameDelay;
                }

                gameObjectReference = loadHandle.Result;

                if (gameObjectReference == null || loadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    var errorText = "";
                    if (loadHandle.OperationException != null)
                    {
                        errorText = " Exception: " + loadHandle.OperationException.Message;
                    }
                    Debug.LogError("Addressable file for could not be located." + errorText);

                    if (failureAction != null)
                    {
                        failureAction();
                    }
                    yield break;
                }

                lock (_syncRoot)
                {
                    if (!AddressableTasksByAddressableId.ContainsKey(addressableId))
                    {
                        AddressableTasksByAddressableId.Add(addressableId, new AddressableTracker<GameObject>(loadHandle));
                    }
                    else
                    {
                        // race condition reached. Another load finished before this one. Throw this away and use the other, to release memory.
                        shouldReleaseLoadedAssetNow = true;
                        gameObjectReference = AddressableTasksByAddressableId[addressableId].AssetHandle.Result;
                    }
                }
            }

            if (shouldReleaseLoadedAssetNow)
            {
                Addressables.Release(loadHandle);
            }

            if (successAction != null)
            {
                successAction(poolItem, gameObjectReference.transform, itemCreatedCallback);
            }
        }

        public static bool IsAddressableValid(AssetReference addressable)
        {
            if (addressable == null)
            {
                return false;
            }

#if UNITY_EDITOR
            return addressable.editorAsset != null;
#else
                return addressable.RuntimeKeyIsValid();
#endif
        }

        public static void RemoveAddressablePrefab(AssetReference addressable)
        {
            if (!IsAddressableValid(addressable))
            {
                return;
            }

            ReleaseAddressableIfNoUses(addressable);
        }

        private static void ReleaseAddressableIfNoUses(AssetReference addressable)
        {
            var addressableId = GetAddressableId(addressable);
            if (!AddressableTasksByAddressableId.ContainsKey(addressableId))
            {
                return;
            }

            ReleaseAddressable(addressableId);
        }

        #region Helper methods

        private static void ReleaseAddressable(string addressableId)
        {
            if (!AddressableTasksByAddressableId.ContainsKey(addressableId))
            {
                return;
            }

            var tracker = AddressableTasksByAddressableId[addressableId];

            var deadHandle = tracker.AssetHandle;
            AddressableTasksByAddressableId.Remove(addressableId);
            Addressables.Release(deadHandle);
        }

        private static string GetAddressableId(AssetReference addressable)
        {
            return addressable.RuntimeKey.ToString();
        }

        #endregion
    }
}
#endif
/*! \endcond */
