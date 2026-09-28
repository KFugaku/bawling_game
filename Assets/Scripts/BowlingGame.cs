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
    [SerializeField] private float horizontalDirectionScale = 0.15f;
    [SerializeField] private float horizontalDirectionDeadZone = 0.12f;
    [SerializeField] private float maximumThrowAngle = 8f;
    [SerializeField] private float throwSpeedBoost = 1.15f;
    [SerializeField] private float minimumThrowPower = 1.5f;
    [SerializeField] private float slowMouseSpeed = 2f;
    [SerializeField] private float fastMouseSpeed = 30f;
    [SerializeField] private float mouseSpeedExponent = 2f;
    [SerializeField] private float mouseSpeedAveraging = 7f;
    [SerializeField] private float velocitySmoothing = 18f;
    [Header("Ball Curve")]
    [SerializeField] private float wheelSpinSampleWindow = 0.2f;
    [SerializeField] private float wheelDeltaForMaximumSpin = 0.4f;
    [SerializeField] private float maximumSideSpin = 26f;
    [SerializeField] private float curveAcceleration = 1.6f;
    [SerializeField] private float curveRampTime = 1.1f;
    [SerializeField] private float spinDecayPerSecond = 0.08f;
    [SerializeField] private float minimumCurveSpeed = 1f;
    [SerializeField] private float curveGuideOffset = 2f;
    [Header("Game Flow")]
    [SerializeField] private float minimumRollDuration = 2.2f;
    [SerializeField] private float maximumRollDuration = 12f;
    [SerializeField] private float pinSettleDuration = 0.8f;
    [SerializeField] private float pinLinearSettleSpeed = 0.08f;
    [SerializeField] private float pinAngularSettleSpeed = 0.25f;

    private readonly Vector3 ballStart = new Vector3(
        0f,
        RegulationBowlingDimensions.BallRadius,
        RegulationBowlingDimensions.BallStartZ);
    private bool thrown;
    private bool ballInGutter;
    private bool holdingBall;
    private bool aimingThrow;
    private bool waitingForNextRoll;
    private bool gameComplete;
    private float throwTime;
    private float pinsStillSince = -1f;
    private float deliveryMouseSpeed;
    private float selectedCurveSpin;
    private float activeCurveSpin;
    private float sampledWheelDelta;
    private int mouseInputWarmupFrames;
    private int currentFrameIndex;
    private int pinsStandingAtRollStart;
    private int lastRollPins;
    private Vector3 deliveryVelocity;
    private LineRenderer aimGuide;
    private readonly Queue<WheelSpinSample> wheelSpinSamples = new Queue<WheelSpinSample>();
    private readonly List<int> rolls = new List<int>();
    private readonly List<int> currentFrameRolls = new List<int>();
    private RackMode nextRackMode = RackMode.FullRack;
    private string statusMessage = string.Empty;
    private string celebrationText = string.Empty;
    private string celebrationSubtext = string.Empty;
    private Color celebrationColor = Color.white;
    private float celebrationStartTime = -10f;

    private const float ReleaseLineZ = 0f;
    private const int AimGuidePointCount = 16;
    private const float CelebrationDuration = 2.4f;

    private enum RackMode
    {
        FullRack,
        StandingPins
    }

    private readonly struct WheelSpinSample
    {
        public WheelSpinSample(float time, float delta)
        {
            Time = time;
            Delta = delta;
        }

        public float Time { get; }
        public float Delta { get; }
    }

    private void Awake()
    {
        BowlingAlleyEnvironment.EnsureCreated();
        BowlingAudioFeedback.EnsureCreated();
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
        StartNewGame();
    }

    private void Update()
    {
        if (ball == null)
        {
            return;
        }

        if (gameComplete)
        {
            return;
        }

        if (waitingForNextRoll)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PrepareNextRoll();
            }

            return;
        }

        if (!thrown)
        {
            HandleMouseThrowInput();
            return;
        }

        if (!ballInGutter && IsBallActuallyInGutter())
        {
            ballInGutter = true;
        }

        UpdateRollCompletion();
    }

    private void FixedUpdate()
    {
        ApplyBallCurve();
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
            UpdateCurveSelection();
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
        ClearCurveInputSamples();
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
        // The release line is visual-only. Keep a ball released directly on it
        // just clear of the line and hand the final kinematic position to physics
        // before applying its roll velocity.
        if (ball.position.z >= ReleaseLineZ - 0.02f)
        {
            ball.position = new Vector3(
                ball.position.x,
                Mathf.Max(ball.position.y, RegulationBowlingDimensions.BallRadius + 0.02f),
                ReleaseLineZ + 0.02f);
            Physics.SyncTransforms();
        }

        ThrowBall(GetReleaseDirection(), GetReleaseSpeed(), selectedCurveSpin);
    }

    private void ThrowBall(Vector3 direction, float power, float curveSpin)
    {
        holdingBall = false;
        aimingThrow = false;
        UnlockCursor();
        SetAimGuideVisible(false);
        thrown = true;
        throwTime = Time.time;
        pinsStillSince = -1f;
        statusMessage = "投球中：ボールとピンが止まるまでお待ちください";
        activeCurveSpin = curveSpin;
        BowlingAudioFeedback.Instance?.PlayThrow(Mathf.InverseLerp(
            minimumThrowPower,
            throwPower * throwSpeedBoost,
            power));
        ball.isKinematic = false;
        ball.linearVelocity = direction * power;
        Vector3 rollingSpin = Vector3.Cross(Vector3.up, direction) * (power * 2f);
        Vector3 sideSpin = Vector3.up * (activeCurveSpin * maximumSideSpin);
        ball.angularVelocity = rollingSpin + sideSpin;
        ball.WakeUp();
    }

    private void UpdateCurveSelection()
    {
        float currentTime = Time.unscaledTime;
        float wheelMovement = -Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(wheelMovement) >= 0.0001f)
        {
            wheelSpinSamples.Enqueue(new WheelSpinSample(currentTime, wheelMovement));
            sampledWheelDelta += wheelMovement;
        }

        float oldestAllowedTime = currentTime - Mathf.Max(wheelSpinSampleWindow, 0.01f);
        while (wheelSpinSamples.Count > 0 && wheelSpinSamples.Peek().Time < oldestAllowedTime)
        {
            sampledWheelDelta -= wheelSpinSamples.Dequeue().Delta;
        }

        selectedCurveSpin = Mathf.Clamp(
            sampledWheelDelta / Mathf.Max(wheelDeltaForMaximumSpin, 0.01f),
            -1f,
            1f);
    }

    private void ClearCurveInputSamples()
    {
        wheelSpinSamples.Clear();
        sampledWheelDelta = 0f;
        selectedCurveSpin = 0f;
    }

    private void ApplyBallCurve()
    {
        if (!thrown || ball == null || ball.isKinematic || ballInGutter)
        {
            return;
        }

        Vector3 laneVelocity = ball.linearVelocity;
        laneVelocity.y = 0f;
        float laneSpeed = laneVelocity.magnitude;
        if (laneSpeed < minimumCurveSpeed || Mathf.Abs(activeCurveSpin) < 0.001f)
        {
            return;
        }

        float ramp = Mathf.Clamp01((Time.time - throwTime) / Mathf.Max(curveRampTime, 0.01f));
        float speedRatio = Mathf.Clamp01(laneSpeed / Mathf.Max(throwPower * throwSpeedBoost, 0.01f));
        Vector3 curveDirection = Vector3.Cross(Vector3.up, laneVelocity.normalized);
        ball.AddForce(
            curveDirection * (activeCurveSpin * curveAcceleration * ramp * speedRatio),
            ForceMode.Acceleration);
        activeCurveSpin = Mathf.MoveTowards(
            activeCurveSpin,
            0f,
            spinDecayPerSecond * Time.fixedDeltaTime);
    }

    private void UpdateRollCompletion()
    {
        float elapsed = Time.time - throwTime;
        if (elapsed < minimumRollDuration)
        {
            return;
        }

        bool timedOut = elapsed >= maximumRollDuration;
        if (!timedOut && !HasBallFinishedTraveling())
        {
            return;
        }

        if (!timedOut && ArePinsMoving())
        {
            pinsStillSince = -1f;
            return;
        }

        if (pinsStillSince < 0f)
        {
            pinsStillSince = Time.time;
        }

        if (timedOut || Time.time - pinsStillSince >= pinSettleDuration)
        {
            CompleteRoll();
        }
    }

    private bool HasBallFinishedTraveling()
    {
        Vector3 velocity = ball.linearVelocity;
        velocity.y = 0f;
        bool stopped = velocity.sqrMagnitude < 0.04f;
        // Do not start the settle timer before the ball reaches the head pin.
        // With a slow throw, the old threshold was over one unit in front of the
        // rack and could finalize a zero just before the collision happened.
        bool reachedPinDeck = ball.position.z >= RegulationBowlingDimensions.FoulLineToHeadPin;
        bool leftPlayableArea = ball.position.y < -2f;
        return stopped || reachedPinDeck || leftPlayableArea;
    }

    private bool ArePinsMoving()
    {
        if (pins == null)
        {
            return false;
        }

        foreach (BowlingPin pin in pins)
        {
            if (pin != null && pin.gameObject.activeInHierarchy &&
                pin.IsMoving(pinLinearSettleSpeed, pinAngularSettleSpeed))
            {
                return true;
            }
        }

        return false;
    }

    private void CompleteRoll()
    {
        int standingPins = CountStandingPins();
        // Always keep the score consistent with the pins the player actually saw fall.
        // A real gutter ball cannot reach the rack, so a separate gutter flag must not
        // erase pins after a curved ball crosses the lane edge near the pin deck.
        int knockedPins = Mathf.Clamp(
            pinsStandingAtRollStart - standingPins,
            0,
            pinsStandingAtRollStart);

        rolls.Add(knockedPins);
        currentFrameRolls.Add(knockedPins);
        lastRollPins = knockedPins;
        TriggerRollCelebration();
        HideBallAfterRoll();
        thrown = false;
        activeCurveSpin = 0f;

        ResolveNextRollState();
        Physics.SyncTransforms();
    }

    private void TriggerRollCelebration()
    {
        bool strike = false;
        bool spare = false;

        if (currentFrameIndex < BowlingScoreCalculator.FrameCount - 1)
        {
            strike = currentFrameRolls.Count == 1 &&
                currentFrameRolls[0] == BowlingScoreCalculator.PinsPerRack;
            spare = currentFrameRolls.Count == 2 &&
                currentFrameRolls[0] < BowlingScoreCalculator.PinsPerRack &&
                currentFrameRolls[0] + currentFrameRolls[1] == BowlingScoreCalculator.PinsPerRack;
        }
        else if (currentFrameRolls.Count == 1)
        {
            strike = currentFrameRolls[0] == BowlingScoreCalculator.PinsPerRack;
        }
        else if (currentFrameRolls.Count == 2)
        {
            int first = currentFrameRolls[0];
            int second = currentFrameRolls[1];
            strike = first == BowlingScoreCalculator.PinsPerRack &&
                second == BowlingScoreCalculator.PinsPerRack;
            spare = first < BowlingScoreCalculator.PinsPerRack &&
                first + second == BowlingScoreCalculator.PinsPerRack;
        }
        else if (currentFrameRolls.Count == 3)
        {
            int first = currentFrameRolls[0];
            int second = currentFrameRolls[1];
            int third = currentFrameRolls[2];
            strike = third == BowlingScoreCalculator.PinsPerRack;
            spare = first == BowlingScoreCalculator.PinsPerRack &&
                second < BowlingScoreCalculator.PinsPerRack &&
                second + third == BowlingScoreCalculator.PinsPerRack;
        }

        if (strike)
        {
            ShowCelebration("STRIKE!", "ストライク！", new Color(1f, 0.72f, 0.12f));
            BowlingAudioFeedback.Instance?.PlayStrike();
        }
        else if (spare)
        {
            ShowCelebration("SPARE!", "ナイスカバー！", new Color(0.18f, 0.88f, 1f));
            BowlingAudioFeedback.Instance?.PlaySpare();
        }
    }

    private void ShowCelebration(string text, string subtext, Color color)
    {
        celebrationText = text;
        celebrationSubtext = subtext;
        celebrationColor = color;
        celebrationStartTime = Time.unscaledTime;
    }

    private bool IsBallActuallyInGutter()
    {
        // Crossing the lane edge alone is not enough: a strongly curved ball can
        // cross that boundary beside the pin deck after it has already hit pins.
        // Treat it as a gutter only after its centre has dropped below lane level,
        // and only while it is still in front of the rack.
        float gutterCentreX = RegulationBowlingDimensions.GutterEntryX +
            RegulationBowlingDimensions.BallRadius * 0.2f;
        float gutterCentreY = RegulationBowlingDimensions.BallRadius * 0.85f;
        float pinDeckStartZ = RegulationBowlingDimensions.FoulLineToHeadPin -
            RegulationBowlingDimensions.PinRowDepth;

        return Mathf.Abs(ball.position.x) > gutterCentreX &&
            ball.position.y < gutterCentreY &&
            ball.position.z < pinDeckStartZ;
    }

    private void ResolveNextRollState()
    {
        int completedFrameNumber = currentFrameIndex + 1;
        if (currentFrameIndex < BowlingScoreCalculator.FrameCount - 1)
        {
            bool strike = currentFrameRolls.Count == 1 &&
                currentFrameRolls[0] == BowlingScoreCalculator.PinsPerRack;
            if (currentFrameRolls.Count == 1 && !strike)
            {
                nextRackMode = RackMode.StandingPins;
                HideFallenPins();
                waitingForNextRoll = true;
                statusMessage = $"第{completedFrameNumber}フレーム 1投目：{lastRollPins}ピン。Spaceで2投目";
                return;
            }

            bool spare = !strike && currentFrameRolls.Count >= 2 &&
                currentFrameRolls[0] + currentFrameRolls[1] == BowlingScoreCalculator.PinsPerRack;
            currentFrameIndex++;
            currentFrameRolls.Clear();
            nextRackMode = RackMode.FullRack;
            waitingForNextRoll = true;
            string result = strike ? "ストライク！" : spare ? "スペア！" : $"{lastRollPins}ピン";
            statusMessage = $"第{completedFrameNumber}フレーム終了：{result} Spaceで第{currentFrameIndex + 1}フレーム";
            return;
        }

        ResolveTenthFrameState();
    }

    private void ResolveTenthFrameState()
    {
        int first = currentFrameRolls[0];
        if (currentFrameRolls.Count == 1)
        {
            nextRackMode = first == BowlingScoreCalculator.PinsPerRack
                ? RackMode.FullRack
                : RackMode.StandingPins;
            if (nextRackMode == RackMode.StandingPins)
            {
                HideFallenPins();
            }

            waitingForNextRoll = true;
            string result = first == BowlingScoreCalculator.PinsPerRack ? "ストライク！" : $"{first}ピン";
            statusMessage = $"第10フレーム 1投目：{result} Spaceで2投目";
            return;
        }

        int second = currentFrameRolls[1];
        if (currentFrameRolls.Count == 2)
        {
            bool firstWasStrike = first == BowlingScoreCalculator.PinsPerRack;
            bool spare = !firstWasStrike && first + second == BowlingScoreCalculator.PinsPerRack;
            if (firstWasStrike || spare)
            {
                nextRackMode = firstWasStrike && second < BowlingScoreCalculator.PinsPerRack
                    ? RackMode.StandingPins
                    : RackMode.FullRack;
                if (nextRackMode == RackMode.StandingPins)
                {
                    HideFallenPins();
                }

                waitingForNextRoll = true;
                string result = spare
                    ? "スペア！"
                    : second == BowlingScoreCalculator.PinsPerRack ? "ストライク！" : second + "ピン";
                statusMessage = $"第10フレーム 2投目：{result} Spaceでボーナス投球";
                return;
            }
        }

        CompleteGame();
    }

    private void PrepareNextRoll()
    {
        ResetThrowState();
        if (nextRackMode == RackMode.FullRack)
        {
            ResetAllPins();
        }

        ResetBallToStart();
        pinsStandingAtRollStart = CountStandingPins();
        waitingForNextRoll = false;
        int rollNumber = currentFrameRolls.Count + 1;
        statusMessage = $"第{currentFrameIndex + 1}フレーム・{rollNumber}投目：右クリックで投球開始";
        Physics.SyncTransforms();
    }

    private void StartNewGame()
    {
        rolls.Clear();
        currentFrameRolls.Clear();
        currentFrameIndex = 0;
        lastRollPins = 0;
        gameComplete = false;
        waitingForNextRoll = false;
        nextRackMode = RackMode.FullRack;
        celebrationText = string.Empty;
        celebrationSubtext = string.Empty;
        celebrationStartTime = -10f;
        ResetThrowState();
        ResetAllPins();
        ResetBallToStart();
        pinsStandingAtRollStart = CountStandingPins();
        statusMessage = "第1フレーム・1投目：右クリックで投球開始";
        Physics.SyncTransforms();
    }

    private void CompleteGame()
    {
        waitingForNextRoll = false;
        gameComplete = true;
        int?[] cumulativeScores = BowlingScoreCalculator.CalculateCumulativeScores(rolls);
        int finalScore = cumulativeScores[BowlingScoreCalculator.FrameCount - 1] ?? 0;
        statusMessage = $"ゲーム終了！ 最終スコア：{finalScore}";
    }

    private void ResetThrowState()
    {
        thrown = false;
        ballInGutter = false;
        holdingBall = false;
        aimingThrow = false;
        pinsStillSince = -1f;
        deliveryVelocity = Vector3.zero;
        deliveryMouseSpeed = 0f;
        ClearCurveInputSamples();
        activeCurveSpin = 0f;
        mouseInputWarmupFrames = 0;
        UnlockCursor();
        SetAimGuideVisible(false);
    }

    private void ResetAllPins()
    {
        if (pins == null)
        {
            return;
        }

        foreach (BowlingPin pin in pins)
        {
            if (pin != null)
            {
                pin.ResetPin();
            }
        }
    }

    private void HideFallenPins()
    {
        if (pins == null)
        {
            return;
        }

        foreach (BowlingPin pin in pins)
        {
            if (pin != null && pin.gameObject.activeInHierarchy && pin.IsFallen)
            {
                pin.HidePin();
            }
        }
    }

    private int CountStandingPins()
    {
        int standing = 0;
        if (pins == null)
        {
            return standing;
        }

        foreach (BowlingPin pin in pins)
        {
            if (pin != null && pin.gameObject.activeInHierarchy && !pin.IsFallen)
            {
                standing++;
            }
        }

        return standing;
    }

    private void HideBallAfterRoll()
    {
        if (ball == null)
        {
            return;
        }

        ball.isKinematic = false;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
        ball.gameObject.SetActive(false);
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
        float forwardSpeed = Mathf.Max(0f, deliveryVelocity.z);
        float horizontalSpeed = deliveryVelocity.x * horizontalDirectionScale;

        // Ignore tiny lateral input at release. This keeps hand jitter from
        // becoming a sharp throw angle while preserving intentional movement.
        if (Mathf.Abs(horizontalSpeed) < horizontalDirectionDeadZone)
        {
            horizontalSpeed = 0f;
        }

        // Direction used to be based on the most recent mouse velocity with no
        // cap. A small sideways motion could therefore dominate a slow forward
        // motion. Keep the analogue feel, but constrain it to a usable angle.
        float maximumHorizontalSpeed = forwardSpeed
            * Mathf.Tan(maximumThrowAngle * Mathf.Deg2Rad);
        horizontalSpeed = Mathf.Clamp(
            horizontalSpeed,
            -maximumHorizontalSpeed,
            maximumHorizontalSpeed);

        return new Vector3(
            horizontalSpeed,
            0f,
            forwardSpeed);
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
        GameObject releaseLine = GameObject.Find("Release Line");
        if (releaseLine != null)
        {
            DisableReleaseLineCollider(releaseLine);
            return;
        }

        releaseLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        releaseLine.name = "Release Line";
        releaseLine.transform.position = new Vector3(0f, 0.012f, ReleaseLineZ);
        releaseLine.transform.localScale = new Vector3(
            RegulationBowlingDimensions.LaneWidth,
            0.024f,
            0.12f);

        DisableReleaseLineCollider(releaseLine);

        Renderer lineRenderer = releaseLine.GetComponent<Renderer>();
        if (lineRenderer != null)
        {
            // Reuse the primitive's built-in material instead of looking up a shader by
            // name. Shader.Find targets can be stripped from WebGL builds, which would
            // make Material's constructor throw and stop the rest of Awake from running.
            lineRenderer.material.color = new Color(0.1f, 0.75f, 1f);
        }
    }

    private static void DisableReleaseLineCollider(GameObject releaseLine)
    {
        Collider lineCollider = releaseLine.GetComponent<Collider>();
        if (lineCollider != null)
        {
            // Disabling is immediate, unlike Destroy, so the ball can never hit
            // the visual marker during the same frame it is released.
            lineCollider.enabled = false;
        }
    }

    private void CreateAimGuide()
    {
        GameObject guideObject = new GameObject("Throw Direction Guide");
        aimGuide = guideObject.AddComponent<LineRenderer>();
        aimGuide.positionCount = AimGuidePointCount;
        aimGuide.startWidth = 0.1f;
        aimGuide.endWidth = 0.035f;
        aimGuide.useWorldSpace = true;

        Renderer releaseLineRenderer = GameObject.Find("Release Line")?.GetComponent<Renderer>();
        Renderer ballRenderer = ball != null ? ball.GetComponent<Renderer>() : null;
        Material sourceMaterial = releaseLineRenderer != null
            ? releaseLineRenderer.sharedMaterial
            : ballRenderer != null ? ballRenderer.sharedMaterial : null;
        if (sourceMaterial != null)
        {
            aimGuide.material = new Material(sourceMaterial)
            {
                color = Color.white
            };
        }

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
        Vector3 forward = GetReleaseDirection();
        Vector3 curveDirection = Vector3.Cross(Vector3.up, forward);
        for (int index = 0; index < AimGuidePointCount; index++)
        {
            float progress = index / (AimGuidePointCount - 1f);
            float forwardDistance = guideLength * progress;
            float curveDistance = selectedCurveSpin * curveGuideOffset * progress * progress;
            aimGuide.SetPosition(
                index,
                start + forward * forwardDistance + curveDirection * curveDistance);
        }
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
        // Lay the HUD out against the rendered game resolution instead of fixed
        // editor pixels. This keeps every score cell on screen at any aspect ratio.
        Matrix4x4 previousGuiMatrix = GUI.matrix;
        float uiScale = Mathf.Clamp(Screen.height / 900f, 0.85f, 1.35f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float viewWidth = Screen.width / uiScale;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            normal = { textColor = Color.white }
        };
        GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUIStyle helpStyle = new GUIStyle(textStyle)
        {
            fontSize = 18
        };
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        Color previousColor = GUI.color;
        GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
        GUI.Box(new Rect(12f, 10f, viewWidth - 24f, 288f), GUIContent.none);
        GUI.color = previousColor;

        GUI.Label(new Rect(24f, 18f, viewWidth - 48f, 42f), "Mini Bowling", titleStyle);
        GUI.Label(
            new Rect(25f, 57f, viewWidth - 50f, 28f),
            "右クリック長押しでボールを持つ → マウス移動で助走・方向を調整 → 右クリックを離して投球（離す直前のホイール操作でカーブ）",
            helpStyle);
        GUI.Label(new Rect(25f, 87f, viewWidth - 50f, 32f), statusMessage, textStyle);
        DrawScoreboard(scoreStyle, textStyle, helpStyle, viewWidth);

        if (aimingThrow)
        {
            float powerPercent = GetReleasePowerPercent();
            float angle = GetThrowAngle();
            string directionText = Mathf.Abs(angle) < 0.5f
                ? "正面 0°"
                : angle < 0f ? $"左 {Mathf.Abs(angle):0.0}°" : $"右 {angle:0.0}°";
            GUI.Label(new Rect(25f, 310f, 245f, 34f), $"方向：{directionText}", textStyle);
            GUI.Label(new Rect(270f, 310f, 60f, 34f), "強さ", textStyle);
            GUI.Box(new Rect(335f, 318f, 220f, 22f), string.Empty);
            GUI.Box(new Rect(335f, 318f, 220f * powerPercent, 22f), string.Empty);
            GUI.Label(
                new Rect(575f, 310f, viewWidth - 600f, 34f),
                $"リリースラインまで {GetDeliveryProgress() * 100f:0}%",
                textStyle);
            string curveText = Mathf.Abs(selectedCurveSpin) < 0.01f
                ? "なし"
                : selectedCurveSpin < 0f ? "左カーブ" : "右カーブ";
            GUI.Label(
                new Rect(25f, 346f, viewWidth - 50f, 34f),
                $"リリース回転：{curveText} {Mathf.Abs(selectedCurveSpin) * 100f:0}%（ホイール速度で変化）",
                textStyle);
        }

        if (waitingForNextRoll)
        {
            GUI.Label(new Rect(25f, 310f, viewWidth - 50f, 34f), "Spaceを押して次の投球へ", textStyle);
        }

        if (gameComplete && GUI.Button(
            new Rect(25f, 310f, 220f, 52f),
            "新しいゲーム",
            buttonStyle))
        {
            StartNewGame();
        }

        DrawCelebrationOverlay(viewWidth, Screen.height / uiScale);

        GUI.matrix = previousGuiMatrix;
    }

    private void DrawCelebrationOverlay(float viewWidth, float viewHeight)
    {
        float elapsed = Time.unscaledTime - celebrationStartTime;
        if (string.IsNullOrEmpty(celebrationText) || elapsed < 0f || elapsed >= CelebrationDuration)
        {
            return;
        }

        float fadeIn = Mathf.Clamp01(elapsed / 0.16f);
        float fadeOut = Mathf.Clamp01((CelebrationDuration - elapsed) / 0.5f);
        float alpha = Mathf.Min(fadeIn, fadeOut);
        float entrance = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.45f), 3f);
        float bounce = 1f + Mathf.Sin(elapsed * 15f) * Mathf.Exp(-elapsed * 4.5f) * 0.16f;
        float centerY = Mathf.Max(410f, viewHeight * 0.54f);
        float bannerWidth = Mathf.Min(viewWidth - 70f, 820f);
        Rect bannerRect = new Rect(
            (viewWidth - bannerWidth) * 0.5f,
            centerY - 86f,
            bannerWidth,
            172f);

        Color previousColor = GUI.color;
        if (elapsed < 0.18f)
        {
            GUI.color = new Color(
                celebrationColor.r,
                celebrationColor.g,
                celebrationColor.b,
                (1f - elapsed / 0.18f) * 0.16f);
            GUI.DrawTexture(new Rect(0f, 0f, viewWidth, viewHeight), Texture2D.whiteTexture);
        }

        GUI.color = new Color(0.025f, 0.035f, 0.065f, 0.86f * alpha);
        GUI.DrawTexture(bannerRect, Texture2D.whiteTexture);
        GUI.color = new Color(celebrationColor.r, celebrationColor.g, celebrationColor.b, 0.9f * alpha);
        GUI.DrawTexture(new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 5f), Texture2D.whiteTexture);
        GUI.DrawTexture(
            new Rect(bannerRect.x, bannerRect.yMax - 5f, bannerRect.width, 5f),
            Texture2D.whiteTexture);

        DrawCelebrationConfetti(viewWidth, centerY, elapsed, alpha);

        GUIStyle mainStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(76f * bounce),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 25,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 1f, 1f, alpha) }
        };

        float riseOffset = Mathf.Lerp(32f, 0f, entrance);
        Rect textRect = new Rect(0f, centerY - 76f + riseOffset, viewWidth, 105f);
        mainStyle.normal.textColor = new Color(0f, 0f, 0f, 0.72f * alpha);
        GUI.Label(new Rect(textRect.x + 4f, textRect.y + 5f, textRect.width, textRect.height), celebrationText, mainStyle);
        mainStyle.normal.textColor = new Color(
            celebrationColor.r,
            celebrationColor.g,
            celebrationColor.b,
            alpha);
        GUI.Label(textRect, celebrationText, mainStyle);
        GUI.Label(
            new Rect(0f, centerY + 25f + riseOffset, viewWidth, 42f),
            celebrationSubtext,
            subStyle);

        GUI.color = previousColor;
    }

    private void DrawCelebrationConfetti(float viewWidth, float centerY, float elapsed, float alpha)
    {
        Color[] colors =
        {
            celebrationColor,
            Color.white,
            new Color(1f, 0.3f, 0.5f),
            new Color(0.4f, 1f, 0.55f)
        };

        for (int index = 0; index < 24; index++)
        {
            float startX = Mathf.Repeat(index * 0.618034f, 1f) * viewWidth;
            float fallProgress = Mathf.Repeat(elapsed * 0.42f + index * 0.071f, 1f);
            float x = startX + Mathf.Sin(elapsed * 5f + index) * 18f;
            float y = centerY - 150f + fallProgress * 300f;
            float width = index % 2 == 0 ? 8f : 13f;
            float height = index % 3 == 0 ? 18f : 10f;
            Color color = colors[index % colors.Length];
            GUI.color = new Color(color.r, color.g, color.b, alpha * 0.88f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        }
    }

    private void DrawScoreboard(
        GUIStyle scoreStyle,
        GUIStyle textStyle,
        GUIStyle helpStyle,
        float viewWidth)
    {
        int?[] cumulativeScores = BowlingScoreCalculator.CalculateCumulativeScores(rolls);
        string[] frameRolls = BowlingScoreCalculator.FormatFrameRolls(rolls);
        const float startX = 25f;
        const float startY = 124f;
        const float cellHeight = 104f;
        float cellWidth = (viewWidth - startX * 2f) / BowlingScoreCalculator.FrameCount;

        int latestResolvedScore = 0;
        for (int frame = 0; frame < BowlingScoreCalculator.FrameCount; frame++)
        {
            float x = startX + frame * cellWidth;
            GUI.Box(new Rect(x, startY, cellWidth - 2f, cellHeight), string.Empty);
            GUI.Label(new Rect(x, startY + 1f, cellWidth - 2f, 28f), (frame + 1).ToString(), scoreStyle);
            GUI.Label(new Rect(x, startY + 30f, cellWidth - 2f, 34f), frameRolls[frame], scoreStyle);
            string scoreText = cumulativeScores[frame]?.ToString();
            if (scoreText == null && !string.IsNullOrEmpty(frameRolls[frame]))
            {
                scoreText = "確定待ち";
            }

            GUI.Label(new Rect(x, startY + 66f, cellWidth - 2f, 34f), scoreText, scoreStyle);
            if (cumulativeScores[frame].HasValue)
            {
                latestResolvedScore = cumulativeScores[frame].Value;
            }
        }

        string progressText = gameComplete
            ? "ゲーム終了"
            : $"第{currentFrameIndex + 1}フレーム / {currentFrameRolls.Count + 1}投目";
        float summaryY = startY + cellHeight + 5f;
        GUI.Label(new Rect(startX, summaryY, 330f, 34f), $"確定済み累計：{latestResolvedScore}", textStyle);
        GUI.Label(new Rect(355f, summaryY, 350f, 34f), progressText, textStyle);
        GUI.Label(
            new Rect(710f, summaryY + 2f, viewWidth - 735f, 30f),
            "各枠：上段＝投球結果 / 下段＝累計",
            helpStyle);
    }
}
