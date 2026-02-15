using System.Collections.Generic;
using UnityEngine;

public class MarkerElementManager : MonoBehaviour
{
    public static MarkerElementManager Instance { get; private set; }

    [Header("Element Prefabs")]
    public GameObject firePrefab;
    public GameObject waterPrefab;
    public GameObject windPrefab;
    public GameObject earthPrefab;
    public GameObject steamPrefab;


    [Header("Combination Settings")]
    public float combineDistance = 0.5f;

    private readonly List<ElementInstance> activeMarkers = new List<ElementInstance>();

    private readonly Dictionary<(ElementType, ElementType), ElementType> combinations =
        new Dictionary<(ElementType, ElementType), ElementType>
        {
            {(ElementType.Fire,  ElementType.Wind),  ElementType.Steam},
            {(ElementType.Wind,  ElementType.Fire),  ElementType.Steam},

            {(ElementType.Fire,  ElementType.Water), ElementType.Mist},
            {(ElementType.Water, ElementType.Fire),  ElementType.Mist},

            {(ElementType.Earth, ElementType.Water), ElementType.Mud},
            {(ElementType.Water, ElementType.Earth), ElementType.Mud},

            {(ElementType.Wind,  ElementType.Earth), ElementType.Dust},
            {(ElementType.Earth, ElementType.Wind),  ElementType.Dust},
        };

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterMarker(int markerId, int rotation, Vector3 worldPos)
    {
        ElementType element = MarkerIdToElement(markerId);
        Debug.Log($"RegisterMarker: ID={markerId}, element={element}, pos={worldPos}");

        ElementInstance instance = new ElementInstance
        {
            id = markerId,
            rotation = rotation,
            element = element,
            worldPosition = worldPos
        };

        // Spawn visual ONCE
        instance.visual = Instantiate(GetPrefabForElement(element), worldPos, Quaternion.identity);
        Debug.Log("Spawned visual: " + instance.visual);

        activeMarkers.Add(instance);

        CheckForCombinations();
    }


    private GameObject GetPrefabForElement(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => firePrefab,
            ElementType.Water => waterPrefab,
            ElementType.Wind => windPrefab,
            ElementType.Earth => earthPrefab,
            ElementType.Steam => steamPrefab,
            _ => null
        };
    }



    private void CheckForCombinations()
    {
        for (int i = 0; i < activeMarkers.Count; i++)
        {
            for (int j = i + 1; j < activeMarkers.Count; j++)
            {
                ElementInstance a = activeMarkers[i];
                ElementInstance b = activeMarkers[j];

                float dist = Vector3.Distance(a.worldPosition, b.worldPosition);

                if (dist <= combineDistance)
                {
                    TryCombine(a, b);
                }
            }
        }
    }

    private void TryCombine(ElementInstance a, ElementInstance b)
    {
        if (combinations.TryGetValue((a.element, b.element), out ElementType result))
        {
            Debug.Log($"Combined {a.element} + {b.element} → {result}");

            // Hide old visuals
            if (a.visual != null) a.visual.SetActive(false);
            if (b.visual != null) b.visual.SetActive(false);

            // Find midpoint
            Vector3 mid = (a.worldPosition + b.worldPosition) / 2f;

            // Spawn combined element
            GameObject combined = Instantiate(GetPrefabForElement(result), mid, Quaternion.identity);

            // Update both markers
            a.element = result;
            b.element = result;

            a.visual = combined;
            b.visual = combined;
            Debug.Log($"COMBINATION TRIGGERED: {a.element} + {b.element}");

        }
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
        public int rotation;
        public ElementType element;
        public Vector3 worldPosition;

        public GameObject visual;
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
        Dust
    }
}
