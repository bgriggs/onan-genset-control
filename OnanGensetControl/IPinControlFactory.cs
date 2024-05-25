using Microsoft.Extensions.Logging;
using System.Device.Gpio;

namespace OnanGensetControl;

public interface IPinControlFactory
{
    public IPinControl CreateRelayControl(int gpioPin, PinMode mode, PinValue initialValue, ILoggerFactory loggerFactory);
}
