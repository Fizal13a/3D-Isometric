using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerMovemntController : MonoBehaviour
{
    PlayerActions playerActions;
    CharacterController characterController;
    Animator animator;

    //setter/getter parameter IDs
    int isWalkingHash;
    int isAttackingHash;
    int attackCountHash;
    int isJumpingHash;
    int isDashingHash;

    //player Input
    [HideInInspector] public Vector2 currentMovementInput;
    UnityEngine.Vector3 currentMovement;
    bool isMovementPressed;
    [SerializeField] private float speed;
    //rotation
    float rotationFactorPerFrame = 15f;
    //jump
    float gravity = -9.8f;
    float groundedGravity = -0.05f;
    bool isJumpPressed = false;
    float initialJumpVelocity;
    float maxJumpHeight = 1f;
    float maxJumpTime = 0.7f;
    bool isJumping = false;
    bool isJumpAnimating = false;
    //Dash
    public float dashTime;
    private bool isDashing = false;
    public float dashingSpeed;
    private Vector2 dashMovementInput;
    private Vector3 dashMovement;
    public float dashCooldownTime = 2f; // Cooldown time for the dash in seconds
    private bool isDashOnCooldown = false; // Flag to track if the dash is on cooldown
    //Sword attack
    private int attackCount;
    private bool isAttacking;
    private bool canGoForwardOnAttack;
    Coroutine attackResetRoutine = null;
    [SerializeField] private float attackingSpeed;

    private void Awake()
    {
        playerActions = new PlayerActions();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        isWalkingHash = Animator.StringToHash("Walking");
        isAttackingHash = Animator.StringToHash("IsAttacking");
        attackCountHash = Animator.StringToHash("AttackCount");
        isJumpingHash = Animator.StringToHash("Jumping");
        isDashingHash = Animator.StringToHash("Dashing");

        //player input callbacks
        playerActions.CharacterControls.Move.started += OnMovementInput;
        playerActions.CharacterControls.Move.canceled += OnMovementInput;
        playerActions.CharacterControls.Move.performed += OnMovementInput;
        playerActions.CharacterControls.Roll.performed += OnRollInput;
        playerActions.CharacterControls.SwordAttack.performed += OnSwordAttack;
        playerActions.CharacterControls.Jump.started += OnJumpPressed;
        playerActions.CharacterControls.Jump.canceled += OnJumpPressed;

        SetUpJumpVariables();
    }

    void OnMovementInput(InputAction.CallbackContext context)
    {
        //if (isDashing) return;

        currentMovementInput = context.ReadValue<Vector2>();
        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;
        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
        dashMovementInput = context.ReadValue<Vector2>();
        dashMovement.x = dashMovementInput.x;
        dashMovement.z = dashMovementInput.y;
    }

    void OnRollInput(InputAction.CallbackContext context)
    {
        if(IsGrounded()) CharacterDash();
    }

    void OnSwordAttack(InputAction.CallbackContext context)
    {
        Attack();
    }

    void OnJumpPressed(InputAction.CallbackContext context)
    {
        isJumpPressed = context.ReadValueAsButton();
    }

    IEnumerator AttackResetRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        attackCount = 0;
    }

    void SetUpJumpVariables()
    {
        float timeToApex = maxJumpTime / 2;
        gravity = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
        initialJumpVelocity = (2 * maxJumpHeight) / timeToApex;
    }

    void Attack()
    {
        if(IsGrounded() && !isAttacking)
        {
            isAttacking= true;
            canGoForwardOnAttack= true;
            animator.SetBool(isAttackingHash, isAttacking);
            if(attackCount < 3 && attackResetRoutine!= null)
            {
                StopCoroutine(attackResetRoutine);
                attackCount++;
                animator.SetInteger(attackCountHash, attackCount);
            }
        }
    }

    void HalfAttack()
    {
        canGoForwardOnAttack= false;
    }

    void EndAttack()
    {
        if(isAttacking)
        {
            isAttacking = false;
            animator.SetBool(isAttackingHash, isAttacking);
            animator.SetInteger(attackCountHash, attackCount);
            attackResetRoutine = StartCoroutine(AttackResetRoutine());
            if (attackCount == 3)
            {
                attackCount = 0;
                animator.SetInteger(attackCountHash, attackCount);
            }
        }

    }

    void HandleJump()
    {
        if (!isJumping && characterController.isGrounded && isJumpPressed)
        {
            animator.SetBool(isJumpingHash, true);
            isJumpAnimating = true;
            isJumpPressed = true;
            currentMovement.y = initialJumpVelocity * 0.5f;
        }
        else if (!isJumpPressed && isJumping && characterController.isGrounded)
        {
            isJumpPressed = false;
        }
    }

    void HandleAnimation()
    {
        bool isWalking = animator.GetBool(isWalkingHash);

        if (isMovementPressed && !isWalking)
        {
            animator.SetBool(isWalkingHash, true);
        }
        else if (!isMovementPressed && isWalking)
        {
            animator.SetBool(isWalkingHash, false);
        }
    }

    void HandleRotation()
    {
        Vector3 positionToLookAt;

        positionToLookAt.x = currentMovement.x;
        positionToLookAt.y = 0;
        positionToLookAt.z = currentMovement.z;

        Quaternion currentRotation = transform.rotation;

        if (isMovementPressed)
        {
            Quaternion targetRotation = UnityEngine.Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationFactorPerFrame * Time.deltaTime);

        }
    }

    void HandleGravity()
    {
        bool isFalling = currentMovement.y <= 0.0f;
        float fallMultiplier = 2.0f;

        if (characterController.isGrounded)
        {
            if (isJumpAnimating)
            {
                animator.SetBool(isJumpingHash, false);
                isJumpAnimating = false;
            }
            currentMovement.y = groundedGravity;
        }
        else if (isFalling)
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * fallMultiplier * Time.deltaTime);
            float nextYVelocity = (previousYVelocity + newYVelocity) * 0.5f;
            currentMovement.y = nextYVelocity;
        }
        else
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * Time.deltaTime);
            float nextYVelocity = (previousYVelocity + newYVelocity) * 0.5f;
            currentMovement.y = nextYVelocity;
            currentMovement.y = nextYVelocity;
        }
    }

    private void Update()
    {
        HandleAnimation();
        HandleRotation();

        if(!canGoForwardOnAttack && !isDashing)
        {
            characterController.Move(currentMovement * speed * Time.deltaTime);
        }
        else if (isDashing && !canGoForwardOnAttack)
        {
            characterController.Move(dashMovement * dashingSpeed * Time.deltaTime);
        }
        else if(canGoForwardOnAttack && !isDashing)
        {
            Vector3 rollingDirection2 = transform.forward;
            characterController.Move(rollingDirection2 * attackingSpeed * Time.deltaTime);
        }
       

        HandleGravity();
        HandleJump();
    }

    public void CharacterDash()
    {
        if (isMovementPressed && !isDashing && !isDashOnCooldown)
        {
            // Perform the dash
            StartCoroutine(StartDashCooldown());
            StartCoroutine(DashRoutine());
        }
    }

    private IEnumerator StartDashCooldown()
    {
        isDashOnCooldown = true;
        float cooldownTimer = dashCooldownTime;

        while (cooldownTimer > 0f)
        {
            // Decrease the cooldown timer each frame
            cooldownTimer -= Time.deltaTime;

            // Wait for the next frame
            yield return null;
        }

        // Reset the cooldown flag and reset the image bar fill amount to 0
        isDashOnCooldown = false;
    }


    IEnumerator DashRoutine()
    {
        isDashing = true;
        animator.SetBool(isDashingHash, true);
        yield return new WaitForSeconds(dashTime);
        isDashing = false;
        animator.SetBool(isDashingHash, false);
    }

    public bool IsGrounded() => characterController.isGrounded;

    private void OnEnable()
    {
        playerActions.CharacterControls.Enable();
    }

    private void OnDisable()
    {
        playerActions.CharacterControls.Disable();
    }
}
