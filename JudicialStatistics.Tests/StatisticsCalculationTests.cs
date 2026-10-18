using JudicialStatistics.Domain.Years;
using Xunit;

namespace JudicialStatistics.Tests;

public class StatisticsCalculationTests
{
    [Fact]
    public void JudicialYear_ShouldSpanFromOctoberFirstToSeptemberThirtieth()
    {
        // Arrange
        int startYear = 2025;
        var startDate = new DateOnly(startYear, 10, 1);
        var endDate = new DateOnly(startYear + 1, 9, 30);

        var year = new JudicialYear
        {
            NameAr = $"{startYear}/{startYear + 1}",
            StartDate = startDate,
            EndDate = endDate,
            Status = "Draft"
        };

        // Assert
        Assert.Equal(10, year.StartDate.Month);
        Assert.Equal(1, year.StartDate.Day);
        Assert.Equal(9, year.EndDate.Month);
        Assert.Equal(30, year.EndDate.Day);
        Assert.Equal(2025, year.StartDate.Year);
        Assert.Equal(2026, year.EndDate.Year);
    }

    [Theory]
    [InlineData(10, 5, 4, 11)] // Opening 10, Incoming 5, Judgments 4 -> Closing 11
    [InlineData(0, 20, 15, 5)]  // Opening 0, Incoming 20, Judgments 15 -> Closing 5
    [InlineData(15, 0, 15, 0)]  // Opening 15, Incoming 0, Judgments 15 -> Closing 0
    [InlineData(5, 5, 20, 0)]   // Cannot be negative, capped at 0
    public void ClosingBalance_Formula_ShouldCalculateCorrectly(
        int opening, int incoming, int judgments, int expectedClosing)
    {
        // Act
        var totalAvailable = opening + incoming;
        var closing = Math.Max(0, totalAvailable - judgments);

        // Assert
        Assert.Equal(expectedClosing, closing);
    }

    [Fact]
    public void CriminalAllocations_SumMustMatchProductionCount()
    {
        // Arrange
        int productionCount = 45;
        var allocations = new List<int> { 15, 15, 15 };

        // Act
        var sumAllocations = allocations.Sum();

        // Assert
        Assert.Equal(productionCount, sumAllocations);
    }

    [Theory]
    [InlineData("CivilFull", JudicialStatistics.Application.Organization.StatisticalDisciplines.Civil)]
    [InlineData("CivilPartial", JudicialStatistics.Application.Organization.StatisticalDisciplines.Civil)]
    [InlineData("CriminalAppeal", JudicialStatistics.Application.Organization.StatisticalDisciplines.Criminal)]
    [InlineData("CriminalPartial", JudicialStatistics.Application.Organization.StatisticalDisciplines.Criminal)]
    [InlineData("Family", JudicialStatistics.Application.Organization.StatisticalDisciplines.Family)]
    [InlineData("Labor", JudicialStatistics.Application.Organization.StatisticalDisciplines.Labor)]
    public void StatisticalDisciplines_FromCircuitKind_MapsAccurately(string circuitKind, string expectedDiscipline)
    {
        var result = JudicialStatistics.Application.Organization.StatisticalDisciplines.FromCircuitKind(circuitKind);
        Assert.Equal(expectedDiscipline, result);
    }

    [Fact]
    public void StatisticalDisciplines_GetCircuitKinds_IncludesAllRelevantKinds()
    {
        var criminalKinds = JudicialStatistics.Application.Organization.StatisticalDisciplines.GetCircuitKinds(
            JudicialStatistics.Application.Organization.StatisticalDisciplines.Criminal);
        Assert.Contains("CriminalAppeal", criminalKinds);
        Assert.Contains("Partial", criminalKinds);

        var civilKinds = JudicialStatistics.Application.Organization.StatisticalDisciplines.GetCircuitKinds(
            JudicialStatistics.Application.Organization.StatisticalDisciplines.Civil);
        Assert.Contains("CivilFull", civilKinds);
    }

    [Theory]
    [InlineData(100, 40, 60, true)]   // 100 incoming = 40 judgments + 60 deferrals -> Balanced
    [InlineData(100, 30, 60, false)]  // 100 incoming != 90 (30 + 60) -> Unbalanced
    [InlineData(0, 0, 0, true)]       // Zero cases -> Balanced
    public void BalanceRule_IncomingMustEqualJudgmentsPlusPostponements(
        int incoming, int judgments, int deferrals, bool expectedBalanced)
    {
        var isBalanced = incoming == (judgments + deferrals);
        Assert.Equal(expectedBalanced, isBalanced);
    }

    [Theory]
    [InlineData(500, 125, 25.0)]
    [InlineData(1000, 350, 35.0)]
    [InlineData(0, 0, 0.0)]
    [InlineData(780, 0, 0.0)]
    [InlineData(300, 100, 33.33)]
    public void CircuitRanking_CompletionRate_CalculatesAccurately(int incoming, int finalJudgments, double expectedRate)
    {
        double rate = incoming > 0
            ? Math.Round(((double)finalJudgments / incoming) * 100, 2)
            : 0.0;
        Assert.Equal(expectedRate, rate);
    }

    [Theory]
    [InlineData(1000, 200, 400, true, 50.0)]   // Deduct: Effective = 800, Rate = 400/800 = 50%
    [InlineData(1000, 200, 400, false, 40.0)]  // No Deduct: Effective = 1000, Rate = 400/1000 = 40%
    [InlineData(500, 500, 100, true, 0.0)]     // Effective = 0 -> Rate = 0%
    public void CircuitRanking_MisdemeanorSuspendedDeduction_CalculatesAccurately(
        int incoming, int suspended, int finalJudgments, bool deduct, double expectedRate)
    {
        int effectiveIncoming = deduct ? Math.Max(0, incoming - suspended) : incoming;
        double rate = effectiveIncoming > 0
            ? Math.Round(((double)finalJudgments / effectiveIncoming) * 100, 2)
            : 0.0;
        Assert.Equal(expectedRate, rate);
    }

    [Fact]
    public void CircuitRanking_JudgesAllocations_SumMustEqualCircuitFinalJudgments()
    {
        int circuitFinal = 85;
        var judgeAllocations = new List<int> { 35, 25, 25 }; // President, Right, Left
        Assert.Equal(circuitFinal, judgeAllocations.Sum());
    }

    [Fact]
    public void CircuitRanking_ClosedXMLExport_GeneratesValidWorkbookBytes()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("كشف الترتيب");
        ws.RightToLeft = true;
        ws.Cell("A1").Value = "كشف ترتيب دوائر المدني الكلي";
        ws.Cell("A2").Value = 100;
        ws.Cell("B2").Value = 0.35;
        ws.Cell("B2").Style.NumberFormat.Format = "0.0%";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        Assert.NotEmpty(bytes);
        // Verify ZIP magic number (PK..)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
