using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PautaDinamicaApp.Models;
using System.Diagnostics;

namespace PautaDinamicaApp.Services
{
    public class PdfService
    {
        public PdfService()
        {
            // Configurar licencia Community (Gratuita para uso individual o pequeñas empresas)
            // IMPORTANTE: Revisar términos si se escala el uso
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GenerateAuditPdf(List<AuditEntry> records, List<FieldDefinition> fields, string pautaName, string outputPath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text($"Reporte de Auditoría: {pautaName}")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            foreach (var record in records)
                            {
                                x.Item().Border(1).BorderColor("#E0E0E0").Padding(10).Column(col =>
                                {
                                    col.Item().Text($"Registro: {record.Timestamp:dd/MM/yyyy HH:mm}").Bold();
                                    col.Item().PaddingBottom(5).LineHorizontal(1).LineColor("#E0E0E0");

                                    foreach (var field in fields)
                                    {
                                        if (record.Values.TryGetValue(field.Id, out var val))
                                        {
                                            var strVal = val?.ToString() ?? "-";
                                            // Layout simple: Etiqueta en negrita, valor al lado
                                            col.Item().Row(row =>
                                            {
                                                row.ConstantItem(150).Text(field.Label).SemiBold();
                                                row.RelativeItem().Text(strVal);
                                            });
                                        }
                                    }
                                });
                                x.Item().Height(20); // Espacio entre registros
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(outputPath);
        }
    }
}
