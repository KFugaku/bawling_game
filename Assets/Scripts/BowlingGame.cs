using System.Collections.Generic;
using UnityEngine;

public class BowlingGame : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private float throwPower = RegulationBowlingDimensions.ThrowSpeed;
    [Header("Mouse Throw")]
    [SerializeField] private float heldBallHeight = 2.2f;
    [SerializeField] private float forwardMouseSensitivity = 0.16f;
    [SerializeField] private float horizontalMouseSensitivity = 0.04f;
    [SerializeField] private float horizontalDirectionScale = 0.3f;
    [SerializeField] private float throwSpeedBoost = 1.15f;
    [SerializeField] private float minimumThrowPower = 1.5f;
    [SerializeField] private float slowMouseSpeed = 2f;
    [SerializeField] private float fastMouseSpeed = 30f;
    [SerializeField] private float mouseSpeedExponent = 2f;
    [SerializeField] private float mouseSpeedAveraging = 7f;
    [SerializeField] private float velocitySmoothing = 18f;

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
    private float deliveryMouseSpeed;
    private int mouseInputWarmupFrames;
    private int score;
    private Vector3 deliveryVelocity;
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

        if (aimingThrow)
        {
            UpdateDelivery();
            UpdateAimGuide();
        }

        if (Input.GetMouseButtonUp(1))
        {
            ReleaseGestureThrow();
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
        BeginDelivery();
    }

    private void BeginDelivery()
    {
        aimingThrow = true;
        deliveryVelocity = Vector3.zero;
        deliveryMouseSpeed = 0f;
        mouseInputWarmupFrames = 2;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetAimGuideVisible(true);
    }

    private void UpdateDelivery()
    {
        Vector2 mouseMovement = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y"));
        if (mouseInputWarmupFrames > 0)
        {
            mouseInputWarmupFrames--;
            mouseMovement = Vector2.zero;
        }

        UpdateMouseSpeed(mouseMovement);

        Vector3 previousPosition = ball.position;
        float nextX = Mathf.Clamp(
            previousPosition.x + mouseMovement.x * horizontalMouseSensitivity,
            -RegulationBowlingDimensions.AimLimit,
            RegulationBowlingDimensions.AimLimit);
        float nextZ = Mathf.Clamp(
            previousPosition.z + mouseMovement.y * forwardMouseSensitivity,
            ballStart.z,
            ReleaseLineZ);

        // Height above the lane is proportional to the square of the remaining
        // distance. Its slope therefore reaches zero exactly at the release line.
        float remainingDistanceRatio = Mathf.Clamp01(
            (ReleaseLineZ - nextZ) / (ReleaseLineZ - ballStart.z));
        float heightAboveLane = (heldBallHeight - RegulationBowlingDimensions.BallRadius)
            * remainingDistanceRatio * remainingDistanceRatio;
        Vector3 nextPosition = new Vector3(
            nextX,
            RegulationBowlingDimensions.BallRadius + heightAboveLane,
            nextZ);
        ball.position = nextPosition;

        Vector3 frameVelocity = (nextPosition - previousPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        frameVelocity.y = 0f;
        if (frameVelocity.sqrMagnitude > 0.0001f)
        {
            float smoothing = 1f - Mathf.Exp(-velocitySmoothing * Time.deltaTime);
            deliveryVelocity = Vector3.Lerp(deliveryVelocity, frameVelocity, smoothing);
        }
    }

    private void UpdateMouseSpeed(Vector2 mouseMovement)
    {
        // Only forward mouse motion builds approach speed. An exponential moving
        // average prevents a single-frame flick from becoming a maximum-power throw.
        float instantForwardSpeed = Mathf.Max(0f, mouseMovement.y)
            / Mathf.Max(Time.deltaTime, 0.001f);
        float averaging = 1f - Mathf.Exp(-mouseSpeedAveraging * Time.deltaTime);
        deliveryMouseSpeed = Mathf.Lerp(deliveryMouseSpeed, instantForwardSpeed, averaging);
    }

    private void ReleaseGestureThrow()
    {
        ThrowBall(GetReleaseDirection(), GetReleaseSpeed());
    }

    private void ThrowBall(Vector3 direction, float power)
    {
        holdingBall = false;
        aimingThrow = false;
        UnlockCursor();
        SetAimGuideVisible(false);
        thrown = true;
        throwTime = Time.time;
        ball.isKinematic = false;
        ball.linearVelocity = direction * power;
        ball.angularVelocity = Vector3.Cross(Vector3.up, direction) * (power * 2f);
    }

    private float GetReleasePowerPercent()
    {
        return Mathf.InverseLerp(
            minimumThrowPower,
            throwPower * throwSpeedBoost,
            GetReleaseSpeed());
    }

    private Vector3 GetReleaseDirection()
    {
        Vector3 forwardVelocity = GetAdjustedReleaseVelocity();
        if (forwardVelocity.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return forwardVelocity.normalized;
    }

    private float GetReleaseSpeed()
    {
        float maximumReleaseSpeed = throwPower * throwSpeedBoost;
        float normalizedMouseSpeed = Mathf.InverseLerp(
            slowMouseSpeed,
            fastMouseSpeed,
            deliveryMouseSpeed);
        float acceleratedMouseSpeed = Mathf.Pow(normalizedMouseSpeed, mouseSpeedExponent);
        float inputSpeed = Mathf.Lerp(minimumThrowPower, maximumReleaseSpeed, acceleratedMouseSpeed);

        // The approach unlocks speed quadratically. Releasing halfway down the
        // approach can therefore use only 25% of the available speed range.
        float progress = GetDeliveryProgress();
        float approachLimit = Mathf.Lerp(
            minimumThrowPower,
            maximumReleaseSpeed,
            progress * progress);
        return Mathf.Min(inputSpeed, approachLimit);
    }

    private Vector3 GetAdjustedReleaseVelocity()
    {
        return new Vector3(
            deliveryVelocity.x * horizontalDirectionScale,
            0f,
            Mathf.Max(0f, deliveryVelocity.z));
    }

    private float GetThrowAngle()
    {
        return Vector3.SignedAngle(Vector3.forward, GetReleaseDirection(), Vector3.up);
    }

    private float GetDeliveryProgress()
    {
        if (ball == null)
        {
            return 0f;
        }

        return Mathf.InverseLerp(ballStart.z, ReleaseLineZ, ball.position.z);
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
        float guideLength = Mathf.Lerp(3f, 8f, GetReleasePowerPercent());
        aimGuide.SetPosition(0, start);
        aimGuide.SetPosition(1, start + GetReleaseDirection() * guideLength);
    }

    private void SetAimGuideVisible(bool isVisible)
    {
        if (aimGuide != null)
        {
            aimGuide.enabled = isVisible;
        }
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        UnlockCursor();
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
        deliveryVelocity = Vector3.zero;
        deliveryMouseSpeed = 0f;
        mouseInputWarmupFrames = 0;
        UnlockCursor();
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
        ball.gameObject.SetActive(true);
        Renderer[] ballRenderers = ball.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer ballRenderer in ballRenderers)
        {
            ballRenderer.enabled = true;
        }

        // Velocity can only be changed while the Rigidbody is dynamic.
        ball.isKinematic = false;
        ball.detectCollisions = true;
        ball.linearDamping = 0f;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.transform.SetPositionAndRotation(ballStart, Quaternion.identity);
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
        GUI.Label(new Rect(25, 66, 1000, 30), "右クリックで投球開始 → マウス移動でボールを運ぶ → 右クリックを離してリリース", textStyle);

        int pinCount = pins?.Length ?? 0;
        string result = roundScored
            ? (ballInGutter ? "ガター：倒したピン 0 / " + pinCount : $"倒したピン: {score} / {pinCount}")
            : ballInGutter ? "ガター！ ピンは倒せません" : "狙いを定めて投げよう";
        GUI.Label(new Rect(25, 96, 500, 30), result, textStyle);

        if (aimingThrow)
        {
            float powerPercent = GetReleasePowerPercent();
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
