using UnityEngine;
using UnityEngine.InputSystem;

public class FlyCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float boostSpeed = 20f;
    public float mouseSensitivity = 2f;
    
    [Header("Movement Smoothing")]
    public float movementSmoothing = 5f;
    public float rotationSmoothing = 5f;
    
    private Vector3 targetVelocity;
    private Vector3 currentVelocity;
    private Vector2 mouseInput;
    private Vector2 moveInput;
    private float mouseX, mouseY;
    private bool isMovingUp, isMovingDown, isBoosting;
    
    // Input Actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction upAction;
    private InputAction downAction;
    private InputAction boostAction;
    private InputAction toggleCursorAction;
    
    void Awake()
    {
        // Create input actions
        moveAction = new InputAction("Move", InputActionType.Value, "<Keyboard>/wasd");
        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        upAction = new InputAction("Up", InputActionType.Button, "<Keyboard>/space");
        downAction = new InputAction("Down", InputActionType.Button, "<Keyboard>/leftShift");
        boostAction = new InputAction("Boost", InputActionType.Button, "<Keyboard>/leftCtrl");
        toggleCursorAction = new InputAction("ToggleCursor", InputActionType.Button, "<Keyboard>/escape");
        
        // Add composite for WASD movement
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
    }
    
    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        upAction.Enable();
        downAction.Enable();
        boostAction.Enable();
        toggleCursorAction.Enable();
        
        // Subscribe to input events
        upAction.performed += OnUpPerformed;
        upAction.canceled += OnUpCanceled;
        downAction.performed += OnDownPerformed;
        downAction.canceled += OnDownCanceled;
        toggleCursorAction.performed += OnToggleCursor;
    }
    
    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        upAction.Disable();
        downAction.Disable();
        boostAction.Disable();
        toggleCursorAction.Disable();
        
        // Unsubscribe from input events
        upAction.performed -= OnUpPerformed;
        upAction.canceled -= OnUpCanceled;
        downAction.performed -= OnDownPerformed;
        downAction.canceled -= OnDownCanceled;
        toggleCursorAction.performed -= OnToggleCursor;
    }
    
    void Start()
    {
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        HandleInput();
        HandleMouseLook();
        HandleMovement();
    }
    
    void HandleInput()
    {
        // Read input values
        moveInput = moveAction.ReadValue<Vector2>();
        mouseInput = lookAction.ReadValue<Vector2>();
        isBoosting = boostAction.IsPressed();
    }
    
    void HandleMouseLook()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX += mouseInput.x * mouseSensitivity * 0.1f;
            mouseY -= mouseInput.y * mouseSensitivity * 0.1f;
            
            // Clamp vertical rotation to prevent flipping
            mouseY = Mathf.Clamp(mouseY, -90f, 90f);
            
            // Apply rotation with smoothing
            Quaternion targetRotation = Quaternion.Euler(mouseY, mouseX, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothing * Time.deltaTime);
        }
    }
    
    void HandleMovement()
    {
        // Get movement input
        float horizontal = moveInput.x; // A/D
        float vertical = moveInput.y;   // W/S
        float upDown = 0f;
        
        // Up/Down movement
        if (isMovingUp)
            upDown = 1f;
        if (isMovingDown)
            upDown = -1f;
            
        // Boost speed when holding Left Control
        float currentSpeed = isBoosting ? boostSpeed : moveSpeed;
        
        // Calculate movement direction relative to camera
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = Vector3.up;
        
        // Calculate target velocity
        targetVelocity = (forward * vertical + right * horizontal + up * upDown) * currentSpeed;
        
        // Smooth movement
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, movementSmoothing * Time.deltaTime);
        
        // Apply movement
        transform.position += currentVelocity * Time.deltaTime;
    }
    
    // Input event handlers
    void OnUpPerformed(InputAction.CallbackContext context) => isMovingUp = true;
    void OnUpCanceled(InputAction.CallbackContext context) => isMovingUp = false;
    void OnDownPerformed(InputAction.CallbackContext context) => isMovingDown = true;
    void OnDownCanceled(InputAction.CallbackContext context) => isMovingDown = false;
    
    void OnToggleCursor(InputAction.CallbackContext context)
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
} 