using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MarkerElementManager : MonoBehaviour
{
    public static MarkerElementManager Instance { get; private set; }

    public Camera arCamera;
    public ARRaycastManager arRaycastManager;
    public ARCameraManager arCameraManager;

    public GameObject firePrefab;
    public GameObject waterPrefab;
    public GameObject windPrefab;
    public GameObject earthPrefab;
    public GameObject steamPrefab;
    public GameObject mistPrefab;
    public GameObject mudPrefab;
    public GameObject dustPrefab;
    public GameObject lavaPrefab;

    private readonly Dictionary<int, ElementInstance> activeMarkers = new Dictionary<int, ElementInstance>();
    private readonly List<ARRaycastHit> rayHits = new List<ARRaycastHit>();

    private readonly Dictionary<(ElementType, ElementType), ElementType> combinations =
        new Dictionary<(ElementType, ElementType), ElementType>
        {
            {(ElementType.Fire,  ElementType.Water), ElementType.Steam},
            {(ElementType.Water, ElementType.Fire),  ElementType.Steam},
            {(ElementType.Fire,  ElementType.Wind), ElementType.Mist},
            {(ElementType.Wind,  ElementType.Fire), ElementType.Mist},
            {(ElementType.Earth, ElementType.Water), ElementType.Mud},
            {(ElementType.Water, ElementType.Earth), ElementType.Mud},
            {(ElementType.Wind,  ElementType.Earth), ElementType.Dust},
            {(ElementType.Earth, ElementType.Wind),  ElementType.Dust},
             {(ElementType.Fire, ElementType.Earth),  ElementType.Lava}
        };

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterMarker(int markerId, int rotationDeg, Vector2 screenPos, PointF[] srcQuad)
    {
        ElementType element = MarkerIdToElement(markerId);

        Vector3 worldPos;
        TrackableType mask = TrackableType.FeaturePoint | TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated;

        if (arRaycastManager.Raycast(screenPos, rayHits, mask))
            worldPos = rayHits[0].pose.position;
        else
        {
            Ray ray = arCamera.ScreenPointToRay(screenPos);
            worldPos = ray.origin + ray.direction * 0.5f;
        }

        if (!activeMarkers.TryGetValue(markerId, out ElementInstance instance))
        {
            instance = new ElementInstance
            {
                id = markerId,
                element = element,
                screenPosition = screenPos,
                worldObject = Instantiate(GetPrefabForElement(element), arCamera.transform.parent)
            };

            activeMarkers[markerId] = instance;
            Debug.Log($"[Marker] Spawned element {element} for marker {markerId}");
        }

        instance.screenPosition = screenPos;
        instance.worldObject.transform.position = worldPos;

        if (arCameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics))
        {
            float fx = intrinsics.focalLength.x;
            float distance = Vector3.Distance(arCamera.transform.position, worldPos);

            float pixelWidth = Vector2.Distance(
                new Vector2(srcQuad[0].X, srcQuad[0].Y),
                new Vector2(srcQuad[1].X, srcQuad[1].Y)
            );

            float worldWidth = (pixelWidth * distance) / fx;

            if (!float.IsInfinity(worldWidth) && !float.IsNaN(worldWidth))
                instance.worldObject.transform.localScale = new Vector3(worldWidth, worldWidth, worldWidth);
        }

        instance.worldObject.transform.LookAt(arCamera.transform);

        TryAutoCombine();
    }

    private void TryAutoCombine()
    {
        if (activeMarkers.Count < 2)
            return;

        var keys = new List<int>(activeMarkers.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                ElementInstance a = activeMarkers[keys[i]];
                ElementInstance b = activeMarkers[keys[j]];

                if (!combinations.TryGetValue((a.element, b.element), out ElementType result))
                    continue;

                Debug.Log($"[Combine] {a.element} + {b.element} → {result}");

                Vector2 midScreen = (a.screenPosition + b.screenPosition) / 2f;

                Vector3 worldPos;
                TrackableType mask = TrackableType.FeaturePoint | TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated;

                if (arRaycastManager.Raycast(midScreen, rayHits, mask))
                    worldPos = rayHits[0].pose.position;
                else
                {
                    Ray ray = arCamera.ScreenPointToRay(midScreen);
                    worldPos = ray.origin + ray.direction * 0.5f;
                }

                Destroy(a.worldObject);
                Destroy(b.worldObject);

                activeMarkers.Remove(a.id);
                activeMarkers.Remove(b.id);

                int newId = Random.Range(10000, 99999);

                GameObject newObj = Instantiate(GetPrefabForElement(result), arCamera.transform.parent);
                newObj.transform.position = worldPos;
                newObj.transform.LookAt(arCamera.transform);

                activeMarkers[newId] = new ElementInstance
                {
                    id = newId,
                    element = result,
                    screenPosition = midScreen,
                    worldObject = newObj
                };

                Debug.Log($"[Combine] Spawned combined element {result}");

                return;
            }
        }
    }


    private GameObject GetPrefabForElement(ElementType type)
    {
        return type switch
        {
            ElementType.Fire => firePrefab,
            ElementType.Water => waterPrefab,
            ElementType.Wind => windPrefab,
            ElementType.Earth => earthPrefab,
            ElementType.Steam => steamPrefab,
            ElementType.Mist => mistPrefab,
            ElementType.Mud => mudPrefab,
            ElementType.Dust => dustPrefab,
            ElementType.Lava => lavaPrefab,
            _ => null
        };
    }

    private ElementType MarkerIdToElement(int id)
    {
        return id switch
        {
            1 => ElementType.Fire,
            2 => ElementType.Water,
            3 => ElementType.Wind,
            4 => ElementType.Earth,
            _ => ElementType.Unknown
        };
    }

    private class ElementInstance
    {
        public int id;
        public ElementType element;
        public Vector2 screenPosition;
        public GameObject worldObject;
    }

    public enum ElementType
    {
        Unknown,
        Fire,
        Water,
        Wind,
        Earth,
        Steam,
        Mist,
        Mud,
        Dust,
        Lava
    }
}