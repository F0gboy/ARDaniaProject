using UnityEngine;

public class MarkerOverlayRenderer : MonoBehaviour
{
    public static MarkerOverlayRenderer Instance;

    [Header("Overlay Camera")]
    public Camera overlayCamera;

    [Header("Prefab Holder (parent object)")]
    public Transform overlayRoot;

    [Header("Depth from Camera")]
    public float overlayDepth = 1f;

    private void Awake()
    {
        Instance = this;
    }

    public GameObject SpawnElement(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, overlayRoot);
        obj.layer = LayerMask.NameToLayer("ElementOverlay");
        return obj;
    }

    public void MoveElement(GameObject obj, Vector2 screenPos)
    {
        Vector3 pos = overlayCamera.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, overlayDepth));

        obj.transform.position = pos;
    }
}
