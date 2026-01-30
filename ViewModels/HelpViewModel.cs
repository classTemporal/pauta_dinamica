using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PautaDinamicaApp.ViewModels;
using PautaDinamicaApp.Services;

namespace PautaDinamicaApp.ViewModels
{
    public class HelpViewModel : ViewModelBase
    {
        private string _title = "Ayuda de la Pauta";
        private string _htmlContent = "";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        public HelpViewModel(string pautaName, string rawContent)
        {
            Title = $"Instrucciones: {pautaName}";
            HtmlContent = ConvertToHtml(rawContent);
        }

        private string ConvertToHtml(string raw)
        {
            bool isDark = ThemeService.CurrentTheme == AppTheme.Dark;
            string bgColor = isDark ? "#12151c" : "#f5f7fa";
            string textColor = isDark ? "#e1e1e1" : "#0f172a";
            string accentColor = "#007bff";
            string secondaryText = isDark ? "#94a3b8" : "#475569";
            string codeBg = isDark ? "#2d3446" : "#e2e8f0";

            if (string.IsNullOrWhiteSpace(raw))
                return $"<html><body style='background-color:{bgColor}; color:{textColor}; font-family:sans-serif;'><i>No hay instrucciones configuradas para esta pauta.</i></body></html>";

            StringBuilder sb = new StringBuilder();
            sb.Append("<html><head><meta charset='UTF-8'>");
            sb.Append("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
            sb.Append("<style>");
            sb.Append($"body {{ background-color: {bgColor}; color: {textColor}; font-family: 'Segoe UI', sans-serif; padding: 20px; line-height: 1.6; overflow-x: hidden; }}");

            // Estilo del scrollbar para el navegador (Webkit)
            sb.Append("::-webkit-scrollbar { width: 10px; }");
            sb.Append("::-webkit-scrollbar-track { background: transparent; }");
            sb.Append($"::-webkit-scrollbar-thumb {{ background-color: {accentColor}; border-radius: 5px; border: 3px solid {bgColor}; opacity: 0.8; }}");
            sb.Append($"::-webkit-scrollbar-thumb:hover {{ background-color: {accentColor}; opacity: 1; }}");

            sb.Append($"h1, h2, h3 {{ color: {accentColor}; }}");
            sb.Append($"code {{ background-color: {codeBg}; padding: 2px 4px; border-radius: 4px; color: #f59e0b; }}");
            sb.Append("ul { padding-left: 20px; }");
            sb.Append("li { margin-bottom: 5px; }");
            sb.Append($"blockquote {{ border-left: 4px solid {accentColor}; padding-left: 15px; color: {secondaryText}; font-style: italic; }}");
            sb.Append("</style></head><body>");

            if (raw.TrimStart().StartsWith("<") && raw.Contains(">"))
            {
                sb.Append(raw);
            }
            else
            {
                string processed = raw;
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^### (.*$)", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^## (.*$)", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^# (.*$)", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\*\*(.*?)\*\*", "<b>$1</b>");
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\*(.*?)\*", "<i>$1</i>");
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^\* (.*$)", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^- (.*$)", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = processed.Replace("\r\n", "\n").Replace("\n", "<br/>");
                sb.Append(processed);
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}
