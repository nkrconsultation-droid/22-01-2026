namespace WaterTreatmentSimulator.Models;

/// <summary>
/// Equipment specifications for heater and centrifuge
/// </summary>
public class Equipment
{
    // Heater properties
    public double HeaterCapacity { get; set; } = 120; // kW
    public double HeaterEfficiency { get; set; } = 92; // %
    public double HeaterMaxTemp { get; set; } = 85; // °C
    public double HeaterTimeConstant { get; set; } = 30; // seconds

    // Centrifuge properties
    public double CentrifugeCapacity { get; set; } = 100; // kW
    public double MaxRPM { get; set; } = 5000;
    public double MinRPM { get; set; } = 1500;
    public double MaxFlow { get; set; } = 15; // m³/h
    public double BowlDiameter { get; set; } = 400; // mm
    public double BowlLength { get; set; } = 1100; // mm
    public double MotorEfficiency { get; set; } = 94; // %
    public double VfdEfficiency { get; set; } = 97; // %
    public double BearingCondition { get; set; } = 100; // %
}

/// <summary>
/// Feed properties for the separation process
/// </summary>
public class FeedProperties
{
    // Phase fractions (must sum to 1.0)
    public double WaterFraction { get; set; } = 0.75;
    public double OilFraction { get; set; } = 0.20;
    public double SolidsFraction { get; set; } = 0.05;

    // Density properties (kg/m³)
    public double WaterDensity { get; set; } = 1000;
    public double OilDensity { get; set; } = 890;
    public double SolidsDensity { get; set; } = 2650;

    // Particle/Droplet sizes (microns)
    public double OilDropletD50 { get; set; } = 25;
    public double SolidsD50 { get; set; } = 80;

    // Rheological properties
    public double OilViscosity { get; set; } = 50; // mPa·s at 25°C
    public double ViscosityTempCoeff { get; set; } = 0.025;

    // Emulsion properties
    public double EmulsionStability { get; set; } = 0.3; // 0-1
    public double InterfacialTension { get; set; } = 25; // mN/m

    // Chemical dosing (ppm)
    public double DemulsifierDose { get; set; } = 50;
    public double DemulsifierEff { get; set; } = 0.7;

    // Water quality
    public double Salinity { get; set; } = 35000; // mg/L TDS
}

/// <summary>
/// PID Control Loop
/// </summary>
public class ControlLoop
{
    public string Tag { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public double PV { get; set; } // Process value
    public double SP { get; set; } // Setpoint
    public double OP { get; set; } // Output %
    public string Mode { get; set; } = "AUTO"; // AUTO or MAN

    // PID tuning parameters
    public double Kp { get; set; } = 1.0;
    public double Ki { get; set; } = 0.1;
    public double Kd { get; set; } = 0.05;
    public double Integral { get; set; } = 0;
    public double LastError { get; set; } = 0;
}

/// <summary>
/// Process state variables
/// </summary>
public class ProcessState
{
    public double FeedTemp { get; set; } = 28;
    public double HeaterTemp { get; set; } = 65;
    public double BowlTemp { get; set; } = 63;
    public double FeedFlow { get; set; } = 12;
    public double WaterOut { get; set; } = 9;
    public double OilOut { get; set; } = 2.4;
    public double SolidsOut { get; set; } = 0.6;
    public double BowlSpeed { get; set; } = 3500;
    public double Vibration { get; set; } = 3.4;
    public double OilEfficiency { get; set; } = 92;
    public double SolidsEfficiency { get; set; } = 96;
    public double WaterQuality { get; set; } = 50; // ppm OiW
    public double HeaterPower { get; set; } = 60;
    public double MotorPower { get; set; } = 75;
    public double TotalPower { get; set; } = 135;
    public double GForce { get; set; } = 2500;
    public double pH { get; set; } = 7.2;
    public double Turbidity { get; set; } = 45;
}

/// <summary>
/// Feed/Slop tank
/// </summary>
public class FeedTank
{
    public string Id { get; set; } = "";
    public double Level { get; set; } // %
    public double WaterPercent { get; set; }
    public double OilPercent { get; set; }
    public double SedimentPercent { get; set; }
    public double Temperature { get; set; }
    public string Status { get; set; } = "ready"; // ready, selected, processing, empty, settling

    public double VolumeM3 => Level / 100.0 * 55.0; // 55 m³ tank
}

/// <summary>
/// Oil storage tank
/// </summary>
public class OilTank
{
    public string Id { get; set; } = "";
    public double Level { get; set; } // %
    public double Temperature { get; set; }
    public string Status { get; set; } = "empty"; // empty, receiving, ready, full

    public double VolumeM3 => Level / 100.0 * 55.0; // 55 m³ tank

    public const double HighLevel = 90;
    public const double HighHighLevel = 95;
}

/// <summary>
/// Evaporation pond
/// </summary>
public class EvaporationPond
{
    public double Capacity { get; set; } = 8000; // m³ (8 ML)
    public double Volume { get; set; } = 2000; // m³
    public double Level => (Volume / Capacity) * 100;
    public double SurfaceArea { get; set; } = 4000; // m²
    public double pH { get; set; } = 7.2;
    public double Turbidity { get; set; } = 45; // NTU
    public double OilInWater { get; set; } = 15; // ppm
    public double EvaporationRate { get; set; } = 8; // mm/day
    public double TotalInflow { get; set; } = 0;
    public double TotalEvaporated { get; set; } = 0;
}

/// <summary>
/// Polishing filter (SPDD1600)
/// </summary>
public class PolishingFilter
{
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = "FILTERING"; // FILTERING, BACKWASH, STANDBY, OFFLINE

    public double InletFlow { get; set; }
    public double OutletFlow { get; set; }
    public double DifferentialPressure { get; set; } = 0.2; // bar

    public double InletTurbidity { get; set; } = 45;
    public double OutletTurbidity { get; set; } = 7;
    public double InletOiW { get; set; } = 50;
    public double OutletOiW { get; set; } = 15;

    public double TurbidityRemoval { get; set; } = 85; // %
    public double OilRemoval { get; set; } = 70; // %

    public double BedSaturation { get; set; } = 0; // %
    public double MediaCondition { get; set; } = 100; // %

    public int BackwashCount { get; set; } = 0;
    public double BackwashRemaining { get; set; } = 0;
    public bool AutoBackwash { get; set; } = true;
    public double BackwashTriggerDP { get; set; } = 1.0; // bar

    public double TotalFiltered { get; set; } = 0; // m³
}

/// <summary>
/// Production totals
/// </summary>
public class ProductionTotals
{
    public double Feed { get; set; } = 0; // m³
    public double Water { get; set; } = 0; // m³
    public double Oil { get; set; } = 0; // m³
    public double Solids { get; set; } = 0; // m³
    public double Energy { get; set; } = 0; // kWh
    public double RunTime { get; set; } = 0; // seconds
}

/// <summary>
/// Operating cost rates
/// </summary>
public class CostRates
{
    public double Electricity { get; set; } = 0.34; // $/kWh
    public double SludgeDisposal { get; set; } = 85; // $/m³
    public double WaterTreatment { get; set; } = 2.5; // $/m³
    public double OilValue { get; set; } = 340; // $/m³
    public double LaborRate { get; set; } = 45; // $/hour
}

/// <summary>
/// KPI targets
/// </summary>
public class KpiTargets
{
    public double OilEfficiency { get; set; } = 90; // %
    public double SolidsEfficiency { get; set; } = 95; // %
    public double WaterQuality { get; set; } = 50; // ppm
    public double MinFlow { get; set; } = 10; // m³/h
    public double MaxEnergy { get; set; } = 15; // kWh/m³
    public double MaxVibration { get; set; } = 5; // mm/s
}

/// <summary>
/// Alarm definition
/// </summary>
public class Alarm
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "warning"; // warning, critical
    public double Value { get; set; }
    public double Limit { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Batch processing phase
/// </summary>
public class BatchPhase
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public double WaterPercent { get; set; }
    public double OilPercent { get; set; }
    public double SedimentPercent { get; set; }
    public double Volume { get; set; } // m³
    public double Temperature { get; set; } // °C
    public double Flow { get; set; } // m³/h
    public double RPM { get; set; }
}

/// <summary>
/// Trend data point
/// </summary>
public class TrendDataPoint
{
    public double Time { get; set; }
    public double Flow { get; set; }
    public double Temperature { get; set; }
    public double Speed { get; set; }
    public double OilEfficiency { get; set; }
    public double WaterQuality { get; set; }
    public double Power { get; set; }
    public double Vibration { get; set; }
    public double pH { get; set; }
    public double Turbidity { get; set; }
}
