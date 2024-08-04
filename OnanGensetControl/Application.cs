using BigMission.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Device.Gpio;
using System.Diagnostics;

namespace OnanGensetControl;

/// <summary>
/// Main processing loop checking for run and status inputs.
/// </summary>
public partial class Application : BackgroundService
{
    private readonly TimeSpan serviceFreq;
    private IConfiguration Config { get; }
    public IDateTimeHelper DateTime { get; }
    private ILogger Logger { get; }

    private readonly IPinControl startRelay;
    private readonly IPinControl stopRelay;
    private readonly IPinControl runCommandInput;
    private readonly IPinControl statusInput;

    private readonly TimeSpan stopDuration;
    private readonly int startRetries;
    private readonly TimeSpan retryWait;
    private readonly TimeSpan primeDuration;
    private readonly TimeSpan postPrimeDelay;
    private readonly TimeSpan startDuration;
    private readonly TimeSpan startStatusCheckDelayDuration;
    private readonly TimeSpan skipPrimeDuration;
    private readonly TimeSpan failureResetDuration;
    private DateTime? failureTime;
    private readonly TimeSpan controlPinDebounce;
    private bool lastAcceptedRunControl;
    private DateTime? lastControlPinChange;

    public Application(IConfiguration config, ILoggerFactory loggerFactory, IPinControlFactory pinControlFactory, IDateTimeHelper dateTime)
    {
        Config = config;
        DateTime = dateTime;
        Logger = loggerFactory.CreateLogger(GetType().Name);

        var startPin = Config.GetValue<int>("StartRelayPin");
        var stopPin = Config.GetValue<int>("StopRelayPin");
        var runControlPin = Config.GetValue<int>("RunControlPin");
        var runningStatusPin = Config.GetValue<int>("RunningStatusPin");
        Logger.LogDebug($"StartPin: {startPin}, StopPin: {stopPin}, RunControlPin: {runControlPin}, RunningStatusPin: {runningStatusPin}");

        // Setup pin access
        // Relay states are inverted--low is on, high is off
        startRelay = pinControlFactory.CreateRelayControl(startPin, PinMode.Output, PinValue.High, loggerFactory);
        stopRelay = pinControlFactory.CreateRelayControl(stopPin, PinMode.Output, PinValue.High, loggerFactory);
        runCommandInput = pinControlFactory.CreateRelayControl(runControlPin, PinMode.Input, PinValue.Low, loggerFactory);
        statusInput = pinControlFactory.CreateRelayControl(runningStatusPin, PinMode.InputPullUp, PinValue.High, loggerFactory);

        serviceFreq = TimeSpan.FromMilliseconds(Config.GetValue<int>("ServiceFreqMs"));
        stopDuration = TimeSpan.FromMilliseconds(Config.GetValue<int>("StopT4Ms"));
        startRetries = Config.GetValue<int>("StartRetries");
        retryWait = TimeSpan.FromSeconds(Config.GetValue<double>("RetryWaitSecs"));
        primeDuration = TimeSpan.FromMilliseconds(Config.GetValue<int>("PrimeT1Ms"));
        postPrimeDelay = TimeSpan.FromMilliseconds(Config.GetValue<int>("StartDelayT2Ms"));
        startDuration = TimeSpan.FromMilliseconds(Config.GetValue<int>("StartT3Ms"));
        startStatusCheckDelayDuration = TimeSpan.FromMilliseconds(Config.GetValue<int>("StartStatusCheckDelayMs"));
        skipPrimeDuration = TimeSpan.FromHours(Config.GetValue<int>("SkipPrimeDurationHours"));
        failureResetDuration = TimeSpan.FromSeconds(Config.GetValue<double>("FailureResetDurationSecs"));
        controlPinDebounce = TimeSpan.FromMilliseconds(Config.GetValue<int>("ControlPinDebounceMs"));

        Logger.LogDebug($"ServiceFreq: {serviceFreq}, StopDuration: {stopDuration}, StartRetries: {startRetries}, RetryWait: {retryWait}, PrimeDuration: {primeDuration}, PostPrimeDelay: {postPrimeDelay}, StartDuration: {startDuration}, SkipPrimeDuration: {skipPrimeDuration}, FailureResetDuration:{failureResetDuration}, ControlPinDebounce:{controlPinDebounce}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Starting main loop");

        var lastRunControlPinState = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var running = !statusInput.IsHigh; // Low is running
                var runControlPin = runCommandInput.IsHigh;
                Logger.LogDebug($"RunControlPin: {runControlPin}, Running: {running}, Failed: {failureTime is not null}");

                // Update control pin debounce on pin change
                if (runControlPin != lastRunControlPinState)
                {
                    if (lastControlPinChange.HasValue && DateTime.Now - lastControlPinChange.Value < controlPinDebounce)
                    {
                        Logger.LogWarning($"Control pin change too soon from {lastRunControlPinState} to {runControlPin}. Last change was {DateTime.Now - lastControlPinChange.Value} ago.");
                    }

                    lastControlPinChange = DateTime.Now;
                    lastRunControlPinState = runControlPin;
                }

                var runControl = lastAcceptedRunControl;
                // Check for debounce period met before accepting new control pin value
                if (lastAcceptedRunControl != runControlPin)
                {
                    if (lastControlPinChange.HasValue && DateTime.Now - lastControlPinChange.Value >= controlPinDebounce)
                    {
                        runControl = runControlPin;
                        Logger.LogDebug($"Control pin change debounce period met. Accepting new value: {lastAcceptedRunControl}.");
                    }
                    else // Skip processing control pin change
                    {
                        Logger.LogDebug($"Ignoring control pin change within debounce period. Current value remains: {runControlPin}.");
                    }
                }

                // Relay states are inverted--low is on, high is off
                var startRelayState = !startRelay.IsHigh;
                var stopRelayState = !stopRelay.IsHigh;
                Logger.LogDebug($"StartRelay: {startRelayState}, StopRelay: {stopRelayState}");

                // Check for failure reset typically after a failed start sequence
                TimeSpan? failedDiff = null;
                if (failureTime.HasValue)
                {
                    failedDiff = DateTime.Now - failureTime;
                    Logger.LogTrace($"In failed state for {failedDiff}");
                }

                // When there is an input command to run, but the generator is not running, start it.
                // Skip if there is a failure in progress. Let it time out with 
                if (runControl && !running && !failedDiff.HasValue || (failedDiff.HasValue && failedDiff.Value > failureResetDuration))
                {
                    Logger.LogInformation("Run command active, but generator is not running. Starting...");
                    if (failedDiff.HasValue && failedDiff.Value > failureResetDuration)
                    {
                        Logger.LogInformation($"Attempting failure reset after waiting {failedDiff}...");
                        failureTime = null;
                    }

                    await ExecuteStartSequence(stoppingToken);
                }

                // See if there was a command change to be processed
                if (runControl != lastAcceptedRunControl || (failedDiff.HasValue && failedDiff.Value > failureResetDuration))
                {
                    if (failedDiff.HasValue && failedDiff.Value > failureResetDuration)
                    {
                        Logger.LogInformation($"Attempting failure reset after waiting {failedDiff}...");
                        failureTime = null;
                    }
                    else
                    {
                        Logger.LogInformation($"Processing RunControl state changing from {lastAcceptedRunControl} to {runControl}...");
                    }

                    // When run command goes active, start the generator
                    if (runControl && !running)
                    {
                        await ExecuteStartSequence(stoppingToken);
                    }
                    // When the run command stops, send stop signal
                    else if (!runControl && running)
                    {
                        await ExecuteStopSequence(stoppingToken);
                    }

                    lastAcceptedRunControl = runControl;
                }
                lastRunControlPinState = runControlPin;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error in main loop");
            }

            Logger.LogTrace($"Processing complete in {sw.ElapsedMilliseconds:0.#}ms");
            await Task.Delay(serviceFreq, stoppingToken);
        }
    }


    //                                  _______________
    // START ___________________________|   Start T3   |_____running...______________________
    //            _____________|delay T2|                                    ____________
    // STOP  _____|  Prime T1  |_____________________________running...______|  Stop T4  |___
    private async Task ExecuteStartSequence(CancellationToken stoppingToken)
    {
        var tries = 0;
        bool running;
        do
        {
            // Wait on retry
            if (tries > 0 && retryWait > TimeSpan.Zero)
            {
                Logger.LogInformation($"Retrying start #{tries + 1} in {retryWait}ms");
                await Task.Delay(retryWait, stoppingToken);
            }

            if (primeDuration > TimeSpan.Zero)
            {
                //// Prime T1
                //Logger.LogInformation($"Sending prime for {primeDuration.TotalMilliseconds}ms...");
                //await Prime(stoppingToken);

                // Delay T2
                if (postPrimeDelay > TimeSpan.Zero)
                {
                    await Task.Delay(postPrimeDelay, stoppingToken);
                }
            }

            // Start T3
            Logger.LogInformation($"Sending start for {startDuration.TotalMilliseconds}ms...");
            await startRelay.TurnOnForDurationAsync(startDuration, stoppingToken);
            Logger.LogInformation($"Start duration finished.");
            tries++;

            // Wait a moment after the start signal before getting the new status
            Logger.LogDebug($"Waiting for {startStatusCheckDelayDuration.TotalMilliseconds}ms before checking status...");
            await Task.Delay(startStatusCheckDelayDuration, stoppingToken);
            running = !statusInput.IsHigh; // Low is running
            Logger.LogInformation($"Running status is now: {running}");
        }
        while (!running && tries < startRetries);

        if (!running)
        {
            failureTime = DateTime.Now;
            Logger.LogWarning($"Failed to start after {tries} tries. Will try again in {failureResetDuration}.");
        }
    }

    private async Task ExecuteStopSequence(CancellationToken stoppingToken)
    {
        await stopRelay.TurnOnForDurationAsync(stopDuration, stoppingToken);

        // Wait a moment after the stop signal to let status stabilize
        Logger.LogDebug($"Waiting for {startStatusCheckDelayDuration} before checking status...");
        await Task.Delay(startStatusCheckDelayDuration, stoppingToken);
        var running = !statusInput.IsHigh; // Low is running
        Logger.LogInformation($"Running status is now: {running}");

        // See if we were successful
        if (running)
        {
            failureTime = DateTime.Now;
            Logger.LogWarning($"Failed to stop. Will try again in {failureResetDuration}.");
        }
    }

    //private async Task Prime(CancellationToken stoppingToken)
    //{
    //    // If prime is set
    //    if (primeDuration <= TimeSpan.Zero)
    //    {
    //        Logger.LogDebug("Skipping prime, there is no duration configured.");
    //        return;
    //    }

    //    // If not skipping
    //    var skip = SkipPrime();
    //    if (skip)
    //    {
    //        Logger.LogDebug("Skipping prime.");
    //        return;
    //    }

    //    // Send prime signal on the stop line
    //    await stopRelay.TurnOnForDurationAsync(stopDuration, stoppingToken);
    //}

    //private bool SkipPrime()
    //{
    //    var lastPrime = LastPrime();
    //    var lastRunning = LastRunning();
    //    if (lastPrime > lastRunning)
    //    {
    //        lastRunning = lastPrime;
    //    }

    //    var diff = DateTime.Now - lastRunning;

    //    // Check time stamp of last prime and end of the last run
    //    return skipPrimeDuration > diff;
    //}

    //private void LogPrime()
    //{
    //    // save to file
    //}

    //private DateTime LastPrime()
    //{
    //    // load from file
    //    return System.DateTime.MinValue;
    //}

    //private void LogRunning()
    //{
    //    // save to file
    //}

    //private DateTime LastRunning()
    //{
    //    // read from file
    //    return System.DateTime.MinValue;
    //}

}
