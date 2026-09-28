using System.Collections.Generic;

public static class BowlingScoreCalculator
{
    public const int FrameCount = 10;
    public const int PinsPerRack = 10;

    public static int?[] CalculateCumulativeScores(IReadOnlyList<int> rolls)
    {
        int?[] scores = new int?[FrameCount];
        int rollIndex = 0;
        int total = 0;

        for (int frame = 0; frame < FrameCount; frame++)
        {
            if (rollIndex >= rolls.Count)
            {
                break;
            }

            if (frame < FrameCount - 1)
            {
                int first = rolls[rollIndex];
                if (first == PinsPerRack)
                {
                    if (rollIndex + 2 >= rolls.Count)
                    {
                        break;
                    }

                    total += PinsPerRack + rolls[rollIndex + 1] + rolls[rollIndex + 2];
                    scores[frame] = total;
                    rollIndex++;
                    continue;
                }

                if (rollIndex + 1 >= rolls.Count)
                {
                    break;
                }

                int second = rolls[rollIndex + 1];
                int frameScore = first + second;
                if (frameScore == PinsPerRack)
                {
                    if (rollIndex + 2 >= rolls.Count)
                    {
                        break;
                    }

                    frameScore += rolls[rollIndex + 2];
                }

                total += frameScore;
                scores[frame] = total;
                rollIndex += 2;
                continue;
            }

            int tenthFirst = rolls[rollIndex];
            if (rollIndex + 1 >= rolls.Count)
            {
                break;
            }

            int tenthSecond = rolls[rollIndex + 1];
            bool earnsBonusRoll = tenthFirst == PinsPerRack || tenthFirst + tenthSecond == PinsPerRack;
            if (earnsBonusRoll && rollIndex + 2 >= rolls.Count)
            {
                break;
            }

            int tenthScore = tenthFirst + tenthSecond;
            if (earnsBonusRoll)
            {
                tenthScore += rolls[rollIndex + 2];
            }

            total += tenthScore;
            scores[frame] = total;
        }

        return scores;
    }

    public static string[] FormatFrameRolls(IReadOnlyList<int> rolls)
    {
        string[] frameTexts = new string[FrameCount];
        int rollIndex = 0;

        for (int frame = 0; frame < FrameCount; frame++)
        {
            if (rollIndex >= rolls.Count)
            {
                frameTexts[frame] = string.Empty;
                continue;
            }

            if (frame < FrameCount - 1)
            {
                int first = rolls[rollIndex];
                if (first == PinsPerRack)
                {
                    frameTexts[frame] = "X";
                    rollIndex++;
                    continue;
                }

                string firstText = FormatPins(first);
                if (rollIndex + 1 >= rolls.Count)
                {
                    frameTexts[frame] = firstText;
                    rollIndex++;
                    continue;
                }

                int second = rolls[rollIndex + 1];
                string secondText = first + second == PinsPerRack ? "/" : FormatPins(second);
                frameTexts[frame] = firstText + " " + secondText;
                rollIndex += 2;
                continue;
            }

            List<string> tenthMarks = new List<string>();
            int tenthFirst = rolls[rollIndex++];
            tenthMarks.Add(tenthFirst == PinsPerRack ? "X" : FormatPins(tenthFirst));

            if (rollIndex < rolls.Count)
            {
                int tenthSecond = rolls[rollIndex++];
                tenthMarks.Add(FormatTenthSecond(tenthFirst, tenthSecond));

                bool earnsBonusRoll = tenthFirst == PinsPerRack || tenthFirst + tenthSecond == PinsPerRack;
                if (earnsBonusRoll && rollIndex < rolls.Count)
                {
                    int tenthThird = rolls[rollIndex];
                    tenthMarks.Add(FormatTenthThird(tenthFirst, tenthSecond, tenthThird));
                }
            }

            frameTexts[frame] = string.Join(" ", tenthMarks);
        }

        return frameTexts;
    }

    private static string FormatTenthSecond(int first, int second)
    {
        if (first == PinsPerRack)
        {
            return second == PinsPerRack ? "X" : FormatPins(second);
        }

        return first + second == PinsPerRack ? "/" : FormatPins(second);
    }

    private static string FormatTenthThird(int first, int second, int third)
    {
        if (third == PinsPerRack)
        {
            return "X";
        }

        if (first == PinsPerRack && second < PinsPerRack && second + third == PinsPerRack)
        {
            return "/";
        }

        return FormatPins(third);
    }

    private static string FormatPins(int pins)
    {
        return pins == 0 ? "-" : pins.ToString();
    }
}
