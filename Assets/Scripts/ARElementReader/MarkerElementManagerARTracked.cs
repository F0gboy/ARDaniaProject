using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class MarkerElementManagerARTracked : MonoBehaviour
{
    public static MarkerElementManagerARTracked Instance { get; private set; }

    [Header("AR")]
    public Camera arCamera;

    [Header("Element Prefabs")]
    public GameObject firePrefab;
    public GameObject waterPrefab;
    public GameObject windPrefab;
    public GameObject earthPrefab;
    public GameObject steamPrefab;
    public GameObject mistPrefab;
    public GameObject mudPrefab;
    public GameObject dustPrefab;
    public GameObject lavaPrefab;

    private ARTrackedImageManager trackedImageManager;

    private readonly Dictionary<string, ElementInstance> activeMarkers =
        new Dictionary<string, ElementInstance>();

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

            {(ElementType.Fire,  ElementType.Earth), ElementType.Lava},
            {(ElementType.Earth, ElementType.Fire),  ElementType.Lava}
        };

    private void Awake()
    {
        Instance = this;
        trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var img in args.added)
            HandleTrackedImage(img);

        foreach (var img in args.updated)
            HandleTrackedImage(img);

        foreach (var img in args.removed)
            RemoveTrackedImage(img);
    }

    private void HandleTrackedImage(ARTrackedImage trackedImage)
    {
        string name = trackedImage.referenceImage.name;
        ElementType element = NameToElement(name);

        if (element == ElementType.Unknown)
            return;

        if (trackedImage.trackingState != TrackingState.Tracking)
        {
            if (activeMarkers.TryGetValue(name, out ElementInstance inst) && inst.worldObject != null)
                inst.worldObject.SetActive(false);
            return;
        }

        Vector3 pos = trackedImage.transform.position;
        Quaternion rot = trackedImage.transform.rotation;

        if (!activeMarkers.TryGetValue(name, out ElementInstance instance))
        {
            GameObject prefab = GetPrefabForElement(element);
            if (prefab == null)
                return;

            GameObject obj = Instantiate(prefab, pos, rot, arCamera.transform.parent);

            instance = new ElementInstance
            {
                id = name,
                element = element,
                worldObject = obj
            };

            activeMarkers[name] = instance;
            Debug.Log($"[Marker] Spawned {element} for image {name}");
        }

        instance.worldObject.SetActive(true);
        instance.worldObject.transform.SetPositionAndRotation(pos, rot);

        float size = trackedImage.size.x;
        instance.worldObject.transform.localScale = new Vector3(size, size, size);

        instance.worldObject.transform.LookAt(arCamera.transform);

        TryAutoCombine();
    }

    private void RemoveTrackedImage(ARTrackedImage trackedImage)
    {
        string name = trackedImage.referenceImage.name;

        if (activeMarkers.TryGetValue(name, out ElementInstance inst))
        {
            if (inst.worldObject != null)
                Destroy(inst.worldObject);

            activeMarkers.Remove(name);
            Debug.Log($"[Marker] Removed {name}");
        }
    }

    private void TryAutoCombine()
    {
        if (activeMarkers.Count < 2)
            return;

        var keys = new List<string>(activeMarkers.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                ElementInstance a = activeMarkers[keys[i]];
                ElementInstance b = activeMarkers[keys[j]];

                if (!combinations.TryGetValue((a.element, b.element), out ElementType result))
                    continue;

                Debug.Log($"[Combine] {a.element} + {b.element} → {result}");

                Vector3 mid = (a.worldObject.transform.position + b.worldObject.transform.position) * 0.5f;

                Destroy(a.worldObject);
                Destroy(b.worldObject);

                activeMarkers.Remove(a.id);
                activeMarkers.Remove(b.id);

                GameObject newPrefab = GetPrefabForElement(result);
                GameObject newObj = Instantiate(newPrefab, mid, Quaternion.identity, arCamera.transform.parent);
                newObj.transform.LookAt(arCamera.transform);

                string newId = $"{result}_{Random.Range(10000, 99999)}";

                activeMarkers[newId] = new ElementInstance
                {
                    id = newId,
                    element = result,
                    worldObject = newObj
                };

                Debug.Log($"[Combine] Spawned {result}");
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

    private ElementType NameToElement(string name)
    {
        return name switch
        {
            "Fire" => ElementType.Fire,
            "Water" => ElementType.Water,
            "Wind" => ElementType.Wind,
            "Earth" => ElementType.Earth,
            _ => ElementType.Unknown
        };
    }

    private class ElementInstance
    {
        public string id;
        public ElementType element;
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
