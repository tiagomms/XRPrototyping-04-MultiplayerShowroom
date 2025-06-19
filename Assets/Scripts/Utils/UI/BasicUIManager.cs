using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Assign all UI panels here (Main Menu should be first)")]
    [SerializeField] private List<GameObject> uiPanels;

    private Stack<GameObject> navigationStack = new Stack<GameObject>();
    private GameObject currentUI;

    private void Start()
    {
        // Hide all panels initially
        foreach (var panel in uiPanels)
            panel.SetActive(false);

        // Show first panel (assumed to be main menu)
        if (uiPanels.Count > 0)
        {
            currentUI = uiPanels[0];
            currentUI.SetActive(true);
        }
    }

    public void GoTo(GameObject nextUI)
    {
        if (nextUI == null || nextUI == currentUI)
            return;

        if (currentUI != null)
        {
            currentUI.SetActive(false);
            navigationStack.Push(currentUI);
        }

        nextUI.SetActive(true);
        currentUI = nextUI;
    }

    public void GoBack()
    {
        if (navigationStack.Count == 0)
            return;

        if (currentUI != null)
            currentUI.SetActive(false);

        currentUI = navigationStack.Pop();
        currentUI.SetActive(true);
    }
}
