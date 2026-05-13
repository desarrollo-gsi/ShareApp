using System.Collections.Generic;

namespace AvaloniaShareApp.Application.UseCases.Documents.Queries.GetDashboardStats
{
    public record DashboardStatsDto(
        string UserName,
        string TotalStorage,
        double StoragePercentage,
        List<RecentFileDto> RecentFiles
    );

    public record RecentFileDto(
        string Name,
        string Type,
        string Modified,
        string IconType,
        string AccentColor
    );
}
