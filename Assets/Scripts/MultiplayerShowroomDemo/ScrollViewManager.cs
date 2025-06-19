using UnityEngine;
using System.Collections.Generic;

public class ScrollViewManager : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab; // your scroll item button prefab
    [SerializeField] private Transform contentParent; // usually Content under Scroll View
    [SerializeField] private List<string> scrollViewEntries;

    [Header("Scene Loader on press")]
    [SerializeField] private bool areButtonsLoadingNextScene = false;
    [SerializeField] private int nextSceneIndex = 1; // NOTE: set -1 if you don't intend to change scene

    private void Start()
    {
        for (int i = 0; i < scrollViewEntries.Count; i++)
        {
            GameObject btnObj = Instantiate(buttonPrefab, contentParent);
            ScrollItemButton btn = btnObj.GetComponent<ScrollItemButton>();
            if (!btn)
            {
                btn = btnObj.AddComponent<ScrollItemButton>();
            }

            if (areButtonsLoadingNextScene)
            {
                btn.SetData(i, scrollViewEntries[i], nextSceneIndex);
            }
            else
            {
                btn.SetData(i, scrollViewEntries[i]);
            }
            
        }
    }
}
