using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class Controller : MonoBehaviour
{
    private BoidsManager boidsManager;

    [SerializeField] private InputActionReference setTargetAction;

    private Vector3 velocity = Vector3.zero;

    [SerializeField] private bool leftController = true;

    private UnityEngine.XR.InputDevice controller;

    private void Awake ()
    {
        boidsManager = FindAnyObjectByType<BoidsManager>();

        setTargetAction.action.performed += SetTarget;
        setTargetAction.action?.Enable();

        controller = InputDevices.GetDeviceAtXRNode(leftController ? XRNode.LeftHand : XRNode.RightHand);
    }

    private void FixedUpdate ()
    {
        if ( boidsManager.ReturnTarget() != null && boidsManager.ReturnTarget() == transform )
        {
            controller.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out velocity);
            boidsManager.SetTargetVelocity(velocity);
        }
    }

    private void SetTarget ( InputAction.CallbackContext ctx )
    {
        Debug.Log("Performed");

        if ( boidsManager.ReturnTarget() == null || boidsManager.ReturnTarget() != transform )
        {
            SetTargetToSelf();
        }
        else if ( boidsManager.ReturnTarget() != null && boidsManager.ReturnTarget() == transform )
        {
            ResetTarget();
        }
    }

    public void SetTargetToSelf ()
    {
        boidsManager.SetTarget(transform, true);
    }

    public void ResetTarget ()
    {
        if ( boidsManager.ReturnTarget() != null && boidsManager.ReturnTarget() == transform )
        {
            boidsManager.SetTarget(null, false);
        }
    }
}