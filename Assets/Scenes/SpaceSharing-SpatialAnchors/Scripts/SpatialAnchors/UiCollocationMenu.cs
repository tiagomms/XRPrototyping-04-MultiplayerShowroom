using TMPro;
using UnityEngine;

/// <summary>
/// Handles UI-related functionality for the collocation system
/// </summary>
public class UiCollocationMenu : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI logText;

    [SerializeField]
    private TextMeshProUGUI pageText;

    public TextMeshProUGUI LogText => logText;
    public TextMeshProUGUI PageText => pageText;

} 