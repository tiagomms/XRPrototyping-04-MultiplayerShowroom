using UnityEngine;

/// <summary>
/// FIXME: this is the demo version - in the real version we should get the data online
/// </summary>
public abstract class BaseGroupObjectDisplayer : MonoBehaviour
{
    [Header("Assign the parent GameObject where objects should be loaded into")]
    [SerializeField] protected Transform parentObj;
    [SerializeField] protected SceneItemRuntimeHolder multiplayerRuntimeData;

    protected void OnValidate()
    {
        if (parentObj == null)
        {
            parentObj = this.gameObject.transform;
        }
    }

    public virtual void Initialize()
    {
    }

    public virtual void ObjectLoad(GameObject obj)
    {
        if (multiplayerRuntimeData.objID != "")
        {
            obj.name = multiplayerRuntimeData.objID;
        }
        // TODO: check/assign component to do smart object placement based on room
    }

    public virtual void HideObjects()
    {
        if (parentObj == null || parentObj.childCount == 0)
        {
            Debug.LogWarning("No content group or children found.");
            return;
        }

        for (int i = 0; i < parentObj.childCount; i++)
        {
            GameObject child = parentObj.GetChild(i).gameObject;
            child.SetActive(false);
        }
    }
}