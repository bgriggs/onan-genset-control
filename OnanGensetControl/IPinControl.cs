using System.Device.Gpio;

namespace OnanGensetControl;

public interface IPinControl
{
    int GpioPin { get; }
    bool IsHigh { get; }

    void TurnOn(PinValue pinValue);
    Task TurnOnForDurationAsync(TimeSpan duration, CancellationToken stoppingToken);
    void TurnOff(PinValue pinValue);
}
