using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PautaDinamicaApp.ViewModels;

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
            if (string.IsNullOrWhiteSpace(raw))
                return "<html><body style='background-color:#1e222d; color:#e1e1e1; font-family:sans-serif;'><i>No hay instrucciones configuradas para esta pauta.</i></body></html>";

            // Basic Markdown-ish conversion
            string html = raw;

            // HTML escape basic
            html = System.Net.WebUtility.HtmlEncode(html);

            // New lines to <br/>
            html = html.Replace("\n", "<br/>");

            // Simple Bold
            // We need a regex for a better approach, but let's do basic manual for now if possible or just use a simple template

            StringBuilder sb = new StringBuilder();
            sb.Append("<html><head><meta charset='UTF-8'><style>");
            sb.Append("body { background-color: #1e222d; color: #e1e1e1; font-family: 'Segoe UI', sans-serif; padding: 20px; line-height: 1.6; }");
            sb.Append("h1, h2, h3 { color: #3b82f6; }");
            sb.Append("code { background-color: #2d3446; padding: 2px 4px; border-radius: 4px; color: #f59e0b; }");
            sb.Append("ul { padding-left: 20px; }");
            sb.Append("li { margin-bottom: 5px; }");
            sb.Append("blockquote { border-left: 4px solid #3b82f6; padding-left: 15px; color: #94a3b8; font-style: italic; }");
            sb.Append("</style></head><body>");

            // If the user already provided HTML (starts with <h or <p or <div), we might trust it more
            // But let's assume they want a mix. One easy way is to check if it looks like HTML.
            if (raw.TrimStart().StartsWith("<") && raw.Contains(">"))
            {
                sb.Append(raw); // Use as is (basic HTML support)
            }
            else
            {
                // Simple MD to HTML logic
                string processed = raw;

                // Headers
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^### (.*$)", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^## (.*$)", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^# (.*$)", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);

                // Bold
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\*\*(.*?)\*\*", "<b>$1</b>");

                // Italic
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"\*(.*?)\*", "<i>$1</i>");

                // Lists
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^\* (.*$)", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
                processed = System.Text.RegularExpressions.Regex.Replace(processed, @"^- (.*$)", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

                // Process line breaks
                processed = processed.Replace("\r\n", "\n").Replace("\n", "<br/>");

                sb.Append(processed);
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}
