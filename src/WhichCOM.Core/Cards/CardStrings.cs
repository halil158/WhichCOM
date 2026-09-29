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

    public required string SerialPorts { get; init; }

    public required string NoDevice { get; init; }

    public required string Copy { get; init; }

    public required string Copied { get; init; }

    public required string New { get; init; }

    public required string Latest { get; init; }

    public required string SerialNumber { get; init; }

    public required string DeviceManager { get; init; }

    /// <summary>Format string with the number of ports that did not fit on the card.</summary>
    public required string More { get; init; }

    public required string Internet { get; init; }

    public required string NoInternet { get; init; }

    public required string NoNetwork { get; init; }

    public required string Connected { get; init; }

    public required string NotConnected { get; init; }

    public required string AudioOutput { get; init; }

    public required string AudioInput { get; init; }

    public required string NoAudioOutput { get; init; }

    public required string NoAudioInput { get; init; }

    public required string Nicknames { get; init; }

    public required string NicknameHint { get; init; }

    public required string NicknamePlaceholder { get; init; }

    public required string NoDeviceForNickname { get; init; }

    public required string Save { get; init; }

    public required string Cancel { get; init; }

    public static CardStrings For(string? language) =>
        string.Equals(language?.Trim(), Turkish, StringComparison.OrdinalIgnoreCase) ? TurkishStrings : EnglishStrings;

    private static CardStrings EnglishStrings { get; } = new()
    {
        Language = English,
        SerialPorts = "Serial ports",
        NoDevice = "No device connected",
        Copy = "Copy",
        Copied = "Copied",
        New = "new",
        Latest = "Latest",
        SerialNumber = "S/N",
        DeviceManager = "Device Manager",
        More = "+{0} more",
        Internet = "Internet access",
        NoInternet = "No internet",
        NoNetwork = "No network",
        Connected = "Connected",
        NotConnected = "Not connected",
        AudioOutput = "Output",
        AudioInput = "Input",
        NoAudioOutput = "No output device",
        NoAudioInput = "No input device",
        Nicknames = "Nicknames",
        NicknameHint = "A nickname stays with the device, also when its port number changes.",
        NicknamePlaceholder = "Nickname",
        NoDeviceForNickname = "Connect a device to give it a nickname.",
        Save = "Save",
        Cancel = "Cancel",
    };

    private static CardStrings TurkishStrings { get; } = new()
    {
        Language = Turkish,
        SerialPorts = "Seri portlar",
        NoDevice = "Bağlı cihaz yok",
        Copy = "Kopyala",
        Copied = "Kopyalandı",
        New = "yeni",
        Latest = "Son takılan",
        SerialNumber = "Seri no",
        DeviceManager = "Aygıt Yöneticisi",
        More = "+{0} port daha",
        Internet = "İnternet erişimi var",
        NoInternet = "İnternet yok",
        NoNetwork = "Ağ bağlantısı yok",
        Connected = "Bağlı",
        NotConnected = "Bağlı değil",
        AudioOutput = "Çıkış",
        AudioInput = "Giriş",
        NoAudioOutput = "Çıkış cihazı yok",
        NoAudioInput = "Giriş cihazı yok",
        Nicknames = "Takma adlar",
        NicknameHint = "Takma ad cihaza bağlıdır; port numarası değişse de kalır.",
        NicknamePlaceholder = "Takma ad",
        NoDeviceForNickname = "Takma ad vermek için bir cihaz bağlayın.",
        Save = "Kaydet",
        Cancel = "Vazgeç",
    };
}
