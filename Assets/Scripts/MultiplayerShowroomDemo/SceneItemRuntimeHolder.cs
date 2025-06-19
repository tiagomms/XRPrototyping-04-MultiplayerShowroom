using UnityEngine;

// TODO: in the future this should hold another scripable object with relevant data
// This becomes just the runtime holder of all scriptable objects
[CreateAssetMenu(fileName = "SceneRuntimeSelectedItem", menuName = "Custom/Scene Runtime Holder")]
public class SceneItemRuntimeHolder : ScriptableObject
{
    public int index;
    public string objID;

    // TODO: instead of loading locally, it should load from photon
    public void SetDataLocally(int newIndex, string newObjID)
    {
        index = newIndex;
        objID = newObjID;
    }

    public void Clear()
    {
        index = -1;
        objID = "";
    }
}
