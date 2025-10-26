using OfficeOpenXml;

namespace SuledFunctions.IntegrationTests.Helpers;

/// <summary>
/// Helper methods for creating test Excel files
/// </summary>
public static class ExcelTestHelper
{
    static ExcelTestHelper()
    {
        // Configure EPPlus license for tests (EPPlus 8+)
        ExcelPackage.License.SetNonCommercialPersonal("Integration Tests");
    }

    /// <summary>
    /// Create a test Excel stream with tournament data
    /// </summary>
    public static MemoryStream CreateTournamentExcelStream(params (string round, int court, string p1_1, string p1_2, string p2_1, string p2_2)[] games)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Tournament");

        // Add header row
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        worksheet.Cells[1, 3].Value = "Player 1.1";
        worksheet.Cells[1, 4].Value = "Player 1.2";
        worksheet.Cells[1, 7].Value = "Player 2.1";
        worksheet.Cells[1, 8].Value = "Player 2.2";

        // Add game data
        for (int i = 0; i < games.Length; i++)
        {
            int row = i + 2;
            var game = games[i];

            if (!string.IsNullOrEmpty(game.round))
                worksheet.Cells[row, 1].Value = game.round;
            if (game.court > 0)
                worksheet.Cells[row, 2].Value = game.court;

            worksheet.Cells[row, 3].Value = game.p1_1;
            worksheet.Cells[row, 4].Value = game.p1_2;
            worksheet.Cells[row, 7].Value = game.p2_1;
            worksheet.Cells[row, 8].Value = game.p2_2;
        }

        var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Create a simple tournament Excel with basic test data
    /// </summary>
    public static MemoryStream CreateSimpleTournamentExcel()
    {
        return CreateTournamentExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris"),
            ("Round 2", 1, "John Doe", "Charlie Davis", "Jane Smith", "Diana Evans"),
            ("", 2, "Alice Brown", "Frank Green", "Bob White", "Grace Harris")
        );
    }

    /// <summary>
    /// Create a tournament Excel with multiple rounds and courts
    /// </summary>
    public static MemoryStream CreateComplexTournamentExcel()
    {
        return CreateTournamentExcelStream(
            // Round 1
            ("Round 1", 1, "Player1 A", "Player1 B", "Player2 A", "Player2 B"),
            ("", 2, "Player3 A", "Player3 B", "Player4 A", "Player4 B"),
            ("", 3, "Player5 A", "Player5 B", "Player6 A", "Player6 B"),
            // Round 2
            ("Round 2", 1, "Player1 A", "Player3 A", "Player2 A", "Player4 A"),
            ("", 2, "Player5 A", "Player1 B", "Player6 A", "Player2 B"),
            ("", 3, "Player3 B", "Player5 B", "Player4 B", "Player6 B"),
            // Round 3
            ("Round 3", 1, "Player1 A", "Player5 A", "Player3 A", "Player2 A"),
            ("", 2, "Player4 A", "Player6 A", "Player1 B", "Player3 B")
        );
    }
}
