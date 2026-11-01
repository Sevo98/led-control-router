using System.Text;
using Renci.SshNet;

namespace LedControl;

internal sealed class RouterLedClient : IDisposable
{
    private static readonly HashSet<string> HiddenLeds = new(StringComparer.OrdinalIgnoreCase)
    {
        "mt76-phy0",
        "mt76-phy1"
    };

    private static readonly Dictionary<string, string> DefaultTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue:power"] = "none",
        ["blue:wan-online"] = "netdev",
        ["blue:wan"] = "netdev",
        ["blue:lan"] = "netdev",
        ["blue:wlan-2ghz"] = "phy0tpt",
        ["blue:wlan-5ghz"] = "phy1tpt",
        ["red:fault"] = "none",
        ["red:wps"] = "none"
    };

    private readonly ConnectionInfo _connectionInfo;
    private SshClient? _client;

    public RouterLedClient(string host, string username, string password)
    {
        _connectionInfo = new ConnectionInfo(
            host,
            username,
            new PasswordAuthenticationMethod(username, password));
    }

    public void Connect()
    {
        _client = new SshClient(_connectionInfo);
        _client.Connect();
    }

    public IReadOnlyList<LedInfo> ListLeds()
    {
        var script = """
            for d in /sys/class/leds/*; do
              [ -e "$d/brightness" ] || continue
              n=$(basename "$d")
              b=$(cat "$d/brightness" 2>/dev/null || echo 0)
              t=$(sed -n 's/.*\[\(.*\)\].*/\1/p' "$d/trigger" 2>/dev/null || echo none)
              echo "$n|$b|$t"
            done
            """;

        var output = Run(script);
        var leds = new List<LedInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            var name = parts[0];
            if (HiddenLeds.Contains(name))
                continue;

            if (!int.TryParse(parts[1], out var brightness))
                brightness = 0;

            leds.Add(new LedInfo(name, brightness, parts[2]));
        }

        return leds
            .OrderBy(l => DisplayOrder(l.Name))
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SetLed(string name, bool on)
    {
        EnsureSafeName(name);

        if (!on)
        {
            Run($"""
                echo none > /sys/class/leds/{name}/trigger
                echo 0 > /sys/class/leds/{name}/brightness
                """);
            return;
        }

        var trigger = DefaultTriggers.GetValueOrDefault(name, "default-on");
        var sb = new StringBuilder();
        sb.AppendLine($"echo {ShellEscape(trigger)} > /sys/class/leds/{name}/trigger");

        if (trigger is "netdev")
        {
            var (dev, modes) = name switch
            {
                "blue:wan-online" => ("wan", "link"),
                "blue:wan" => ("wan", "tx rx"),
                "blue:lan" => ("br-lan", "link tx rx"),
                _ => ("wan", "link")
            };

            sb.AppendLine($"[ -e /sys/class/leds/{name}/device_name ] && echo {ShellEscape(dev)} > /sys/class/leds/{name}/device_name");
            sb.AppendLine($"[ -e /sys/class/leds/{name}/link ] && echo 0 > /sys/class/leds/{name}/link; [ -e /sys/class/leds/{name}/tx ] && echo 0 > /sys/class/leds/{name}/tx; [ -e /sys/class/leds/{name}/rx ] && echo 0 > /sys/class/leds/{name}/rx");
            foreach (var mode in modes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"[ -e /sys/class/leds/{name}/{mode} ] && echo 1 > /sys/class/leds/{name}/{mode}");
        }
        else if (trigger is "none" or "default-on")
        {
            sb.AppendLine($"echo $(cat /sys/class/leds/{name}/max_brightness) > /sys/class/leds/{name}/brightness");
        }

        Run(sb.ToString());
    }

    public void TurnAllOff()
    {
        Run("/etc/init.d/led turnoff 2>/dev/null || for d in /sys/class/leds/*/brightness; do echo 0 > \"$d\"; done");
        // Ensure activity triggers are stopped so LEDs stay dark.
        Run("""
            for d in /sys/class/leds/*; do
              [ -e "$d/trigger" ] || continue
              n=$(basename "$d")
              case "$n" in mt76-phy*) continue ;; esac
              echo none > "$d/trigger"
              echo 0 > "$d/brightness"
            done
            """);
    }

    public void TurnAllOn()
    {
        Run("/etc/init.d/led turnon 2>/dev/null || true");
        // Restore Wi-Fi throughput triggers if init script did not.
        Run("""
            [ -e /sys/class/leds/blue:wlan-2ghz/trigger ] && echo phy0tpt > /sys/class/leds/blue:wlan-2ghz/trigger
            [ -e /sys/class/leds/blue:wlan-5ghz/trigger ] && echo phy1tpt > /sys/class/leds/blue:wlan-5ghz/trigger
            [ -e /sys/class/leds/blue:power/brightness ] && echo $(cat /sys/class/leds/blue:power/max_brightness) > /sys/class/leds/blue:power/brightness
            """);
    }

    public void Dispose()
    {
        if (_client is null)
            return;

        if (_client.IsConnected)
            _client.Disconnect();

        _client.Dispose();
        _client = null;
    }

    private string Run(string command)
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException(UiText.T("No SSH connection.", "Нет SSH-подключения."));

        // Transfer script via base64 to avoid fragile shell quoting.
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(command.Replace("\r\n", "\n")));
        using var cmd = _client.CreateCommand($"echo {b64} | base64 -d | /bin/sh");
        cmd.CommandTimeout = TimeSpan.FromSeconds(15);
        var result = cmd.Execute();
        if (cmd.ExitStatus is not null and not 0 && string.IsNullOrWhiteSpace(result) && !string.IsNullOrWhiteSpace(cmd.Error))
            throw new InvalidOperationException(cmd.Error.Trim());

        return result;
    }

    private static int DisplayOrder(string name) => name switch
    {
        "blue:power" => 1,
        "blue:wan-online" => 2,
        "blue:wan" => 3,
        "blue:lan" => 4,
        "blue:wlan-2ghz" => 5,
        "blue:wlan-5ghz" => 6,
        "red:wps" => 7,
        "red:fault" => 8,
        _ => 100
    };

    private static void EnsureSafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is ':' or '-' or '_')))
            throw new ArgumentException(UiText.T($"Invalid LED name: {name}", $"Некорректное имя LED: {name}"));
    }

    private static string ShellEscape(string value) => value.Replace("'", "'\\''");
}
