using Microsoft.Extensions.Logging;
using System.Device.Gpio;

namespace OnanGensetControl;

internal class RpiPin : IPinControl
{
    private ILogger Logger { get; }

    public bool IsHigh
    {
        get { return controller.Read(GpioPin) == PinValue.High; }
    }

    public int GpioPin { get; private set; }
    private readonly GpioController controller;

    public RpiPin(GpioController controller, int gpioPin, PinMode mode, PinValue initialValue, ILoggerFactory loggerFactory)
    {
        this.controller = controller;
        Logger = loggerFactory.CreateLogger(GetType().Name);
        GpioPin = gpioPin;
        controller.OpenPin(GpioPin, mode, initialValue);
    }

    public void TurnOff(PinValue pinValue)
    {
        Logger.LogDebug($"Turning off pin {GpioPin} with value: {pinValue}");
        if (controller.GetPinMode(GpioPin) != PinMode.Output)
            throw new InvalidOperationException("Pin is not set to output mode.");

        controller.Write(GpioPin, pinValue);
    }

    public void TurnOn(PinValue pinValue)
    {
        Logger.LogDebug($"Turning on pin {GpioPin} with value: {pinValue}");
        if (controller.GetPinMode(GpioPin) != PinMode.Output)
            throw new InvalidOperationException("Pin is not set to output mode.");

        controller.Write(GpioPin, pinValue);
    }

    public async Task TurnOnForDurationAsync(TimeSpan duration, CancellationToken stoppingToken)
    {
        Logger.LogDebug($"Turning on pin {GpioPin} for duration {duration}");
        if (controller.GetPinMode(GpioPin) != PinMode.Output)
            throw new InvalidOperationException("Pin is not set to output mode.");

        TurnOn(PinValue.Low);
        await Task.Delay(duration, stoppingToken);
        TurnOff(PinValue.High);
    }
}
