using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanetObject : MonoBehaviour 
{
    public SFXType _sfx;
    public enum SFXType
    {
        Ray,
        Lightning
    };

    public void MakeSFX()
    {
        GameObject sfx;
        switch (_sfx)
        {
            case SFXType.Ray:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("SFX");
                break;
            case SFXType.Lightning:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("SFX2");
                break;

            default:
                sfx = ObjectPooler.SharedInstance.GetPooledObject("SFX");
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
