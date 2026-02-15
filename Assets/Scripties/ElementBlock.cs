using UnityEngine;
using Drawing;

[RequireComponent(typeof(SphereCollider))]
public class ElementBlock : MonoBehaviour
{
    [Header("Block Type")]
    [Tooltip("Database containing all element types and recipes")]
    public ElementDatabase database;
    
    [Tooltip("Current element type")]
    public ElementID currentElement = ElementID.Fire;
    
    [Header("Box Settings")]
    [Tooltip("Size of the wirebox (width, height, depth)")]
    public Vector3 boxSize = new Vector3(1f, 1f, 1f);
    
    [Tooltip("Color of the wirebox lines")]
    public Color boxColor = new Color(0f, 1f, 1f, 1f); // Cyan
    
    [Header("Label Settings")]
    [Tooltip("Color of the label text")]
    public Color labelColor = Color.white;
    
    [Tooltip("Font size of the label (in world units)")]
    public float labelFontSize = 0.75f;
    
    [Header("Merge Settings")]
    [Tooltip("Distance at which blocks can merge")]
    public float mergeDistance = 1.5f;
    
    [Tooltip("Cooldown after merging before this block can merge again")]
    public float mergeCooldown = 0.5f;

    private GameObject currentPrefabInstance;
    private ElementID lastElement = ElementID.None;
    private SphereCollider mergeCollider;
    private float lastMergeTime = -999f;

    void Awake()
    {
        // Setup trigger collider for merge detection
        mergeCollider = GetComponent<SphereCollider>();
        mergeCollider.isTrigger = true;
        mergeCollider.radius = mergeDistance;
    }

    void Start()
    {
        UpdateElementPrefab();
        lastElement = currentElement;
    }

    void Update()
    {
        // Check if element changed
        if (currentElement != lastElement)
        {
            UpdateElementPrefab();
            lastElement = currentElement;
        }
        
        // Update merge collider radius if merge distance changed
        if (mergeCollider.radius != mergeDistance)
        {
            mergeCollider.radius = mergeDistance;
        }
        
        DrawWireboxAndLabel();
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if we're on cooldown
        if (Time.time - lastMergeTime < mergeCooldown)
            return;
        
        // Check if the other object is an ElementBlock
        ElementBlock otherBlock = other.GetComponent<ElementBlock>();
        if (otherBlock == null)
            return;
        
        // Check if other block is also on cooldown
        if (Time.time - otherBlock.lastMergeTime < otherBlock.mergeCooldown)
            return;
        
        // Try to combine
        if (TryCombineWith(otherBlock, out ElementID result))
        {
            // Change this block to the result
            SetElement(result);
            lastMergeTime = Time.time;
            
            // Destroy the other block
            Destroy(otherBlock.gameObject);
        }
    }

    private void UpdateElementPrefab()
    {
        // Destroy old prefab instance
        if (currentPrefabInstance != null)
        {
            Destroy(currentPrefabInstance);
            currentPrefabInstance = null;
        }
        
        // Get element data from database
        if (database != null)
        {
            ElementTypeSO elementData = database.GetElement(currentElement);
            if (elementData != null && elementData.prefab != null)
            {
                // Instantiate new prefab at local position (0, 0, 0)
                currentPrefabInstance = Instantiate(elementData.prefab, transform);
                currentPrefabInstance.transform.localPosition = Vector3.zero;
                currentPrefabInstance.transform.localRotation = Quaternion.identity;
            }
        }
    }
    
    /// <summary>
    /// Attempt to combine this element with another element block
    /// </summary>
    public bool TryCombineWith(ElementBlock other, out ElementID result)
    {
        if (database == null)
        {
            result = ElementID.None;
            return false;
        }
        
        ElementTypeSO resultElement = database.Combine(this.currentElement, other.currentElement);
        if (resultElement != null)
        {
            result = resultElement.id;
            return true;
        }
        
        result = ElementID.None;
        return false;
    }
    
    /// <summary>
    /// Change this block to a different element type
    /// </summary>
    public void SetElement(ElementID newElement)
    {
        currentElement = newElement;
    }

    private void DrawWireboxAndLabel()
    {
        // Calculate the center of the box
        // The GameObject's position should be at the center of the bottom plane
        // So we offset the box center upward by half its height
        Vector3 boxCenter = transform.position + Vector3.up * (boxSize.y * 0.5f);
        
        // Create a rotation quaternion for the box to match GameObject's rotation
        Quaternion boxRotation = transform.rotation;
        
        // Draw the wirebox
        Draw.ingame.WireBox(boxCenter, boxRotation, boxSize, boxColor);
        
        // Position label above the box
        Vector3 labelPosition = transform.position + Vector3.up * (boxSize.y + 1);
        
        // Make label face the camera
        Camera cam = Camera.main;
        if (cam == null) cam = Camera.current;
        
        Quaternion labelRotation = Quaternion.identity;
        
        if (cam != null)
        {
            Vector3 cameraDirection = (labelPosition - cam.transform.position).normalized;
            labelRotation = Quaternion.LookRotation(cameraDirection);
        }
        
        // Draw the text with current element ID as label
        Draw.ingame.Label3D(
            labelPosition,
            labelRotation,
            currentElement.ToString(),
            labelFontSize,
            LabelAlignment.Center,
            labelColor
        );
    }
    
    void OnDestroy()
    {
        if (currentPrefabInstance != null)
        {
            Destroy(currentPrefabInstance);
        }
    }
}