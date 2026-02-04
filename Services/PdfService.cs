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

        public void GenerateAuditPdf(List<AuditEntry> records, List<FieldDefinition> fields, List<ExportColumnConfig>? pdfConfig, string pautaName, string outputPath)
        {
            // Mapeo de tipos real de los campos (ID -> Definición)
            var fieldMeta = fields.ToDictionary(f => f.Id, f => f);

            // Si no hay configuración de PDF, generamos una por defecto basada en el orden de los campos
            if (pdfConfig == null || !pdfConfig.Any())
            {
                pdfConfig = fields.OrderBy(f => f.Order).Select(f => new ExportColumnConfig
                {
                    FieldId = f.Id,
                    CustomHeader = f.Label,
                    Type = f.Type,
                    IsExportEnabled = true
                }).ToList();
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                    page.Header().PaddingBottom(10).Column(col =>
                    {
                        col.Item().AlignCenter().Text($"Reporte de Auditoría: {pautaName}")
                            .ExtraBold().FontSize(22).FontColor("#2d4059");

                        col.Item().PaddingTop(5).LineHorizontal(2).LineColor("#2d4059");
                    });

                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                    {
                        foreach (var record in records)
                        {
                            col.Item().PaddingBottom(5).Text($"Fecha de Registro: {record.Timestamp:dd/MM/yyyy HH:mm}").Italic().FontSize(9).FontColor(Colors.Grey.Medium);

                            // Agrupamos por secciones basadas en la configuración de PDF
                            var currentSection = new List<ExportColumnConfig>();
                            string sectionTitle = "Información General";

                            foreach (var configItem in pdfConfig.Where(c => c.IsExportEnabled))
                            {
                                // Obtener el tipo real: 
                                // 1. De la definición (si existe)
                                // 2. Del configItem (si se guardó)
                                // 3. Por prefijo de ID
                                FieldType actualType = configItem.Type;
                                if (fieldMeta.TryGetValue(configItem.FieldId, out var def))
                                {
                                    actualType = def.Type;
                                }
                                else if (configItem.FieldId.StartsWith("s_"))
                                {
                                    actualType = FieldType.Separator;
                                }

                                if (actualType == FieldType.Separator)
                                {
                                    if (currentSection.Any())
                                    {
                                        RenderSection(col, sectionTitle, currentSection, record, fieldMeta);
                                        currentSection.Clear();
                                    }
                                    sectionTitle = configItem.CustomHeader;
                                }
                                else
                                {
                                    currentSection.Add(configItem);
                                }
                            }

                            if (currentSection.Any())
                            {
                                RenderSection(col, sectionTitle, currentSection, record, fieldMeta);
                            }

                            if (records.Count > 1 && record != records.Last())
                            {
                                col.Item().PageBreak();
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" de ");
                        x.TotalPages();
                    });
                });
            })
            .GeneratePdf(outputPath);
        }

        private string FormatValue(object? val, FieldType type)
        {
            if (val == null) return "-";
            string s = val.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return "-";

            if (type == FieldType.Boolean)
            {
                if (s.Equals("True", StringComparison.OrdinalIgnoreCase) || s == "1") return "Sí";
                if (s.Equals("False", StringComparison.OrdinalIgnoreCase) || s == "0") return "No";
            }
            return s;
        }

        private void RenderSection(ColumnDescriptor col, string title, List<ExportColumnConfig> items, AuditEntry record, Dictionary<string, FieldDefinition> fieldMeta)
        {
            col.Item().PaddingBottom(15).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(180);
                    columns.RelativeColumn();
                });

                // Header de la sección (Casilla azul oscuro)
                table.Cell().RowSpan(1).ColumnSpan(2).Background("#34495e").Padding(5).Text(title).Bold().FontColor(Colors.White);

                foreach (var item in items)
                {
                    var rawVal = record.Values.TryGetValue(item.FieldId, out var val) ? val : null;

                    // Prioridad absoluta al tipo de la definición
                    FieldType actualType = fieldMeta.TryGetValue(item.FieldId, out var def) ? def.Type : FieldType.Text;
                    var value = FormatValue(rawVal, actualType);

                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Background(Colors.Grey.Lighten4).Text(item.CustomHeader).SemiBold();

                    var cell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value);

                    // Colores especiales: verdes para Sí/Cumple, rojos para No/No cumple
                    if (value.Equals("Cumple", StringComparison.OrdinalIgnoreCase) || value.Equals("Sí", StringComparison.OrdinalIgnoreCase))
                    {
                        cell.FontColor(Colors.Green.Medium).Bold();
                    }
                    else if (value.Equals("No cumple", StringComparison.OrdinalIgnoreCase) || value.Equals("No", StringComparison.OrdinalIgnoreCase))
                    {
                        cell.FontColor(Colors.Red.Medium).Bold();
                    }
                    else if (value.Contains("%"))
                    {
                        cell.Bold();
                    }
                }
            });
        }
    }
}
