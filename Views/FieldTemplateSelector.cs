using System.Windows;
using System.Windows.Controls;
using PautaDinamicaApp.ViewModels;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public class FieldTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? NumericTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? DropdownTemplate { get; set; }
        public DataTemplate? DateTemplate { get; set; }
        public DataTemplate? TimeTemplate { get; set; }
        public DataTemplate? SeparatorTemplate { get; set; }
        public DataTemplate? CalculationTemplate { get; set; }
        public DataTemplate? AverageTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is DynamicFieldVM fieldVM)
            {
                return fieldVM.Type switch
                {
                    FieldType.Text => TextTemplate,
                    FieldType.Numeric => NumericTemplate,
                    FieldType.Boolean => BooleanTemplate,
                    FieldType.Dropdown => DropdownTemplate,
                    FieldType.Date => DateTemplate,
                    FieldType.Time => TimeTemplate,
                    FieldType.Separator => SeparatorTemplate,
                    FieldType.Calculation => CalculationTemplate,
                    FieldType.Average => AverageTemplate,
                    _ => TextTemplate
                };
            }
            return base.SelectTemplate(item, container);
        }
    }
}
