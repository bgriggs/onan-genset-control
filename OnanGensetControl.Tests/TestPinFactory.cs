using Microsoft.Extensions.Logging;
using System.Device.Gpio;

namespace OnanGensetControl.Tests;

internal class TestPinFactory : IPinControlFactory
{
    public Dictionary<int, TestPin> Pins { get; } = [];
    public IPinControl CreateRelayControl(int gpioPin, PinMode mode, PinValue initialValue, ILoggerFactory loggerFactory)
    {
        var pin = new TestPin();
        Pins[gpioPin] = pin;
        return pin;
    }
}
