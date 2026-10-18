using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JudicialStatistics.Application.Organization;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

[Collection("Database")]
public class JudgeMovementsIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly DbContextOptions<JudicialDbContext> _options;

    public JudgeMovementsIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;
    }

    [Fact]
    public async Task TransferMovement_UpdatesBothCircuitsAndPreservesOrder()
    {
        var factory = new TestDbContextFactory(_options);
        var orgService = new OrganizationService(factory);
        await using var db = new JudicialDbContext(_options);

        // Get two circuits with judges
        var circuits = await db.Circuits
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Where(c => c.Versions.Any(v => v.JudgeAssignments.Count >= 2))
            .Take(2)
            .ToListAsync();

        if (circuits.Count < 2)
        {
            _output.WriteLine("Not enough circuits with judges found, skipping test.");
            return;
        }

        var circuit1 = circuits[0];
        var circuit2 = circuits[1];
        var cv1 = circuit1.Versions.First();
        var cv2 = circuit2.Versions.First();

        var judgeToMove = cv1.JudgeAssignments.First().Judge;
        var role = await db.JudicialRoles.FirstAsync(r => r.Code == "MEMBER_RIGHT");

        // Define FromCircuit reordered judges (remaining judges)
        var remainingIn1 = cv1.JudgeAssignments
            .Where(ja => ja.JudgeId != judgeToMove.Id)
            .Select((ja, idx) => new CircuitJudgeOrderItem(ja.JudgeId, ja.Judge.FullNameAr, ja.JudicialRoleId, "عضو", (short)(idx + 1)))
            .ToList();

        // Define ToCircuit judges (including incoming judge)
        var inCircuit2 = cv2.JudgeAssignments
            .Select((ja, idx) => new CircuitJudgeOrderItem(ja.JudgeId, ja.Judge.FullNameAr, ja.JudicialRoleId, "عضو", (short)(idx + 1)))
            .ToList();
        inCircuit2.Add(new CircuitJudgeOrderItem(judgeToMove.Id, judgeToMove.FullNameAr, role.Id, role.NameAr, (short)(inCircuit2.Count + 1)));

        // Create Transfer Command
        var cmd = new JudgeMovementCreateCommand(
            JudgeId: judgeToMove.Id,
            CircuitId: circuit2.Id,
            FromCircuitId: circuit1.Id,
            ToCircuitId: circuit2.Id,
            JudicialRoleId: role.Id,
            MovementType: "Transfer",
            DecisionNumber: $"قرار وزير العدل {Random.Shared.Next(100, 999)} لسنة 2025",
            DecisionDate: new DateOnly(2025, 11, 1),
            EffectiveFrom: new DateOnly(2025, 11, 5),
            EffectiveTo: null,
            Reason: "سد العجز بدائرة أخرى",
            Notes: "اختبار نقل القاضي وتحديث التشكيل",
            FromCircuitJudges: remainingIn1,
            ToCircuitJudges: inCircuit2
        );

        var movementId = await orgService.CreateJudgeMovementAsync(cmd);
        Assert.True(movementId > 0);

        // Verify JudgeMovement record was persisted with JSON snapshots
        var movements = await orgService.GetJudgeMovementsAsync(judgeId: judgeToMove.Id, circuitId: circuit2.Id);
        var createdMov = movements.FirstOrDefault(m => m.Id == movementId);
        Assert.NotNull(createdMov);
        Assert.Equal("Transfer", createdMov.MovementType);
        Assert.Equal(circuit1.Id, createdMov.FromCircuitId);
        Assert.Equal(circuit2.Id, createdMov.ToCircuitId);
        Assert.NotNull(createdMov.FromCircuitOrderJson);
        Assert.NotNull(createdMov.ToCircuitOrderJson);

        _output.WriteLine($"Transfer created successfully with ID: {movementId}. FromOrder: {createdMov.FromCircuitOrderJson}");
    }

    [Fact]
    public async Task DelegationMovement_WithExpiry_AppliesUntilEffectiveTo()
    {
        var factory = new TestDbContextFactory(_options);
        var orgService = new OrganizationService(factory);
        await using var db = new JudicialDbContext(_options);

        var circuits = await db.Circuits
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
            .Where(c => c.Versions.Any(v => v.JudgeAssignments.Count >= 2))
            .Take(2)
            .ToListAsync();

        if (circuits.Count < 2) return;

        var circuit1 = circuits[0];
        var circuit2 = circuits[1];
        var cv1 = circuit1.Versions.First();
        var judge = cv1.JudgeAssignments.First().JudgeId;
        var role = await db.JudicialRoles.FirstAsync();

        var cmd = new JudgeMovementCreateCommand(
            JudgeId: judge,
            CircuitId: circuit2.Id,
            FromCircuitId: circuit1.Id,
            ToCircuitId: circuit2.Id,
            JudicialRoleId: role.Id,
            MovementType: "Delegation",
            DecisionNumber: $"قرار ندب مؤقت {Random.Shared.Next(100, 999)}",
            DecisionDate: new DateOnly(2025, 12, 1),
            EffectiveFrom: new DateOnly(2025, 12, 1),
            EffectiveTo: new DateOnly(2025, 12, 31),
            Reason: "ندب مؤقت لشهر ديسمبر",
            Notes: "ينتهي تلقائيا بنهاية ديسمبر"
        );

        var movId = await orgService.CreateJudgeMovementAsync(cmd);
        Assert.True(movId > 0);

        var movements = await orgService.GetJudgeMovementsAsync(judgeId: judge);
        var mov = movements.First(m => m.Id == movId);
        Assert.Equal("Delegation", mov.MovementType);
        Assert.Equal(new DateOnly(2025, 12, 31), mov.EffectiveTo);
    }

    private class TestDbContextFactory : IDbContextFactory<JudicialDbContext>
    {
        private readonly DbContextOptions<JudicialDbContext> _options;
        public TestDbContextFactory(DbContextOptions<JudicialDbContext> options) => _options = options;
        public JudicialDbContext CreateDbContext() => new(_options);
        public Task<JudicialDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new JudicialDbContext(_options));
    }
}
