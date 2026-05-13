using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaShareApp.Application.UseCases.Documents.Queries.GetDashboardStats
{
    public record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;

    public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
    {
        public Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
        {
            // In a real app, this would call repositories. 
            // For now, we move the "hardcoded" logic here to respect the architecture.
            var stats = new DashboardStatsDto(
                "Usuario",
                "25 GB",
                45,
                new List<RecentFileDto>
                {
                    new("Propuesta_Proyecto.docx", "Documento", "2h ago", "Word", "#4F46E5"),
                    new("Presupuesto_Q2.xlsx", "Hoja de Cálculo", "5h ago", "Excel", "#10B981"),
                    new("Presentacion_Ventas.pdf", "Presentación", "Yesterday", "PDF", "#EF4444"),
                    new("Contrato_Final.docx", "Documento", "2 days ago", "Word", "#4F46E5")
                }
            );

            return Task.FromResult(stats);
        }
    }
}
