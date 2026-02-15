using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class TrackedImageElementSpawner : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager imageManager;

    private void OnEnable()
    {
        imageManager.trackedImagesChanged += OnChanged;
    }

    private void OnDisable()
    {
        imageManager.trackedImagesChanged -= OnChanged;
    }

    private void OnChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var tracked in args.added)
            HandleMarker(tracked);

        foreach (var tracked in args.updated)
            HandleMarker(tracked);
    }

    private void HandleMarker(ARTrackedImage tracked)
    {
        string markerName = tracked.referenceImage.name;
        Vector3 pos = tracked.transform.position;

        Debug.Log("Detected marker: " + markerName);

        int markerId = MarkerNameToId(markerName);

        MarkerElementManager.Instance.RegisterMarker(markerId, 0, pos);
    }

    private int MarkerNameToId(string name)
    {
        return name switch
        {
            "marker1" => 1,
            "marker2" => 2,
            "marker3" => 3,
            "marker4" => 4,
            "marker5" => 5,
            "marker6" => 6,
            _ => -1
        };
    }
}
