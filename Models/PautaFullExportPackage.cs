using System.Collections.Generic;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Models
{
    /// <summary>
    /// Package that contains both the metadata (schema) and the field definitions (structure)
    /// for a full pauta configuration export/import.
    /// </summary>
    public class PautaFullExportPackage
    {
        public string Version { get; set; } = "1.0";
        public PautaSchema Metadata { get; set; } = new();
        public List<FieldDefinition> Fields { get; set; } = new();
    }
}
