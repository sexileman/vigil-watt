namespace VigilWatt.Core.Plugins.Mchose;

/// <summary>迈从家族接口的去向。<see cref="None"/> = 不是本插件的接口。</summary>
public enum MchoseInterfaceKind
{
    /// <summary>不是迈从家族的配置通道。</summary>
    None,

    /// <summary>键盘 V1 会话通道（20 字节 `0x13`，G87 / G87 V2 接收器）。</summary>
    KeyboardV1,

    /// <summary>鼠标 V2 世代厂商接口（≥60 字节，含 report ID）。</summary>
    MouseV2,

    /// <summary>鼠标 K7 V2 Ultra+ 的命令通道（64 字节 output `0x4D`，3837:1018）。</summary>
    MouseK7,

    /// <summary>MCHOSE G3 Ultra（Realtek 0BDA:4762）：E2 帧同 MouseV2，但触发走 feature 0x11。</summary>
    MouseG3Ultra,

    /// <summary>V9 Pro 耳机（64 字节、reportId `0x55`）。</summary>
    HeadsetV9,
}

/// <summary>
/// 功 能： 按接口指纹判定一个 HID 接口属于迈从家族的哪条链路。
/// 2026/10/19 21:10:00 xliu
/// 纯函数、零 I/O：插件分流与单测共用同一份判据，避免"判据写在两处、改一处忘一处"。
/// 帧与判据来源见 docs/hardware/mchose.md §1、§1.6，以及 docs/hardware/mchose-v9-pro.md。
/// </summary>
public static class MchoseInterfaceClassifier
{
    /// <summary>新世代厂 ID（G87 V2 接收器 3837:201B、A7 V2 Pro 接收器 3837:100A）。</summary>
    public const ushort MchoseVid = 0x3837;

    /// <summary>老款 G87 世代（41E4:2001），协议同为 V1 会话。</summary>
    public const ushort MchoseLegacyVid = 0x41E4;

    /// <summary>G3 Ultra 的接收器是 Realtek 方案（0BDA:4762，「G3 Mouse 8K」）。</summary>
    public const ushort G3UltraVid = 0x0BDA;
    public const ushort G3UltraPid = 0x4762;

    /// <summary>G3 Ultra 的电量链路在 0xFF01 厂商集合上（65 字节进出、feature 0x11 触发）。</summary>
    private const ushort G3UltraUsagePage = 0xFF01;

    /// <summary>键盘 V1 会话通道的报告长度（进出各 20 字节）。</summary>
    private const int KeyboardReportLength = 20;

    /// <summary>
    /// 判定接口去向。<b>耳机必须排在最前</b>：它也是 64 字节输出，落到鼠标分支会收到 `0x11/0x06` 触发帧。
    /// 品牌排除（罗技/雷柏/北通/英菲克/HECATE/AULA 的 VID）留在插件里做纵深防御，这里只管迈从自己的指纹。
    /// </summary>
    public static MchoseInterfaceKind Classify(ushort vendorId, ushort productId, string path,
        ushort usagePage, ushort usage, ushort inputLength, ushort outputLength,
        ushort featureLength, byte? outputReportId)
    {
        // 耳机是独立 VID/PID，判据与键鼠完全独立；同 VID 的其它 PID 一律不认（不做型号推断）
        if (vendorId == MchoseV9ProCodec.VendorId)
            return MchoseV9ProCodec.IsSupportedInterface(vendorId, productId, path, usagePage, usage,
                inputLength, outputLength, featureLength, outputReportId)
                ? MchoseInterfaceKind.HeadsetV9
                : MchoseInterfaceKind.None;

        if (vendorId != MchoseVid && vendorId != MchoseLegacyVid)
            return vendorId == G3UltraVid ? ClassifyG3Ultra(productId, usagePage, inputLength, featureLength) : MchoseInterfaceKind.None;

        // consumer/system/标准键盘接口即使产品名含 MCHOSE 也不是配置通道，必须是厂商用法 + 可用 I/O
        if (usagePage < 0xFF00 && usage < 0xFF00) return MchoseInterfaceKind.None;

        if (outputLength == KeyboardReportLength && inputLength == KeyboardReportLength)
            return MchoseInterfaceKind.KeyboardV1;

        // K7 必须排在下面的"≥60 字节只走鼠标"之前：它的命令通道正好也是 64/64，
        // 落到 V2 分支会被当成"等 E2 状态帧"的接收器，白等两轮、一帧读不到。
        // 判据是完整指纹（PID + 厂商用法 + 双方向长度），同 VID 的其它 PID 不受影响。
        if (MchoseK7Codec.IsSupportedInterface(vendorId, productId, usagePage, usage, inputLength, outputLength))
            return MchoseInterfaceKind.MouseK7;

        // 鼠标接收器厂商接口 65 = 64 数据 + 1 report ID；V2 世代长度差异大，按 ≥60 收口
        if (outputLength >= 60 && inputLength >= 60) return MchoseInterfaceKind.MouseV2;

        return MchoseInterfaceKind.None;
    }

    /// <summary>G3 Ultra 只认 0xFF01 的 65 字节厂商集合；同 VID/PID 的 FF0B 集合不是电量通道。</summary>
    private static MchoseInterfaceKind ClassifyG3Ultra(ushort productId, ushort usagePage,
        ushort inputLength, ushort featureLength)
        => productId == G3UltraPid
           && usagePage == G3UltraUsagePage
           && inputLength >= 64
           && featureLength >= 64
            ? MchoseInterfaceKind.MouseG3Ultra
            : MchoseInterfaceKind.None;
}
