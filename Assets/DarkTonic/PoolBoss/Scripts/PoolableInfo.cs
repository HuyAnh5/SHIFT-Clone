using UnityEngine;

/*! \cond PRIVATE */
// ReSharper disable once CheckNamespace
namespace DarkTonic.PoolBoss {
	public class PoolableInfo : MonoBehaviour
    {
        public bool AllowInScenePoolables = true;
        public string poolItemName = string.Empty;
	
	    void OnSpawned() {
            if (AllowInSceneRegistering) {
                PoolBoss.UnregisterNonStartInScenePoolable(this);
            }
        }

		void OnEnable() {
            if (AllowInSceneRegistering) {
                PoolBoss.RegisterPotentialInScenePoolable(this);
            }
        }

	    void OnDisable() {
            if (AllowInSceneRegistering)
            {
                PoolBoss.UnregisterNonStartInScenePoolable(this);
            }
        }

        void Reset() {
			if (!Application.isPlaying) {
				FindPoolItemName();
			}
		}
		
		public void FindPoolItemName() {
			if (!string.IsNullOrEmpty(poolItemName)) {
				return;
			}
			
			poolItemName = PoolBoss.GetPrefabShortName(name);
		}
		
		/// <summary>
		/// This will get called instead by other scripts if you already know the name
		/// </summary>
		/// <param name="itemName"></param>
		public void SetPoolItemName(string itemName) {
			poolItemName = itemName;
		}
		
		public string ItemName {
			get {
				if (string.IsNullOrEmpty(poolItemName)) {
					FindPoolItemName();
				}
				
				return poolItemName;
			}
		}

        private bool AllowInSceneRegistering
        {
            get
            {
				if (PoolBoss.Instance != null && PoolBoss.Instance.allowInScenePoolables && AllowInScenePoolables)
                {
                    return true;
                }

                return false;
            }
        }
	}
}
/*! \endcond */
