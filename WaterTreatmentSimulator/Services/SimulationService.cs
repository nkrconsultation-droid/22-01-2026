using System.Timers;
using WaterTreatmentSimulator.Models;

namespace WaterTreatmentSimulator.Services;

public class SimulationService : IDisposable
{
    private System.Timers.Timer? _timer;
    private DateTime _lastUpdate = DateTime.Now;
    private readonly Random _random = new();

    // Simulation state
    public bool IsRunning { get; private set; }
    public double SimSpeed { get; set; } = 10;
    public double SimTime { get; private set; }

    // Core models
    public Equipment Equipment { get; } = new();
    public FeedProperties FeedProps { get; } = new();
    public ProcessState Process { get; } = new();
    public ProductionTotals Totals { get; } = new();
    public CostRates Costs { get; } = new();
    public KpiTargets Targets { get; } = new();
    public EvaporationPond Pond { get; } = new();
    public PolishingFilter Filter { get; } = new();

    // Control loops
    public ControlLoop TIC { get; } = new() { Tag = "TIC-001", Description = "Heater Outlet Temp", Unit = "°C", PV = 65, SP = 65, OP = 50, Kp = 2.0, Ki = 0.1, Kd = 0.5 };
    public ControlLoop FIC { get; } = new() { Tag = "FIC-001", Description = "Feed Flow Rate", Unit = "m³/h", PV = 12, SP = 12, OP = 60, Kp = 1.5, Ki = 0.2, Kd = 0.1 };
    public ControlLoop SIC { get; } = new() { Tag = "SIC-001", Description = "Bowl Speed", Unit = "RPM", PV = 3500, SP = 3500, OP = 70, Kp = 0.5, Ki = 0.05, Kd = 0.02 };

    // Tanks
    public List<FeedTank> FeedTanks { get; } = new();
    public List<OilTank> OilTanks { get; } = new();
    public string? SelectedFeedTank { get; set; }
    public string SelectedOilTank { get; set; } = "HT-001";

    // Batch mode
    public bool IsBatchMode { get; private set; }
    public int CurrentBatchPhase { get; private set; }
    public double BatchVolumeRemaining { get; private set; }
    public List<BatchPhase> BatchPhases { get; } = new();

    // Alarms
    public List<Alarm> ActiveAlarms { get; } = new();

    // Trend data
    public List<TrendDataPoint> TrendData { get; } = new();
    private double _lastLogTime = 0;

    // Events
    public event Action? OnStateChanged;
    public event Action<string, string>? OnEventLogged;

    public SimulationService()
    {
        InitializeTanks();
        InitializeBatchPhases();
    }

    private void InitializeTanks()
    {
        FeedTanks.AddRange(new[]
        {
            new FeedTank { Id = "VT-001", Level = 85, WaterPercent = 75, OilPercent = 20, SedimentPercent = 5, Temperature = 28, Status = "ready" },
            new FeedTank { Id = "VT-002", Level = 62, WaterPercent = 70, OilPercent = 25, SedimentPercent = 5, Temperature = 30, Status = "ready" },
            new FeedTank { Id = "VT-003", Level = 45, WaterPercent = 80, OilPercent = 15, SedimentPercent = 5, Temperature = 25, Status = "settling" },
            new FeedTank { Id = "VT-004", Level = 95, WaterPercent = 65, OilPercent = 30, SedimentPercent = 5, Temperature = 32, Status = "ready" },
            new FeedTank { Id = "VT-005", Level = 30, WaterPercent = 85, OilPercent = 12, SedimentPercent = 3, Temperature = 26, Status = "ready" },
            new FeedTank { Id = "VT-006", Level = 78, WaterPercent = 72, OilPercent = 22, SedimentPercent = 6, Temperature = 29, Status = "ready" },
        });

        OilTanks.AddRange(new[]
        {
            new OilTank { Id = "HT-001", Level = 15, Temperature = 45, Status = "receiving" },
            new OilTank { Id = "HT-002", Level = 45, Temperature = 42, Status = "ready" },
            new OilTank { Id = "HT-003", Level = 0, Temperature = 25, Status = "empty" },
            new OilTank { Id = "HT-004", Level = 78, Temperature = 40, Status = "ready" },
            new OilTank { Id = "HT-005", Level = 0, Temperature = 25, Status = "empty" },
            new OilTank { Id = "HT-006", Level = 30, Temperature = 38, Status = "ready" },
        });
    }

    private void InitializeBatchPhases()
    {
        BatchPhases.AddRange(new[]
        {
            new BatchPhase { Name = "Heavy Sediment", Icon = "🪨", WaterPercent = 60, OilPercent = 10, SedimentPercent = 30, Volume = 2.75, Temperature = 55, Flow = 6, RPM = 4200 },
            new BatchPhase { Name = "Mixed Sludge", Icon = "🌊", WaterPercent = 65, OilPercent = 15, SedimentPercent = 20, Volume = 5.5, Temperature = 58, Flow = 8, RPM = 4000 },
            new BatchPhase { Name = "Emulsion Layer", Icon = "🧴", WaterPercent = 50, OilPercent = 45, SedimentPercent = 5, Volume = 8.25, Temperature = 65, Flow = 10, RPM = 3800 },
            new BatchPhase { Name = "Oil-Rich", Icon = "🛢️", WaterPercent = 30, OilPercent = 65, SedimentPercent = 5, Volume = 11.0, Temperature = 68, Flow = 12, RPM = 3500 },
            new BatchPhase { Name = "Water-Rich", Icon = "💧", WaterPercent = 85, OilPercent = 12, SedimentPercent = 3, Volume = 22.0, Temperature = 62, Flow = 14, RPM = 3200 },
            new BatchPhase { Name = "Final Rinse", Icon = "✨", WaterPercent = 95, OilPercent = 4, SedimentPercent = 1, Volume = 5.5, Temperature = 55, Flow = 10, RPM = 3000 },
        });
    }

    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _lastUpdate = DateTime.Now;

        _timer = new System.Timers.Timer(50); // 20 Hz update rate
        _timer.Elapsed += OnTimerTick;
        _timer.Start();

        OnEventLogged?.Invoke("START", "Simulation started");
        OnStateChanged?.Invoke();
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;

        OnEventLogged?.Invoke("STOP", "Simulation stopped");
        OnStateChanged?.Invoke();
    }

    public void StartBatch(string tankId)
    {
        var tank = FeedTanks.FirstOrDefault(t => t.Id == tankId);
        if (tank == null || tank.Status == "empty") return;

        tank.Status = "processing";
        SelectedFeedTank = tankId;

        FeedProps.WaterFraction = tank.WaterPercent / 100.0;
        FeedProps.OilFraction = tank.OilPercent / 100.0;
        FeedProps.SolidsFraction = tank.SedimentPercent / 100.0;

        BatchVolumeRemaining = tank.VolumeM3;
        IsBatchMode = true;
        CurrentBatchPhase = 0;
        SimTime = 0;

        OnEventLogged?.Invoke("BATCH", $"Started batch from {tankId} - {BatchVolumeRemaining:F1} m³");
        Start();
    }

    public void SelectTank(string tankId)
    {
        if (IsRunning) return;

        foreach (var tank in FeedTanks)
        {
            if (tank.Id == tankId)
                tank.Status = "selected";
            else if (tank.Status == "selected")
                tank.Status = "ready";
        }
        SelectedFeedTank = tankId;
        OnStateChanged?.Invoke();
    }

    public void Reset()
    {
        Stop();
        SimTime = 0;
        IsBatchMode = false;
        CurrentBatchPhase = 0;
        BatchVolumeRemaining = 55;

        Totals.Feed = 0;
        Totals.Water = 0;
        Totals.Oil = 0;
        Totals.Solids = 0;
        Totals.Energy = 0;
        Totals.RunTime = 0;

        Pond.Volume = 2000;
        Pond.TotalInflow = 0;
        Pond.TotalEvaporated = 0;

        Filter.TotalFiltered = 0;
        Filter.BackwashCount = 0;
        Filter.BedSaturation = 0;

        TrendData.Clear();
        ActiveAlarms.Clear();
        _lastLogTime = 0;

        foreach (var tank in FeedTanks.Where(t => t.Status == "processing"))
            tank.Status = "ready";

        SelectedFeedTank = null;

        OnEventLogged?.Invoke("RESET", "Simulation reset");
        OnStateChanged?.Invoke();
    }

    private void OnTimerTick(object? sender, ElapsedEventArgs e)
    {
        var now = DateTime.Now;
        var dt = (now - _lastUpdate).TotalSeconds * SimSpeed;
        _lastUpdate = now;

        SimTime += dt;
        UpdateSimulation(dt);
        OnStateChanged?.Invoke();
    }

    private void UpdateSimulation(double dt)
    {
        // Get feed composition
        double waterFrac = FeedProps.WaterFraction;
        double oilFrac = FeedProps.OilFraction;
        double solidsFrac = FeedProps.SolidsFraction;

        // Update batch phase setpoints if in batch mode
        if (IsBatchMode && CurrentBatchPhase < BatchPhases.Count)
        {
            var phase = BatchPhases[CurrentBatchPhase];
            waterFrac = phase.WaterPercent / 100.0;
            oilFrac = phase.OilPercent / 100.0;
            solidsFrac = phase.SedimentPercent / 100.0;
            TIC.SP = phase.Temperature;
            FIC.SP = phase.Flow;
            SIC.SP = phase.RPM;
        }

        // Update process temperatures
        Process.HeaterTemp += (TIC.SP - Process.HeaterTemp) * (1 - Math.Exp(-dt / Equipment.HeaterTimeConstant));
        Process.HeaterTemp = Clamp(Process.HeaterTemp + GaussianRandom(0, 0.3), 25, Equipment.HeaterMaxTemp);
        Process.BowlTemp += (Process.HeaterTemp - Process.BowlTemp - 2) * dt * 0.1;
        Process.BowlTemp = Clamp(Process.BowlTemp + GaussianRandom(0, 0.2), 25, 90);

        // Update flow
        Process.FeedFlow += (FIC.SP - Process.FeedFlow) * dt * 0.5;
        Process.FeedFlow = Clamp(Process.FeedFlow * (1 + GaussianRandom(0, 0.03)), 0, Equipment.MaxFlow);

        // Update speed
        var speedTarget = (SIC.OP / 100.0) * Equipment.MaxRPM;
        Process.BowlSpeed += (speedTarget - Process.BowlSpeed) * dt * 0.3;
        Process.BowlSpeed = Clamp(Process.BowlSpeed + GaussianRandom(0, 5), Equipment.MinRPM, Equipment.MaxRPM);

        // Calculate efficiencies using Stokes Law model
        CalculateEfficiency(waterFrac, oilFrac, solidsFrac);

        // Update control loops
        UpdatePID(TIC, Process.HeaterTemp, dt);
        UpdatePID(FIC, Process.FeedFlow, dt);
        UpdatePID(SIC, Process.BowlSpeed, dt);

        // Calculate outputs
        Process.OilOut = Process.FeedFlow * oilFrac * (Process.OilEfficiency / 100.0);
        Process.SolidsOut = Process.FeedFlow * solidsFrac * (Process.SolidsEfficiency / 100.0);
        Process.WaterOut = Process.FeedFlow - Process.OilOut - Process.SolidsOut;

        // Calculate vibration
        var baseVib = (1.5 + (Process.BowlSpeed / 5000.0) * 2) * (2 - Equipment.BearingCondition / 100.0);
        Process.Vibration = Math.Sqrt(Math.Pow(baseVib + GaussianRandom(0, 0.3), 2) + Math.Pow(baseVib * 0.9 + GaussianRandom(0, 0.3), 2));

        // Calculate power
        var mass = Process.FeedFlow * 1000.0 / 3600.0;
        Process.HeaterPower = Clamp((mass * 4.186 * (Process.HeaterTemp - Process.FeedTemp)) / (Equipment.HeaterEfficiency / 100.0), 0, Equipment.HeaterCapacity);
        Process.MotorPower = Equipment.CentrifugeCapacity * (0.3 + 0.7 * Math.Pow(Process.BowlSpeed / Equipment.MaxRPM, 3)) *
                            (0.5 + 0.5 * Process.FeedFlow / Equipment.MaxFlow) / ((Equipment.MotorEfficiency / 100.0) * (Equipment.VfdEfficiency / 100.0));
        Process.TotalPower = Process.HeaterPower + Process.MotorPower;

        // Update polishing filter
        UpdatePolishingFilter(dt);

        // Update pond
        UpdatePond(dt);

        // Update oil tank
        UpdateOilTank(dt);

        // Update batch mode
        if (IsBatchMode)
        {
            var volumeProcessed = (Process.FeedFlow / 3600.0) * dt;
            BatchVolumeRemaining -= volumeProcessed;

            if (SelectedFeedTank != null)
            {
                var tank = FeedTanks.FirstOrDefault(t => t.Id == SelectedFeedTank);
                if (tank != null)
                    tank.Level = Math.Max(0, (BatchVolumeRemaining / 55.0) * 100);
            }

            // Check phase progression
            var totalVol = BatchPhases.Sum(p => p.Volume);
            var processed = totalVol - BatchVolumeRemaining;
            double cumVol = 0;
            for (int i = 0; i < BatchPhases.Count; i++)
            {
                cumVol += BatchPhases[i].Volume;
                if (processed < cumVol)
                {
                    if (i != CurrentBatchPhase)
                    {
                        CurrentBatchPhase = i;
                        OnEventLogged?.Invoke("PHASE", BatchPhases[i].Name);
                    }
                    break;
                }
            }

            // Check batch completion
            if (BatchVolumeRemaining <= 0)
            {
                IsBatchMode = false;
                if (SelectedFeedTank != null)
                {
                    var tank = FeedTanks.FirstOrDefault(t => t.Id == SelectedFeedTank);
                    if (tank != null)
                    {
                        tank.Status = "empty";
                        tank.Level = 0;
                    }
                }
                SelectedFeedTank = null;
                OnEventLogged?.Invoke("COMPLETE", $"Batch complete - {totalVol:F0} m³ processed");
                Stop();
            }
        }

        // Update totals
        Totals.Feed += Process.FeedFlow * dt / 3600.0;
        Totals.Water += Process.WaterOut * dt / 3600.0;
        Totals.Oil += Process.OilOut * dt / 3600.0;
        Totals.Solids += Process.SolidsOut * dt / 3600.0;
        Totals.Energy += Process.TotalPower * dt / 3600.0;
        Totals.RunTime += dt;

        // Log trend data
        if (SimTime - _lastLogTime >= 1.0)
        {
            _lastLogTime = SimTime;
            TrendData.Add(new TrendDataPoint
            {
                Time = SimTime,
                Flow = Process.FeedFlow,
                Temperature = Process.HeaterTemp,
                Speed = Process.BowlSpeed,
                OilEfficiency = Process.OilEfficiency,
                WaterQuality = Process.WaterQuality,
                Power = Process.TotalPower,
                Vibration = Process.Vibration,
                pH = Process.pH,
                Turbidity = Process.Turbidity
            });

            if (TrendData.Count > 300)
                TrendData.RemoveAt(0);
        }

        // Check alarms
        CheckAlarms();
    }

    private void CalculateEfficiency(double waterFrac, double oilFrac, double solidsFrac)
    {
        // Simplified Stokes Law separation model
        var r = Equipment.BowlDiameter / 2000.0; // m
        var w = Process.BowlSpeed * 2 * Math.PI / 60.0; // rad/s
        var g = w * w * r;
        Process.GForce = g / 9.81;

        // Density differences
        var waterDensityAdj = FeedProps.WaterDensity + FeedProps.Salinity * 0.0007;
        var oilWaterDeltaRho = Math.Abs(waterDensityAdj - FeedProps.OilDensity);
        var solidsWaterDeltaRho = FeedProps.SolidsDensity - waterDensityAdj;

        // Viscosity (temperature adjusted)
        var viscRef = FeedProps.OilViscosity * 0.001;
        var visc = viscRef * Math.Exp(FeedProps.ViscosityTempCoeff * (25 - Process.BowlTemp) * 10);

        // Stokes settling velocities
        var oilD50m = FeedProps.OilDropletD50 * 1e-6;
        var oilSettle = (oilD50m * oilD50m * oilWaterDeltaRho * g) / (18 * visc);

        var solidsD50m = FeedProps.SolidsD50 * 1e-6;
        var solidsSettle = (solidsD50m * solidsD50m * solidsWaterDeltaRho * g) / (18 * visc);

        // Residence time
        var vol = Math.PI * r * r * (Equipment.BowlLength / 1000.0);
        var resTime = vol / Math.Max(Process.FeedFlow / 3600.0, 0.001);
        var dist = r * 0.3;

        // Base efficiency (logistic curve)
        double oilEff = 100.0 / (1 + Math.Exp(-2.5 * (oilSettle * resTime / dist - 1)));
        double solidsEff = 100.0 / (1 + Math.Exp(-2.5 * (solidsSettle * resTime / dist - 1)));

        // Apply modifiers
        var demulsifierEffect = FeedProps.DemulsifierDose > 0 ? FeedProps.DemulsifierEff * Math.Min(1, FeedProps.DemulsifierDose / 100.0) : 0;
        var emulFac = 1 - FeedProps.EmulsionStability * 0.3 * (1 - demulsifierEffect);
        var tempFac = 1 + (Process.BowlTemp - 60) * 0.008;
        var flowFac = Math.Max(0.6, 1 - (Process.FeedFlow - 10) * 0.04);

        Process.OilEfficiency = Clamp(oilEff * flowFac * emulFac * tempFac + GaussianRandom(0, 1), 0, 99.5);
        Process.SolidsEfficiency = Clamp(solidsEff * flowFac * tempFac + GaussianRandom(0, 0.5), 0, 99.9);

        // Water quality (OiW ppm)
        var oilCarryover = Process.FeedFlow * oilFrac * (1 - Process.OilEfficiency / 100.0);
        var waterOutputFlow = Math.Max(Process.WaterOut, 0.001);
        Process.WaterQuality = (oilCarryover / waterOutputFlow) * 1e6 * (FeedProps.OilDensity / FeedProps.WaterDensity);

        // pH and turbidity
        Process.pH = Clamp(7.0 + (1 - oilFrac * 10) * 0.3 - (Process.BowlTemp - 60) * 0.01 + GaussianRandom(0, 0.15), 4.0, 10.0);
        Process.Turbidity = Clamp(5 + solidsFrac * 500 + Process.WaterQuality * 0.002 + GaussianRandom(0, 5), 5, 500);
    }

    private void UpdatePID(ControlLoop loop, double pv, double dt)
    {
        loop.PV = pv;
        if (loop.Mode != "AUTO") return;

        var err = loop.SP - pv;
        if (Math.Abs(err) < 0.5) return;

        var newInt = Clamp(loop.Integral + err * dt, -50 / Math.Max(loop.Ki, 0.01), 50 / Math.Max(loop.Ki, 0.01));
        var op = Clamp(50 + loop.Kp * err + loop.Ki * newInt + loop.Kd * (err - loop.LastError) / Math.Max(dt, 0.01), 0, 100);

        loop.OP = op;
        loop.Integral = newInt;
        loop.LastError = err;
    }

    private void UpdatePolishingFilter(double dt)
    {
        if (!Filter.Enabled || Filter.Status == "OFFLINE")
        {
            Filter.OutletFlow = 0;
            Filter.OutletTurbidity = Process.Turbidity;
            Filter.OutletOiW = Process.WaterQuality;
            return;
        }

        if (Filter.Status == "BACKWASH")
        {
            Filter.BackwashRemaining -= dt;
            if (Filter.BackwashRemaining <= 0)
            {
                Filter.Status = "FILTERING";
                Filter.BedSaturation *= 0.05;
                Filter.DifferentialPressure = 0.2;
            }
            return;
        }

        Filter.InletFlow = Process.WaterOut;
        Filter.InletTurbidity = Process.Turbidity;
        Filter.InletOiW = Process.WaterQuality;

        var loadingFactor = 1 - Math.Pow(Filter.BedSaturation / 100.0, 0.7);
        var mediaFactor = Filter.MediaCondition / 100.0;

        Filter.TurbidityRemoval = 85 * loadingFactor * mediaFactor;
        Filter.OilRemoval = 70 * loadingFactor * mediaFactor;

        Filter.OutletTurbidity = Filter.InletTurbidity * (1 - Filter.TurbidityRemoval / 100.0);
        Filter.OutletOiW = Filter.InletOiW * (1 - Filter.OilRemoval / 100.0);
        Filter.OutletFlow = Process.WaterOut;

        Filter.BedSaturation += dt * 0.001 * Process.WaterOut;
        Filter.DifferentialPressure = 0.2 * (1 + Filter.BedSaturation / 100.0 * 6);

        Filter.TotalFiltered += (Process.WaterOut / 3600.0) * dt;

        if (Filter.AutoBackwash && Filter.DifferentialPressure >= Filter.BackwashTriggerDP)
        {
            Filter.Status = "BACKWASH";
            Filter.BackwashRemaining = 300;
            Filter.BackwashCount++;
            OnEventLogged?.Invoke("FILTER", "Auto-backwash triggered");
        }
    }

    private void UpdatePond(double dt)
    {
        var waterAdd = (Filter.OutletFlow / 3600.0) * dt;
        var evaporated = (Pond.SurfaceArea * Pond.EvaporationRate / 1000.0) / 86400.0 * dt;

        Pond.Volume = Clamp(Pond.Volume + waterAdd - evaporated, 0, Pond.Capacity);
        Pond.TotalInflow += waterAdd;
        Pond.TotalEvaporated += evaporated;

        // Blend water quality
        if (Pond.Volume > 0)
        {
            var blendFactor = waterAdd / (Pond.Volume + waterAdd);
            Pond.OilInWater = Pond.OilInWater * (1 - blendFactor) + Filter.OutletOiW * blendFactor;
            Pond.Turbidity = Math.Max(5, (Pond.Turbidity * (1 - blendFactor) + Filter.OutletTurbidity * blendFactor) * 0.95);
        }
        Pond.pH = Process.pH;
    }

    private void UpdateOilTank(double dt)
    {
        var oilAdd = (Process.OilOut / 3600.0) * dt;
        var tank = OilTanks.FirstOrDefault(t => t.Id == SelectedOilTank);
        if (tank != null && tank.Level < 100)
        {
            tank.Level = Math.Min(100, tank.Level + (oilAdd / 55.0) * 100);
            tank.Status = tank.Level >= OilTank.HighHighLevel ? "full" : "receiving";

            if (tank.Level >= OilTank.HighHighLevel)
            {
                OnEventLogged?.Invoke("INTERLOCK", $"{tank.Id} HIGH-HIGH level");
                Stop();
            }
        }
    }

    private void CheckAlarms()
    {
        ActiveAlarms.Clear();

        if (Process.HeaterTemp > 78)
            ActiveAlarms.Add(new Alarm { Type = "HIGH_TEMP", Severity = "critical", Value = Process.HeaterTemp, Limit = 78 });

        if (Process.Vibration > 7)
            ActiveAlarms.Add(new Alarm { Type = "HIGH_VIBRATION", Severity = "critical", Value = Process.Vibration, Limit = 7 });

        if (Process.OilEfficiency < 80)
            ActiveAlarms.Add(new Alarm { Type = "LOW_EFFICIENCY", Severity = "warning", Value = Process.OilEfficiency, Limit = 80 });
    }

    public void RefillTanks(double level = 85)
    {
        foreach (var tank in FeedTanks.Where(t => t.Status != "processing"))
        {
            tank.Level = level;
            tank.Status = "ready";
        }
        OnEventLogged?.Invoke("REFILL", $"Tanks refilled to {level}%");
        OnStateChanged?.Invoke();
    }

    public void ShipOil()
    {
        var totalVol = OilTanks.Sum(t => t.VolumeM3);
        foreach (var tank in OilTanks)
        {
            tank.Level = 0;
            tank.Status = "empty";
        }
        OnEventLogged?.Invoke("SHIP", $"Shipped {totalVol:F1} m³ oil");
        OnStateChanged?.Invoke();
    }

    public void TriggerBackwash()
    {
        if (Filter.Status == "FILTERING")
        {
            Filter.Status = "BACKWASH";
            Filter.BackwashRemaining = 300;
            Filter.BackwashCount++;
            OnEventLogged?.Invoke("FILTER", "Manual backwash triggered");
            OnStateChanged?.Invoke();
        }
    }

    private double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    private double GaussianRandom(double mean, double stdDev)
    {
        if (stdDev == 0) return mean;
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    public string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
