namespace Winland.Domain;

public readonly record struct DisplayColorReading(
    bool HdrSupported,
    bool WideSupported,
    bool HdrOn,
    bool WideOn,
    bool AdvancedSupported,
    bool Split,
    int BitsPerChannel,
    float MinLuminance = -1,
    float MaxLuminance = -1,
    float MaxAvgLuminance = -1,
    bool Chroma = false);
