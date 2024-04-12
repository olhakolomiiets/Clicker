using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanetObject : MonoBehaviour 
{
    public SFXType _sfx;
    public enum SFXType
    {
        Ray,
        BigRay,
        Lightning 
    };

    public void MakeSFX()
    {
        GameObject sfx;
        switch (_sfx)
        {
            case SFXType.Ray:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("VFX1");
                break;
            case SFXType.BigRay:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("VFX2");
                break;
            case SFXType.Lightning:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("VFX3");
                break;

            default:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("VFX1");
                break;
        }

        if (sfx != null)
        {
            sfx.transform.parent = null;
            sfx.transform.localScale = new Vector3(1, 1, 1);
            sfx.transform.SetParent(this.transform);

            sfx.transform.localPosition = Vector3.zero;
            sfx.transform.localEulerAngles = Vector3.zero;
            sfx.SetActive(true);
            StartCoroutine(DisableSFX(sfx));
        }
    }

    private IEnumerator DisableSFX(GameObject sfx)
    {
        yield return new WaitForSeconds(1.5f);
        sfx.SetActive(false);
    }
}
