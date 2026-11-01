<p align="center">
  <img src="assets/openwrt-router.png" alt="OpenWrt LED Control" width="720" />
</p>

<p align="center">
  <a href="#russian"><strong>Русский</strong></a>
  &nbsp;·&nbsp;
  <a href="#english"><strong>English</strong></a>
</p>

<a id="russian"></a>

# OpenWrt LED Control

Небольшое консольное Windows-приложение для управления светодиодными индикаторами роутера на **OpenWrt** по SSH.

Показывает текущее состояние LED и позволяет включать или выключать их по одному либо все сразу.

## Возможности

- Подключение к роутеру по SSH (IP, логин и пароль вводятся при запуске)
- Список индикаторов с состоянием (вкл/выкл) и активным триггером
- Переключение отдельного LED
- Включение всех индикаторов в режиме OpenWrt по умолчанию
- Полное выключение всех индикаторов

Проверено на **Cudy WBR3000UAX v1** (OpenWrt 25.12), но подойдёт и для других устройств OpenWrt с LED в `/sys/class/leds`.

## Быстрый старт

Готовый файл:

```text
publish\LedControl.exe
```

Запустите exe, укажите IP роутера, логин и пароль — откроется меню управления индикаторами.

### Запуск из исходников

Нужен [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
dotnet run -c Release
```

### Сборка exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

## Управление

| Команда | Действие |
|--------|----------|
| `1`…`N` | Переключить выбранный LED |
| `on N` / `off N` | Включить / выключить LED №N |
| `a` | Включить все (режим OpenWrt по умолчанию) |
| `x` | Выключить все |
| `r` | Обновить список |
| `q` | Выход |

## Требования

- Windows x64
- Роутер с OpenWrt и включённым SSH
- Учётная запись с доступом к `/sys/class/leds` (обычно `root`)

## Как это работает

Приложение подключается по SSH и управляет LED через sysfs OpenWrt:

- чтение `/sys/class/leds/*/brightness` и `trigger`
- выключение: `trigger=none`, `brightness=0`
- включение: восстановление типичных триггеров устройства
- массовые операции: `/etc/init.d/led turnoff` / `turnon`

## Лицензия

MIT

---

<a id="english"></a>

# OpenWrt LED Control

A small Windows console application for controlling the LED indicators of an **OpenWrt** router over SSH.

It shows the current LED state and lets you turn them on or off one at a time or all at once.

## Features

- Connect to the router over SSH (IP, username, and password are entered at startup)
- A list of indicators with their state (on/off) and the active trigger
- Toggle an individual LED
- Turn all indicators on in the default OpenWrt mode
- Turn all indicators completely off

Tested on **Cudy WBR3000UAX v1** (OpenWrt 25.12), and it is also suitable for other OpenWrt devices with LEDs in `/sys/class/leds`.

## Quick start

Ready-made file:

```text
publish\LedControl.exe
```

Run the exe, enter the router IP, username, and password — the indicator control menu opens.

### Run from source

You need the [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
dotnet run -c Release
```

### Build the exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

## Controls

| Command | Action |
|--------|----------|
| `1`…`N` | Toggle the selected LED |
| `on N` / `off N` | Turn LED #N on / off |
| `a` | Turn all on (default OpenWrt mode) |
| `x` | Turn all off |
| `r` | Refresh the list |
| `q` | Quit |

## Requirements

- Windows x64
- A router running OpenWrt with SSH enabled
- An account with access to `/sys/class/leds` (usually `root`)

## How it works

The application connects over SSH and controls the LEDs through OpenWrt sysfs:

- reading `/sys/class/leds/*/brightness` and `trigger`
- turning off: `trigger=none`, `brightness=0`
- turning on: restoring the device's typical triggers
- bulk operations: `/etc/init.d/led turnoff` / `turnon`

## License

MIT
