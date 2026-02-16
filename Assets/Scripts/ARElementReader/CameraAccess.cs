using UnityEngine;
using UnityEngine.Android;

public class CameraPermissionRequester : MonoBehaviour
{
    private void Start()
    {
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            Debug.Log("[Permission] Camera permission requested");
        }
        else
        {
            Debug.Log("[Permission] Camera already authorized");
        }
#endif

#if PLATFORM_IOS
        // iOS handles this via Info.plist automatically
        Debug.Log("[Permission] iOS will prompt via Info.plist");
#endif
    }
}