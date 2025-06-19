using UnityEngine;

/// <summary>
/// FIXME: this is the demo version - in the real version we should get the data online
/// </summary>
public class GroupObjectDisplayerDemo : BaseGroupObjectDisplayer
{

    private void Start()
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
    public override void Initialize()
    {
        // Defensive fallback
        if (parentObj == null || parentObj.childCount == 0)
        {
            Debug.LogWarning("No content group or children found.");
            return;
        }

        // Loop through all children
        // NOTE: this is a way to guarantee if there is a index / contentGroup demo cases mismatch
        int indexFiltered = multiplayerRuntimeData.index % parentObj.childCount;
        GameObject child = parentObj.GetChild(indexFiltered).gameObject;
        child.SetActive(true);
        ObjectLoad(child);
    }
}
