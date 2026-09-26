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
        Initialize();
    }

    public bool IsFallen =>
        transform.position.y < 0.35f ||
        Vector3.Dot(transform.up, Vector3.up) < 0.7f;

    public void ResetPin()
    {
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

        // A 16 lb ball versus a 3.5 lb pin maps to 6.0 versus 1.31 in this scene.
        body.mass = 1.31f;
        body.linearDamping = 0.16f;
        body.angularDamping = 0.24f;
        body.centerOfMass = new Vector3(0f, -0.22f, 0f);
    }
}
