using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//Unity Script | 0 references
public class NetworkPlayer : MonoBehaviour
{
    [SerializeField]
    Rigidbody rigidbody3D;

    [SerializeField]
    ConfigurableJoint mainJoint;

    //Input
    [SerializeField] InputActionAsset inputActionAsset;
    InputActionMap playerActionMap;
    InputAction moveAction;
    InputAction jumpAction;
    Vector2 moveInputVector = Vector2.zero;
    bool isJumpButtonPressed = false;

    //Controller settings
    float maxSpeed = 3;

    //States
    bool isGrounded = false;

    //Raycasts
    RaycastHit[] raycastHits = new RaycastHit[10];

    // Start is called before the first frame update
    //Unity Message | 0 references
    void Start()
    {
        if (inputActionAsset != null)
        {
            playerActionMap = inputActionAsset.FindActionMap("Player");
            moveAction = playerActionMap.FindAction("Move");
            jumpAction = playerActionMap.FindAction("Jump");
            playerActionMap.Enable();
        }
    }

    void OnDestroy()
    {
        if (playerActionMap != null)
            playerActionMap.Disable();
    }

    // Update is called once per frame
    //Unity Message | 0 references
    void Update()
    {
        //Move input (WASD)
        if (moveAction != null)
            moveInputVector = moveAction.ReadValue<Vector2>();

        //Jump input (Space)
        if (jumpAction != null && jumpAction.triggered)
            isJumpButtonPressed = true;
    }

    //Unity Message | 0 references
    void FixedUpdate()
    {
        //Assume that we are not grounded.
        isGrounded = false;

        //Check if we are grounded
        int numberOFHits = Physics.SphereCastNonAlloc(rigidbody3D.position, 0.1f, transform.up * -1, raycastHits, 0.5f);

        //Check for valid results
        for (int i = 0; i < numberOFHits; i++)
        {
            //Ignore self hits
            if (raycastHits[i].transform.root == transform)
                continue;

            isGrounded = true;

            break;
        }

        //Слабая дополнительная гравитация в воздухе — для агрессивного «невесомого» полёта
        if (!isGrounded)
            rigidbody3D.AddForce(Vector3.down * 2);

        float inputMagnitude = moveInputVector.magnitude;

        if (inputMagnitude != 0)
        {
            Quaternion desiredDirection = Quaternion.LookRotation(new Vector3(moveInputVector.x, 0, moveInputVector.y * -1), transform.up);

            //Rotate target towards direction
            mainJoint.targetRotation = Quaternion.RotateTowards(mainJoint.targetRotation, desiredDirection, Time.fixedDeltaTime * 360);

            Vector3 localVelocityVsForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);

            float localForwardVelocity = localVelocityVsForward.magnitude;

            if(localForwardVelocity < maxSpeed)
            {
                //Move the character in the direction it is facing
                rigidbody3D.AddForce(transform.forward * inputMagnitude * 30);
            }
        }

        if(isGrounded && isJumpButtonPressed)
        {
            rigidbody3D.AddForce(Vector3.up * 48, ForceMode.Impulse);

            isJumpButtonPressed = false;
        }
    }
}
