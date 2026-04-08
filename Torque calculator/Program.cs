#pragma warning disable IDE2001

namespace Torque_calculator;

internal static class Program
{
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    static readonly string[] accelerator = File.ReadAllLines("accelerator.csv");

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    static readonly string[] throttle = File.ReadAllLines("throttle.csv");

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    static string[]? boost;

    /// <summary>
    /// The Requested Torque headers in the <see cref="throttle"/> table.
    /// </summary>
    static readonly float[] throttleRequestedTorqueHeaders = Array.ConvertAll(throttle[0].Split(','), float.Parse);

    /// <summary>
    /// The RPMs in the throttle table.
    /// </summary>
    static readonly short[] throttleRpmList = throttle.Skip(1).Select(line => short.Parse(line.Split(',')[0])).ToArray();

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    static float[][] finalCalculation = new float[accelerator.Length][];

    /// <summary>
    /// There are several assumptions made about the format of the data.
    /// 1. The top row must be the headers, sorted ascending.
    /// 2. The leftmost column must be the RPM, sorted ascending.
    /// </summary>
    /// <param name="args"></param>
    static void Main(string[] args)
    {
        try {
            boost = File.ReadAllLines("boost.csv");
        }
        catch(Exception) {
            // Do nothing, let boost remain null
        }

        for(int j = 1; j < finalCalculation.Length; j++) {
            finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
        }

        for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
            float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse);

            float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

            finalCalculation[i] = new float[torqueValuesAtRpm.Length];
            finalCalculation[i][0] = rpm;

            for(int j = 1; j < torqueValuesAtRpm.Length; j++) { // Starting at 1 to ignore the RPM column
                finalCalculation[i][j] = rpm.LookupThrottlePlateOpeningAngle(torqueValuesAtRpm[j]);
            }

            for(int j = 1; j < torqueValuesAtRpm.Length; j++) { // Target Boost
                // TODO
            }
        }
    }

    /// <summary>
    /// Gets the Throttle Plate Opening Angle for a given <paramref name="requestedTorque"/> at a specified <paramref name="rpm"/>.
    /// </summary>
    /// <param name="requestedTorque"></param>
    /// <param name="rpm"></param>
    /// <returns></returns>
    public static float LookupThrottlePlateOpeningAngle(this float rpm, float requestedTorque) {
        for(int i = 1; i < throttleRequestedTorqueHeaders.Length; i++) { // Starting at 1 to ignore the blank column
            if(throttleRequestedTorqueHeaders[i] >= requestedTorque) { // We have found the Requested Torque area
                for(int j = 1; j < throttleRpmList.Length; j++) {
                    if(throttleRpmList[j] >= rpm) { // We have found the RPM area
                        // Get the four values
                        float nextRpmNextTorque = float.Parse(throttle[j].Split(',')[i]);
                        float nextRpmPreviousTorque;
                        if(i == 1) {
                            nextRpmPreviousTorque = nextRpmNextTorque;
                        }
                        else {
                            nextRpmPreviousTorque = float.Parse(throttle[j].Split(',')[i - 1]);
                        }

                        float previousRpmNextTorque;
                        if(j == 1) {
                            previousRpmNextTorque = nextRpmNextTorque;
                        }
                        else {
                            previousRpmNextTorque = float.Parse(throttle[j - 1].Split(',')[i]);
                        }

                        float previousRpmPreviousTorque;
                        if(i == 1 && j == 1) { // If both RPM and Requested Torque are at their minimums, no need to interpolate
                            previousRpmPreviousTorque = nextRpmNextTorque; // All four values are the same in this instance
                        }
                        else if(i == 1 && j != 1) { // If only the Requsted Torque is minimum value, set to the next Requested Torque
                            previousRpmPreviousTorque = previousRpmNextTorque; // Previous torque and next torque are the same value
                        }
                        else if(i != 1 && j == 1) { // If only the RPM is minimum value, set to the next RPM
                            previousRpmPreviousTorque = nextRpmPreviousTorque; // Previous RPM and next RPM are the same value
                        }
                        else {
                            previousRpmPreviousTorque = float.Parse(throttle[j - 1].Split(',')[i - 1]);
                        }

                        // Get the percentage change in Requested Torque from closest floor
                        float distanceFromPreviousTorque = requestedTorque - throttleRequestedTorqueHeaders[i - 1];
                        float totalTorqueGap = throttleRequestedTorqueHeaders[i] - throttleRequestedTorqueHeaders[i - 1];

                        float torquePercentageChangeFromPrevious;
                        if(totalTorqueGap == 0) {
                            torquePercentageChangeFromPrevious = 0;
                        }
                        else {
                            torquePercentageChangeFromPrevious = distanceFromPreviousTorque / totalTorqueGap;
                        }

                        // Get the percentage change in RPM from closest floor
                        float distanceFromPreviousRpm = rpm - throttleRpmList[j - 1];
                        float totalRpmGap = throttleRpmList[j] - throttleRpmList[j - 1];

                        float rpmPercentageChangeFromPrevious;
                        if(totalRpmGap == 0) {
                            rpmPercentageChangeFromPrevious = 0;
                        }
                        else {
                            rpmPercentageChangeFromPrevious = distanceFromPreviousRpm / totalRpmGap;
                        }

                        // Calculate the interpolated throttle position using the RPM ceiling
                        float nextRpmthrottlePositionGap = nextRpmNextTorque - nextRpmPreviousTorque;
                        float nextRpmThrottlePosition = nextRpmPreviousTorque + (nextRpmthrottlePositionGap * torquePercentageChangeFromPrevious);

                        // Calculate the interpolated throttle position using the RPM floor
                        float previousRpmthrottlePositionGap = previousRpmNextTorque - previousRpmPreviousTorque;
                        float previousRpmThrottlePosition = previousRpmPreviousTorque + (previousRpmthrottlePositionGap * torquePercentageChangeFromPrevious);

                        // Calculate the final interpolated throttle position using the two RPM-based values
                        return previousRpmThrottlePosition + (nextRpmThrottlePosition - previousRpmThrottlePosition) * rpmPercentageChangeFromPrevious;
                    }
                }
            }
        }

        throw new Exception("Either the RPM or Requested Torque were greater than the allowed range.");
    }
}
