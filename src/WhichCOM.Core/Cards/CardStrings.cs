namespace WhichCOM.Core.Cards;

public enum CardSize
{
    Small,
    Medium,
    Large,
}

/// <summary>Texts shown on the widgets. English is the fallback language.</summary>
public sealed record CardStrings
{
    public const string English = "en";
    public const string Turkish = "tr";

    public required string Language { get; init; }

    public required string NoDevice { get; init; }

    public required string Copy { get; init; }

    public required string Copied { get; init; }

    public required string New { get; init; }

    public required string Latest { get; init; }

    public required string SerialNumber { get; init; }

    public required string DeviceManager { get; init; }

    /// <summary>Format string with the number of ports that did not fit on the card.</summary>
    public required string More { get; init; }

    public static CardStrings For(string? language) =>
        string.Equals(language?.Trim(), Turkish, StringComparison.OrdinalIgnoreCase) ? TurkishStrings : EnglishStrings;

    private static CardStrings EnglishStrings { get; } = new()
    {
        Language = English,
        NoDevice = "No device connected",
        Copy = "Copy",
        Copied = "Copied",
        New = "new",
        Latest = "Latest",
        SerialNumber = "S/N",
        DeviceManager = "Device Manager",
        More = "+{0} more",
    };

    private static CardStrings TurkishStrings { get; } = new()
    {
        Language = Turkish,
        NoDevice = "Bağlı cihaz yok",
        Copy = "Kopyala",
        Copied = "Kopyalandı",
        New = "yeni",
        Latest = "Son takılan",
        SerialNumber = "Seri no",
        DeviceManager = "Aygıt Yöneticisi",
        More = "+{0} port daha",
    };
}
