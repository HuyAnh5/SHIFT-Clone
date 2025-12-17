/*! \cond PRIVATE */
using UnityEngine;

namespace DarkTonic.PoolBoss.Examples
{
	public class PB_Instructions : MonoBehaviour
	{
		public Transform robotKylePrefab;

		private Vector3 spawnPos = new Vector3(-150, 0, 150);

		public void Spawn10()
		{
			for (var i = 0; i < 10; i++)
			{
				PoolBoss.SpawnInPool(robotKylePrefab, spawnPos, robotKylePrefab.rotation);
				spawnPos += new Vector3(40, 0, 0);
			}

			spawnPos.y = 0;
			spawnPos.z -= 100;
			spawnPos.x = -150;
		}

		public void DespawnAll()
        {
            PoolBoss.DespawnAllOfPrefab(robotKylePrefab);
            spawnPos = new Vector3(-150, 0, 150);
        }
	}
}
/*! \endcond */