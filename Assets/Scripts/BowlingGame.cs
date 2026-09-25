using UnityEngine;

public class BowlingGame : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private float aimSpeed = 4f;
    [SerializeField] private float throwPower = 14f;

    private readonly Vector3 ballStart = new Vector3(0f, 0.5f, -8f);
    private bool thrown;
    private bool roundScored;
    private float throwTime;
    private int score;

    private void Start()
    {
        ResetRound();
    }

    private void Update()
    {
        if (!thrown)
        {
            float movement = Input.GetAxisRaw("Horizontal") * aimSpeed * Time.deltaTime;
            Vector3 nextPosition = ball.position + new Vector3(movement, 0f, 0f);
            nextPosition.x = Mathf.Clamp(nextPosition.x, -1.8f, 1.8f);
            ball.MovePosition(nextPosition);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                thrown = true;
                throwTime = Time.time;
                ball.isKinematic = false;
                ball.AddForce(Vector3.forward * throwPower, ForceMode.Impulse);
            }
        }

        if (thrown && !roundScored && Time.time - throwTime > 3f)
        {
            score = CountFallenPins();
            roundScored = true;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetRound();
        }
    }

    private int CountFallenPins()
    {
        int fallen = 0;
        foreach (BowlingPin pin in pins)
        {
            if (pin.IsFallen)
            {
                fallen++;
            }
        }

        return fallen;
    }

    private void ResetRound()
    {
        thrown = false;
        roundScored = false;
        score = 0;

        ball.isKinematic = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.position = ballStart;
        ball.rotation = Quaternion.identity;

        foreach (BowlingPin pin in pins)
        {
            pin.ResetPin();
        }
    }

    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(24, 22, 500, 40), "Mini Bowling", titleStyle);
        GUI.Label(new Rect(25, 66, 600, 30), "← → で狙う　Space で投球　R でやり直し", textStyle);

        string result = roundScored ? $"倒したピン: {score} / {pins.Length}" : "狙いを定めて投げよう";
        GUI.Label(new Rect(25, 96, 500, 30), result, textStyle);

        if (roundScored && GUI.Button(new Rect(25, 134, 130, 36), "もう一度投げる"))
        {
            ResetRound();
        }
    }
}

public class BowlingPin : MonoBehaviour
{
    private Rigidbody body;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public bool IsFallen => transform.position.y < 0.35f || Vector3.Dot(transform.up, Vector3.up) < 0.7f;

    public void ResetPin()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPosition, startRotation);
        body.Sleep();
    }
}
