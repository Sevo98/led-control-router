namespace LedControl;

internal sealed record LedInfo(string Name, int Brightness, string Trigger)
{
    public bool IsOn => Brightness > 0;

    public string DisplayName => Name switch
    {
        "blue:power" => "Power",
        "blue:wan-online" => "Internet",
        "blue:wan" => "WAN",
        "blue:lan" => "LAN",
        "blue:wlan-2ghz" => "Wi-Fi 2.4 GHz",
        "blue:wlan-5ghz" => "Wi-Fi 5 GHz",
        "red:fault" => "Fault",
        "red:wps" => "WPS",
        _ => Name
    };
}
