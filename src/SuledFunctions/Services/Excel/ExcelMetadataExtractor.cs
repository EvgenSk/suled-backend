using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;
using System.Globalization;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Extracts tournament metadata from filename and Excel cells
/// </summary>
public class ExcelMetadataExtractor(ILogger<ExcelMetadataExtractor> logger) : IExcelMetadataExtractor
{

    /// <summary>
    /// Extract metadata from filename pattern: tournament_2025-11-15_Chicago_DivisionA.xlsx
    /// </summary>
    public void ExtractFromFileName(Tournament tournament, string fileName)
    {
        try
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var parts = nameWithoutExt.Split('_', StringSplitOptions.RemoveEmptyEntries);
            
            // Expected format: name_date_location_division
            // e.g., "SummerChampionship_2025-11-15_Chicago_DivisionA"
            if (parts.Length < 2) return;
            
            // First part is tournament name
            tournament.Name = parts[0];
                
            // Try to parse date from second part
            if (DateTime.TryParseExact(parts[1], "dd'-'MM'-'yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var startDate))
            {
                tournament.StartDate = startDate;
            }
                
            // Third part is location (if exists)
            if (parts.Length >= 3)
            {
                tournament.Location = parts[2];
            }
                
            // Fourth part is division (if exists)
            if (parts.Length >= 4)
            {
                tournament.Division = parts[3];
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not extract metadata from filename {FileName}", fileName);
        }
    }
    
    /// <summary>
    /// Try to extract metadata from rows of cell values.
    /// Expected optional metadata in columns J and K (0-based indices 9 and 10):
    /// Row 0: Tournament Name: [value]
    /// Row 1: Location: [value]
    /// Row 2: Date: [value]
    /// Row 3: Division: [value]
    /// Row 4: Start Time: [value]
    /// Row 5: End Time: [value]
    /// Row 6: Rules: [value]
    /// </summary>
    public void ExtractFromExcel(Tournament tournament, string[][] rows)
    {
        try
        {
            // Look for metadata in first few rows
            for (int row = 0; row < Math.Min(10, rows.Length); row++)
            {
                var labelCell = GetCell(rows, row, 9).Trim();
                var valueCell = GetCell(rows, row, 10).Trim();
                
                if (string.IsNullOrWhiteSpace(labelCell)) continue;
                
                ExtractMetadataField(tournament, labelCell, valueCell);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not extract metadata from Excel content");
        }
    }

    private static string GetCell(string[][] rows, int rowIdx, int colIdx)
    {
        if (rowIdx >= rows.Length) return string.Empty;
        var row = rows[rowIdx];
        if (colIdx >= row.Length) return string.Empty;
        return row[colIdx] ?? string.Empty;
    }

    private void ExtractMetadataField(Tournament tournament, string label, string value)
    {
        // Check for common metadata labels
        if (label.Contains("Tournament", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(value))
                tournament.Name = value;
        }
        else if (label.Contains("Location", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(value))
                tournament.Location = value;
        }
        else if (label.Contains("Date", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.TryParseExact(value, "dd'.'MM'.'yyyy",
               CultureInfo.InvariantCulture,
               DateTimeStyles.None,
               out var date))
                tournament.StartDate = date;
        }
        else if (label.Contains("Start Time", StringComparison.OrdinalIgnoreCase) ||
                 label.Contains("Begin Time", StringComparison.OrdinalIgnoreCase))
        {
            if (TimeSpan.TryParse(value, out var startTime))
                tournament.StartTime = startTime;
        }
        else if (label.Contains("End Time", StringComparison.OrdinalIgnoreCase) ||
                 label.Contains("Finish Time", StringComparison.OrdinalIgnoreCase))
        {
            if (TimeSpan.TryParse(value, out var endTime))
                tournament.EndTime = endTime;
        }
        else if (label.Contains("Division", StringComparison.OrdinalIgnoreCase) ||
                 label.Contains("Category", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(value))
                tournament.Division = value;
        }
        else if (label.Contains("Description", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(value))
                tournament.Description = value;
        }
        else if (label.Contains("Rules", StringComparison.OrdinalIgnoreCase) ||
                 label.Contains("Rule", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(value))
                tournament.Rules = value;
        }
		else if (label.Contains("Warmup", StringComparison.OrdinalIgnoreCase))
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				// Extract numeric value from string (handles "5", "5 mins", "10 minutes", etc.)
				var numericValue = new string([.. value.Where(char.IsDigit)]);
				if (!string.IsNullOrWhiteSpace(numericValue) && int.TryParse(numericValue, out var minutes))
				{
					tournament.Warmup = TimeSpan.FromMinutes(minutes);
				}
			}
		}
	}

	/// <summary>
	/// Determine tournament status based on dates
	/// </summary>
	public void DetermineStatus(Tournament tournament)
    {
        if (!tournament.StartDate.HasValue)
        {
            tournament.Status = TournamentStatus.Upcoming;
            return;
        }
        
        var now = DateTime.UtcNow;
        var startDate = tournament.StartDate.Value.Date;
        // If a StartTime is available, incorporate it so that a same-day tournament
        // is only considered InProgress once the actual start time has passed.
        var startDateTime = tournament.StartTime.HasValue
            ? startDate.Add(tournament.StartTime.Value)
            : startDate;
        // Tournaments that have no explicit EndDate finish on the same day they start.
        // Use EndTime when available for a precise boundary; otherwise end of that calendar day.
        var endDate = tournament.EndDate.HasValue
            ? tournament.EndDate.Value
            : tournament.EndTime.HasValue
                ? startDate.Add(tournament.EndTime.Value)
                : startDate.AddDays(1);
        
        if (now < startDateTime)
        {
            tournament.Status = TournamentStatus.Upcoming;
        }
        else if (now >= endDate)
        {
            tournament.Status = TournamentStatus.Completed;
        }
        else
        {
            tournament.Status = TournamentStatus.InProgress;
        }
    }
}
