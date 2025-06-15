using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadedController : MonoBehaviour
{
    [Header("Objects to activate on scene load")]
    [SerializeField] private List<GameObject> gameObjectsToActivate = new();

    [Header("Objects to deactivate on scene load")]
    [SerializeField] private List<GameObject> gameObjectsToDeactivate = new();

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(EnsureCorrectPosition(scene));
    }

    private IEnumerator EnsureCorrectPosition(Scene scene)
    {
        yield return new WaitForSeconds(1f); // wait a frame
        foreach (var go in gameObjectsToActivate)
        {
            if (go != null)
                go.SetActive(true);
        }

        foreach (var go in gameObjectsToDeactivate)
        {
            if (go != null)
                go.SetActive(false);
        }

        Sampleton.Log($"[{nameof(SceneLoadedController)}] Scene loaded: {scene.name} — toggled UI states.");
    }
}
