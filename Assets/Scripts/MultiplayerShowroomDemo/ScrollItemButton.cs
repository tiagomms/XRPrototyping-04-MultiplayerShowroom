using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Utils;

public class ScrollItemButton : MonoBehaviour
{
    private int itemIndex;
    private string itemName;
    private int sceneIndex;

    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    [SerializeField] private SceneItemRuntimeHolder sceneItemRuntimeHolder;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();

        if (sceneItemRuntimeHolder == null)
        {
            var item = Resources.Load<SceneItemRuntimeHolder>(ConstantPaths.RESOURCES_MULTIPLAYER);
        }
    }

    private void GoToNextScene()
    {
        // store data heading to next scene
        sceneItemRuntimeHolder.SetDataLocally(itemIndex, itemName);

        if (sceneIndex >= 0)
            SceneManager.LoadScene(sceneIndex);
    }

    // Optional: expose this so you can initialize dynamically
    public void SetData(int index, string name, int sceneIndex)
    {
        itemIndex = index;
        itemName = name;
        this.sceneIndex = sceneIndex;
        label.text = $"{(index+1).ToString("0.##")}. {name}";

        if (sceneIndex >= 0)
        {
            button.onClick.AddListener(GoToNextScene);
        }
    }

    // Optional: expose this so you can initialize dynamically
    // to ignore scene transition
    public void SetData(int index, string name)
    {
        SetData(index, name, -1);
    }
}
