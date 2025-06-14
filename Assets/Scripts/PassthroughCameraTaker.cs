using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using PassthroughCameraSamples;
using TMPro;
using UnityEngine.Events;
using System;
using UnityEngine.InputSystem;

public class PassthroughCameraTaker : MonoBehaviour
{
    // TODO: later on change this section
    [Header("Input")]
    [SerializeField] private InputActionReference inputToTakePhoto;

    [Header("Camera Access")]
    public WebCamTextureManager webcamManager;
    private Texture2D currentPicture;


    [Header("Debug")]
    [SerializeField] private List<Texture2D> debugFakePictureList;
    private int _debugFakePictureIndex;

    public UnityEvent<Texture2D> onPictureTaken;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // TODO: Different event based on actual trigger
        inputToTakePhoto.action.started += TakePhoto;
    }

    private void OnDestroy() {
        inputToTakePhoto.action.started -= TakePhoto;
    }

    private void TakePhoto(InputAction.CallbackContext context)
    {
#if !UNITY_EDITOR
        TakePicture();
#else
        PlaceFakePicture();
#endif
        onPictureTaken?.Invoke(currentPicture);
    }

    public void PlaceFakePicture()
    {
        currentPicture = debugFakePictureList[_debugFakePictureIndex];
        _debugFakePictureIndex = (_debugFakePictureIndex + 1) % debugFakePictureList.Count;
    }

    public void TakePicture()
    {
        // TODO: crop image based on gesture
        int width = webcamManager.WebCamTexture.width;
        int height = webcamManager.WebCamTexture.height;

        if (currentPicture == null)
        {
            currentPicture = new Texture2D(width, height);
        }

        Color32[] pixels = new Color32[width * height];
        webcamManager.WebCamTexture.GetPixels32(pixels);

        currentPicture.SetPixels32(pixels);
        currentPicture.Apply();

    }


}

