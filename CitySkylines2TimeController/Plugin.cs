using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CitySkylines2TimeController;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "kpteam.cityskylines2.timecontroller";
    public const string PluginName = "CitySkylines2 Time Controller";
    public const string PluginVersion = "0.1.0";

    private ConfigEntry<KeyCode> _speedUpKey = null!;
    private ConfigEntry<KeyCode> _speedDownKey = null!;
    private ConfigEntry<KeyCode> _pauseKey = null!;
    private ConfigEntry<KeyCode> _normalSpeedKey = null!;

    private ConfigEntry<KeyCode> _setMorningKey = null!;
    private ConfigEntry<KeyCode> _setNoonKey = null!;
    private ConfigEntry<KeyCode> _setEveningKey = null!;
    private ConfigEntry<KeyCode> _setMidnightKey = null!;
    private ConfigEntry<KeyCode> _toggleFreezeTimeKey = null!;

    private ConfigEntry<float> _speedStep = null!;
    private ConfigEntry<float> _minSpeed = null!;
    private ConfigEntry<float> _maxSpeed = null!;

    private bool _paused;
    private bool _freezeTimeOfDay;
    private float _frozenHour = 12f;

    private float _lastOverlayDraw;
    private string _overlayText = string.Empty;

    private void Awake()
    {
        BindConfig();
        _overlayText = BuildOverlayText();

        var harmony = new Harmony(PluginGuid);
        harmony.PatchAll();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        Logger.LogInfo("Hotkeys: F6 speed+, F5 speed-, F7 pause, F8 normal, F9/F10/F11/F12 time presets, F4 freeze toggle");
    }

    private void BindConfig()
    {
        _speedUpKey = Config.Bind("Hotkeys", "SpeedUp", KeyCode.F6, "Increase time scale");
        _speedDownKey = Config.Bind("Hotkeys", "SpeedDown", KeyCode.F5, "Decrease time scale");
        _pauseKey = Config.Bind("Hotkeys", "PauseToggle", KeyCode.F7, "Pause or resume simulation time");
        _normalSpeedKey = Config.Bind("Hotkeys", "NormalSpeed", KeyCode.F8, "Reset speed to x1");

        _setMorningKey = Config.Bind("Hotkeys", "SetMorning", KeyCode.F9, "Set time-of-day to 08:00");
        _setNoonKey = Config.Bind("Hotkeys", "SetNoon", KeyCode.F10, "Set time-of-day to 12:00");
        _setEveningKey = Config.Bind("Hotkeys", "SetEvening", KeyCode.F11, "Set time-of-day to 18:00");
        _setMidnightKey = Config.Bind("Hotkeys", "SetMidnight", KeyCode.F12, "Set time-of-day to 00:00");
        _toggleFreezeTimeKey = Config.Bind("Hotkeys", "ToggleFreezeTime", KeyCode.F4, "Freeze/unfreeze time-of-day at last set value");

        _speedStep = Config.Bind("Speed", "Step", 0.25f, "Time scale step size");
        _minSpeed = Config.Bind("Speed", "Min", 0f, "Minimum allowed time scale");
        _maxSpeed = Config.Bind("Speed", "Max", 10f, "Maximum allowed time scale");
    }

    private void Update()
    {
        HandleSpeedInput();
        HandleTimeOfDayInput();

        if (_freezeTimeOfDay)
        {
            TrySetGameTimeOfDay(_frozenHour);
        }

        if (Time.unscaledTime - _lastOverlayDraw > 0.15f)
        {
            _overlayText = BuildOverlayText();
            _lastOverlayDraw = Time.unscaledTime;
        }
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(12f, 12f, 720f, 28f), _overlayText);
    }

    private void HandleSpeedInput()
    {
        if (Input.GetKeyDown(_speedUpKey.Value))
        {
            SetTimescale(Time.timeScale + _speedStep.Value);
            _paused = false;
        }

        if (Input.GetKeyDown(_speedDownKey.Value))
        {
            SetTimescale(Time.timeScale - _speedStep.Value);
            _paused = Time.timeScale <= 0f;
        }

        if (Input.GetKeyDown(_normalSpeedKey.Value))
        {
            SetTimescale(1f);
            _paused = false;
        }

        if (Input.GetKeyDown(_pauseKey.Value))
        {
            _paused = !_paused;
            SetTimescale(_paused ? 0f : Math.Max(1f, _minSpeed.Value));
        }
    }

    private void HandleTimeOfDayInput()
    {
        if (Input.GetKeyDown(_setMorningKey.Value))
        {
            SetTimePreset(8f);
        }

        if (Input.GetKeyDown(_setNoonKey.Value))
        {
            SetTimePreset(12f);
        }

        if (Input.GetKeyDown(_setEveningKey.Value))
        {
            SetTimePreset(18f);
        }

        if (Input.GetKeyDown(_setMidnightKey.Value))
        {
            SetTimePreset(0f);
        }

        if (Input.GetKeyDown(_toggleFreezeTimeKey.Value))
        {
            _freezeTimeOfDay = !_freezeTimeOfDay;
            Logger.LogInfo(_freezeTimeOfDay
                ? $"Time-of-day freeze enabled at {_frozenHour.ToString("0.00", CultureInfo.InvariantCulture)}h"
                : "Time-of-day freeze disabled");
        }
    }

    private void SetTimePreset(float hour)
    {
        _frozenHour = NormalizeHour(hour);
        if (!TrySetGameTimeOfDay(_frozenHour))
        {
            Logger.LogWarning("Could not find a writable in-game time-of-day field/property automatically.");
        }
    }

    private void SetTimescale(float value)
    {
        var clamped = Mathf.Clamp(value, _minSpeed.Value, _maxSpeed.Value);
        Time.timeScale = clamped;
        Logger.LogInfo($"Simulation speed set to x{clamped.ToString("0.00", CultureInfo.InvariantCulture)}");
    }

    private static float NormalizeHour(float hour)
    {
        var normalized = hour % 24f;
        return normalized < 0f ? normalized + 24f : normalized;
    }

    private bool TrySetGameTimeOfDay(float hour)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var normalized = NormalizeHour(hour);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.FullName == null)
                {
                    continue;
                }

                if (!LooksLikeTimeOwner(type.FullName))
                {
                    continue;
                }

                object? instance = null;
                if (!type.IsAbstract && !type.IsSealed)
                {
                    instance = FindUnityObjectOfType(type);
                    if (instance == null)
                    {
                        continue;
                    }
                }

                foreach (var prop in type.GetProperties(Flags))
                {
                    if (!prop.CanWrite)
                    {
                        continue;
                    }

                    if (!LooksLikeHourMember(prop.Name))
                    {
                        continue;
                    }

                    if (TryAssign(prop.PropertyType, normalized, out var converted))
                    {
                        prop.SetValue(instance, converted);
                        Logger.LogInfo($"Set {type.FullName}.{prop.Name} to {normalized:0.00}h");
                        return true;
                    }
                }

                foreach (var field in type.GetFields(Flags))
                {
                    if (!LooksLikeHourMember(field.Name))
                    {
                        continue;
                    }

                    if (TryAssign(field.FieldType, normalized, out var converted))
                    {
                        field.SetValue(instance, converted);
                        Logger.LogInfo($"Set {type.FullName}.{field.Name} to {normalized:0.00}h");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool LooksLikeTimeOwner(string fullName)
    {
        var lowered = fullName.ToLowerInvariant();
        return lowered.Contains("time") || lowered.Contains("day") || lowered.Contains("sun") || lowered.Contains("cycle");
    }

    private static bool LooksLikeHourMember(string name)
    {
        var lowered = name.ToLowerInvariant();
        return lowered.Contains("hour")
               || lowered.Contains("timeofday")
               || lowered.Contains("timeofday")
               || lowered == "time"
               || lowered == "currenttime";
    }

    private static bool TryAssign(Type type, float hour, out object? converted)
    {
        if (type == typeof(float))
        {
            converted = hour;
            return true;
        }

        if (type == typeof(double))
        {
            converted = (double)hour;
            return true;
        }

        if (type == typeof(int))
        {
            converted = Mathf.RoundToInt(hour);
            return true;
        }

        converted = null;
        return false;
    }

    private static object? FindUnityObjectOfType(Type type)
    {
        var method = typeof(UnityEngine.Object)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "FindObjectOfType" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Type));

        return method?.Invoke(null, new object[] { type });
    }

    private string BuildOverlayText()
    {
        return $"{PluginName} | Speed x{Time.timeScale:0.00} | Pause {(_paused ? "On" : "Off")} | Freeze TOD {(_freezeTimeOfDay ? "On" : "Off")} @ {_frozenHour:0.00}h";
    }
}
