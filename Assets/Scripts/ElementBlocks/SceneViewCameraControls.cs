using UnityEngine;

public class SceneViewCameraControls : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed when using WASD")]
    public float moveSpeed = 10f;
    
    [Tooltip("Speed multiplier when holding Shift")]
    public float fastMoveMultiplier = 3f;
    
    [Tooltip("Speed multiplier when holding Ctrl")]
    public float slowMoveMultiplier = 0.25f;
    
    [Header("Rotation Settings")]
    [Tooltip("Mouse sensitivity for rotation")]
    public float mouseSensitivity = 3f;
    
    [Header("Zoom Settings")]
    [Tooltip("Scroll wheel zoom speed")]
    public float scrollSpeed = 5f;
    
    [Tooltip("Alt+Right-click zoom speed")]
    public float altZoomSpeed = 0.5f;
    
    [Header("Pan Settings")]
    [Tooltip("Middle mouse pan speed")]
    public float panSpeed = 0.5f;
    
    [Header("Orbit Settings")]
    [Tooltip("Point to orbit around (updated when Alt+clicking)")]
    public Vector3 orbitPoint = Vector3.zero;
    
    [Tooltip("Distance from orbit point")]
    public float orbitDistance = 10f;
    
    private Vector3 lastMousePosition;
    private bool isOrbiting = false;
    private bool isFlyMode = false;
    
    void Start()
    {
        // Initialize orbit point to camera forward
        orbitPoint = transform.position + transform.forward * orbitDistance;
    }
    
    void Update()
    {
        HandleFlyMode();
        HandleOrbit();
        HandlePan();
        HandleZoom();
    }
    
    void HandleFlyMode()
    {
        // Right mouse button + WASD for fly mode (like Scene view)
        if (Input.GetMouseButtonDown(1))
        {
            isFlyMode = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        if (Input.GetMouseButtonUp(1))
        {
            isFlyMode = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        
        if (isFlyMode)
        {
            // Mouse look
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            transform.Rotate(Vector3.up, mouseX, Space.World);
            transform.Rotate(Vector3.right, -mouseY, Space.Self);
            
            // WASD movement
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMoveMultiplier;
            if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMoveMultiplier;
            
            Vector3 movement = Vector3.zero;
            
            if (Input.GetKey(KeyCode.W)) movement += transform.forward;
            if (Input.GetKey(KeyCode.S)) movement -= transform.forward;
            if (Input.GetKey(KeyCode.A)) movement -= transform.right;
            if (Input.GetKey(KeyCode.D)) movement += transform.right;
            if (Input.GetKey(KeyCode.E)) movement += transform.up;
            if (Input.GetKey(KeyCode.Q)) movement -= transform.up;
            
            transform.position += movement * speed * Time.deltaTime;
            
            // Update orbit point
            orbitPoint = transform.position + transform.forward * orbitDistance;
        }
    }
    
    void HandleOrbit()
    {
        // Alt + Left mouse button for orbit
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
        {
            isOrbiting = true;
            // Update orbit point based on raycast or current forward distance
            UpdateOrbitPoint();
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isOrbiting = false;
        }
        
        if (isOrbiting && Input.GetKey(KeyCode.LeftAlt))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            // Orbit around the point
            transform.RotateAround(orbitPoint, Vector3.up, mouseX);
            transform.RotateAround(orbitPoint, transform.right, -mouseY);
            
            // Update distance
            orbitDistance = Vector3.Distance(transform.position, orbitPoint);
        }
        
        // Alt + Right mouse for zoom in orbit mode
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(1))
        {
            float mouseY = Input.GetAxis("Mouse Y");
            float zoomAmount = mouseY * altZoomSpeed;
            
            Vector3 direction = (transform.position - orbitPoint).normalized;
            Vector3 newPosition = transform.position + direction * zoomAmount;
            
            // Don't let camera go past orbit point
            if (Vector3.Distance(newPosition, orbitPoint) > 0.1f)
            {
                transform.position = newPosition;
                orbitDistance = Vector3.Distance(transform.position, orbitPoint);
            }
        }
    }
    
    void HandlePan()
    {
        // Middle mouse button for panning
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePosition = Input.mousePosition;
        }
        
        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            
            Vector3 move = -transform.right * delta.x * panSpeed * Time.deltaTime;
            move += -transform.up * delta.y * panSpeed * Time.deltaTime;
            
            transform.position += move;
            orbitPoint += move; // Move orbit point with camera when panning
            
            lastMousePosition = Input.mousePosition;
        }
    }
    
    void HandleZoom()
    {
        // Scroll wheel zoom (dolly)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            float zoomAmount = scroll * scrollSpeed;
            transform.position += transform.forward * zoomAmount;
            
            // Update orbit distance
            orbitDistance = Vector3.Distance(transform.position, orbitPoint);
        }
    }
    
    void UpdateOrbitPoint()
    {
        // Try to find a point in front of camera using raycast
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            orbitPoint = hit.point;
        }
        else
        {
            // Use current forward distance
            orbitPoint = transform.position + transform.forward * orbitDistance;
        }
        
        orbitDistance = Vector3.Distance(transform.position, orbitPoint);
    }
}