namespace PunchPoint.Core.Models;

public enum EmployeeStatus
{
    Active = 0,
    Inactive = 1,
}

public enum PayType
{
    Hourly = 0,
    Salary = 1,
}

public sealed class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string MiddleInitial { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Badge { get; set; } = "";
    public string Pin { get; set; } = "";
    public string EmployeeNumber { get; set; } = "";
    public int? ShiftId { get; set; }
    public string Message { get; set; } = "";
    public DateTime? HireDate { get; set; }
    public string ExportId { get; set; } = "";
    public string Email { get; set; } = "";
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public PayType PayType { get; set; } = PayType.Hourly;
    public decimal? PayRate { get; set; }
    public string Notes { get; set; } = "";
    public string Custom1 { get; set; } = "";
    public string Custom2 { get; set; } = "";
    public string Custom3 { get; set; } = "";
    public string Custom4 { get; set; } = "";
    public string Custom5 { get; set; } = "";

    /// <summary>Primary key in the imported timeQplus database (Employees.EmployeeID).</summary>
    public int? LegacyId { get; set; }

    public string FullName => string.Join(" ",
        new[] { FirstName, MiddleInitial, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>"Last, First" form used by exports.</summary>
    public string SortName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{LastName}, {FirstName}";

    public string Initials
    {
        get
        {
            string a = FirstName.Length > 0 ? FirstName[..1] : "";
            string b = LastName.Length > 0 ? LastName[..1] : "";
            string s = (a + b).ToUpperInvariant();
            return s.Length == 0 ? "?" : s;
        }
    }

    public bool IsActive => Status == EmployeeStatus.Active;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? $"(employee {Id})" : FullName;

    public override string ToString() => DisplayName;
}
