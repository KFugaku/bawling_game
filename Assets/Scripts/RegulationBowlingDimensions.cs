/// <summary>
/// USBC regulation dimensions expressed in this project's world scale.
/// One world unit equals the 8.5 inch diameter of a bowling ball.
/// </summary>
public static class RegulationBowlingDimensions
{
    public const float InchesPerUnit = 8.5f;
    public const float BallDiameter = 1f;
    public const float BallRadius = BallDiameter * 0.5f;
    public const float BallMass = 6f;

    public const float ApproachLength = 180f / InchesPerUnit;
    public const float LaneWidth = 41.5f / InchesPerUnit;
    public const float FoulLineToHeadPin = 720f / InchesPerUnit;
    public const float HeadPinToRearDeck = 34.1875f / InchesPerUnit;
    public const float LaneLength = FoulLineToHeadPin + HeadPinToRearDeck;
    public const float FoulLineWidth = 0.75f / InchesPerUnit;

    public const float GutterWidth = 9.25f / InchesPerUnit;
    public const float GutterFrontDepth = 1.875f / InchesPerUnit;
    public const float GutterRearDepth = 3.5f / InchesPerUnit;
    public const float PitDepth = 30f / InchesPerUnit;
    public const float PitFloorDepth = 10f / InchesPerUnit;
    public const float KickbackFaceSpacing = 60.125f / InchesPerUnit;
    public const float KickbackHeight = 20.5f / InchesPerUnit;

    public const float PinHeight = 15f / InchesPerUnit;
    public const float PinMaximumDiameter = 4.797f / InchesPerUnit;
    public const float PinWeight = BallMass * 3.5f / 16f;
    public const float PinCenterSpacing = 12f / InchesPerUnit;
    public const float PinRowDepth = PinCenterSpacing * 0.8660254f;
    public const float PinCenterOfMassFromBase = 5.781f / InchesPerUnit;
    public const float PinCenterOfMassLocalY =
        (PinCenterOfMassFromBase - PinHeight * 0.5f) / (PinHeight * 0.5f);

    public const float BallStartZ = -9f;
    public const float ThrowSpeed = 38f;
    public const float AimLimit = LaneWidth * 0.5f - BallRadius;
}
