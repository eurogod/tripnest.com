namespace TripNest.Core.Enums;

// Pre-booking guest enquiries shown in the landlord workspace.
public enum InquiryStatus
{
    New,
    Replied,
    Archived
}

// Host operational task board (distinct from tenant-reported Maintenance).
public enum HostTaskType
{
    Cleaning,
    Maintenance,
    Inspection,
    Restock
}

public enum HostTaskPriority
{
    Low,
    Medium,
    High
}

public enum HostTaskStatus
{
    Todo,
    InProgress,
    Done
}

// A landlord's staff roster (co-hosts, cleaners, etc.).
public enum TeamMemberRole
{
    Owner,
    CoHost,
    Cleaner,
    Maintenance,
    Agent
}

public enum TeamMemberStatus
{
    Active,
    Invited,
    Suspended
}

// Owner Exchange community board.
public enum ExchangeCategory
{
    Tips,
    Suppliers,
    Regulation,
    Marketplace,
    General
}

// Host resource library.
public enum ResourceCategory
{
    Guide,
    Policy,
    Template,
    Video
}

// Monthly landlord payout statements.
public enum StatementStatus
{
    Pending,
    Paid
}
