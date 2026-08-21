using WinDND.Core.Models;

namespace WinDND.Core.Tests;

[TestClass]
public sealed class SoundActionTests
{
    [TestMethod]
    [DataRow("mute", SoundActionKind.Mute, 0)]
    [DataRow("UNMUTE", SoundActionKind.Unmute, 0)]
    [DataRow("0", SoundActionKind.SetVolume, 0)]
    [DataRow("73", SoundActionKind.SetVolume, 73)]
    [DataRow("100", SoundActionKind.SetVolume, 100)]
    public void TryParse_AcceptsSupportedActions(string value, SoundActionKind kind, int volume)
    {
        bool parsed = SoundAction.TryParse(value, out SoundAction action);

        Assert.IsTrue(parsed);
        Assert.AreEqual(kind, action.Kind);
        Assert.AreEqual(volume, action.Volume);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("101")]
    [DataRow("-1")]
    [DataRow("quiet")]
    [DataRow("10.5")]
    public void TryParse_RejectsUnsupportedActions(string value)
    {
        Assert.IsFalse(SoundAction.TryParse(value, out _));
    }
}
