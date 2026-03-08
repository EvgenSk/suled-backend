using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;
using System.Globalization;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Extracts tournament metadata from filename and Excel cells
/// </summary>
public class ExcelMetadataExtractor : IExcelMetadataExtractor
{
    private readonly ILogger<ExcelMetadataExtractor> _logger;

    public ExcelMetadataExtractor(ILogger<ExcelMetadataExtractor> logger)
    {
        _logger = logger;
    }

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
            if (parts.Length >= 2)
            {
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract metadata from filename {FileName}", fileName);
        }
    }
    
    /// <summary>
    /// Try to extract metadata from Excel cells
    /// Expected format (optional):
    /// Row 1: Tournament Name: [value]
    /// Row 2: Location: [value]
    /// Row 3: Date: [value]
    /// Row 4: Division: [value]
    /// Row 5: Start Time: [value]
    /// Row 6: End Time: [value]
    /// Row 7: Rules: [value]
    /// </summary>
    public void ExtractFromExcel(Tournament tournament, ExcelWorksheet worksheet)
    {
        try
        {
            // Look for metadata in first few rows (increased to 10 to accommodate more fields)
            for (int row = 1; row <= Math.Min(10, worksheet.Dimension?.End.Row ?? 0); row++)
            {
                var labelCell = worksheet.Cells[row, 10].Text.Trim();
                var valueCell = worksheet.Cells[row, 11].Text.Trim();
                
                if (string.IsNullOrWhiteSpace(labelCell)) continue;
                
                ExtractMetadataField(tournament, labelCell, valueCell);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract metadata from Excel content");
        }
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
        // When no explicit end date, treat the tournament as running for the full calendar day
        // (end = start of next day). This avoids spuriously marking same-day tournaments
        // as Completed the moment they are uploaded, since StartDate is midnight.
        var endDate = tournament.EndDate.HasValue
            ? tournament.EndDate.Value
            : startDate.AddDays(1);
        
        if (now < startDate)
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
