using System.Device.Gpio;

namespace OnanGensetControl.Tests;

internal class TestPin : IPinControl
{
    public int GpioPin { get; set; }
    public bool IsHigh { get; set; }
    public int OnCount { get; set; }
    public int OffCount { get; set; }

    public void TurnOff(PinValue pinValue)
    {
        IsHigh = false;
        OffCount++;
    }

    public void TurnOn(PinValue pinValue)
    {
        IsHigh = true;
        OnCount++;
    }

    public Task TurnOnForDurationAsync(TimeSpan duration, CancellationToken stoppingToken)
    {
        TurnOn(PinValue.Low);
        TurnOff(PinValue.High);
        return Task.CompletedTask;
    }
}
