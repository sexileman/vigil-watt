using VigilWatt.Core.Plugins.Logitech;
using Xunit;

namespace VigilWatt.Core.Tests;

/// <summary>
/// 功 能： G435Codec —— PA 命令、无线/OTA 回包窗口与毫伏曲线。
/// 2026/10/21 20:10:00 jiang
/// 样本来自 046D:0ACB 接收器实测与 deddroid/logitech-g435-battery 的公开用例。
/// </summary>
public class G435CodecTests
{
    [Fact]
    public void G1_连接状态回包_值0表示耳机未连接()
    {
        byte[] reply = TestFrames.Hex("06 50 49 0E C2 00 00 00 00 02 00 E3 00 FF FF FF FF");
        Assert.True(G435Codec.TryParseRemote(reply, G435Codec.RemoteGetConnectStatus, out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void G2_模式回包_值1表示remote模式()
    {
        byte[] reply = TestFrames.Hex("06 50 49 0E C3 00 00 00 00 02 00 E0 01 FF FF FF FF");
        Assert.True(G435Codec.TryParseRemote(reply, G435Codec.RemoteGetMode, out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void G3_电池回包_4146mV()
    {
        // 046D:0ACB feature 6 真机样本（Rust 实现同源用例）
        byte[] reply = TestFrames.Hex("06 50 49 01 C8 75 48 0D 00 07 00 06 82 00 32 10 00 00 FF FF");
        Assert.True(G435Codec.TryParseBattery(reply, out var mv));
        Assert.Equal(4146, mv);
    }

    [Fact]
    public void G4_未连接时的210mV垃圾值被拒绝()
    {
        // 本机实测：耳机未开机时同一命令回 D2 00 = 210mV，必须拒绝而不是显示 0%
        byte[] reply = TestFrames.Hex("06 50 49 01 C0 9F F3 FB 03 07 00 06 84 00 D2 00 00 00 FF FF");
        Assert.False(G435Codec.TryParseBattery(reply, out _));
    }

    [Fact]
    public void G5_毫伏曲线与社区实现一致()
    {
        Assert.Equal(100, G435Codec.MillivoltsToPercent(4300));
        Assert.Equal(95, G435Codec.MillivoltsToPercent(4146));
        Assert.Equal(40, G435Codec.MillivoltsToPercent(3725));
        Assert.Equal(0, G435Codec.MillivoltsToPercent(3300));
    }

    [Fact]
    public void G6_PA命令头与长度()
    {
        var command = G435Codec.BuildCommand(G435Codec.CategoryWireless, 1, G435Codec.RemoteGetConnectStatus);
        Assert.Equal(new byte[] { 0x50, 0x41, 0x0E, 0x01, 0x01, 0xE3 }, command);
    }
}
