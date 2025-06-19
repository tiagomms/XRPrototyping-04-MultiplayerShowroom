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
        EnsureCorrectPosition(scene);
    }

    private void EnsureCorrectPosition(Scene scene)
    {
        // need to yield an extra frame to properly account for layout recalcs~
        _ = StartCoroutine(coroutine());
        return;

        IEnumerator coroutine()
        {
            yield return null;
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
}
