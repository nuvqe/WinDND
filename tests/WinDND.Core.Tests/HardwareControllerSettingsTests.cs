using WinDND.Core.Models;

namespace WinDND.Core.Tests;

[TestClass]
public sealed class HardwareControllerSettingsTests
{
    [TestMethod]
    public void DefaultsAreSafeAndMatchTwoButtonController()
    {
        HardwareControllerSettings settings = new();

        Assert.IsFalse(settings.Enabled);
        Assert.AreEqual(MicrophoneScope.AllActive, settings.MicrophoneScope);
        Assert.AreEqual(HardwareKeyRouting.WinDndOnly, settings.KeyRouting);
        Assert.AreEqual(HardwareButtonAction.ToggleMicrophone, settings.LeftButtonAction);
        Assert.AreEqual(HardwareButtonAction.ToggleOutput, settings.RightButtonAction);
        Assert.AreEqual(HardwareIndicator.Microphone, settings.LeftIndicator);
        Assert.AreEqual(HardwareIndicator.Microphone, settings.RightIndicator);
        Assert.AreEqual("#FF0000", settings.MutedColor);
        Assert.AreEqual("#0000FF", settings.UnmutedColor);
        CollectionAssert.AreEqual(new[] { 0x0002, 0x0003, 0x000C }, settings.ProductIds);
    }

    [TestMethod]
    public void CloneDoesNotShareProductIdCollection()
    {
        AppSettings original = new();
        AppSettings clone = original.Clone();

        clone.Hardware.ProductIds.Add(0x0009);
        clone.Hardware.KeyRouting = HardwareKeyRouting.WinDndAndForegroundApplication;

        CollectionAssert.AreEqual(new[] { 0x0002, 0x0003, 0x000C }, original.Hardware.ProductIds);
        CollectionAssert.AreEqual(new[] { 0x0002, 0x0003, 0x000C, 0x0009 }, clone.Hardware.ProductIds);
        Assert.AreEqual(HardwareKeyRouting.WinDndOnly, original.Hardware.KeyRouting);
        Assert.AreEqual(HardwareKeyRouting.WinDndAndForegroundApplication, clone.Hardware.KeyRouting);
    }
}
