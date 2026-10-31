using BaseMVC.Domain.ExamTypes;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.ExamTypes;

public static class ExamTypeSeeder
{
    public static readonly IReadOnlyList<string> Defaults =
    [
        "Chuẩn đầu ra CNTT",
        "Chuẩn đầu ra Ngoại ngữ",
        "Chứng chỉ Ứng dụng CNTT Cơ bản",
        "Kiểm tra Trình Độ ngoại ngữ",
        "Tiếng Anh Tăng Cường"
    ];

    // Adds only the missing default names, so existing or renamed types are never overwritten.
    public static async Task SeedAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existing = (await dbContext.ExamTypes.Select(item => item.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = Defaults.Where(name => !existing.Contains(name)).ToList();
        if (missing.Count == 0) return;

        foreach (var name in missing)
            dbContext.ExamTypes.Add(new ExamType { Name = name, CreatedAtUtc = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
