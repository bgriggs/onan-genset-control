using Microsoft.Extensions.Configuration;
using System.Device.Gpio;

namespace OnanGensetControl.Tests;

[TestClass]
public class ApplicationTests
{
    private Application? application;
    private IConfiguration? configuration;
    private TestLoggerFactory? loggerFactory;
    private TestPinFactory? factory;
    private TestDateTime? dateTime;
    private readonly Dictionary<string, string?> configValues = new()
    {
        { "ServiceFreqMs", "10" },
        { "StartRelayPin", "1" },
        { "StopRelayPin", "2" },
        { "RunControlPin", "3" },
        { "RunningStatusPin", "4" },
        { "PrimeT1Ms", "0" },
        { "StartDelayT2Ms", "0" },
        { "StartT3Ms", "30" },
        { "StartStatusCheckDelayMs", "0" },
        { "StopT4Ms", "30" },
        { "StartRetries", "3" },
        { "RetryWaitSecs", "0.03" }, // 30ms
        { "SkipPrimeDurationHours", "336" },
        { "FailureResetDurationSecs", "0.1" },
    };

    [TestInitialize]
    public void Setup()
    {
        factory = new TestPinFactory();
        dateTime = new TestDateTime();
        loggerFactory = new TestLoggerFactory();

        InitializeApplicationWithConfiguration();
    }

    private void InitializeApplicationWithConfiguration()
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        application = new Application(configuration, loggerFactory!, factory!, dateTime!);
    }

    // External start / stop

    /// <summary>
    /// Test normal start from a stopped state and no retries.
    /// </summary>
    [TestMethod]
    public async Task ShouldStartNormally()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        // Make sure it is not running
        runningStatus.TurnOff(PinValue.Low);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to start
        runPin.TurnOn(PinValue.Low);

        // Set status to indicate it is now running
        var setRunningTask = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(5);
            runningStatus.TurnOn(PinValue.High);
        });
        await setRunningTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(1, startRelay.OnCount);
        Assert.AreEqual(1, startRelay.OffCount);
        Assert.AreEqual(true, runPin.IsHigh);
        Assert.AreEqual(true, runningStatus.IsHigh);
    }

    [TestMethod]
    public async Task ShouldStop()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var stopRelay = factory!.Pins[2];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        runningStatus.TurnOn(PinValue.High);
        runPin.TurnOn(PinValue.High);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to stop
        runPin.TurnOff(PinValue.Low);

        // Set status to indicate it is now stopped
        var setRunningTask = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(25); // Set it to have stopped before the stop command ends after 30ms
            runningStatus.TurnOff(PinValue.Low);
        });
        await setRunningTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(1, stopRelay.OnCount);
        Assert.AreEqual(1, stopRelay.OffCount);
        Assert.AreEqual(false, runPin.IsHigh);
        Assert.AreEqual(false, runningStatus.IsHigh);
    }

    /// <summary>
    /// Start on two retries, i.e. starts the 3rd time.
    /// </summary>
    [TestMethod]
    public async Task ShouldStart_TwoRetries()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        // Make sure it is not running
        runningStatus.TurnOff(PinValue.Low);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to start
        runPin.TurnOn(PinValue.High);

        var failDelayTask = Task.Run(async () =>
        {
            // Fail the first two starts (each is 30ms)
            await Task.Delay(62);
        });

        await failDelayTask;

        // Set status to indicate it is now running
        var setRunningTask = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(2);
            runningStatus.TurnOn(PinValue.High);
        });
        await setRunningTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(3, startRelay.OnCount);
        Assert.AreEqual(3, startRelay.OffCount);
        Assert.AreEqual(true, runPin.IsHigh);
        Assert.AreEqual(true, runningStatus.IsHigh);
    }

    [TestMethod]
    public async Task ShouldFail_ExceedsRetries()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        // Make sure it is not running
        runningStatus.TurnOff(PinValue.Low);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to start
        runPin.TurnOn(PinValue.High);

        var failDelayTask = Task.Run(async () =>
        {
            // Fail the first two starts (each is 30ms)
            await Task.Delay(90);
        });

        await failDelayTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(3, startRelay.OnCount);
        Assert.AreEqual(3, startRelay.OffCount);
        Assert.AreEqual(true, runPin.IsHigh);
        Assert.AreEqual(false, runningStatus.IsHigh);
    }

    [TestMethod]
    public async Task ShouldStart_ExceedsRetries_LongResetRetry()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        // Make sure it is not running
        runningStatus.TurnOff(PinValue.Low);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to start
        runPin.TurnOn(PinValue.High);

        var failDelayTask = Task.Run(async () =>
        {
            // Fail the first two starts (each is 30ms)
            await Task.Delay(90);
        });
        await failDelayTask;

        // Wait for long reset retry period of 100ms
        var longWaitTask = Task.Run(async () =>
        {
            await Task.Delay(105);
        });
        await longWaitTask;

        // Set status to indicate it is now running
        var setRunningTask = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(25);
            runningStatus.TurnOn(PinValue.High);
        });
        await setRunningTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(5, startRelay.OnCount);
        Assert.AreEqual(5, startRelay.OffCount);
        Assert.AreEqual(true, runPin.IsHigh);
        Assert.AreEqual(true, runningStatus.IsHigh);
    }

    [TestMethod]
    public async Task ShouldStop_ExceedsRetries_LongResetRetry()
    {
        // Arrange
        var startRelay = factory!.Pins[1];
        var stopRelay = factory!.Pins[2];
        var runPin = factory!.Pins[3];
        var runningStatus = factory!.Pins[4];

        // Act
        runningStatus.TurnOn(PinValue.High);
        runPin.TurnOn(PinValue.High);

        // Start the main application loop
        var source = new CancellationTokenSource();
        var exeTask = application!.StartAsync(source.Token);

        await Task.Delay(100);

        // Signal to stop
        runPin.TurnOff(PinValue.High);

        // Wait for long reset retry period 100ms
        var longWaitTask = Task.Run(async () =>
        {
            await Task.Delay(105);
        });
        await longWaitTask;

        // Set status to indicate it is now stopped
        var setRunningTask = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(25); // Set it to have stopped before the stop command ends after 30ms
            runningStatus.TurnOff(PinValue.Low);
        });
        await setRunningTask;

        var cancelTask = Task.Run(async () =>
        {
            await Task.Delay(200);
            source.Cancel();
        });

        await exeTask;
        await cancelTask;

        // Assert
        Assert.AreEqual(2, stopRelay.OnCount);
        Assert.AreEqual(2, stopRelay.OffCount);
        Assert.AreEqual(false, runPin.IsHigh);
        Assert.AreEqual(false, runningStatus.IsHigh);
    }
}