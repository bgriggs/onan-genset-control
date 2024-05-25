using Microsoft.Extensions.Logging;
using System.Device.Gpio;

namespace OnanGensetControl;

internal class RpiPinControlFactory : IPinControlFactory
{
    private readonly GpioController controller = new();

    public IPinControl CreateRelayControl(int gpioPin, PinMode mode, PinValue initialValue, ILoggerFactory loggerFactory)
    {
        return new RpiPin(controller, gpioPin, mode, initialValue, loggerFactory);
    }
}
