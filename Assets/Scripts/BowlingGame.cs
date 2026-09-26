using System.Collections.Generic;
using UnityEngine;

public class BowlingGame : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private float aimSpeed = 4f;
    [SerializeField] private float throwPower = 18f;

    private readonly Vector3 ballStart = new Vector3(0f, 0.5f, -4.5f);
    private bool thrown;
    private bool roundScored;
    private float throwTime;
    private int score;

    private void Awake()
    {
        ResolveSceneReferences();

        // Keep the release point fully inside the Game view on every machine.
        Camera gameCamera = Camera.main;
        if (gameCamera != null)
        {
            gameCamera.transform.position = new Vector3(0f, 3.6f, -12.5f);
            gameCamera.transform.LookAt(new Vector3(0f, 0.45f, 8f));
            gameCamera.fieldOfView = 60f;
        }
    }

    private void Start()
    {
        ResetRound();
    }

    private void Update()
    {
        if (ball == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetRound();
            return;
        }

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
                ball.linearVelocity = Vector3.forward * throwPower;
                ball.angularVelocity = Vector3.right * (throwPower * 2f);
            }
        }

        if (thrown && !roundScored && Time.time - throwTime > 3f)
        {
            score = CountFallenPins();
            roundScored = true;
        }

    }

    private int CountFallenPins()
    {
        int fallen = 0;
        if (pins == null)
        {
            return fallen;
        }

        foreach (BowlingPin pin in pins)
        {
            if (pin != null && pin.IsFallen)
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

        if (ball != null)
        {
            // Velocity can only be changed while the Rigidbody is dynamic.
            ball.isKinematic = false;
            ball.linearDamping = 0f;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.position = ballStart;
            ball.rotation = Quaternion.identity;
            ball.isKinematic = true;
        }

        if (pins != null)
        {
            foreach (BowlingPin pin in pins)
            {
                if (pin != null)
                {
                    pin.ResetPin();
                }
            }
        }

        Physics.SyncTransforms();
    }

    private void ResolveSceneReferences()
    {
        if (ball == null)
        {
            GameObject ballObject = GameObject.Find("Ball");
            if (ballObject != null)
            {
                ball = ballObject.GetComponent<Rigidbody>();
            }
        }

        bool needsPinRefresh = pins == null || pins.Length == 0;
        if (!needsPinRefresh)
        {
            foreach (BowlingPin pin in pins)
            {
                if (pin == null)
                {
                    needsPinRefresh = true;
                    break;
                }
            }
        }

        if (needsPinRefresh)
        {
            List<BowlingPin> discoveredPins = new List<BowlingPin>();
            Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (!sceneTransform.name.StartsWith("Pin "))
                {
                    continue;
                }

                BowlingPin pin = sceneTransform.GetComponent<BowlingPin>();
                if (pin == null)
                {
                    pin = sceneTransform.gameObject.AddComponent<BowlingPin>();
                }

                discoveredPins.Add(pin);
            }

            discoveredPins.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            pins = discoveredPins.ToArray();
        }

        if (ball == null)
        {
            Debug.LogError("BowlingGame: Ball Rigidbody could not be found.");
        }

        if (pins == null || pins.Length == 0)
        {
            Debug.LogError("BowlingGame: Bowling pins could not be found.");
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

        int pinCount = pins?.Length ?? 0;
        string result = roundScored ? $"倒したピン: {score} / {pinCount}" : "狙いを定めて投げよう";
        GUI.Label(new Rect(25, 96, 500, 30), result, textStyle);

        if (GUI.Button(new Rect(25, 134, 130, 36), "リセット (R)"))
        {
            ResetRound();
        }

        if (roundScored && GUI.Button(new Rect(25, 178, 130, 36), "もう一度投げる"))
        {
            ResetRound();
        }
    }
}
