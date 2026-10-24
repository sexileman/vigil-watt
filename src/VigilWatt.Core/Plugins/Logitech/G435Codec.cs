namespace VigilWatt.Core.Plugins.Logitech;

/// <summary>
/// 功 能： 罗技 G435 耳机（046D:0ACB）的纯帧编解码：PA 命令、无线回包、OTA 电压回包、毫伏曲线。
/// 2026/10/21 20:10:00 jiang
/// 传输层（Realtek 桥 / feature 0x06）在 LogitechPlugin；帧来源见 docs/hardware/logitech.md。
/// </summary>
internal static class G435Codec
{
    /// <summary>Realtek 桥的 feature report ID（进出都是 6）。</summary>
    public const byte ReportId = 6;

    public const byte CategoryOta = 0x06;
    public const byte CategoryWireless = 0x0E;

    public const byte RemoteGetMode = 0xE0;
    public const byte RemoteSetMode = 0xE1;
    public const byte RemoteGetConnectStatus = 0xE3;

    /// <summary>构造 PA 命令：`50 41 category seq len payload…`（len 是 payload 长度）。</summary>
    public static byte[] BuildCommand(byte category, byte seq, params byte[] payload)
    {
        var command = new byte[5 + payload.Length];
        command[0] = 0x50;
        command[1] = 0x41;
        command[2] = category;
        command[3] = seq;
        command[4] = (byte)payload.Length;
        Array.Copy(payload, 0, command, 5, payload.Length);
        return command;
    }

    /// <summary>无线类命令回包窗口：`02 00 &lt;cmd&gt; &lt;value&gt;`。</summary>
    public static bool TryParseRemote(byte[] buffer, byte command, out byte value)
    {
        for (int i = 0; i + 3 < buffer.Length; i++)
        {
            if (buffer[i] == 0x02 && buffer[i + 1] == 0x00 && buffer[i + 2] == command)
            {
                value = buffer[i + 3];
                return true;
            }
        }
        value = 0;
        return false;
    }

    /// <summary>OTA 电池回包窗口：`07 00 06 &lt;seq|0x80&gt; 00 &lt;mV lo&gt; &lt;mV hi&gt;`，只收锂电合理区间。</summary>
    public static bool TryParseBattery(byte[] buffer, out ushort millivolts)
    {
        for (int i = 0; i + 6 < buffer.Length; i++)
        {
            if (buffer[i] == 0x07 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x06 &&
                (buffer[i + 3] & 0x80) != 0 && buffer[i + 4] == 0x00)
            {
                int mv = buffer[i + 5] | (buffer[i + 6] << 8);
                if (mv is >= 2500 and <= 4600)
                {
                    millivolts = (ushort)mv;
                    return true;
                }
            }
        }
        millivolts = 0;
        return false;
    }

    /// <summary>静置锂电电压 → 百分比（社区同款曲线；充电时读数会偏高）。</summary>
    public static int MillivoltsToPercent(int mv)
    {
        (int Mv, int Pct)[] curve =
        {
            (4200, 100), (4150, 95), (4100, 90), (4050, 85), (4000, 80), (3950, 75),
            (3900, 70), (3850, 62), (3800, 55), (3750, 45), (3700, 35), (3650, 25),
            (3600, 15), (3550, 10), (3500, 5), (3400, 0),
        };
        if (mv >= curve[0].Mv) return 100;
        for (int i = 0; i + 1 < curve.Length; i++)
        {
            var (hiMv, hiPct) = curve[i];
            var (loMv, loPct) = curve[i + 1];
            if (mv >= loMv)
            {
                double t = (double)(mv - loMv) / (hiMv - loMv);
                return (int)Math.Round(loPct + t * (hiPct - loPct));
            }
        }
        return 0;
    }
}
