namespace Barabashka.Contracts
{
    /// <summary>
    /// Насколько действие значимо. Observe - читает и ничего не меняет; Prepare - меняет только данные самого приложения,
    /// обратимое состояние телефона или открывает готовый экран; Act - действие с внешним эффектом (звонок, сообщение, удаление).
    /// </summary>
    public enum RiskLevel { Observe, Prepare, Act }

    public enum NetworkNeed { None, Optional, Required }

    public enum CapabilityState
    {
        Available,
        NeedsPermission,
        NeedsSetup,
        Offline,
        BlockedByLabel,
        Unsupported,
        Disabled
    }

    /// <summary>Описание возможности агента: что это, насколько значимо, что ей нужно.</summary>
    public sealed class CapabilityInfo
    {
        public string Id;
        public string Title;
        public RiskLevel Risk;
        public NetworkNeed Network;
        public bool AndroidOnly;
        public string[] Permissions = new string[0];
        /// <summary>Нельзя отменить (удаление): всегда требует подтверждения, даже в режиме «полный цикл».</summary>
        public bool Irreversible;
        /// <summary>Инструмент сам применяет режим действий (ему нужно имя получателя для вопроса): звонок, сообщение, будильник.</summary>
        public bool SelfGated;
        /// <summary>Идентификатор коннектора, если нужна внешняя служба (пока не используется).</summary>
        public string Connector;
    }

    /// <summary>Состояние возможности в данный момент и причина, если её нельзя выполнить.</summary>
    public sealed class CapabilityStatus
    {
        public CapabilityState State;
        public string Reason;

        /// <summary>Можно пробовать выполнять: разрешение при необходимости будет запрошено при выполнении.</summary>
        public bool CanRun { get { return State == CapabilityState.Available || State == CapabilityState.NeedsPermission; } }
    }
}
