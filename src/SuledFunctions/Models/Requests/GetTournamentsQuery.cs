using SuledFunctions.Models;

namespace SuledFunctions.Models.Requests;

/// <summary>
/// Query parameters for tournament search
/// </summary>
public class GetTournamentsQuery
{
    /// <summary>
    /// Filter tournaments starting from this date
    /// </summary>
    public DateTime? StartDateFrom { get; set; }

    /// <summary>
    /// Filter tournaments starting up to this date
    /// </summary>
    public DateTime? StartDateTo { get; set; }

    /// <summary>
    /// Filter by location (partial match, case-insensitive)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Filter by division (partial match, case-insensitive)
    /// </summary>
    public string? Division { get; set; }

    /// <summary>
    /// Filter by tournament status
    /// </summary>
    public TournamentStatus? Status { get; set; }

    /// <summary>
    /// Maximum number of results to return (1-500, default: 100)
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Page number for pagination (1-based, default: 1)
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Number of items per page (1-100, default: 20)
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Validates and applies defaults to the query
    /// </summary>
    public void ApplyDefaults()
    {
        MaxResults = MaxResults.HasValue ? Math.Clamp(MaxResults.Value, 1, 500) : 100;
        Page = Page.HasValue ? Math.Max(Page.Value, 1) : 1;
        PageSize = PageSize.HasValue ? Math.Clamp(PageSize.Value, 1, 100) : 20;
    }

    /// <summary>
    /// Parses query from query string collection
    /// </summary>
    public static GetTournamentsQuery Parse(System.Collections.Specialized.NameValueCollection query)
    {
        var result = new GetTournamentsQuery();

        // Parse dates
        if (query["startDateFrom"] != null && DateTime.TryParse(query["startDateFrom"], out var dateFrom))
        {
            result.StartDateFrom = dateFrom;
        }

        if (query["startDateTo"] != null && DateTime.TryParse(query["startDateTo"], out var dateTo))
        {
            result.StartDateTo = dateTo;
        }

        // Parse strings
        result.Location = query["location"];
        result.Division = query["division"];

        // Parse status
        if (query["status"] != null && Enum.TryParse<TournamentStatus>(query["status"], true, out var parsedStatus))
        {
            result.Status = parsedStatus;
        }

        // Parse pagination
        if (query["maxResults"] != null && int.TryParse(query["maxResults"], out var maxResults))
        {
            result.MaxResults = maxResults;
        }

        if (query["page"] != null && int.TryParse(query["page"], out var page))
        {
            result.Page = page;
        }

        if (query["pageSize"] != null && int.TryParse(query["pageSize"], out var pageSize))
        {
            result.PageSize = pageSize;
        }

        result.ApplyDefaults();
        return result;
    }
}
