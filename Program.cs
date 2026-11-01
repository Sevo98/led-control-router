using LedControl;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "OpenWrt LED Control";

Console.WriteLine("Подключение к роутеру OpenWrt");
Console.WriteLine();

var host = ReadHost("IP-адрес роутера: ");
var user = ReadRequired("Логин: ");
var password = ReadPassword("Пароль: ");
Console.WriteLine();

using var router = new RouterLedClient(host, user, password);

try
{
    Console.WriteLine($"Подключение к {user}@{host} ...");
    router.Connect();
    Console.WriteLine("Подключено.\n");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Не удалось подключиться: {ex.Message}");
    return 1;
}

while (true)
{
    IReadOnlyList<LedInfo> leds;
    try
    {
        leds = router.ListLeds();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Ошибка чтения LED: {ex.Message}");
        return 1;
    }

    PrintStatus(leds);
    PrintMenu(leds.Count);

    Console.Write("> ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        continue;

    if (input is "q" or "Q" or "0")
        break;

    try
    {
        HandleCommand(router, leds, input);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }

    Console.WriteLine();
}

return 0;

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        Console.WriteLine("Значение не может быть пустым.");
    }
}

static string ReadHost(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine("Значение не может быть пустым.");
            continue;
        }

        var host = NormalizeHost(value);
        if (string.IsNullOrEmpty(host))
        {
            Console.WriteLine("Не удалось распознать адрес. Пример: 192.168.10.1");
            continue;
        }

        if (!string.Equals(value, host, StringComparison.Ordinal))
            Console.WriteLine($"Используется адрес: {host}");

        return host;
    }
}

static string NormalizeHost(string input)
{
    var value = input.Trim().Trim('"').Trim('\'');
    if (value.Length == 0)
        return string.Empty;

    // Accept pasted LuCI URLs: http://192.168.10.1/ -> 192.168.10.1
    if (!value.Contains("://", StringComparison.Ordinal) && value.Contains('/'))
        value = "http://" + value.TrimStart('/');

    if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "ssh"))
    {
        return uri.IdnHost;
    }

    // Plain host/IP, optionally with path leftovers: 192.168.10.1/...
    var slash = value.IndexOf('/');
    if (slash >= 0)
        value = value[..slash];

    var at = value.LastIndexOf('@');
    if (at >= 0)
        value = value[(at + 1)..];

    // Strip optional :port for IPv4/hostname (keep [IPv6]:port as-is via Uri above)
    if (value.Count(c => c == ':') == 1)
        value = value.Split(':')[0];

    return value.Trim();
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new System.Text.StringBuilder();

    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            if (password.Length == 0)
            {
                Console.WriteLine("Значение не может быть пустым.");
                Console.Write(prompt);
                continue;
            }

            return password.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length == 0)
                continue;

            password.Length--;
            Console.Write("\b \b");
            continue;
        }

        if (key.KeyChar == '\0')
            continue;

        password.Append(key.KeyChar);
        Console.Write('*');
    }
}

static void PrintStatus(IReadOnlyList<LedInfo> leds)
{
    Console.WriteLine("Индикаторы роутера");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"{"#",-3} {"Имя",-18} {"Состояние",-10} {"Триггер",-12}");
    Console.WriteLine(new string('-', 52));

    for (var i = 0; i < leds.Count; i++)
    {
        var led = leds[i];
        var state = led.IsOn ? "ВКЛ" : "выкл";
        Console.WriteLine($"{i + 1,-3} {led.DisplayName,-18} {state,-10} {led.Trigger,-12}");
    }

    Console.WriteLine(new string('-', 52));
}

static void PrintMenu(int ledCount)
{
    Console.WriteLine();
    Console.WriteLine($"1-{ledCount}  переключить выбранный LED");
    Console.WriteLine("a       включить все (режим OpenWrt по умолчанию)");
    Console.WriteLine("x       выключить все");
    Console.WriteLine("on N    включить LED №N");
    Console.WriteLine("off N   выключить LED №N");
    Console.WriteLine("r       обновить список");
    Console.WriteLine("q       выход");
}

static void HandleCommand(RouterLedClient router, IReadOnlyList<LedInfo> leds, string input)
{
    if (input is "r" or "R")
        return;

    if (input is "a" or "A")
    {
        router.TurnAllOn();
        Console.WriteLine("Все индикаторы возвращены к режиму OpenWrt.");
        return;
    }

    if (input is "x" or "X")
    {
        router.TurnAllOff();
        Console.WriteLine("Все индикаторы выключены.");
        return;
    }

    if (TryParseNumbered(input, "on", out var onIndex))
    {
        ApplyOn(router, leds, onIndex);
        return;
    }

    if (TryParseNumbered(input, "off", out var offIndex))
    {
        ApplyOff(router, leds, offIndex);
        return;
    }

    if (int.TryParse(input, out var toggleIndex))
    {
        var led = GetLed(leds, toggleIndex);
        if (led.IsOn)
            ApplyOff(router, leds, toggleIndex);
        else
            ApplyOn(router, leds, toggleIndex);
        return;
    }

    Console.WriteLine("Неизвестная команда.");
}

static void ApplyOn(RouterLedClient router, IReadOnlyList<LedInfo> leds, int index)
{
    var led = GetLed(leds, index);
    router.SetLed(led.Name, on: true);
    Console.WriteLine($"{led.DisplayName}: включен.");
}

static void ApplyOff(RouterLedClient router, IReadOnlyList<LedInfo> leds, int index)
{
    var led = GetLed(leds, index);
    router.SetLed(led.Name, on: false);
    Console.WriteLine($"{led.DisplayName}: выключен.");
}

static LedInfo GetLed(IReadOnlyList<LedInfo> leds, int index)
{
    if (index < 1 || index > leds.Count)
        throw new InvalidOperationException($"Номер должен быть от 1 до {leds.Count}.");
    return leds[index - 1];
}

static bool TryParseNumbered(string input, string command, out int index)
{
    index = 0;
    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 2 || !parts[0].Equals(command, StringComparison.OrdinalIgnoreCase))
        return false;
    return int.TryParse(parts[1], out index);
}
