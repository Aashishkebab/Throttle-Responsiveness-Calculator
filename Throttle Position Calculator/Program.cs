#pragma warning disable IDE2001

namespace Throttle_Position_Calculator;

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
            try {
                boost = File.ReadAllLines("boost.csv");
            }
            catch(Exception) {
                // Do nothing, let boost remain null
            }

            finalCalculation[0] = new float[accelerator[0].Split(',').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) {
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
            }

            for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse);

                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length];
                finalCalculation[i][0] = rpm;

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Starting at 1 to ignore the RPM column
                    finalCalculation[i][j] = throttle.LookupValueInTable(rpm, torqueValuesAtRpm[j]);
                }

                if(boost != null) { // Boost calculations are optional
                    for(int j = 1; j < finalCalculation[i].Length; j++) { // Target Boost
                        finalCalculation[i][j] += finalCalculation[i][j] * (boost.LookupValueInTable(rpm, finalCalculation[i][j]) / 14.7); // Divide by 14.7 to get a multiplier related to atmospheric pressure
                    }
                }
            }

            File.WriteAllLines("sensitivity.csv", finalCalculation.Select(row => string.Join("\t", row)));
            
            Console.WriteLine("Successfully wrote sensitivity.csv");
            Console.WriteLine("Press ENTER to close.");
            Console.ReadLine();
        }
        catch(Exception exception) {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }
    }

    /// <summary>
    /// Gets the Throttle Plate Opening Angle for a given <paramref name="yValue"/> at a specified <paramref name="rpm"/>.
    /// </summary>
    /// <param name="yValue"></param>
    /// <param name="values"></param>
    /// <param name="rpm"></param>
    /// <returns></returns>
    public static float LookupValueInTable(this string[] values, float rpm, float yValue) {
        float[] tableHeaders = Array.ConvertAll(values[0].Split(','), theValue => string.IsNullOrWhiteSpace(theValue) ? -1 : float.Parse(theValue));
        short[] rpmList = values.Select(line => short.Parse(string.IsNullOrWhiteSpace(line.Split(',')[0]) ? "-1" : line.Split(',')[0])).ToArray();
        
        for(int i = 1; i < rpmList.Length; i++) {
            if(rpmList[i] >= rpm) { // We have found the matching y columns
                for(int j = 1; j < tableHeaders.Length; j++) { // Starting at 1 to ignore the blank column
                    if(tableHeaders[j] >= yValue) { // We have found the matching x columns
                        // Get the four values
                        float rpmCeilingValueCeiling = float.Parse(values[i].Split(',')[j]);
                        
                        float rpmFloorValueCeiling;
                        if(i == 1) {
                            rpmFloorValueCeiling = rpmCeilingValueCeiling;
                        }
                        else {
                            rpmFloorValueCeiling = float.Parse(values[i - 1].Split(',')[j]);
                        }
                        
                        float rpmCeilingValueFloor;
                        if(j == 1) {
                            rpmCeilingValueFloor = rpmCeilingValueCeiling;
                        }
                        else {
                            rpmCeilingValueFloor = float.Parse(values[i].Split(',')[j - 1]);
                        }

                        float rpmFloorValueFloor;
                        if(i == 1 && j == 1) { // If both RPM and Requested Torque are at their minimums, no need to interpolate
                            rpmFloorValueFloor = rpmCeilingValueCeiling; // All four values are the same in this instance
                        }
                        else if(i != 1 && j == 1) { // If only the Requested Torque is minimum value, set to the next Requested Torque
                            rpmFloorValueFloor = rpmFloorValueCeiling; // Previous torque and next torque are the same value
                        }
                        else if(i == 1 && j != 1) { // If only the RPM is minimum value, set to the next RPM
                            rpmFloorValueFloor = rpmCeilingValueFloor; // Previous RPM and next RPM are the same value
                        }
                        else {
                            rpmFloorValueFloor = float.Parse(values[i - 1].Split(',')[j - 1]);
                        }
                        
                        // Get the percentage change in RPM from closest floor
                        float distanceFromPreviousRpm = rpm - rpmList[i - 1];
                        float totalRpmGap = rpmList[i] - rpmList[i - 1];

                        float rpmPercentageChangeFromPrevious;
                        if(totalRpmGap == 0) {
                            rpmPercentageChangeFromPrevious = 0;
                        }
                        else {
                            rpmPercentageChangeFromPrevious = distanceFromPreviousRpm / totalRpmGap;
                        }

                        // Get the percentage change in Requested Torque from closest floor
                        float distanceFromPreviousValue = yValue - tableHeaders[j - 1];
                        float totalValueGap = tableHeaders[j] - tableHeaders[j - 1];

                        float valuePercentageChangeFromPrevious;
                        if(totalValueGap == 0) {
                            valuePercentageChangeFromPrevious = 0;
                        }
                        else {
                            valuePercentageChangeFromPrevious = distanceFromPreviousValue / totalValueGap;
                        }

                        // Calculate the interpolated throttle position using the RPM ceiling
                        float rpmCeilingFinalValueGap = rpmCeilingValueCeiling - rpmCeilingValueFloor;
                        float rpmCeilingFinalValue = rpmCeilingValueFloor + (rpmCeilingFinalValueGap * valuePercentageChangeFromPrevious);

                        // Calculate the interpolated throttle position using the RPM floor
                        float rpmFloorFinalValueGap = rpmFloorValueCeiling - rpmFloorValueFloor;
                        float rpmFloorFinalValue = rpmFloorValueFloor + (rpmFloorFinalValueGap * valuePercentageChangeFromPrevious);

                        // Calculate the final interpolated throttle position using the two RPM-based values
                        return rpmFloorFinalValue + (rpmCeilingFinalValue - rpmFloorFinalValue) * rpmPercentageChangeFromPrevious;
                    }
                }
            }
        }

        throw new Exception("Either the RPM or value were greater than the allowed range.");
    }
}
