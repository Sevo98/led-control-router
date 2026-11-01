using LedControl;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "OpenWrt LED Control";

UiText.Use(ReadLanguage());
Console.WriteLine();

Console.WriteLine(UiText.T(
    "Connection to the OpenWrt router",
    "Подключение к роутеру OpenWrt"));
Console.WriteLine();

var host = ReadHost(UiText.T("Router IP address: ", "IP-адрес роутера: "));
var user = ReadRequired(UiText.T("Username: ", "Логин: "));
var password = ReadPassword(UiText.T("Password: ", "Пароль: "));
Console.WriteLine();

using var router = new RouterLedClient(host, user, password);

try
{
    Console.WriteLine(UiText.T(
        $"Connecting to {user}@{host} ...",
        $"Подключение к {user}@{host} ..."));
    router.Connect();
    Console.WriteLine(UiText.T("Connected.\n", "Подключено.\n"));
}
catch (Exception ex)
{
    Console.Error.WriteLine(UiText.T(
        $"Could not connect: {ex.Message}",
        $"Не удалось подключиться: {ex.Message}"));
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
        Console.Error.WriteLine(UiText.T(
            $"Failed to read LEDs: {ex.Message}",
            $"Ошибка чтения LED: {ex.Message}"));
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
        Console.WriteLine(UiText.T($"Error: {ex.Message}", $"Ошибка: {ex.Message}"));
    }

    Console.WriteLine();
}

return 0;

static AppLanguage ReadLanguage()
{
    Console.WriteLine("Select language / Выберите язык:");
    Console.WriteLine("  1  English");
    Console.WriteLine("  2  Русский");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine()?.Trim();
        switch (input?.ToLowerInvariant())
        {
            case "1" or "en" or "english":
                Console.WriteLine("Language: English");
                return AppLanguage.English;
            case "2" or "ru" or "russian" or "русский":
                Console.WriteLine("Язык: русский");
                return AppLanguage.Russian;
            default:
                Console.WriteLine("Enter 1 or 2. / Введите 1 или 2.");
                break;
        }
    }
}

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        WriteEmptyValue();
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
            WriteEmptyValue();
            continue;
        }

        var host = NormalizeHost(value);
        if (string.IsNullOrEmpty(host))
        {
            Console.WriteLine(UiText.T(
                "Could not recognize the address. Example: 192.168.10.1",
                "Не удалось распознать адрес. Пример: 192.168.10.1"));
            continue;
        }

        if (!string.Equals(value, host, StringComparison.Ordinal))
            Console.WriteLine(UiText.T($"Using address: {host}", $"Используется адрес: {host}"));

        return host;
    }
}

static void WriteEmptyValue() =>
    Console.WriteLine(UiText.T("Value cannot be empty.", "Значение не может быть пустым."));

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
                WriteEmptyValue();
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
    Console.WriteLine(UiText.T("Router indicators", "Индикаторы роутера"));
    Console.WriteLine(new string('-', 52));
    Console.WriteLine(UiText.T(
        $"{"#",-3} {"Name",-18} {"State",-10} {"Trigger",-12}",
        $"{"#",-3} {"Имя",-18} {"Состояние",-10} {"Триггер",-12}"));
    Console.WriteLine(new string('-', 52));

    for (var i = 0; i < leds.Count; i++)
    {
        var led = leds[i];
        var state = led.IsOn ? UiText.T("ON", "ВКЛ") : UiText.T("off", "выкл");
        Console.WriteLine($"{i + 1,-3} {led.DisplayName,-18} {state,-10} {led.Trigger,-12}");
    }

    Console.WriteLine(new string('-', 52));
}

static void PrintMenu(int ledCount)
{
    Console.WriteLine();
    Console.WriteLine(UiText.T(
        $"1-{ledCount}  toggle the selected LED",
        $"1-{ledCount}  переключить выбранный LED"));
    Console.WriteLine(UiText.T(
        "a       turn all on (default OpenWrt mode)",
        "a       включить все (режим OpenWrt по умолчанию)"));
    Console.WriteLine(UiText.T("x       turn all off", "x       выключить все"));
    Console.WriteLine(UiText.T("on N    turn LED #N on", "on N    включить LED №N"));
    Console.WriteLine(UiText.T("off N   turn LED #N off", "off N   выключить LED №N"));
    Console.WriteLine(UiText.T("r       refresh the list", "r       обновить список"));
    Console.WriteLine(UiText.T("q       quit", "q       выход"));
}

static void HandleCommand(RouterLedClient router, IReadOnlyList<LedInfo> leds, string input)
{
    if (input is "r" or "R")
        return;

    if (input is "a" or "A")
    {
        router.TurnAllOn();
        Console.WriteLine(UiText.T(
            "All indicators restored to OpenWrt mode.",
            "Все индикаторы возвращены к режиму OpenWrt."));
        return;
    }

    if (input is "x" or "X")
    {
        router.TurnAllOff();
        Console.WriteLine(UiText.T(
            "All indicators turned off.",
            "Все индикаторы выключены."));
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

    Console.WriteLine(UiText.T("Unknown command.", "Неизвестная команда."));
}

static void ApplyOn(RouterLedClient router, IReadOnlyList<LedInfo> leds, int index)
{
    var led = GetLed(leds, index);
    router.SetLed(led.Name, on: true);
    Console.WriteLine(UiText.T($"{led.DisplayName}: turned on.", $"{led.DisplayName}: включен."));
}

static void ApplyOff(RouterLedClient router, IReadOnlyList<LedInfo> leds, int index)
{
    var led = GetLed(leds, index);
    router.SetLed(led.Name, on: false);
    Console.WriteLine(UiText.T($"{led.DisplayName}: turned off.", $"{led.DisplayName}: выключен."));
}

static LedInfo GetLed(IReadOnlyList<LedInfo> leds, int index)
{
    if (index < 1 || index > leds.Count)
        throw new InvalidOperationException(UiText.T(
            $"Number must be from 1 to {leds.Count}.",
            $"Номер должен быть от 1 до {leds.Count}."));
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
