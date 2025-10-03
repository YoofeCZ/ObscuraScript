using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Obscurus.Player;   // pro PlayerShooter

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraPivot;
    public Camera playerCamera;
    public PlayerShooter shooter;   // nově – střelba / zbraně

    [Header("Systems (optional)")]
    public StaminaSystem stamina;
    public HealthSystem  health;
    public ArmorSystem   armor;
    public AlchemyPerks  perks;

    [Header("Movement")]
    public float walkSpeed   = 4.0f;
    public float runSpeed    = 7.0f;
    public float crouchSpeed = 2.0f;
    public float jumpHeight  = 1.2f;
    public float gravity     = -20f;
    public float airControl  = 0.6f;

    [Header("Coyote Time")]
    public float baseCoyoteTime = 0f;
    float lastGroundedTime = -999f;

    [Header("Crouch")]
    public float standHeight  = 1.8f;
    public float crouchHeight = 1.1f;
    public float crouchLerp   = 12f;

    [Header("Look")]
    public float baseSensitivity = 0.12f;
    public float pitchMin = -85f;
    public float pitchMax =  85f;

    [Header("Combat / Aim")]
    public float aimFov = 68f;
    public float aimSensMult = 0.6f;

    [Header("Headbob (movement)")]
    public float bobWalkAmp   = 0.03f;
    public float bobRunAmp    = 0.05f;
    public float bobCrouchAmp = 0.02f;
    public float bobFreq      = 10f;

    [Header("Headbob (idle breathing)")]
    public float bobIdleAmp    = 0.008f;
    public float bobIdleSway   = 0.004f;
    public float bobIdleFreq   = 1.4f;
    public float bobIdleLerp   = 3.5f;

    [Range(0.7f, 1.0f)] public float eyeHeightFactor = 0.92f;

    [Header("UI")]
    public Obscurus.UI.InventoryOverlayUI inventoryOverlay;

    CharacterController cc;
    float yaw, pitch;
    float verticalVelocity;
    bool  crouched;
    float targetHeight;
    float sensMult = 1f;
    bool  invertY;
    bool _isSprinting; 

    void Reset()
    {
        if (!cameraPivot)
        {
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0, 0.9f, 0);
            cameraPivot = pivot;
        }
        if (!playerCamera)
        {
            var camGO = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            camGO.transform.SetParent(cameraPivot, false);
            camGO.transform.localPosition = Vector3.zero;
            playerCamera = camGO.GetComponent<Camera>();
        }
        if (playerCamera && playerCamera.tag != "MainCamera")
            playerCamera.tag = "MainCamera";
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!stamina) stamina = GetComponent<StaminaSystem>();
        if (!health)  health  = GetComponent<HealthSystem>();
        if (!armor)   armor   = GetComponent<ArmorSystem>();
        if (!perks)   perks   = GetComponent<AlchemyPerks>();
        if (health && !health.armor && armor) health.armor = armor;

        if (!shooter) shooter = GetComponent<PlayerShooter>();
        if (!shooter) shooter = gameObject.AddComponent<PlayerShooter>();

        cc.height = standHeight;
        cc.center = new Vector3(0, cc.height * 0.5f, 0);
        targetHeight = standHeight;

        if (playerCamera)
            playerCamera.fieldOfView = PlayerPrefs.GetFloat("fov", 90f);

        sensMult = Mathf.Max(0.01f, PlayerPrefs.GetFloat("mouse_sens", 1f));
        invertY  = PlayerPrefs.GetInt("invert_y", 0) == 1;

        var rot = transform.rotation.eulerAngles;
        yaw = rot.y;
        if (cameraPivot) pitch = cameraPivot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        if (!inventoryOverlay)
            inventoryOverlay = FindObjectOfType<Obscurus.UI.InventoryOverlayUI>(true);
    }

    void Update()
    {
        // === Toggle inventáře ===
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && inventoryOverlay &&
            (kb[Key.I].wasPressedThisFrame || kb[Key.Tab].wasPressedThisFrame))
        {
            if (MenuController.I == null || !MenuController.I.IsOverlayOpen)
                inventoryOverlay.Toggle();
        }
#else
        if (inventoryOverlay &&
            (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab)))
        {
            if (MenuController.I == null || !MenuController.I.IsOverlayOpen)
                inventoryOverlay.Toggle();
        }
#endif

        // === Input pro pohyb ===
        ReadInput(out Vector2 moveInput, out Vector2 lookDelta,
                  out bool jumpPressed, out bool sprintHeld, out bool crouchPressed);

        // === Input pro střelbu ===
        ReadCombatInput(out bool fireHeld, out bool firePressed,
                        out bool reloadPressed, out bool aimHeld,
                        out int slotKey, out int wheelDir);

        bool overlayOpen = inventoryOverlay && (MenuController.I != null && MenuController.I.IsOverlayOpen);
        bool blockInput  = overlayOpen || Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked;

        // --- Look ---
        float sens = baseSensitivity * sensMult;
        if (aimHeld) sens *= aimSensMult;

        if (!blockInput)
        {
#if ENABLE_INPUT_SYSTEM
            yaw   += lookDelta.x * sens;
            pitch += (invertY ? lookDelta.y : -lookDelta.y) * sens;
#else
            yaw   += lookDelta.x * sens * 10f;
            pitch += (invertY ? lookDelta.y : -lookDelta.y) * 10f * sens;
#endif
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraPivot) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // --- Aim FOV ---
        if (playerCamera)
        {
            float baseFov = PlayerPrefs.GetFloat("fov", 90f);
            float targetFov = (aimHeld && !blockInput) ? aimFov : baseFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * 10f);
        }

        // --- Crouch ---
        if (crouchPressed) crouched = !crouched;
        targetHeight = crouched ? crouchHeight : standHeight;
        cc.height = Mathf.Lerp(cc.height, targetHeight, Time.deltaTime * crouchLerp);
        cc.center = new Vector3(0, cc.height * 0.5f, 0);

        // --- Movement ---
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        bool grounded = cc.isGrounded;
        if (grounded) lastGroundedTime = Time.time;

        if (aimHeld) sprintHeld = false; // při aimu nemůžeme sprintovat

        // Aim ruší sprint – to už děláš výše: if (aimHeld) sprintHeld = false;

// Chci sprintovat pouze když držím klávesu a aspoň trochu se hýbu (volitelné)
        bool wantSprint = sprintHeld /* && (moveInput.sqrMagnitude > 0.0001f) */;

        if (stamina)
        {
            if (wantSprint)
            {
                bool startingNow = !_isSprinting; // přechod: jen první frame po zapnutí
                bool canContinue = stamina.ConsumeForSprint(Time.deltaTime, startingNow);
                _isSprinting = wantSprint && canContinue;  // běžíme dál, dokud má stamina > 0
            }
            else
            {
                _isSprinting = false; // klávesa puštěná nebo aim apod.
            }
        }
        else
        {
            _isSprinting = wantSprint; // fallback bez stamina systému
        }


        float targetSpeed = crouched ? crouchSpeed : (_isSprinting ? runSpeed : walkSpeed);

        float strafeMult = (perks ? perks.StrafeMult_WhileShooting(fireHeld) : 1f);

        Vector3 wish = (transform.right * (inputDir.x * strafeMult) +
                        transform.forward * inputDir.z) * targetSpeed;

        if (grounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;

            float coyoteWindow = baseCoyoteTime + (perks ? perks.ExtraCoyoteSeconds : 0f);
            bool canAttemptJump = grounded || (Time.time - lastGroundedTime) <= coyoteWindow;

            if (jumpPressed && canAttemptJump)
            {
                bool canJump = true;
                if (stamina && stamina.jumpCost > 0f)
                    canJump = stamina.TrySpend(stamina.jumpCost);

                if (canJump)
                {
                    verticalVelocity = Mathf.Sqrt(2f * -gravity * Mathf.Max(0.01f, jumpHeight));
                    lastGroundedTime = -999f;
                }
            }
        }
        else wish *= airControl;

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 vel = wish + Vector3.up * verticalVelocity;
        cc.Move(vel * Time.deltaTime);

        // --- Combat volání ---
        if (!blockInput && shooter)
        {
            shooter.SetAim(aimHeld);
            if (wheelDir < 0) shooter.Prev();
            else if (wheelDir > 0) shooter.Next();
            if (slotKey >= 0) shooter.EquipSlot(slotKey);

            if (firePressed)
            {
                if (TrySpendForAttack(false))
                    shooter.Fire(false);
            }

            if (reloadPressed) shooter.Reload();
        }

        // --- Headbob ---
        if (cameraPivot)
        {
            float eye = cc.height * eyeHeightFactor;
            bool moving = grounded && inputDir.sqrMagnitude > 0.001f;

            if (moving)
            {
                float amp  = crouched ? bobCrouchAmp : (_isSprinting ? bobRunAmp : bobWalkAmp);
                float t    = Time.time * bobFreq * (_isSprinting ? 1.2f : 1f);
                float bobY = Mathf.Sin(t) * amp;
                float bobX = Mathf.Cos(t * 0.5f) * amp * 0.6f;
                var target = new Vector3(bobX, eye + bobY, 0f);
                cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, target, Time.deltaTime * 12f);
            }
            else if (grounded)
            {
                float t    = Time.time * bobIdleFreq;
                float ampY = bobIdleAmp   * (crouched ? 0.8f : 1f);
                float ampX = bobIdleSway  * (crouched ? 0.8f : 1f);
                float bobY = Mathf.Sin(t)         * ampY;
                float bobX = Mathf.Sin(t * 0.45f) * ampX;
                var target = new Vector3(bobX, eye + bobY, 0f);
                cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, target, Time.deltaTime * bobIdleLerp);
            }
            else
            {
                var target = new Vector3(0f, eye, 0f);
                cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, target, Time.deltaTime * 10f);
            }
        }
    }

    void ReadInput(out Vector2 move, out Vector2 look, out bool jump, out bool sprint, out bool crouchToggle)
    {
        move = Vector2.zero; look = Vector2.zero; jump = sprint = crouchToggle = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            jump  = kb.spaceKey.wasPressedThisFrame;
            sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            crouchToggle = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
        }
        if (mouse != null) look = mouse.delta.ReadValue();
#else
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        jump = Input.GetKeyDown(KeyCode.Space);
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        crouchToggle = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
#endif
    }

    void ReadCombatInput(out bool fireHeld, out bool firePressed,
                         out bool reloadPressed, out bool aimHeld,
                         out int slotKey, out int wheelDir)
    {
        fireHeld = firePressed = reloadPressed = aimHeld = false;
        slotKey = -1;
        wheelDir = 0;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var ms = Mouse.current;
        if (ms != null)
        {
            fireHeld    = ms.leftButton.isPressed;
            firePressed = ms.leftButton.wasPressedThisFrame;
            aimHeld     = ms.rightButton.isPressed;

            float dy = ms.scroll.ReadValue().y;
            if (dy > 0.5f)  wheelDir = -1;
            else if (dy < -0.5f) wheelDir = +1;
        }
        if (kb != null)
        {
            reloadPressed = kb.rKey.wasPressedThisFrame;
            if (kb.digit1Key.wasPressedThisFrame) slotKey = 0;
            else if (kb.digit2Key.wasPressedThisFrame) slotKey = 1;
            else if (kb.digit3Key.wasPressedThisFrame) slotKey = 2;
            else if (kb.digit4Key.wasPressedThisFrame) slotKey = 3;
            else if (kb.digit5Key.wasPressedThisFrame) slotKey = 4;
            else if (kb.digit6Key.wasPressedThisFrame) slotKey = 5;
            else if (kb.digit7Key.wasPressedThisFrame) slotKey = 6;
            else if (kb.digit8Key.wasPressedThisFrame) slotKey = 7;
            else if (kb.digit9Key.wasPressedThisFrame) slotKey = 8;
        }
#else
        fireHeld    = Input.GetMouseButton(0);
        firePressed = Input.GetMouseButtonDown(0);
        aimHeld     = Input.GetMouseButton(1);
        reloadPressed = Input.GetKeyDown(KeyCode.R);

        float dy = Input.GetAxis("Mouse ScrollWheel");
        if (dy > 0.05f)  wheelDir = -1;
        else if (dy < -0.05f) wheelDir = +1;

        if (Input.GetKeyDown(KeyCode.Alpha1)) slotKey = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) slotKey = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) slotKey = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) slotKey = 3;
        else if (Input.GetKeyDown(KeyCode.Alpha5)) slotKey = 4;
        else if (Input.GetKeyDown(KeyCode.Alpha6)) slotKey = 5;
        else if (Input.GetKeyDown(KeyCode.Alpha7)) slotKey = 6;
        else if (Input.GetKeyDown(KeyCode.Alpha8)) slotKey = 7;
        else if (Input.GetKeyDown(KeyCode.Alpha9)) slotKey = 8;
#endif
    }

    public bool TrySpendForAttack(bool heavy)
    {
        if (!stamina) return true;
        float cost = heavy ? stamina.heavyAttackCost : stamina.lightAttackCost;
        return stamina.TrySpend(cost);
    }
}
