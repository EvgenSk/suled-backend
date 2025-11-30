using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;

namespace SuledFunctions.Repositories;

/// <summary>
/// Query specification for tournament searches
/// </summary>
public class TournamentQuerySpec
{
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public string? Location { get; set; }
    public string? Division { get; set; }
    public TournamentStatus? Status { get; set; }
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Builds SQL WHERE clauses based on the query specification
    /// </summary>
    public List<string> BuildWhereClauses()
    {
        var clauses = new List<string>();

        if (StartDateFrom.HasValue)
            clauses.Add("c.startDate >= @startDateFrom");

        if (StartDateTo.HasValue)
            clauses.Add("c.startDate <= @startDateTo");

        if (!string.IsNullOrWhiteSpace(Location))
            clauses.Add("CONTAINS(c.location, @location, true)");

        if (!string.IsNullOrWhiteSpace(Division))
            clauses.Add("CONTAINS(c.division, @division, true)");

        if (Status.HasValue)
            clauses.Add("c.status = @status");

        return clauses;
    }
}
