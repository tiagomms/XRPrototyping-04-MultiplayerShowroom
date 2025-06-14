using UnityEngine;
using PassthroughCameraSamples;
using System;
using System.Runtime.CompilerServices;

public class PassthroughCameraDisplay : MonoBehaviour
{
    public WebCamTextureManager webcamManager;
    public Renderer quadRenderer;
    public string textureName;
    public float quadDistance = 1;

    private Vector3 originalScale;

    [SerializeField] private PassthroughCameraTaker passthroughCameraTaker;

    [Header("Debug")]
    [SerializeField] private Transform cameraTransform; 

    private Texture2D _currentPicture;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalScale = quadRenderer.transform.localScale;
        quadRenderer.gameObject.SetActive(false);
        passthroughCameraTaker.onPictureTaken.AddListener(DisplayPhotoOnQuad);
    }
    private void OnDestroy() 
    {
        passthroughCameraTaker.onPictureTaken.RemoveListener(DisplayPhotoOnQuad);
    }

    public void DisplayPhotoOnQuad(Texture2D newPicture)
    {
        _currentPicture = newPicture;
        // FIXME: where to place quad, and how long
        quadRenderer.gameObject.SetActive(true);
        quadRenderer.material.SetTexture(textureName, _currentPicture);
        PlaceQuad();
        /*
#if !UNITY_EDITOR
        PlaceQuadInFrontOfMe();
#else
        DebugPlaceQuadInFrontOfMe();
#endif
        */
    }

    private void PlaceQuad()
    {
        Transform quadTransform = quadRenderer.transform;

        float ratio = (float)_currentPicture.height / (float)_currentPicture.width;
        quadTransform.localScale = new Vector3(originalScale.x, originalScale.y * ratio, 1);
    }

    #region DEPRECATED METHODS
    private void DebugPlaceQuadInFrontOfMe()
    {
        Transform quadTransform = quadRenderer.transform;

        quadTransform.position = cameraTransform.position + quadDistance * cameraTransform.forward;
        quadTransform.rotation = cameraTransform.rotation;

        float quadScale = 1f;
        float ratio = (float)_currentPicture.height / (float)_currentPicture.width;
        quadTransform.localScale = new Vector3(quadScale, quadScale * ratio, 1);
    }

    // TODO: improve based on cropped image later
#if UNITY_ANDROID
    public void PlaceQuadInFrontOfMe()
    {
        Transform quadTransform = quadRenderer.transform;

        Pose cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(PassthroughCameraEye.Left);

        Vector2Int resolution = PassthroughCameraUtils.GetCameraIntrinsics(PassthroughCameraEye.Left).Resolution;
        
        quadTransform.position = cameraPose.position + cameraPose.forward * quadDistance;
        quadTransform.rotation = cameraPose.rotation;

        Ray leftSide = PassthroughCameraUtils.ScreenPointToRayInCamera(PassthroughCameraEye.Left, new Vector2Int(0, resolution.y / 2));
        Ray rightSide = PassthroughCameraUtils.ScreenPointToRayInCamera(PassthroughCameraEye.Left, new Vector2Int(resolution.x, resolution.y / 2));

        float horizontalFov = Vector3.Angle(leftSide.direction, rightSide.direction);

        float quadScale = 2 * quadDistance * Mathf.Tan((horizontalFov * Mathf.Deg2Rad) / 2);

        float ratio = (float)_currentPicture.height / (float)_currentPicture.width;
        
        // maintain quadTransform scale but update ratio
        quadTransform.localScale = new Vector3(originalScale.x * quadScale, originalScale.y * quadScale * ratio, 1);
    }
#endif
    #endregion

}