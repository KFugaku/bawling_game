using System.Collections.Generic;
using UnityEngine;

public class BowlingGame : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private float throwPower = RegulationBowlingDimensions.ThrowSpeed;
    [Header("Mouse Throw")]
    [SerializeField] private float maximumGestureDistance = 520f;
    [SerializeField] private float minimumThrowPower = 16f;
    [SerializeField] private float heldBallHeight = 2.2f;
    [SerializeField] private float deliveryDuration = 1.35f;
    [SerializeField] private float maximumThrowAngle = 35f;

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
    private float deliveryStartTime;
    private int score;
    private Vector2 gestureStart;
    private LineRenderer aimGuide;

    private const float ReleaseLineZ = 0f;

    private void Awake()
    {
        BowlingAlleyEnvironment.EnsureCreated();
        ResolveSceneReferences();
        CreateReleaseLine();
        CreateAimGuide();

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
            BeginDelivery();
        }

        if (aimingThrow)
        {
            UpdateDelivery();
            UpdateAimGuide();
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (aimingThrow)
            {
                ReleaseGestureThrow();
            }
            else
            {
                DropHeldBall();
            }
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
        ball.position = new Vector3(ballStart.x, heldBallHeight, ballStart.z);
        SetAimGuideVisible(false);
    }

    private void BeginDelivery()
    {
        aimingThrow = true;
        gestureStart = Input.mousePosition;
        deliveryStartTime = Time.time;
        SetAimGuideVisible(true);
    }

    private void UpdateDelivery()
    {
        float progress = GetDeliveryProgress();
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        ball.position = new Vector3(
            ballStart.x,
            Mathf.Lerp(heldBallHeight, RegulationBowlingDimensions.BallRadius, easedProgress),
            Mathf.Lerp(ballStart.z, ReleaseLineZ, easedProgress));
    }

    private void ReleaseGestureThrow()
    {
        ThrowBall(GetGestureDirection(), GetGestureThrowPower());
    }

    private void ThrowBall(Vector3 direction, float power)
    {
        holdingBall = false;
        aimingThrow = false;
        SetAimGuideVisible(false);
        thrown = true;
        throwTime = Time.time;
        ball.isKinematic = false;
        ball.linearVelocity = direction * power;
        ball.angularVelocity = Vector3.Cross(Vector3.up, direction) * (power * 2f);
    }

    private void DropHeldBall()
    {
        holdingBall = false;
        aimingThrow = false;
        SetAimGuideVisible(false);
        ball.isKinematic = false;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
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

    private Vector3 GetGestureDirection()
    {
        Vector2 gesture = GetGesture();
        if (gesture.sqrMagnitude < 1f)
        {
            return Vector3.forward;
        }

        float rawAngle = Mathf.Atan2(gesture.x, Mathf.Max(1f, gesture.y)) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, -maximumThrowAngle, maximumThrowAngle);
        return Quaternion.Euler(0f, clampedAngle, 0f) * Vector3.forward;
    }

    private float GetThrowAngle()
    {
        return Vector3.SignedAngle(Vector3.forward, GetGestureDirection(), Vector3.up);
    }

    private float GetDeliveryProgress()
    {
        return Mathf.Clamp01((Time.time - deliveryStartTime) / Mathf.Max(0.1f, deliveryDuration));
    }

    private void CreateReleaseLine()
    {
        if (GameObject.Find("Release Line") != null)
        {
            return;
        }

        GameObject releaseLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        releaseLine.name = "Release Line";
        releaseLine.transform.position = new Vector3(0f, 0.012f, ReleaseLineZ);
        releaseLine.transform.localScale = new Vector3(
            RegulationBowlingDimensions.LaneWidth,
            0.024f,
            0.12f);

        Collider lineCollider = releaseLine.GetComponent<Collider>();
        if (lineCollider != null)
        {
            Destroy(lineCollider);
        }

        Renderer lineRenderer = releaseLine.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        lineRenderer.material = new Material(shader)
        {
            color = new Color(0.1f, 0.75f, 1f)
        };
    }

    private void CreateAimGuide()
    {
        GameObject guideObject = new GameObject("Throw Direction Guide");
        aimGuide = guideObject.AddComponent<LineRenderer>();
        aimGuide.positionCount = 2;
        aimGuide.startWidth = 0.1f;
        aimGuide.endWidth = 0.035f;
        aimGuide.useWorldSpace = true;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        aimGuide.material = new Material(shader);
        aimGuide.startColor = new Color(0.2f, 1f, 0.85f, 0.95f);
        aimGuide.endColor = new Color(0.2f, 1f, 0.85f, 0.2f);
        SetAimGuideVisible(false);
    }

    private void UpdateAimGuide()
    {
        if (aimGuide == null)
        {
            return;
        }

        Vector3 start = ball.position + Vector3.up * 0.06f;
        float guideLength = Mathf.Lerp(3f, 8f, GetGesturePowerPercent());
        aimGuide.SetPosition(0, start);
        aimGuide.SetPosition(1, start + GetGestureDirection() * guideLength);
    }

    private void SetAimGuideVisible(bool isVisible)
    {
        if (aimGuide != null)
        {
            aimGuide.enabled = isVisible;
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
        ballInGutter = false;
        roundScored = false;
        holdingBall = false;
        aimingThrow = false;
        SetAimGuideVisible(false);
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
        GUI.Label(new Rect(25, 66, 920, 30), "右クリックで持ち上げる → Spaceで助走 → マウスで方向を決めて右クリックを離す", textStyle);

        int pinCount = pins?.Length ?? 0;
        string result = roundScored
            ? (ballInGutter ? "ガター：倒したピン 0 / " + pinCount : $"倒したピン: {score} / {pinCount}")
            : ballInGutter ? "ガター！ ピンは倒せません" : "狙いを定めて投げよう";
        GUI.Label(new Rect(25, 96, 500, 30), result, textStyle);

        if (aimingThrow)
        {
            float powerPercent = GetGesturePowerPercent();
            float angle = GetThrowAngle();
            string directionText = Mathf.Abs(angle) < 0.5f
                ? "正面 0°"
                : angle < 0f ? $"左 {Mathf.Abs(angle):0.0}°" : $"右 {angle:0.0}°";
            GUI.Label(new Rect(25, 126, 230, 30), $"方向：{directionText}", textStyle);
            GUI.Label(new Rect(255, 126, 64, 30), "強さ", textStyle);
            GUI.Box(new Rect(320, 132, 170, 18), string.Empty);
            GUI.Box(new Rect(320, 132, 170f * powerPercent, 18), string.Empty);
            GUI.Label(new Rect(505, 126, 350, 30), $"リリースラインまで {GetDeliveryProgress() * 100f:0}%", textStyle);
        }
        else if (holdingBall)
        {
            GUI.Label(new Rect(25, 126, 700, 30), "ボールを持ち上げています。右クリックを押したままSpaceで助走開始", textStyle);
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
