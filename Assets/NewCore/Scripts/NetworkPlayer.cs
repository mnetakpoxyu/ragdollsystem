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
    private PlayerInputActions inputActions;
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
        inputActions = new PlayerInputActions();
        inputActions.Enable();
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
        }
    }

    // Update is called once per frame
    //Unity Message | 0 references
    void Update()
    {
        //Move input (WASD)
        moveInputVector = inputActions.Player.Move.ReadValue<Vector2>();

        //Jump input (Space)
        if (inputActions.Player.Jump.triggered)
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

        //Apply extra gravity to charcater to make it less floaty
        if (!isGrounded)
            rigidbody3D.AddForce(Vector3.down * 10);

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
            rigidbody3D.AddForce(Vector3.up * 20, ForceMode.Impulse);

            isJumpButtonPressed = false;
        }
    }
}
