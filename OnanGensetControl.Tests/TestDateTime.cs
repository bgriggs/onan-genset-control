using BigMission.TestHelpers;

namespace OnanGensetControl.Tests;

internal class TestDateTime : IDateTimeHelper
{
    public DateTime? DateTimeTestValue { get; set; }

    public DateTime Now => DateTimeTestValue ?? DateTime.Now;

    public DateTime UtcNow => DateTimeTestValue ?? DateTime.UtcNow;
}
