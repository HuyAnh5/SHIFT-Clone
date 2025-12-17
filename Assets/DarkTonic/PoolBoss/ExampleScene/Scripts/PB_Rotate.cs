/*! \cond PRIVATE */
using UnityEngine;

namespace DarkTonic.PoolBoss.Examples
{
	public class PB_Rotate : MonoBehaviour
	{
		// Update is called once per frame
		void Update()
		{
			this.transform.Rotate(Vector3.up * 200 * Time.deltaTime);
		}
	}
}
/*! \endcond */