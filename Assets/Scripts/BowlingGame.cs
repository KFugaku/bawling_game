using System.Collections.Generic;
using UnityEngine;

public class BowlingGame : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private float aimSpeed = 4f;
    [SerializeField] private float throwPower = RegulationBowlingDimensions.ThrowSpeed;
    [Header("Mouse Throw")]
    [SerializeField] private bool enableKeyboardDebug = true;
    [SerializeField] private float minimumGestureForwardDistance = 40f;
    [SerializeField] private float maximumGestureDistance = 520f;
    [SerializeField] private float minimumThrowPower = 16f;

    private readonly Vector3 ballStart = new Vector3(
        0f,
        RegulationBowlingDimensions.BallRadius,
        RegulationBowlingDimensions.BallStartZ);
    private bool thrown;
    private bool ballInGutter;
    private bool roundScored;
    private bool holdingBall;
    private bool aimingThrow;
    private float throwTime;
    private int score;
    private Vector2 gestureStart;

    private void Awake()
    {
        BowlingAlleyEnvironment.EnsureCreated();
        ResolveSceneReferences();

        // Use a low, behind-the-ball view so players can read the lane and aim their throw.
        Camera gameCamera = Camera.main;
        if (gameCamera != null)
        {
            gameCamera.transform.position = new Vector3(
                0f,
                RegulationBowlingDimensions.PlayerCameraHeight,
                RegulationBowlingDimensions.PlayerCameraZ);
            gameCamera.transform.LookAt(new Vector3(
                0f,
                RegulationBowlingDimensions.PlayerCameraTargetHeight,
                RegulationBowlingDimensions.PlayerCameraTargetZ));
            gameCamera.fieldOfView = RegulationBowlingDimensions.PlayerCameraFieldOfView;
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
            HandleMouseThrowInput();

            if (enableKeyboardDebug && !holdingBall)
            {
                HandleKeyboardDebugInput();
            }
        }

        if (thrown && !ballInGutter && Mathf.Abs(ball.position.x) > RegulationBowlingDimensions.GutterEntryX)
        {
            ballInGutter = true;
        }

        if (thrown && !roundScored && Time.time - throwTime > 4f)
        {
            score = ballInGutter ? 0 : CountFallenPins();
            roundScored = true;
        }

    }

    private void HandleMouseThrowInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            BeginHoldingBall();
        }

        if (!holdingBall)
        {
            return;
        }

        if (!aimingThrow && Input.GetKeyDown(KeyCode.Space))
        {
            aimingThrow = true;
            gestureStart = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (aimingThrow && GetGesture().y >= minimumGestureForwardDistance)
            {
                ReleaseGestureThrow();
            }
            else
            {
                CancelHeldBall();
            }
        }
    }

    private void HandleKeyboardDebugInput()
    {
        float movement = Input.GetAxisRaw("Horizontal") * aimSpeed * Time.deltaTime;
        Vector3 nextPosition = ball.position + new Vector3(movement, 0f, 0f);
        nextPosition.x = Mathf.Clamp(nextPosition.x, -RegulationBowlingDimensions.AimLimit, RegulationBowlingDimensions.AimLimit);
        ball.MovePosition(nextPosition);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ThrowBall(Vector3.forward, throwPower);
        }
    }

    private void BeginHoldingBall()
    {
        holdingBall = true;
        aimingThrow = false;
        ball.isKinematic = false;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
    }

    private void ReleaseGestureThrow()
    {
        Vector2 gesture = GetGesture();
        Vector3 direction = new Vector3(gesture.x, 0f, gesture.y).normalized;
        ThrowBall(direction, GetGestureThrowPower());
    }

    private void ThrowBall(Vector3 direction, float power)
    {
        holdingBall = false;
        aimingThrow = false;
        thrown = true;
        throwTime = Time.time;
        ball.isKinematic = false;
        ball.linearVelocity = direction * power;
        ball.angularVelocity = Vector3.Cross(Vector3.up, direction) * (power * 2f);
    }

    private void CancelHeldBall()
    {
        holdingBall = false;
        aimingThrow = false;
        ResetBallToStart();
    }

    private Vector2 GetGesture()
    {
        Vector2 gesture = (Vector2)Input.mousePosition - gestureStart;
        return new Vector2(gesture.x, Mathf.Max(0f, gesture.y));
    }

    private float GetGestureThrowPower()
    {
        float gestureAmount = Mathf.Clamp01(GetGesture().magnitude / maximumGestureDistance);
        return Mathf.Lerp(minimumThrowPower, throwPower, gestureAmount);
    }

    private float GetGesturePowerPercent()
    {
        return Mathf.InverseLerp(minimumThrowPower, throwPower, GetGestureThrowPower());
    }

    private string GetGestureDirectionName()
    {
        Vector2 gesture = GetGesture();
        if (Mathf.Abs(gesture.x) <= gesture.y * 0.25f)
        {
            return "正面";
        }

        return gesture.x < 0f ? "左前" : "右前";
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
        ballInGutter = false;
        roundScored = false;
        holdingBall = false;
        aimingThrow = false;
        score = 0;

        if (ball != null)
        {
            ResetBallToStart();
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

    private void ResetBallToStart()
    {
        // Velocity can only be changed while the Rigidbody is dynamic.
        ball.isKinematic = false;
        ball.linearDamping = 0f;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.position = ballStart;
        ball.rotation = Quaternion.identity;
        ball.isKinematic = true;
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
        GUI.Label(new Rect(25, 66, 920, 30), "右クリックで持つ → Spaceで構える → マウスを前へ動かして右クリックを離す", textStyle);

        int pinCount = pins?.Length ?? 0;
        string result = roundScored
            ? (ballInGutter ? "ガター：倒したピン 0 / " + pinCount : $"倒したピン: {score} / {pinCount}")
            : ballInGutter ? "ガター！ ピンは倒せません" : "狙いを定めて投げよう";
        GUI.Label(new Rect(25, 96, 500, 30), result, textStyle);

        if (aimingThrow)
        {
            float powerPercent = GetGesturePowerPercent();
            GUI.Label(new Rect(25, 126, 180, 30), $"投球準備：{GetGestureDirectionName()}", textStyle);
            GUI.Label(new Rect(205, 126, 64, 30), "強さ", textStyle);
            GUI.Box(new Rect(270, 132, 170, 18), string.Empty);
            GUI.Box(new Rect(270, 132, 170f * powerPercent, 18), string.Empty);
            GUI.Label(new Rect(450, 126, 220, 30), "右クリックを離して投球", textStyle);
        }
        else if (holdingBall)
        {
            GUI.Label(new Rect(25, 126, 700, 30), "ボールを持っています。Spaceで投球準備、右クリックを離すとキャンセル", textStyle);
        }

        if (GUI.Button(new Rect(25, 168, 130, 36), "リセット (R)"))
        {
            ResetRound();
        }

        if (roundScored && GUI.Button(new Rect(25, 212, 130, 36), "もう一度投げる"))
        {
            ResetRound();
        }
    }
}
