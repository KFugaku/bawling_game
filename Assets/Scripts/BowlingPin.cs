using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BowlingPin : MonoBehaviour
{
    private Rigidbody body;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool initialized;

    private void Awake()
    {
        ConfigureVisual();
        Initialize();
    }

    public bool IsFallen =>
        transform.position.y < 0.35f ||
        Vector3.Dot(transform.up, Vector3.up) < 0.7f;

    public bool IsMoving(float linearSpeedThreshold, float angularSpeedThreshold)
    {
        Initialize();
        if (!gameObject.activeInHierarchy || body == null || body.IsSleeping())
        {
            return false;
        }

        return body.linearVelocity.sqrMagnitude > linearSpeedThreshold * linearSpeedThreshold ||
            body.angularVelocity.sqrMagnitude > angularSpeedThreshold * angularSpeedThreshold;
    }

    public void HidePin()
    {
        Initialize();
        if (body != null)
        {
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        gameObject.SetActive(false);
    }

    public void ResetPin()
    {
        gameObject.SetActive(true);
        Initialize();
        if (body == null)
        {
            return;
        }

        body.isKinematic = true;
        body.position = startPosition;
        body.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.Sleep();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        body = GetComponent<Rigidbody>();
        ConfigurePhysics();
        startPosition = transform.position;
        startRotation = transform.rotation;
        initialized = true;
    }

    private void ConfigurePhysics()
    {
        if (body == null)
        {
            return;
        }

        // A 16 lb ball versus a 3.5 lb pin, with the USBC target center of mass.
        body.mass = RegulationBowlingDimensions.PinWeight;
        body.linearDamping = 0.16f;
        body.angularDamping = 0.24f;
        body.centerOfMass = new Vector3(0f, RegulationBowlingDimensions.PinCenterOfMassLocalY, 0f);
    }

    private void ConfigureVisual()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = BowlingPinMesh.SharedMesh;
        }

        Transform lowerStripe = transform.Find("Red Stripe");
        if (lowerStripe == null)
        {
            return;
        }

        ConfigureStripe(lowerStripe, 0.28f);
        Transform upperStripe = transform.Find("Red Stripe Upper");
        if (upperStripe == null)
        {
            GameObject stripeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stripeObject.name = "Red Stripe Upper";
            stripeObject.transform.SetParent(transform, false);
            Renderer sourceRenderer = lowerStripe.GetComponent<Renderer>();
            Renderer stripeRenderer = stripeObject.GetComponent<Renderer>();
            if (sourceRenderer != null && stripeRenderer != null)
            {
                stripeRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }

            Collider stripeCollider = stripeObject.GetComponent<Collider>();
            if (stripeCollider != null)
            {
                Destroy(stripeCollider);
            }

            upperStripe = stripeObject.transform;
        }

        ConfigureStripe(upperStripe, 0.42f);
    }

    private static void ConfigureStripe(Transform stripe, float localY)
    {
        stripe.localPosition = new Vector3(0f, localY, 0f);
        stripe.localScale = new Vector3(0.39f, 0.025f, 0.39f);
    }
}
