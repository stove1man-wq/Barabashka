using System;
using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;
using UnityEngine;

public enum PolicyKind { Run, Confirm, Deny }

public sealed class PolicyDecision
{
    public PolicyKind Kind;
    public string Reason;
}

/// <summary>
/// Единая точка решения «выполнить / спросить / отказать» для возможностей агента.
/// Правила:
/// 1. Возможность можно запретить совсем (/политика call запрет).
/// 2. Наблюдение и подготовка выполняются сразу.
/// 3. Необратимые действия (удаление) всегда требуют подтверждения, даже в режиме «полный цикл».
/// 4. Действия с внешним эффектом (звонок, сообщение) идут по режиму действий: общему (настройка «Действия агента») или своему у каждой возможности.
/// Вызывать из главного потока.
/// </summary>
public static class ActionPolicy
{
    private const string Prefix = "pol.";

    /// <summary>Свой режим возможности: manual, confirm, auto, deny; null - как в общей настройке.</summary>
    public static string GetOverride(string id)
    {
        try
        {
            string value = PlayerPrefs.GetString(Prefix + id, string.Empty);
            return value.Length == 0 ? null : value;
        }
        catch (Exception) { return null; }
    }

    public static void SetOverride(string id, string mode)
    {
        try
        {
            if (string.IsNullOrEmpty(mode)) PlayerPrefs.DeleteKey(Prefix + id);
            else PlayerPrefs.SetString(Prefix + id, mode);
            PlayerPrefs.Save();
        }
        catch (Exception) { }
    }

    /// <summary>Режим действий для возможности: свой или общий; необратимое не бывает «полным циклом».</summary>
    public static ActionLevel LevelFor(string id)
    {
        ActionLevel level = AppSettings.Actions;
        switch (GetOverride(id))
        {
            case "manual": level = ActionLevel.Manual; break;
            case "confirm": level = ActionLevel.Confirm; break;
            case "auto": level = ActionLevel.Auto; break;
        }
        CapabilityInfo info = CapabilityRegistry.Get(id);
        if (info != null && info.Irreversible && level == ActionLevel.Auto) level = ActionLevel.Confirm;
        return level;
    }

    public static PolicyDecision Decide(CapabilityInfo info)
    {
        if (info == null) return new PolicyDecision { Kind = PolicyKind.Run };
        if (GetOverride(info.Id) == "deny")
            return new PolicyDecision { Kind = PolicyKind.Deny, Reason = "вы это запретили (вернуть: /политика " + info.Id + " сброс)" };
        if (info.Irreversible)
            return new PolicyDecision { Kind = PolicyKind.Confirm };
        if (info.Risk != RiskLevel.Act || info.SelfGated)
            return new PolicyDecision { Kind = PolicyKind.Run };
        // Действие с внешним эффектом, которое инструмент сам не разбирает по режимам: вручную и с подтверждением - спрашиваем.
        return new PolicyDecision { Kind = LevelFor(info.Id) == ActionLevel.Auto ? PolicyKind.Run : PolicyKind.Confirm };
    }

    /// <summary>Режим из слова: ручной, голос/подтверждение, авто/полный, запрет, сброс. null, если не понял.</summary>
    public static string ParseMode(string word)
    {
        string w = (word ?? string.Empty).Trim().ToLowerInvariant();
        if (w.StartsWith("ручн", StringComparison.Ordinal) || w == "manual") return "manual";
        if (w.StartsWith("голос", StringComparison.Ordinal) || w.StartsWith("подтвер", StringComparison.Ordinal) || w == "confirm") return "confirm";
        if (w.StartsWith("авто", StringComparison.Ordinal) || w.StartsWith("полн", StringComparison.Ordinal) || w == "auto") return "auto";
        if (w.StartsWith("запрет", StringComparison.Ordinal) || w.StartsWith("запрещ", StringComparison.Ordinal) || w == "deny") return "deny";
        if (w.StartsWith("сброс", StringComparison.Ordinal) || w.StartsWith("общ", StringComparison.Ordinal) || w == "reset") return "reset";
        return null;
    }

    private static string ModeTitle(string mode)
    {
        switch (mode)
        {
            case "manual": return "вручную";
            case "confirm": return "с подтверждением";
            case "auto": return "полный цикл";
            case "deny": return "запрещено";
            default: return null;
        }
    }

    /// <summary>Обработка «/политика» без аргументов (отчёт) и «/политика возможность режим».</summary>
    public static string Handle(string args)
    {
        string[] p = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length >= 2)
        {
            string mode = ParseMode(p[p.Length - 1]);
            string spec = string.Join(" ", p, 0, p.Length - 1);
            CapabilityInfo info = CapabilityRegistry.Find(spec);
            if (info == null) return "Не нашёл такую возможность. Список: /возможности";
            if (mode == null) return "Режим: ручной, голос, авто, запрет или сброс.";
            if (mode == "reset") { SetOverride(info.Id, null); return info.Title + ": как в общей настройке («Действия агента»)."; }
            if (mode == "auto" && info.Irreversible) return info.Title + ": необратимое действие, всегда с подтверждением.";
            SetOverride(info.Id, mode);
            return info.Title + ": " + ModeTitle(mode) + ".";
        }

        var sb = new StringBuilder();
        sb.Append("Общий режим действий: ").Append(AppSettings.ActionsTitle(AppSettings.Actions)).Append('.');
        foreach (CapabilityInfo info in CapabilityRegistry.List)
        {
            if (info.Risk != RiskLevel.Act && !info.SelfGated) continue;
            string own = GetOverride(info.Id);
            string title = own != null ? ModeTitle(own) : AppSettings.ActionsTitle(LevelFor(info.Id));
            sb.Append("\n- ").Append(info.Title).Append(": ").Append(title);
            if (own == null) sb.Append(" (общий)");
            if (info.Irreversible) sb.Append(", удаление всегда с подтверждением");
        }
        sb.Append("\nСменить: /политика <возможность> ручной | голос | авто | запрет | сброс");
        return sb.ToString();
    }
}
