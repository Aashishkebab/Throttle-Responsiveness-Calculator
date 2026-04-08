#pragma warning disable IDE2001

namespace Throttle_Position_Calculator;

internal static class Program
{
    private const float TANH_DIVISOR = 55F;
    private const float TANH_MULTIPLIER = 3.3F;
    
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    private static readonly string[] accelerator = File.ReadAllLines("accelerator.csv");

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    private static readonly string[] throttle = File.ReadAllLines("throttle.csv");

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    private static string[]? boost;

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];

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
                        finalCalculation[i][j] += finalCalculation[i][j] * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation[i][j]) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER); // Divide by 14.7 to get a multiplier related to atmospheric pressure
                    }
                }
            }

            File.WriteAllLines("sensitivity.csv", finalCalculation.Select(row => string.Join("\t", row).Replace("-∞", "0")));
            
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
            if(rpmList[i] >= rpm || i == rpmList.Length - 1) { // We have found the matching y columns
                for(int j = 1; j < tableHeaders.Length; j++) { // Starting at 1 to ignore the blank column
                    if(tableHeaders[j] >= yValue || j == tableHeaders.Length - 1) { // We have found the matching x columns
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
                        float distanceFromRpmCeiling = rpmList[i] - rpm;
                        float totalRpmGap = rpmList[i] - rpmList[i - 1];

                        float rpmPercentageChangeFromCeiling;
                        if(totalRpmGap == 0 || (i == rpmList.Length - 1 && rpm > rpmList[i])) {
                            rpmPercentageChangeFromCeiling = 0;
                        }
                        else {
                            rpmPercentageChangeFromCeiling = distanceFromRpmCeiling / totalRpmGap;
                        }

                        // Get the percentage change in yValue from closest floor in the table
                        float distanceFromCeilingValue = tableHeaders[j] - yValue;
                        float totalValueGap = tableHeaders[j] - tableHeaders[j - 1];

                        float valuePercentageChangeFromCeiling;
                        if(totalValueGap == 0 || (j == tableHeaders.Length - 1 && yValue > tableHeaders[j])) {
                            valuePercentageChangeFromCeiling = 0;
                        }
                        else {
                            valuePercentageChangeFromCeiling = distanceFromCeilingValue / totalValueGap;
                        }

                        // Calculate the interpolated throttle position using the RPM ceiling
                        float rpmCeilingFinalValueGap = rpmCeilingValueCeiling - rpmCeilingValueFloor;
                        float rpmCeilingFinalValue = rpmCeilingValueCeiling - (rpmCeilingFinalValueGap * valuePercentageChangeFromCeiling);

                        // Calculate the interpolated throttle position using the RPM floor
                        float rpmFloorFinalValueGap = rpmFloorValueCeiling - rpmFloorValueFloor;
                        float rpmFloorFinalValue = rpmFloorValueCeiling - (rpmFloorFinalValueGap * valuePercentageChangeFromCeiling);

                        // Calculate the final interpolated throttle position using the two RPM-based values
                        return rpmCeilingFinalValue - ((rpmCeilingFinalValue - rpmFloorFinalValue) * rpmPercentageChangeFromCeiling);
                    }
                }
            }
        }

        throw new Exception("The fabric of spacetime is unraveling.");
    }
}
